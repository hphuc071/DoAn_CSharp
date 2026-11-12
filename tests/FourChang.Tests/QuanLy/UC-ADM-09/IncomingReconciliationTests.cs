using System.Text.Json;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Tests.Shared;

namespace FourChang.Tests.Admin;

/// <summary>Đối soát tiền vào và hết hạn chạy API/SQL thật trong database kiểm thử riêng.</summary>
[Collection("AdminFeatures"), Trait("UseCase", "UC-ADM-09"), Trait("Actor", "QuanLy")]
public sealed class IncomingReconciliationTests(AdminFeatureFixture fixture)
{
    /// <summary>Thiếu tiền không nhận đơn, giao dịch lặp không cộng lại, đủ tiền đúng hạn mới chờ xác nhận.</summary>
    [Fact]
    public async Task TienThieu_KhongNhan_DuTienMoiNhan_KhongCongLap()
    {
        var s = await Prepare(false);
        var first = await Input(s, s.Total / 2);
        await Match(first);
        Assert.Equal("PENDING_PAYMENT", await State(s));
        var current = await Payment(s);
        await Match(first with { PhienBanDuLieu = current.PhienBanDuLieu, MaThaoTac = Guid.NewGuid() });
        Assert.Equal(s.Total / 2, (await Payment(s)).SoTienDaXacMinh);
        await Match(await Input(s, s.Total / 2));
        Assert.Equal("PENDING_CONFIRMATION", await State(s));
        Assert.Equal(s.Total, (await Payment(s)).SoTienDaXacMinh);
    }

    /// <summary>Tiền đến muộn giải phóng giữ chỗ, tạo nguồn hoàn LATE_PAYMENT và không phục hồi đơn hết hạn.</summary>
    [Fact]
    public async Task TienMuon_Expired_KhongHoiSinh_KhongNhapKho()
    {
        var s = await Prepare(true);
        var stock = await fixture.Api.Sql("SELECT SUM(TonThucTe) FROM dbo.TonKho");
        var input = await Input(s, s.Total);
        await Match(input with { QuanLyDaDoiChieu = false }, 400);
        await Match(input);
        Assert.Equal("EXPIRED", await State(s));
        Assert.Equal("LATE_PAYMENT", await fixture.Api.Sql($"SELECT LoaiNguon FROM dbo.HoSoHoanTien WHERE MaDonHang={s.Order}"));
        Assert.Equal(stock, await fixture.Api.Sql("SELECT SUM(TonThucTe) FROM dbo.TonKho"));
        Assert.Equal("0.0000", await fixture.Api.Sql($"SELECT SUM(SoLuongGiuCho-SoLuongDaGiaiPhong-SoLuongDaTieuHao) FROM dbo.GiuChoNguyenLieu WHERE MaChiTietDonHang={s.Line}"));
        var payment = await Payment(s);
        await Match(input with { PhienBanDuLieu = payment.PhienBanDuLieu, MaThaoTac = Guid.NewGuid() });
        Assert.Equal("EXPIRED", await State(s));
        Assert.Equal("1", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.HoSoHoanTien WHERE MaDonHang={s.Order}"));
    }

    /// <summary>Admin phải đối chiếu sao kê trước chốt hết hạn; đã thu đủ không được chuyển EXPIRED.</summary>
    [Fact]
    public async Task ChotHetHan_CanChungCu_DaThuDuKhongChoChot()
    {
        var s = await Prepare(true);
        var order = (await fixture.Send("GET", $"/api/admin/orders/{s.Order}")).Deserialize<AdminOrderDetail>(Options)!;
        var input = new OrderActionInput("EXPIRE", "Đã đến hạn chờ tiền", order.Don.PhienBanDuLieu, Guid.NewGuid());
        await fixture.Send("POST", $"/api/admin/orders/{s.Order}/actions", input, 409);
        await fixture.Send("POST", $"/api/admin/orders/{s.Order}/actions", input with { ChungCu = "Sao kê kiểm thử không có tiền vào", DaDoiChieuSaoKe = true, MaThaoTac = Guid.NewGuid() });
        Assert.Equal("EXPIRED", await State(s));
        var paid = await Prepare(true);
        await fixture.Api.Sql($"UPDATE dbo.ThanhToan SET SoTienDaXacMinh=SoTienYeuCau,TrangThai='VERIFIED',XacMinhLuc=SYSUTCDATETIME() WHERE MaThanhToan={paid.Payment}");
        order = (await fixture.Send("GET", $"/api/admin/orders/{paid.Order}")).Deserialize<AdminOrderDetail>(Options)!;
        await fixture.Send("POST", $"/api/admin/orders/{paid.Order}/actions", input with { PhienBanDuLieu=order.Don.PhienBanDuLieu, ChungCu="Đã thu đủ", DaDoiChieuSaoKe=true, MaThaoTac=Guid.NewGuid() }, 409);
    }

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Chỉ đổi dữ liệu fixture thành Web chưa trả tiền, không tạo dữ liệu giả trong database quán.</summary>
    private async Task<OperationsScenario> Prepare(bool expired)
    {
        var s = await OperationsScenarioSeed.Create(fixture, paid: false);
        var customer = await fixture.Api.NewCustomer("incoming");
        await fixture.Api.Sql($"UPDATE dbo.DonHang SET MaKhachHang={customer.Id},KenhDatHang='WEB',TrangThai='PENDING_PAYMENT',HetHanLuc=DATEADD(minute,{(expired ? -10 : 10)},SYSUTCDATETIME()) WHERE MaDonHang={s.Order}; UPDATE dbo.ThanhToan SET PhuongThuc='BANK_TRANSFER',TienKhachDua=NULL WHERE MaThanhToan={s.Payment}");
        return s;
    }

    /// <summary>Lấy rowversion mới trước mỗi giao dịch ngân hàng riêng biệt.</summary>
    private async Task<IncomingReconciliationInput> Input(OperationsScenario s, decimal amount) => new(s.Payment, "TEST", "ACCOUNT-TEST", Guid.NewGuid().ToString("N"), amount, DateTime.UtcNow.AddMinutes(-1), "Sao kê kiểm thử tin cậy", "Admin đã đối chiếu tiền vào", true, (await Payment(s)).PhienBanDuLieu, Guid.NewGuid());
    /// <summary>Đọc yêu cầu thu từ endpoint Admin bảo vệ.</summary>
    private async Task<IncomingPaymentView> Payment(OperationsScenario s) => (await fixture.Send("GET", $"/api/admin/refunds/incoming/{s.Payment}")).Deserialize<IncomingPaymentView>(Options)!;
    /// <summary>Gửi lệnh đối soát như ứng dụng quản lý.</summary>
    private Task<System.Text.Json.Nodes.JsonNode> Match(IncomingReconciliationInput input, int expected = 200) => fixture.Send("POST", "/api/admin/refunds/incoming/reconcile", input, expected);
    /// <summary>Đọc trạng thái cuối trong SQL để phát hiện trường hợp UI tự hiển thị thành công sai.</summary>
    private Task<string> State(OperationsScenario s) => fixture.Api.Sql($"SELECT TrangThai FROM dbo.DonHang WHERE MaDonHang={s.Order}");
}
