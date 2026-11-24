using System.Text.Json;
using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Orders;
using static FourChang.Tests.Shared.TestData;
namespace FourChang.Tests.Admin;

/// <summary>UC-ADM-05: HTTP/SQL thật trong database GUID, kiểm tra phân vai và kết quả duyệt.</summary>
[Collection("AdminFeatures"), Trait("UseCase", "UC-ADM-05"), Trait("Actor", "QuanLy")]
public sealed class OrderAdministrationTests(AdminFeatureFixture fixture)
{
    [Theory, InlineData("ACCEPT"), InlineData("START"), InlineData("FINISH"), InlineData("DELIVER"), InlineData("COMPLETE"), InlineData("CANCEL"), InlineData("RETRY_DELIVERY"), InlineData("EXPIRE")]
    public async Task QuanLy_KhongThayNhanVienXuLyDon(string action)
    {
        var s = await OperationsScenarioSeed.Create(fixture);
        var before = await Detail(s.Order);
        var stock = await fixture.Api.Sql("SELECT SUM(TonThucTe) FROM dbo.TonKho");
        await fixture.Send("POST", $"/api/admin/orders/{s.Order}/actions", new OrderActionInput(action,"Kiểm thử phân quyền",before.Don.PhienBanDuLieu,Guid.NewGuid(),s.Line,2),403);
        var after = await Detail(s.Order);
        Assert.Equal(before.Don.PhienBanDuLieu,after.Don.PhienBanDuLieu);
        Assert.Equal(stock,await fixture.Api.Sql("SELECT SUM(TonThucTe) FROM dbo.TonKho"));
    }

    [Theory, InlineData(true,true), InlineData(false,true), InlineData(true,false)]
    public async Task QuyetDinhHuy_GiuLichSu_KhongChiTienSom(bool approve, bool paid)
    {
        var s = await OperationsScenarioSeed.Create(fixture,"ACCEPTED",paid);
        var employee = await fixture.Api.Employee(fixture.Admin,"Nhân viên kiểm thử hủy");
        var opened = (await fixture.Api.Send("POST","/api/pos/shifts",Body(("MaNhanVien",employee.Number("maNhanVien")),("HoTen",employee.Text("hoTen")),("TenCa","Sáng"),("MaMay",Guid.NewGuid().ToString("N")),("TienMatDauCa",500000),("MaThaoTac",NewId())))).Expect(200,"Mở ca").Json;
        var staff = TestApi.Bearer(opened);
        var before = await Detail(s.Order); var group = Guid.NewGuid();
        var input = JsonSerializer.SerializeToNode(new OrderActionInput("CANCEL","Khách yêu cầu hủy",before.Don.PhienBanDuLieu,group,s.Line,2));
        (await fixture.Api.Send("POST",$"/api/pos/orders/{s.Order}/actions",input,staff)).Expect(200,"Gửi yêu cầu");
        var pending = await Detail(s.Order);
        Assert.Equal("ACCEPTED",pending.Don.TrangThai);
        Assert.Equal(2,pending.MonYeuCauHuy!.Single().SoLy);
        var command = new CancellationCommand(approve?"APPROVE":"REJECT",group,"Kết quả kiểm tra",pending.Don.PhienBanDuLieu,Guid.NewGuid(),paid);
        await fixture.Send("POST",$"/api/admin/orders/{s.Order}/cancellation",command with { PhienBanDuLieu=before.Don.PhienBanDuLieu, MaThaoTac=Guid.NewGuid() },409);
        await fixture.Send("POST",$"/api/admin/orders/{s.Order}/cancellation",command);
        await fixture.Send("POST",$"/api/admin/orders/{s.Order}/cancellation",command);
        var result = await Detail(s.Order);
        Assert.Equal(approve?"CANCELLED":"ACCEPTED",result.Don.TrangThai);
        Assert.Equal(approve?"APPROVED":"REJECTED",result.Don.KetQuaHuy!.TrangThai);
        Assert.Equal("0",await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.LanChiHoanTien c JOIN dbo.HoSoHoanTien h ON h.MaHoanTien=c.MaHoanTien WHERE h.MaDonHang={s.Order}"));
        var outcome=approve?"APPROVED":"REJECTED";
        var list=await fixture.Send("GET",$"/api/admin/orders?NhomDon=CANCELLATION&KetQuaHuy={outcome}&TimKiem={Uri.EscapeDataString(await fixture.Api.Sql($"SELECT MaDonHienThi FROM dbo.DonHang WHERE MaDonHang={s.Order}"))}&KichThuocTrang=1");
        Assert.Equal(1,list["tongSo"]!.GetValue<int>());
        Assert.Equal(s.Order,list["duLieu"]![0]!["maDonHang"]!.GetValue<long>());
    }

    [Fact]
    public async Task GiaoThatBai_ChiCanLyDo_KhongHoiKho()
    {
        var s=await OperationsScenarioSeed.Create(fixture,"DELIVERING");
        await fixture.Api.Sql($"UPDATE dbo.DonHang SET HinhThucNhanMon='DELIVERY',DiaChiGiaoDaChot=N'12 Nguyễn Trãi' WHERE MaDonHang={s.Order}");
        var detail=await Detail(s.Order); var stock=await fixture.Api.Sql("SELECT SUM(TonThucTe) FROM dbo.TonKho");
        var command=new OrderActionInput("FAIL_DELIVERY","Không liên hệ được khách",detail.Don.PhienBanDuLieu,Guid.NewGuid());
        await fixture.Send("POST",$"/api/admin/orders/{s.Order}/actions",command);
        await fixture.Send("POST",$"/api/admin/orders/{s.Order}/actions",command);
        Assert.Equal("DELIVERY_FAILED",(await Detail(s.Order)).Don.TrangThai);
        Assert.Equal(stock,await fixture.Api.Sql("SELECT SUM(TonThucTe) FROM dbo.TonKho"));
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

    private async Task<AdminOrderDetail> Detail(long id) => (await fixture.Send("GET", $"/api/admin/orders/{id}")).Deserialize<AdminOrderDetail>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
}
