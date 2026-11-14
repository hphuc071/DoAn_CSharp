using System.Text.Json;
using System.Text.Json.Nodes;
using FourChang.Tests.Admin;
using FourChang.Tests.Shared;
using FourChang.Presentation.Shared.Contracts.Admin;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Staff;

/// <summary>Toàn chuỗi dùng HTTP thật và SQL fixture riêng: khách đề nghị, Staff chuyển, Admin quyết định và Staff chi tại quầy.</summary>
public sealed class CancellationApprovalTests
{
    /// <summary>Không hủy sớm, không nhảy vai trò, duyệt mới giải phóng kho; tiền mặt ghi đúng ca và chỉ một lần.</summary>
    [Fact, Trait("Feature", "CancellationApproval")]
    public async Task YeuCau_Chuyen_Duyet_HoanTienMat_NguyenTuVaChongTrung()
    {
        await using var api = await TestApi.Start("Cancellation-Approval", runtime: new() { ["Orders__CancellationEnabled"] = "true", ["Orders__SqlIntegrationValidated"] = "true" });
        var admin = await api.AdminLogin(); var customer = await api.NewCustomer(); var owner = await api.Login(customer);
        var data = await WebOrder(api, customer.Id);
        var session = await Open(api, admin, 500000); var staff = TestApi.Bearer(session);
        var version = (await Detail(api, data.Order, admin)).Don.PhienBanDuLieu;
        var requestId = Guid.NewGuid();
        var request = Body(("Reason", "Khách không thể đến nhận món"), ("ExpectedVersion", Convert.ToBase64String(version)));
        var headers = new Dictionary<string,string>(owner) { ["Idempotency-Key"] = requestId.ToString() };
        var requested = (await api.Send("POST", $"/api/customer/orders/{data.Order}/cancel", request, headers)).Expect(200, "Khách gửi đề nghị").Json;
        (await api.Send("POST", $"/api/customer/orders/{data.Order}/cancel", request, headers)).Expect(200, "Gửi lại cùng đề nghị");
        Assert.Equal("PENDING_CONFIRMATION", requested.Text("trangThai"));
        Assert.Equal("0", await api.Sql($"SELECT COUNT(*) FROM dbo.HoSoHoanTien WHERE MaDonHang={data.Order}"));
        Assert.Equal("0", await api.Sql($"SELECT SoLuongDaHuy FROM dbo.ChiTietDonHang WHERE MaChiTietDonHang={data.Line}"));
        var holds = await api.Sql($"SELECT SUM(SoLuongGiuCho-SoLuongDaGiaiPhong) FROM dbo.GiuChoNguyenLieu WHERE MaChiTietDonHang={data.Line}");
        var detail = await Detail(api, data.Order, admin);
        await Command(api, data.Order, "APPROVE", requestId, detail, admin, 409);
        await Command(api, data.Order, "APPROVE", requestId, detail, staff, 403);
        (await api.Send("POST", $"/api/pos/orders/{data.Order}/actions", JsonSerializer.SerializeToNode(new OrderActionInput("ACCEPT", "Nhận đơn", detail.Don.PhienBanDuLieu, Guid.NewGuid())), staff)).Expect(409, "Chờ hủy không được tiếp nhận");
        await Command(api, data.Order, "FORWARD", requestId, detail, staff);
        detail = await Detail(api, data.Order, admin);
        Assert.Equal("FORWARDED", detail.Don.YeuCauHuy!.TrangThai);
        Assert.Equal(holds, await api.Sql($"SELECT SUM(SoLuongGiuCho-SoLuongDaGiaiPhong) FROM dbo.GiuChoNguyenLieu WHERE MaChiTietDonHang={data.Line}"));
        // Nếu không lưu được audit, toàn bộ hủy/kho/duyệt tiền phải rollback, không báo hủy thành công một phần.
        await api.Sql("CREATE TRIGGER dbo.TestFailCancelApprove ON dbo.NhatKyHeThong AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE HanhDong='CANCELLATION_APPROVE') THROW 51012,'Test cancel approval rollback',1; END");
        await Command(api, data.Order, "APPROVE", requestId, detail, admin, -1);
        Assert.Equal("PENDING_CONFIRMATION", (await Detail(api, data.Order, admin)).Don.TrangThai);
        Assert.Equal("0", await api.Sql($"SELECT COUNT(*) FROM dbo.HoSoHoanTien WHERE MaDonHang={data.Order}"));
        Assert.Equal(holds, await api.Sql($"SELECT SUM(SoLuongGiuCho-SoLuongDaGiaiPhong) FROM dbo.GiuChoNguyenLieu WHERE MaChiTietDonHang={data.Line}"));
        await api.Sql("DROP TRIGGER dbo.TestFailCancelApprove");
        await Command(api, data.Order, "APPROVE", requestId, detail, admin);
        var approved = await Detail(api, data.Order, admin);
        Assert.Equal("CANCELLED", approved.Don.TrangThai);
        Assert.Equal("0.0000", await api.Sql($"SELECT SUM(SoLuongGiuCho-SoLuongDaGiaiPhong) FROM dbo.GiuChoNguyenLieu WHERE MaChiTietDonHang={data.Line}"));
        Assert.Equal("0", await api.Sql($"SELECT COUNT(*) FROM dbo.LanChiHoanTien a JOIN dbo.HoSoHoanTien r ON a.MaHoanTien=r.MaHoanTien WHERE r.MaDonHang={data.Order}"));
        var refund = (await api.Send("GET", $"/api/pos/orders/{data.Order}/counter-refunds", headers: staff)).Expect(200, "Khoản được duyệt tại quầy").Json.AsArray().Single()!;
        var payId = Guid.NewGuid(); var refundId = refund.Number("maHoanTien");
        var pay = Body(("NguoiNhan", refund.Text("nguoiNhan")), ("ChungCu", "Biên nhận quầy HT-TEST, khách ký nhận"),
            ("DaGiaoTienVaKhachDaNhan", true), ("PhienBanDuLieu", refund.Text("phienBanDuLieu")), ("MaThaoTac", payId.ToString()));
        var poor = TestApi.Bearer(await Open(api, admin, 0));
        (await api.Send("POST", $"/api/pos/orders/counter-refunds/{refundId}", pay, poor)).Expect(409, "Ca không đủ quỹ");
        pay["NguoiNhan"] = "Người khác";
        (await api.Send("POST", $"/api/pos/orders/counter-refunds/{refundId}", pay, staff)).Expect(409, "Không đúng người nhận");
        pay["NguoiNhan"] = refund.Text("nguoiNhan");
        await api.Sql("CREATE TRIGGER dbo.TestFailCounterRefund ON dbo.NhatKyHeThong AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE HanhDong='COUNTER_REFUND') THROW 51013,'Test counter refund rollback',1; END");
        Assert.True((await api.Send("POST", $"/api/pos/orders/counter-refunds/{refundId}", pay, staff)).Status >= 400);
        Assert.Equal("0", await api.Sql($"SELECT COUNT(*) FROM dbo.LanChiHoanTien WHERE MaHoanTien={refundId}"));
        await api.Sql("DROP TRIGGER dbo.TestFailCounterRefund");
        var receipts = await Task.WhenAll(api.Send("POST", $"/api/pos/orders/counter-refunds/{refundId}", pay, staff), api.Send("POST", $"/api/pos/orders/counter-refunds/{refundId}", pay, staff));
        foreach (var receipt in receipts) receipt.Expect(200, "Giao tiền và gửi lại chống trùng");
        Assert.Equal("1", await api.Sql($"SELECT COUNT(*) FROM dbo.LanChiHoanTien WHERE MaHoanTien={refundId} AND TrangThai='SUCCEEDED' AND MaCaLamViec={session.Number("shiftId")}"));
        var close = Body(("MaThaoTac", NewId()), ("TienMatThucDem", 500000 - data.Total));
        (await api.Send("POST", "/api/pos/shifts/current/close", close, staff)).Expect(200, "Đối chiếu quỹ đã trừ khoản hoàn");
    }

