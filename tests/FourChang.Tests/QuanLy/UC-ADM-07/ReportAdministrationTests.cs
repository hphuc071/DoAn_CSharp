using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using FourChang.Api.Features.Admin.Reports;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Tests.Shared;
using Microsoft.Extensions.Configuration;
using static FourChang.Tests.Shared.TestData;
namespace FourChang.Tests.Admin;

/// <summary>UC-ADM-07: báo cáo/Excel từ SQL fixture thật, không cộng đơn chưa hoàn thành hoặc hoàn chưa chi.</summary>
[Collection("AdminFeatures"), Trait("UseCase", "UC-ADM-07"), Trait("Actor", "QuanLy")]
public sealed class ReportAdministrationTests(AdminFeatureFixture fixture)
{
    [Fact]
    public async Task ChiTinhDonHoanThanh_BoLyHuy_KhongLayGiaMenuMoi()
    {
        var completed=await OperationsScenarioSeed.Create(fixture,"COMPLETED");
        foreach(var state in new[]{"PENDING_CONFIRMATION","ACCEPTED","PREPARING","READY","CANCELLED","DELIVERY_FAILED"})
        {
            var other=await OperationsScenarioSeed.Create(fixture,state);
            await fixture.Api.Sql($"UPDATE dbo.DonHang SET MaCaLamViec={completed.Shift} WHERE MaDonHang={other.Order}");
        }
        var result=await Snapshot(Period(completed.Shift));
        Assert.Equal("REPORT_SIMPLE",result.Loai); Assert.Equal(1,result.ChiSo.HoanTat); Assert.Equal(completed.Total,result.ChiSo.DoanhThuThuan);
        Assert.Equal(new[]{"Đơn hoàn thành","Món bán chạy","Tiền đã hoàn"},result.Bang.Select(x=>x.Ten));
        Assert.Single(result.Bang[0].Dong); Assert.Equal($"POS-{completed.Order}",result.Bang[0].Dong[0][0]);
        Assert.Equal("2",Assert.Single(result.Bang[1].Dong)[1]); Assert.Empty(result.Bang[2].Dong);
        var empty=await Snapshot(Period(completed.Shift) with {TuLuc=DateTime.UtcNow.AddYears(-10),DenLuc=DateTime.UtcNow.AddYears(-9)});
        Assert.Equal(0,empty.ChiSo.HoanTat); Assert.Equal(0,empty.ChiSo.DoanhThuThuan); Assert.All(empty.Bang,x=>Assert.Empty(x.Dong));
    }

    [Fact]
    public async Task HoanTien_ChiTinhKhiDaTra_ThuocCaChiKhongThuocCaCu()
    {
        var data=await OperationsScenarioSeed.Create(fixture,"ACCEPTED");
        var staff=await OperationsScenarioSeed.RequestAndApprove(fixture,data);
        var refund=(await fixture.Send("GET","/api/admin/refunds")).Deserialize<List<RefundView>>(JsonOptions)!.Single(x=>x.MaDonHang==data.Order);
        Assert.Empty((await Snapshot(Period(data.Shift))).Bang[2].Dong);
        var payload=Body(("NguoiNhan",refund.NguoiNhan),("ChungCu","Khách ký nhận tại quầy"),("DaGiaoTienVaKhachDaNhan",true),("PhienBanDuLieu",Convert.ToBase64String(refund.PhienBanDuLieu)),("MaThaoTac",NewId()));
        (await fixture.Api.Send("POST",$"/api/pos/orders/counter-refunds/{refund.MaHoanTien}",payload,staff)).Expect(200,"Chi hoàn thật");
        (await fixture.Api.Send("POST",$"/api/pos/orders/counter-refunds/{refund.MaHoanTien}",payload,staff)).Expect(200,"Không chi lại");
        var shift=long.Parse(await fixture.Api.Sql($"SELECT MaCaLamViec FROM dbo.LanChiHoanTien WHERE MaHoanTien={refund.MaHoanTien}"));
        Assert.NotEqual(data.Shift,shift);
        var report=await Snapshot(Period(shift));
        Assert.Equal(0,report.ChiSo.HoanTat); Assert.Equal(0,report.ChiSo.DoanhThuThuan); Assert.Equal(data.Total/2,report.ChiSo.HoanTienMat);
        Assert.Equal($"POS-{data.Order}",Assert.Single(report.Bang[2].Dong)[0]);
        Assert.Equal(0,(await Snapshot(Period(data.Shift))).ChiSo.HoanTienMat);
    }

