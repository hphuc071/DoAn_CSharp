using System.Text.Json;
using System.Text.Json.Nodes;
using FourChang.Tests.Admin;
using FourChang.Tests.Shared;
using FourChang.Presentation.Shared.Contracts.Admin;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Staff;

/// <summary>Kiểm thử quyền Staff trên HTTP/SQL thật, không dùng quyền Admin để thực thi tiến trình.</summary>
[Collection("AdminFeatures"), Trait("Actor", "NhanVien"), Trait("Feature", "StaffOrderWorkflow")]
public sealed class StaffOrderWorkflowTests(AdminFeatureFixture fixture)
{
    /// <summary>Nhân viên xác nhận, pha và hoàn tất; gửi lại không trừ kho hai lần, ca khác không đọc receipt.</summary>
    [Fact]
    public async Task NhanVien_XacNhan_CapNhatDungTienTrinh_VaChongTrung()
    {
        var scenario = await OperationsScenarioSeed.Create(fixture);
        var session = await OpenSession(); var staff = TestApi.Bearer(session);
        var read = (await fixture.Api.Send("GET", "/api/pos/orders", headers: staff)).Expect(200, "Staff tra đơn").Json;
        Assert.True(read.Number("tongSo") > 0);
        var current = await Detail(scenario.Order, staff);
        await Send(scenario.Order, new("CANCEL", "", current.Don.PhienBanDuLieu, Guid.NewGuid(), scenario.Line, 1), staff, 400);
        var accept = new OrderActionInput("ACCEPT", "", current.Don.PhienBanDuLieu, Guid.NewGuid());
        await Send(scenario.Order, accept, staff);
        await Send(scenario.Order, accept, staff);
        Assert.Equal("ACCEPTED", (await Detail(scenario.Order, staff)).Don.TrangThai);
        (await fixture.Api.Send("GET", $"/api/pos/orders/operations/{accept.MaThaoTac}", headers: staff)).Expect(200, "Ca thực hiện tra kết quả");
        var other = await Open();
        (await fixture.Api.Send("GET", $"/api/pos/orders/operations/{accept.MaThaoTac}", headers: other)).Expect(404, "Ca khác không tra được biên nhận");
        await Send(scenario.Order, accept with { MaThaoTac = Guid.NewGuid() }, staff, 409);
        foreach (var action in new[] { "START", "FINISH", "COMPLETE" })
        {
            current = await Detail(scenario.Order, staff);
            var input = new OrderActionInput(action, "", current.Don.PhienBanDuLieu, Guid.NewGuid(), scenario.Line, 2);
            await Send(scenario.Order, input, staff);
            await Send(scenario.Order, input, staff);
        }
        Assert.Equal("COMPLETED", (await Detail(scenario.Order, staff)).Don.TrangThai);
        var finishing=(await fixture.Api.Send("GET",$"/api/admin/work-sessions/{session.Number("shiftId")}",headers:fixture.Admin)).Expect(200,"Ca thực sự hoàn thành").Json;
        Assert.Equal(1,finishing.Number("soDonHoanThanh"));Assert.Equal(scenario.Total,finishing["tongTienDonHoanThanh"]!.GetValue<decimal>());
        Assert.Equal(0,finishing.Number("tienMatDuKienHienTai")); // Tiền đã thu ở ca nguồn, không cộng lại ca làm xong.
        var source=(await fixture.Api.Send("GET",$"/api/admin/work-sessions/{scenario.Shift}",headers:fixture.Admin)).Expect(200,"Ca tạo không nhận lại doanh số hoàn tất").Json;
        Assert.Equal(0,source.Number("soDonHoanThanh"));Assert.Equal(0,source.Number("tongTienDonHoanThanh"));
        Assert.Equal("3", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.NhatKyHeThong WHERE LoaiTacNhan='STAFF' AND MaDoiTuong='{scenario.Order}' AND HanhDong IN ('ORDER_START','ORDER_FINISH','ORDER_COMPLETE') AND LEN(LyDo)>0"));
        Assert.Equal("1", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.NhatKyHeThong WHERE LoaiTacNhan='STAFF' AND HanhDong='ORDER_ACCEPT' AND MaDoiTuong='{scenario.Order}' AND LEN(LyDo)>0"));
        Assert.Equal("1", await fixture.Api.Sql($"SELECT COUNT(DISTINCT MaThaoTac) FROM dbo.BienDongKho WHERE MaChiTietDonHang={scenario.Line} AND LoaiBienDong='ORDER_CONSUMPTION'"));
    }

    /// <summary>Không nhảy bước, không gọi Admin, không chốt ngoại lệ hoặc xử lý tiếp sau khi ca kết thúc.</summary>
    [Fact]
    public async Task PhanQuyen_KhongVuotTrangThai_KhongXuLySauKhiKetThucCa()
    {
        var scenario = await OperationsScenarioSeed.Create(fixture);
        var staff = await Open();
        var current = await Detail(scenario.Order, staff);
        await Send(scenario.Order, new("COMPLETE", "Không được nhảy bước", current.Don.PhienBanDuLieu, Guid.NewGuid()), staff, 409);
        foreach (var action in new[] { "EXPIRE", "FAIL_DELIVERY", "RETRY_DELIVERY" })
            await Send(scenario.Order, new(action, "Không được quyền quản trị", current.Don.PhienBanDuLieu, Guid.NewGuid()), staff, 403);
        (await fixture.Api.Send("GET", "/api/admin/orders", headers: staff)).Expect(403, "Staff không gọi API Admin");
        (await fixture.Api.Send("GET", "/api/pos/orders")).Expect(401, "Không có phiên không xem đơn");
        var session = await OpenSession();
        var ended = TestApi.Bearer(session);
        await fixture.Api.Sql($"UPDATE dbo.CaLamViec SET TrangThai='CLOSED',KetThucLuc=SYSUTCDATETIME(),TienMatDuKienCuoiCa=0,TienMatThucDem=0 WHERE MaCaLamViec={session.Number("shiftId")}");
        Assert.True((await fixture.Api.Send("GET", "/api/pos/orders", headers: ended)).Status is 401 or 403);
    }

    /// <summary>Đơn Web chờ nhận xuất hiện trong nhóm ca này; tiếp nhận gắn ca xử lý, không chuyển ca đã thu tiền.</summary>
    [Fact]
    public async Task NhanDonWeb_GanCaXuLy_KhongChuyenNguonTien()
    {
        var scenario=await OperationsScenarioSeed.Create(fixture);var customer=await fixture.Api.NewCustomer("shift");
        await fixture.Api.Sql($"UPDATE dbo.DonHang SET KenhDatHang='WEB',MaKhachHang={customer.Id},MaCaLamViec=NULL WHERE MaDonHang={scenario.Order}");
        var session=await OpenSession();var staff=TestApi.Bearer(session);
        var list=(await fixture.Api.Send("GET","/api/pos/orders?PhamViCa=CURRENT",headers:staff)).Expect(200,"Đơn Web chờ nhận").Json;
        Assert.Contains(list["duLieu"]!.AsArray(),x=>x!.Number("maDonHang")==scenario.Order);
        var detail=await Detail(scenario.Order,staff);
        await Send(scenario.Order,new("ACCEPT","",detail.Don.PhienBanDuLieu,Guid.NewGuid()),staff);
        Assert.Equal(session.Number("shiftId").ToString(),await fixture.Api.Sql($"SELECT MaCaLamViec FROM dbo.DonHang WHERE MaDonHang={scenario.Order}"));
        Assert.Equal(scenario.Shift.ToString(),await fixture.Api.Sql($"SELECT MaCaLamViec FROM dbo.ThanhToan WHERE MaThanhToan={scenario.Payment}"));
    }

    /// <summary>Tạo phiên ca ký qua endpoint mở ca, không chế token trong test.</summary>
    private async Task<JsonNode> OpenSession()
    {
        var employee = await fixture.Api.Employee(fixture.Admin, "Trần Hoàng Phúc");
        return (await fixture.Api.Send("POST", "/api/pos/shifts", Body(("MaNhanVien", employee.Number("maNhanVien")),
            ("HoTen", employee.Text("hoTen")), ("TenCa", "Sáng"), ("MaMay", "STAFF-ORDER-TEST"), ("TienMatDauCa", 0), ("MaThaoTac", NewId())))).Expect(200, "Mở ca Staff").Json;
    }

    /// <summary>Lấy header phiên Staff độc lập để kiểm tra phân quyền trên HTTP.</summary>
    private async Task<Dictionary<string, string>> Open() => TestApi.Bearer(await OpenSession());

    /// <summary>Đọc snapshot mới nhất để dùng đúng rowversion khi cập nhật.</summary>
    private async Task<AdminOrderDetail> Detail(long id, Dictionary<string, string> staff) =>
        (await fixture.Api.Send("GET", $"/api/pos/orders/{id}", headers: staff)).Expect(200, "Staff xem món và trạng thái").Json.Deserialize<AdminOrderDetail>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    /// <summary>Gửi lệnh typed qua API Staff và đối chiếu mã phản hồi.</summary>
    private async Task Send(long id, OrderActionInput input, Dictionary<string, string> staff, int expected = 200) =>
        (await fixture.Api.Send("POST", $"/api/pos/orders/{id}/actions", JsonSerializer.SerializeToNode(input), staff)).Expect(expected, input.HanhDong);
}