    /// <summary>Từ chối trả đơn về luồng nhận bình thường; không giải phóng kho hay tạo hoàn tiền, khách khác không gửi được.</summary>
    [Fact, Trait("Feature", "CancellationApproval")]
    public async Task TuChoi_KhongHuyKhongHoan_KhachKhacKhongDuocYeuCau()
    {
        await using var api = await TestApi.Start("Cancellation-Reject", runtime: new() { ["Orders__CancellationEnabled"] = "true", ["Orders__SqlIntegrationValidated"] = "true" });
        var admin = await api.AdminLogin(); var customer = await api.NewCustomer(); var owner = await api.Login(customer);
        var data = await WebOrder(api, customer.Id); var detail = await Detail(api, data.Order, admin);
        var operation = Guid.NewGuid(); var body = Body(("Reason", "Khách đề nghị hủy"), ("ExpectedVersion", Convert.ToBase64String(detail.Don.PhienBanDuLieu)));
        var other = await api.Login(await api.NewCustomer("other")); other["Idempotency-Key"] = operation.ToString();
        (await api.Send("POST", $"/api/customer/orders/{data.Order}/cancel", body, other)).Expect(404, "Không hủy đơn người khác");
        owner["Idempotency-Key"] = operation.ToString();
        (await api.Send("POST", $"/api/customer/orders/{data.Order}/cancel", body, owner)).Expect(200, "Đúng chủ đơn");
        var staff = TestApi.Bearer(await Open(api, admin, 500000)); detail = await Detail(api, data.Order, admin);
        await Command(api, data.Order, "FORWARD", operation, detail, staff); detail = await Detail(api, data.Order, admin);
        await Command(api, data.Order, "REJECT", operation, detail, admin); detail = await Detail(api, data.Order, admin);
        Assert.Null(detail.Don.YeuCauHuy); Assert.Equal("PENDING_CONFIRMATION", detail.Don.TrangThai);
        Assert.Equal("0", await api.Sql($"SELECT COUNT(*) FROM dbo.HoSoHoanTien WHERE MaDonHang={data.Order}"));
        (await api.Send("POST", $"/api/pos/orders/{data.Order}/actions", JsonSerializer.SerializeToNode(new OrderActionInput("ACCEPT", "Tiếp nhận sau từ chối", detail.Don.PhienBanDuLieu, Guid.NewGuid())), staff)).Expect(200, "Được tiếp nhận sau khi từ chối");
    }

