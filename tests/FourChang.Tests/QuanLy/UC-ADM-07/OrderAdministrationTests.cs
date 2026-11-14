using System.Text.Json;
using FourChang.Tests.Shared;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Tests.Admin;

/// <summary>UC-ADM-07: mọi lệnh gọi HTTP thật, giá/định mức/tồn đều từ SQL fixture.</summary>
[Collection("AdminFeatures"), Trait("UseCase", "UC-ADM-07"), Trait("Actor", "QuanLy")]
public sealed class OrderAdministrationTests(AdminFeatureFixture fixture)
{
    /// <summary>Bắt đầu pha trừ kho đúng một lần; pha xong/hoàn tất không trừ lại và không thể hủy ly đã pha.</summary>
    [Fact]
    public async Task VongDoi_TruKhoLucBatDau_KhongTruLaiLucHoanTat()
    {
        var data = await OperationsScenarioSeed.Create(fixture);
        await Act(data.Order, "ACCEPT");
        var stockBefore = await fixture.Api.Sql("SELECT SUM(TonThucTe) FROM dbo.TonKho");
        var detail = await Detail(data.Order);
        var start = new OrderActionInput("START", "Bắt đầu 2 ly", detail.Don.PhienBanDuLieu, Guid.NewGuid(), data.Line, 2);
        await fixture.Send("POST", $"/api/admin/orders/{data.Order}/actions", start);
        await fixture.Send("POST", $"/api/admin/orders/{data.Order}/actions", start);
        var afterStart = await fixture.Api.Sql("SELECT SUM(TonThucTe) FROM dbo.TonKho"); Assert.NotEqual(stockBefore, afterStart);
        await Act(data.Order, "CANCEL", data.Line, 1, 409);
        await Act(data.Order, "FINISH", data.Line, 2); await Act(data.Order, "COMPLETE");
        Assert.Equal(afterStart, await fixture.Api.Sql("SELECT SUM(TonThucTe) FROM dbo.TonKho"));
        Assert.Equal("COMPLETED", (await Detail(data.Order)).Don.TrangThai);
        await Act(data.Order, "START", data.Line, 1, 409);
    }
    /// <summary>Hủy trước pha giải phóng giữ chỗ, tạo REQUESTED hoàn, không ghi khoản chi hoặc nhập nguyên liệu.</summary>
    [Fact]
    public async Task HuyMotPhan_GiaiPhongGiuCho_TaoHoSoChoHoan()
    {
        var data = await OperationsScenarioSeed.Create(fixture); await Act(data.Order, "ACCEPT");
        var before = await fixture.Api.Sql("SELECT SUM(TonThucTe) FROM dbo.TonKho");
        await Act(data.Order, "CANCEL", data.Line, 1);
        var detail = await Detail(data.Order);
        Assert.Equal(1, detail.Mon.Single().DaHuy); Assert.Equal(data.Total / 2, detail.Don.TaiChinh.PhaiHoan);
        Assert.Equal(before, await fixture.Api.Sql("SELECT SUM(TonThucTe) FROM dbo.TonKho"));
        Assert.Equal("1", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.HoSoHoanTien WHERE MaDonHang={data.Order} AND TrangThai='REQUESTED' AND LoaiNguon='CANCELLATION'"));
        Assert.Equal("0", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.LanChiHoanTien a JOIN dbo.HoSoHoanTien r ON r.MaHoanTien=a.MaHoanTien WHERE r.MaDonHang={data.Order}"));
    }
    /// <summary>Chặn bàn giao khi chưa thu đủ và chặn ghi đè version cũ.</summary>
    [Fact]
    public async Task ChuaThuDu_KhongHoanTat_VersionCuKhongGhiDe()
    {
        var data = await OperationsScenarioSeed.Create(fixture, paid: false); var old = await Detail(data.Order);
        await Act(data.Order, "ACCEPT");
        await fixture.Send("POST", $"/api/admin/orders/{data.Order}/actions", new OrderActionInput("START", "Phiên bản cũ", old.Don.PhienBanDuLieu, Guid.NewGuid(), data.Line, 2), 409);
        await Act(data.Order, "START", data.Line, 2); await Act(data.Order, "FINISH", data.Line, 2); await Act(data.Order, "COMPLETE", expected: 409);
        Assert.Equal("READY", (await Detail(data.Order)).Don.TrangThai);
    }
    /// <summary>Giao thất bại cần chứng cứ/mã xử lý, không nhập lại nguyên liệu đã pha; giao lại cần xác nhận.</summary>
    [Fact]
    public async Task GiaoThatBai_CoChungCu_GiaoLai_KhongHoiKho()
    {
        var s = await OperationsScenarioSeed.Create(fixture, "ACCEPTED");
        await fixture.Api.Sql($"UPDATE dbo.DonHang SET HinhThucNhanMon='DELIVERY',DiaChiGiaoDaChot=N'Địa chỉ kiểm thử',ViDo=10.77,KinhDo=106.69,KhoangCachGiaoKm=2 WHERE MaDonHang={s.Order}");
        await Act(s.Order, "START", s.Line, 2); await Act(s.Order, "FINISH", s.Line, 2); await Act(s.Order, "DELIVER");
        var stock = await fixture.Api.Sql("SELECT SUM(TonThucTe) FROM dbo.TonKho");
        await Act(s.Order, "FAIL_DELIVERY", expected:409);
        var order=await Detail(s.Order);
        await fixture.Send("POST", $"/api/admin/orders/{s.Order}/actions", new OrderActionInput("RETRY_DELIVERY", "Khách đồng ý giao lại", order.Don.PhienBanDuLieu, Guid.NewGuid(), ChungCu:"Khách xác nhận qua cuộc gọi"));
        Assert.Equal("DELIVERING", (await Detail(s.Order)).Don.TrangThai);
        order=await Detail(s.Order);
        await fixture.Send("POST", $"/api/admin/orders/{s.Order}/actions", new OrderActionInput("FAIL_DELIVERY", "Không liên hệ được khách", order.Don.PhienBanDuLieu, Guid.NewGuid(), ChungCu:"Biên bản giao kiểm thử", ResolutionCode:"CUSTOMER_UNREACHABLE"));
        Assert.Equal("DELIVERY_FAILED", (await Detail(s.Order)).Don.TrangThai);
        await Act(s.Order, "CANCEL", s.Line, 2, 409);
        await Act(s.Order, "RETRY_DELIVERY", expected:409);
        Assert.Equal(stock, await fixture.Api.Sql("SELECT SUM(TonThucTe) FROM dbo.TonKho"));
    }

    /// <summary>Địa chỉ hồ sơ có trên đơn mang đi; thay hồ sơ không làm đổi địa chỉ giao đã chốt.</summary>
    [Fact]
    public async Task DiaChiKhachHang_TachRiengDiaChiGiaoDaChot()
    {
        var scenario = await OperationsScenarioSeed.Create(fixture);
        var customer = await fixture.Api.NewCustomer("address");
        await fixture.Api.Sql($"""
            UPDATE dbo.DonHang SET MaKhachHang={customer.Id} WHERE MaDonHang={scenario.Order};
            INSERT dbo.DiaChiKhachHang(MaKhachHang,TenNguoiNhan,DienThoaiNguoiNhan,DiaChiChiTiet,LaDiaChiMacDinh,DangSuDung,ThoiDiemTao,ThoiDiemCapNhat)
            VALUES({customer.Id},N'Nguyễn Minh Anh','0901234567',N'123 Nguyễn Trãi, TP. Hồ Chí Minh',1,1,SYSUTCDATETIME(),SYSUTCDATETIME());
            """);
        var takeaway = await Detail(scenario.Order);
        Assert.Equal("123 Nguyễn Trãi, TP. Hồ Chí Minh", takeaway.DiaChiKhachHang);
        Assert.Null(takeaway.DiaChi);
        await fixture.Api.Sql($"UPDATE dbo.DonHang SET HinhThucNhanMon='DELIVERY',DiaChiGiaoDaChot=N'45 Lê Lợi, TP. Hồ Chí Minh' WHERE MaDonHang={scenario.Order}; UPDATE dbo.DiaChiKhachHang SET DiaChiChiTiet=N'Địa chỉ hồ sơ mới' WHERE MaKhachHang={customer.Id}");
        var delivery = await Detail(scenario.Order);
        Assert.Equal("45 Lê Lợi, TP. Hồ Chí Minh", delivery.DiaChi);
        Assert.Equal("Địa chỉ hồ sơ mới", delivery.DiaChiKhachHang);
    }

    /// <summary>Tạo lệnh từ version mới nhất, để test thể hiện rõ việc kiểm tra máy chủ.</summary>
    private async Task Act(long order, string action, long? line = null, int quantity = 0, int expected = 200)
    {
        var data = await Detail(order);
        await fixture.Send("POST", $"/api/admin/orders/{order}/actions", new OrderActionInput(action, "Kiểm thử xử lý đơn", data.Don.PhienBanDuLieu, Guid.NewGuid(), line, quantity), expected);
    }
    /// <summary>Đọc DTO dùng chung với màn hình Admin.</summary>
    private async Task<AdminOrderDetail> Detail(long id) => (await fixture.Send("GET", $"/api/admin/orders/{id}")).Deserialize<AdminOrderDetail>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
}