    [Fact]
    public async Task Excel_DungSnapshot_CoNamSheet_KhongCoCongThuc_PdfBiChan()
    {
        var data=await OperationsScenarioSeed.Create(fixture,"COMPLETED"); var snapshot=await Snapshot(Period(data.Shift));
        await fixture.Send("POST","/api/admin/reports/exports",new ReportExportInput(snapshot.MaSnapshot,"PDF",Guid.NewGuid()),400);
        var input=new ReportExportInput(snapshot.MaSnapshot,"EXCEL",Guid.NewGuid());
        var job=(await fixture.Send("POST","/api/admin/reports/exports",input)).Deserialize<ReportExportJob>(JsonOptions)!;
        Assert.Equal(job.MaCongViec.ToString(),(await fixture.Send("POST","/api/admin/reports/exports",input)).Text("maCongViec"));
        // Một đơn mới hoàn thành sau snapshot không được lọt vào workbook của kỳ đã chụp.
        var later=await OperationsScenarioSeed.Create(fixture,"COMPLETED");
        await fixture.Api.Sql($"UPDATE dbo.DonHang SET MaCaLamViec={data.Shift} WHERE MaDonHang={later.Order}");
        for(var i=0;i<100;i++)
        {
            job=(await fixture.Send("GET",$"/api/admin/reports/exports/{job.MaCongViec}")).Deserialize<ReportExportJob>(JsonOptions)!;
            if(job.TrangThai is "READY" or "FAILED")break; await Task.Delay(150);
        }
        Assert.True(job.TrangThai=="READY",job.Loi);
        using var client=new HttpClient(); client.DefaultRequestHeaders.Add("Authorization",fixture.Admin["Authorization"]);
        var bytes=await client.GetByteArrayAsync(fixture.Api.Url+$"/api/admin/reports/exports/{job.MaCongViec}/file");
        await File.WriteAllBytesAsync(Path.Combine(fixture.Api.Artifacts,"ADM07-report.xlsx"),bytes);
        using var zip=new ZipArchive(new MemoryStream(bytes)); XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        Assert.Equal(5,zip.Entries.Count(x=>x.FullName.StartsWith("xl/worksheets/sheet")&&x.FullName.EndsWith(".xml")));
        using(var stream=zip.GetEntry("xl/worksheets/sheet2.xml")!.Open())
        {
            var xml=XDocument.Load(stream); Assert.Contains(xml.Descendants(ns+"v"),x=>x.Value==data.Total.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Assert.DoesNotContain("Tỷ lệ",xml.ToString());
        }
        foreach(var entry in zip.Entries.Where(x=>x.FullName.StartsWith("xl/worksheets/")))
        {using var stream=entry.Open(); Assert.Empty(XDocument.Load(stream).Descendants(ns+"f"));}
        using(var stream=zip.GetEntry("xl/worksheets/sheet3.xml")!.Open())
        {var xml=XDocument.Load(stream).ToString();Assert.Contains($"POS-{data.Order}",xml);Assert.DoesNotContain($"POS-{later.Order}",xml);}
    }

    [Fact]
    public async Task BoLocSaiVaKhach_BiChan_NhatKyChiDocVaCheBiMat()
    {
        await fixture.Send("POST","/api/admin/reports/snapshot",new ReportFilter(DateTime.UtcNow,DateTime.UtcNow.AddDays(-1)),400);
        await fixture.Send("POST","/api/admin/reports/snapshot",Period(null) with {Kenh="UNKNOWN"},400);
        var customer=await fixture.Api.Login(await fixture.Api.NewCustomer());
        (await fixture.Api.Send("GET","/api/admin/reports/audit",headers:customer)).Expect(403,"Khách không xem nhật ký");
        (await fixture.Api.Send("POST","/api/admin/reports/snapshot",JsonSerializer.SerializeToNode(Period(null)),customer)).Expect(403,"Khách không xem báo cáo");
        var audit=(await fixture.Send("GET","/api/admin/reports/audit")).Deserialize<AdminPage<AdminTraceRow>>(JsonOptions)!;
        Assert.True(audit.TongSo>=0);
        var denied=await fixture.Api.Send("DELETE","/api/admin/reports/audit",headers:fixture.Admin); Assert.True(denied.Status is 404 or 405);
    }

    [Fact]
    public void Audit_CheBiMat_QuyenDocVaXuatDocLap()
    {
        var cleaned=AuditRedaction.Clean("{\"MatKhau\":\"secret-pass\",\"Items\":[{\"Token\":\"secret-token\",\"MaCa\":12}],\"TaiKhoan\":\"111122223333\"}")!;
        Assert.DoesNotContain("secret-pass",cleaned); Assert.DoesNotContain("secret-token",cleaned); Assert.DoesNotContain("111122223333",cleaned); Assert.Contains("12",cleaned);
        var permission=new ReportPermissions(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["AdminPermissions:ReportsRead"]="false",["AdminPermissions:AuditRead"]="true",["AdminPermissions:AuditExport"]="false" }).Build());
        permission.Demand("AuditRead"); Assert.Throws<FourChang.Api.Shared.Operations.AdminRule>(()=>permission.Demand("ReportsRead"));
        Assert.Throws<FourChang.Api.Shared.Operations.AdminRule>(()=>permission.Demand("AuditExport"));
    }
    /// <summary>Đi qua Staff gửi hủy/Admin duyệt rồi pha phần còn lại: số ly hủy không vào top món hay tổng đơn.</summary>
    [Fact]
    public async Task DonHuyMotPhan_HoanThanhChiCongPhanDaPhucVu()
    {
        var data=await OperationsScenarioSeed.Create(fixture,"ACCEPTED");
        var staff=await OperationsScenarioSeed.RequestAndApprove(fixture,data);
        foreach(var action in new[]{"START","FINISH","COMPLETE"})
        {
            var detail=(await fixture.Send("GET",$"/api/admin/orders/{data.Order}")).Deserialize<AdminOrderDetail>(JsonOptions)!;
            var input=new OrderActionInput(action,"",detail.Don.PhienBanDuLieu,Guid.NewGuid(),action=="COMPLETE"?null:data.Line,1);
            (await fixture.Api.Send("POST",$"/api/pos/orders/{data.Order}/actions",JsonSerializer.SerializeToNode(input),staff)).Expect(200,"Pha/bàn giao phần còn lại");
        }
        var completionShift=long.Parse(await fixture.Api.Sql($"SELECT TOP(1) MaCaLamViec FROM dbo.NhatKyHeThong WHERE LoaiDoiTuong='DonHang' AND HanhDong='ORDER_COMPLETE' AND MaDoiTuong='{data.Order}' ORDER BY MaNhatKy DESC"));
        var report=await Snapshot(Period(completionShift));
        Assert.Equal(1,report.ChiSo.HoanTat); Assert.Equal(data.Total/2,report.ChiSo.DoanhThuThuan);
        Assert.Equal("1",Assert.Single(report.Bang[1].Dong)[1]); Assert.Equal(0,report.ChiSo.HoanTienMat);
    }
    private static readonly JsonSerializerOptions JsonOptions=new(JsonSerializerDefaults.Web);
    private static ReportFilter Period(long? shift)=>new(DateTime.UtcNow.Date.AddDays(-1),DateTime.UtcNow.Date.AddDays(1),MaCa:shift);
    private async Task<ReportSnapshot> Snapshot(ReportFilter filter)=>(await fixture.Send("POST","/api/admin/reports/snapshot",filter)).Deserialize<ReportSnapshot>(JsonOptions)!;
}