    /// <summary>Nhân viên gửi chỉ tạo yêu cầu chờ duyệt; duyệt/từ chối không được nhầm với đã hủy hoặc đã chi tiền.</summary>
    [Theory, InlineData(true), InlineData(false)]
    public async Task NhanVienGuiHuy_PhaiChoQuanLyQuyetDinh(bool approve)
    {
        await using var api = await TestApi.Start("Staff-Cancel-Approval");
        var admin = await api.AdminLogin();
        var data = await OperationsScenarioSeed.Create(api, state: "ACCEPTED");
        var staff = TestApi.Bearer(await Open(api, admin, 500000));
        var detail = await Detail(api, data.Order, admin);
        var holds = await api.Sql($"SELECT SUM(SoLuongGiuCho-SoLuongDaGiaiPhong) FROM dbo.GiuChoNguyenLieu WHERE MaChiTietDonHang={data.Line}");
        var operation = Guid.NewGuid();
        var input = JsonSerializer.SerializeToNode(new OrderActionInput("CANCEL", "Khách yêu cầu hủy tại quầy", detail.Don.PhienBanDuLieu, operation, data.Line, 2));
        (await api.Send("POST", $"/api/pos/orders/{data.Order}/actions", input, staff)).Expect(200, "Gửi yêu cầu chờ duyệt");
        (await api.Send("POST", $"/api/pos/orders/{data.Order}/actions", input, staff)).Expect(200, "Gửi lại không trùng");
        detail = await Detail(api, data.Order, admin);
        Assert.Equal("ACCEPTED", detail.Don.TrangThai);
        Assert.Equal("FORWARDED", detail.Don.YeuCauHuy!.TrangThai);
        Assert.Equal("1", await api.Sql($"SELECT COUNT(*) FROM dbo.HuyMon WHERE MaNhomHuy='{operation}'"));
        Assert.Equal("0", await api.Sql($"SELECT SoLuongDaHuy FROM dbo.ChiTietDonHang WHERE MaChiTietDonHang={data.Line}"));
        Assert.Equal("0", await api.Sql($"SELECT COUNT(*) FROM dbo.HoSoHoanTien WHERE MaDonHang={data.Order}"));
        Assert.Equal(holds, await api.Sql($"SELECT SUM(SoLuongGiuCho-SoLuongDaGiaiPhong) FROM dbo.GiuChoNguyenLieu WHERE MaChiTietDonHang={data.Line}"));
        await Command(api, data.Order, "APPROVE", operation, detail, staff, 403);
        (await api.Send("POST", $"/api/pos/orders/{data.Order}/actions", JsonSerializer.SerializeToNode(new OrderActionInput("START", "Pha", detail.Don.PhienBanDuLieu, Guid.NewGuid(), data.Line, 2)), staff)).Expect(409, "Chờ duyệt không pha");
        await Command(api, data.Order, approve ? "APPROVE" : "REJECT", operation, detail, admin);
        detail = await Detail(api, data.Order, admin);
        Assert.Null(detail.Don.YeuCauHuy);
        Assert.Equal(approve ? "CANCELLED" : "ACCEPTED", detail.Don.TrangThai);
        Assert.Equal(approve ? "2" : "0", await api.Sql($"SELECT SoLuongDaHuy FROM dbo.ChiTietDonHang WHERE MaChiTietDonHang={data.Line}"));
        Assert.Equal(approve ? "1" : "0", await api.Sql($"SELECT COUNT(*) FROM dbo.HoSoHoanTien WHERE MaDonHang={data.Order} AND TrangThai='APPROVED'"));
        Assert.Equal("0", await api.Sql($"SELECT COUNT(*) FROM dbo.LanChiHoanTien c JOIN dbo.HoSoHoanTien h ON h.MaHoanTien=c.MaHoanTien WHERE h.MaDonHang={data.Order}"));
        Assert.Equal(approve ? "0.0000" : holds, await api.Sql($"SELECT SUM(SoLuongGiuCho-SoLuongDaGiaiPhong) FROM dbo.GiuChoNguyenLieu WHERE MaChiTietDonHang={data.Line}"));
    }
    /// <summary>Seed snapshot tồn tại rồi gắn chủ đơn Web thật trong database test; không sửa database đồ án.</summary>
    private static async Task<OperationsScenario> WebOrder(TestApi api, long customer)
    {
        var data = await OperationsScenarioSeed.Create(api);
        await api.Sql($"UPDATE dbo.DonHang SET KenhDatHang='WEB',MaKhachHang={customer},MaCaLamViec=NULL WHERE MaDonHang={data.Order}; UPDATE dbo.ThanhToan SET PhuongThuc='BANK_TRANSFER',MaCaLamViec=NULL WHERE MaDonHang={data.Order};");
        return data;
    }

