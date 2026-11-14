using FourChang.Tests.Admin;
using FourChang.Tests.Shared;
using Microsoft.Playwright;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests;

/// <summary>Razor/Web/API/SQL thật: nút gửi đề nghị có CSRF/version, không hiển thị đã hủy khi đang chờ.</summary>
public sealed class CancellationRequestBrowserTests
{
    /// <summary>Khách gửi từ form thật, thấy trạng thái chờ và không bị xóa tiền/giỏ/đơn; bố cục mobile không tràn.</summary>
    [Fact, Trait("Feature", "CancellationApproval"), Trait("Layer", "Browser")]
    public async Task GuiYeuCauTuWebsite_HienTrangThaiCho_VaResponsive()
    {
        await using var f = await BrowserFixture.Start("Cancellation-Browser", new() { ["Orders__SqlIntegrationValidated"] = "true", ["Orders__CancellationEnabled"] = "true" });
        var customer = await f.Api.NewCustomer();
        var scenario = await OperationsScenarioSeed.Create(f.Api);
        await f.Api.Sql($"UPDATE dbo.DonHang SET KenhDatHang='WEB',MaKhachHang={customer.Id},MaCaLamViec=NULL WHERE MaDonHang={scenario.Order}; UPDATE dbo.ThanhToan SET PhuongThuc='BANK_TRANSFER',MaCaLamViec=NULL WHERE MaDonHang={scenario.Order}");
        await f.Login(customer.Input.Text("TenDangNhap"), customer.Input.Text("MatKhau"));
        await f.Page.GotoAsync(f.WebUrl + "/Orders/" + scenario.Order);
        await f.Page.Locator("details.commerce-cancellation > summary").ClickAsync();
        await f.Page.Locator("textarea[name=reason]").FillAsync("Khách đề nghị hủy để đổi lịch nhận món");
        await f.Button("Gửi yêu cầu hủy").ClickAsync();
        await Assertions.Expect(f.Page.GetByRole(AriaRole.Heading, new() { Name = "Yêu cầu hủy đang chờ xử lý" })).ToBeVisibleAsync();
        await Assertions.Expect(f.Page.GetByText("Đơn chưa bị hủy và chưa thực hiện hoàn tiền.", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(f.Page.GetByRole(AriaRole.Button, new() { Name = "Gửi yêu cầu hủy", Exact = true })).ToHaveCountAsync(0);
        Assert.Equal("PENDING_CONFIRMATION", await f.Api.Sql($"SELECT TrangThai FROM dbo.DonHang WHERE MaDonHang={scenario.Order}"));
        Assert.Equal("0", await f.Api.Sql($"SELECT COUNT(*) FROM dbo.HoSoHoanTien WHERE MaDonHang={scenario.Order}"));
        await f.Screenshot("cancellation-request-desktop");
        await f.Page.SetViewportSizeAsync(390, 844);
        Assert.True(await f.Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth + 1"));
        await f.Screenshot("cancellation-request-mobile");
        // Thông báo trong DANH SÁCH chỉ xuất hiện sau khi có phê duyệt tiền mặt thật, không dựa vào client.
        var admin = await f.Api.AdminLogin();
        var employee = await f.Api.Employee(admin, "Nguyễn Hoàng Minh");
        var session = (await f.Api.Send("POST", "/api/pos/shifts", Body(("MaNhanVien", employee.Number("maNhanVien")), ("HoTen", employee.Text("hoTen")), ("TenCa", "Sáng"), ("MaMay", "NOTICE-COUNTER"), ("TienMatDauCa", 500000), ("MaThaoTac", NewId())))).Expect(200, "Mở ca chuyển yêu cầu").Json;
        var staff = TestApi.Bearer(session);
        foreach (var action in new[] { "FORWARD", "APPROVE" })
        {
            var detail = (await f.Api.Send("GET", $"/api/admin/orders/{scenario.Order}", headers: admin)).Expect(200, "Đọc yêu cầu").Json["don"]!;
            var command = Body(("HanhDong", action), ("MaNhomHuy", detail["yeuCauHuy"]!.Text("maNhomHuy")), ("LyDo", "Đã kiểm tra và đồng ý yêu cầu khách"), ("PhienBanDuLieu", detail.Text("phienBanDuLieu")), ("MaThaoTac", NewId()), ("DaKiemTraNguoiNhan", true));
            (await f.Api.Send("POST", $"/api/{(action == "FORWARD" ? "pos" : "admin")}/orders/{scenario.Order}/cancellation", command, action == "FORWARD" ? staff : admin)).Expect(200, action);
        }
        await f.Page.GotoAsync(f.WebUrl + "/Orders");
        await Assertions.Expect(f.Page.Locator(".order-counter-refund-notice")).ToBeVisibleAsync();
        await Assertions.Expect(f.Page.Locator(".order-counter-refund-notice")).ToContainTextAsync("Vui lòng đến quầy để nhận hoàn tiền");
        Assert.True(await f.Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth + 1"));
        await f.Screenshot("cancelled-order-list-refund-mobile");
        await f.Page.SetViewportSizeAsync(1366, 900); await f.Screenshot("cancelled-order-list-refund-desktop");
        var refund = (await f.Api.Send("GET", $"/api/pos/orders/{scenario.Order}/counter-refunds", headers: staff)).Expect(200, "Hồ sơ được duyệt").Json.AsArray().Single()!;
        (await f.Api.Send("POST", $"/api/pos/orders/counter-refunds/{refund.Number("maHoanTien")}", Body(("NguoiNhan", refund.Text("nguoiNhan")), ("ChungCu", "Khách ký nhận tại quầy trong test"), ("DaGiaoTienVaKhachDaNhan", true), ("PhienBanDuLieu", refund.Text("phienBanDuLieu")), ("MaThaoTac", NewId())), staff)).Expect(200, "Chi khoản hoàn");
        await f.Page.ReloadAsync();
        await Assertions.Expect(f.Page.Locator(".order-counter-refund-notice")).ToHaveCountAsync(0);
    }
}
