using System.Text.Json;
using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using FourChang.Presentation.Shared.Contracts.Admin;
using static FourChang.Tests.Shared.TestData;
namespace FourChang.Tests.Admin;

/// <summary>UC-ADM-06: hồ sơ đã duyệt khi hủy chỉ theo dõi; chi thực qua Staff quầy, không tạo giao dịch giả.</summary>
[Collection("AdminFeatures"), Trait("UseCase", "UC-ADM-06"), Trait("Actor", "QuanLy")]
public sealed class RefundAdministrationTests(AdminFeatureFixture fixture)
{
    [Fact]
    public async Task DuyetHuy_TheoDoiChoNhan_ChiMotLan_ThuocDungNhanVienVaCa()
    {
        var s=await OperationsScenarioSeed.Create(fixture,"ACCEPTED",invoice:true);
        var staff=await OperationsScenarioSeed.RequestAndApprove(fixture,s);
        var before=(await All()).Single(x=>x.MaDonHang==s.Order);
        Assert.Equal("WAITING",before.TrangThaiTheoDoi); Assert.Equal(0,before.DaHoan);
        Assert.Equal(s.Total/2,before.ConHoan); Assert.Equal($"POS-{s.Order}",before.MaDon);
        var payable=(await fixture.Api.Send("GET",$"/api/pos/orders/{s.Order}/counter-refunds",headers:staff)).Expect(200,"Khoản được duyệt").Json.AsArray().Single()!;
        var command=Body(("NguoiNhan",payable.Text("nguoiNhan")),("ChungCu","Khách ký nhận tại quầy"),("DaGiaoTienVaKhachDaNhan",true),("PhienBanDuLieu",payable.Text("phienBanDuLieu")),("MaThaoTac",NewId()));
        (await fixture.Api.Send("POST",$"/api/pos/orders/counter-refunds/{before.MaHoanTien}",command,staff)).Expect(200,"Staff trả tiền");
        (await fixture.Api.Send("POST",$"/api/pos/orders/counter-refunds/{before.MaHoanTien}",command,staff)).Expect(200,"Replay không chi lại");
        var after=(await All()).Single(x=>x.MaHoanTien==before.MaHoanTien);
        Assert.Equal("PAID",after.TrangThaiTheoDoi); Assert.Equal(s.Total/2,after.DaHoan); Assert.Equal(0,after.ConHoan);
        var attempt=Assert.Single(after.LanChi); Assert.Equal("Nhân viên giao diện",attempt.NguoiChi); Assert.NotNull(attempt.MaCa); Assert.NotNull(attempt.CoKetQuaLuc);
        Assert.Equal("1",await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.LanChiHoanTien WHERE MaHoanTien={before.MaHoanTien}"));
        Assert.Equal("1",await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.HoaDon WHERE MaDonHang={s.Order}"));
        var filtered=await fixture.Send("GET",$"/api/admin/refunds?search={before.MaHoan}&status=PAID&from={DateTime.UtcNow.AddDays(-1):O}&to={DateTime.UtcNow.AddDays(1):O}");
        Assert.Single(filtered.AsArray());
        Assert.Empty((await fixture.Send("GET",$"/api/admin/refunds?search={before.MaHoan}&status=WAITING")).AsArray());
    }

    [Fact]
    public async Task KhoanHoanTaiQuay_KhongSuaDuyetHoacChiTuApiQuanLyCu()
    {
        var s=await OperationsScenarioSeed.Create(fixture,"ACCEPTED");
        await OperationsScenarioSeed.RequestAndApprove(fixture,s);
        var record=(await All()).Single(x=>x.MaDonHang==s.Order);
        await fixture.Send("POST",$"/api/admin/refunds/{record.MaHoanTien}/decision",new RefundDecisionInput(true,record.YeuCau,"Thử duyệt lại",true,record.PhienBanDuLieu,Guid.NewGuid()),409);
        await fixture.Send("POST",$"/api/admin/refunds/{record.MaHoanTien}/attempts",new RefundAttemptInput(record.YeuCau,null,DateTime.UtcNow.AddSeconds(-1),"Thử chi từ Admin",true,s.Shift,record.PhienBanDuLieu,Guid.NewGuid()),409);
        var request=new RefundRequestInput(s.Order,s.Payment,record.MaHuyMon,"CANCELLATION",record.YeuCau,"BANK_TRANSFER",record.NguoiNhan!,"Ngân hàng TEST","111122223333","Thử đổi khoản hoàn","Chứng cứ TEST",record.PhienBanDuLieu,Guid.NewGuid());
        await fixture.Send("PUT",$"/api/admin/refunds/{record.MaHoanTien}",request,409);
        await fixture.Send("POST","/api/admin/refunds",request with {PhienBanDuLieu=null,MaThaoTac=Guid.NewGuid()},409);
        await fixture.Send("POST","/api/admin/refunds",request with {MaHuyMon=null,PhienBanDuLieu=null,MaThaoTac=Guid.NewGuid()},409);
        var after=(await All()).Single(x=>x.MaHoanTien==record.MaHoanTien);
        Assert.Equal(record.PhienBanDuLieu,after.PhienBanDuLieu); Assert.Empty(after.LanChi); Assert.Equal("WAITING",after.TrangThaiTheoDoi);
    }

    [Fact]
    public async Task BoLocSai_BiChan_KhongTuGhiTien()
    {
        await fixture.Send("GET","/api/admin/refunds?status=SUCCEEDED",expected:400);
        await fixture.Send("GET","/api/admin/refunds?from=2026-11-20T00:00:00Z&to=2026-11-19T00:00:00Z",expected:400);
        var customer=await fixture.Api.Login(await fixture.Api.NewCustomer());
        (await fixture.Api.Send("GET","/api/admin/refunds",headers:customer)).Expect(403,"Khách không xem hồ sơ quán");
    }

    [Theory, InlineData("PENDING"), InlineData("FAILED"), InlineData("RECONCILING")]
    public void ChiChuaThanhCong_KhongTinhDaHoan(string state)
    {
        var attempt=new RefundAttemptView(1,1,30000,"CASH",state,null,DateTime.UtcNow,null,null,[]);
        var record=new RefundView(1,"HT-1",1,1,1,"CANCELLATION",30000,30000,"APPROVED","CASH","Khách",null,null,"Hủy",null,[],[attempt]);
        Assert.Equal(0,record.DaHoan); Assert.Equal(30000,record.ConHoan); Assert.NotEqual("PAID",record.TrangThaiTheoDoi);
    }
    private async Task<List<RefundView>> All() => (await fixture.Send("GET","/api/admin/refunds")).Deserialize<List<RefundView>>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
}