    /// <summary>Mở ca bằng mã/họ tên thật qua endpoint công khai hiện có.</summary>
    private static async Task<JsonNode> Open(TestApi api, Dictionary<string,string> admin, decimal opening)
    {
        var employee = await api.Employee(admin, "Nguyễn Hoàng Minh");
        return (await api.Send("POST", "/api/pos/shifts", Body(("MaNhanVien", employee.Number("maNhanVien")), ("HoTen", employee.Text("hoTen")), ("TenCa", "Sáng"), ("MaMay", "COUNTER-REFUND"), ("TienMatDauCa", opening), ("MaThaoTac", NewId())))).Expect(200, "Mở ca quầy").Json;
    }

    /// <summary>Lấy rowversion sau mỗi bước, kiểm chứng bằng DTO dùng ở màn hình Quản lý.</summary>
    private static async Task<AdminOrderDetail> Detail(TestApi api, long id, Dictionary<string,string> admin) =>
        (await api.Send("GET", $"/api/admin/orders/{id}", headers: admin)).Expect(200, "Đọc đơn").Json.Deserialize<AdminOrderDetail>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    /// <summary>Thực thi một bước chuyển/duyệt theo vai trò thực; không gọi trực tiếp service để bỏ qua phân quyền.</summary>
    private static async Task Command(TestApi api, long id, string action, Guid group, AdminOrderDetail detail, Dictionary<string,string> actor, int expected = 200)
    {
        var input = Body(("HanhDong", action), ("MaNhomHuy", group.ToString()), ("LyDo", "Đã đối chiếu yêu cầu của khách"), ("PhienBanDuLieu", Convert.ToBase64String(detail.Don.PhienBanDuLieu)), ("MaThaoTac", NewId()), ("DaKiemTraNguoiNhan", true));
        var role = action == "FORWARD" || expected == 403 ? "pos" : "admin";
        var result = await api.Send("POST", $"/api/{role}/orders/{id}/cancellation", input, actor);
        if (expected == -1) Assert.True(result.Status >= 400); else result.Expect(expected, action);
    }
}
