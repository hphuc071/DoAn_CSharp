using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using FourChang.Api.Features.Admin.Reports;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Tests.Shared;
using Microsoft.Extensions.Configuration;

namespace FourChang.Tests.Admin;

/// <summary>UC-ADM-10: chỉ tiêu thật, file xuất đúng mốc, không có công thức Excel và quyền audit độc lập.</summary>
[Collection("AdminFeatures"), Trait("UseCase", "UC-ADM-10"), Trait("Actor", "QuanLy")]
public sealed class ReportAdministrationTests(AdminFeatureFixture fixture)
{
    /// <summary>Đơn hoàn tất và CASH đã xác minh có cùng giá trị nhưng là hai chỉ tiêu riêng; dữ liệu kỳ rỗng có tỷ lệ null.</summary>
    [Fact]
    public async Task DoanhThuVaDongTien_TachRieng_KyRongKhongCoTyLeGia()
    {
        var scenario = await OperationsScenarioSeed.Create(fixture, "COMPLETED");
        var filter = new ReportFilter(DateTime.UtcNow.Date.AddDays(-1), DateTime.UtcNow.Date.AddDays(1), MaCa: scenario.Shift);
        var snapshot = await Snapshot(filter);
        Assert.Equal(scenario.Total, snapshot.ChiSo.DoanhThuThuan); Assert.Equal(scenario.Total, snapshot.ChiSo.ThuTienMat); Assert.Equal(0, snapshot.ChiSo.ThuChuyenKhoan);
        Assert.Equal(scenario.Total, snapshot.ChiSo.DongTienRong); Assert.Equal(1, snapshot.ChiSo.HoanTat);
        var empty = await Snapshot(filter with { TuLuc = DateTime.UtcNow.Date.AddYears(-10), DenLuc = DateTime.UtcNow.Date.AddYears(-9) });
        Assert.Equal(0, empty.ChiSo.DoanhThuThuan); Assert.Equal(0, empty.ChiSo.ThuTienMat); Assert.Null(empty.ChiSo.TyLeHoanTat);
    }
    /// <summary>Xuất workbook/PDF qua job thật, source thay đổi sau snapshot không làm số liệu file đổi theo.</summary>
    [Theory]
    [InlineData("EXCEL")]
    [InlineData("PDF")]
    public async Task JobXuat_DungSnapshot_KhongCoFileDo(string format)
    {
        var scenario = await OperationsScenarioSeed.Create(fixture, "COMPLETED");
        var snapshot = await Snapshot(new(DateTime.UtcNow.Date.AddDays(-1), DateTime.UtcNow.Date.AddDays(1), MaCa: scenario.Shift));
        var input = new ReportExportInput(snapshot.MaSnapshot, format, Guid.NewGuid());
        var job = (await fixture.Send("POST", "/api/admin/reports/exports", input)).Deserialize<ReportExportJob>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var duplicate = await fixture.Send("POST", "/api/admin/reports/exports", input);
        Assert.Equal(job.MaCongViec.ToString(), duplicate.Text("maCongViec"));
        for (var i = 0; i < 100; i++)
        {
            job = (await fixture.Send("GET", $"/api/admin/reports/exports/{job.MaCongViec}")).Deserialize<ReportExportJob>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            if (job.TrangThai is "READY" or "FAILED") break; await Task.Delay(200);
        }
        Assert.True(job.TrangThai == "READY", job.Loi); Assert.Equal(snapshot.MocDuLieu, job.MocDuLieu);
        var history = (await fixture.Send("GET", "/api/admin/reports/exports")).Deserialize<List<ReportExportJob>>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Contains(history, x => x.MaCongViec == job.MaCongViec && x.TrangThai == "READY");
        using var client = new HttpClient(); client.DefaultRequestHeaders.Add("Authorization", fixture.Admin["Authorization"]);
        using var response = await client.GetAsync(fixture.Api.Url + $"/api/admin/reports/exports/{job.MaCongViec}/file");
        Assert.True(response.IsSuccessStatusCode); var bytes = await response.Content.ReadAsByteArrayAsync();
        await File.WriteAllBytesAsync(Path.Combine(fixture.Api.Artifacts, "report-" + format + (format == "PDF" ? ".pdf" : ".xlsx")), bytes);
        if (format == "PDF") Assert.True(bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8));
        else
        {
            using var zip = new ZipArchive(new MemoryStream(bytes)); XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            Assert.NotNull(zip.GetEntry("xl/workbook.xml"));
            foreach (var entry in zip.Entries.Where(x => x.FullName.StartsWith("xl/worksheets/")))
            { using var stream = entry.Open(); var xml = XDocument.Load(stream); Assert.Empty(xml.Descendants(ns + "f")); }
            Assert.Contains(zip.Entries, x => x.FullName == "xl/worksheets/sheet2.xml");
        }
    }
    /// <summary>Thu chuyển khoản nhiều đợt được tính theo từng giao dịch, không dồn hết vào giờ xác minh cuối.</summary>
    [Fact]
    public async Task ThuNhieuDot_KhacKy_KhongDonVaoLanXacMinhCuoi()
    {
        var s = await OperationsScenarioSeed.Create(fixture, paid:false);
        await fixture.Api.Sql($"UPDATE dbo.ThanhToan SET PhuongThuc='BANK_TRANSFER',TienKhachDua=NULL WHERE MaThanhToan={s.Payment}");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var yesterday = DateTime.UtcNow.Date.AddDays(-1).AddHours(12);
        foreach (var time in new[] { yesterday, DateTime.UtcNow.AddMinutes(-1) })
        {
            var p = (await fixture.Send("GET", $"/api/admin/refunds/incoming/{s.Payment}")).Deserialize<IncomingPaymentView>(options)!;
            await fixture.Send("POST", "/api/admin/refunds/incoming/reconcile", new IncomingReconciliationInput(s.Payment, "TEST", "ACCOUNT-TEST", Guid.NewGuid().ToString("N"), s.Total/2, time, "Sao kê kiểm thử đúng thời điểm", "Đối chiếu từng giao dịch", true, p.PhienBanDuLieu, Guid.NewGuid()));
        }
        var previous = await Snapshot(new(yesterday.Date, yesterday.Date.AddDays(1), MaCa:s.Shift));
        var today = await Snapshot(new(DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(1), MaCa:s.Shift));
        Assert.Equal(s.Total/2, previous.ChiSo.ThuChuyenKhoan);
        Assert.Equal(s.Total/2, today.ChiSo.ThuChuyenKhoan);
        Assert.Equal(0, today.ChiSo.DoanhThuThuan);
    }

    /// <summary>Che trường bí mật trong mảng lồng nhau và quyền audit không phụ thuộc quyền reports.</summary>
    [Fact]
    public void Audit_CheBiMat_QuyenDocVaXuatDocLap()
    {
        var cleaned = AuditRedaction.Clean("{\"MatKhau\":\"hidden-password\",\"Items\":[{\"Token\":\"hidden-token\",\"MaCa\":12}],\"TaiKhoan\":\"111122223333\"}")!;
        Assert.DoesNotContain("hidden-password", cleaned); Assert.DoesNotContain("hidden-token", cleaned); Assert.DoesNotContain("111122223333", cleaned); Assert.Contains("12", cleaned);
        var permissions = new ReportPermissions(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AdminPermissions:ReportsRead"] = "false", ["AdminPermissions:AuditRead"] = "true", ["AdminPermissions:AuditExport"] = "false" }).Build());
        permissions.Demand("AuditRead"); Assert.Throws<FourChang.Api.Shared.Operations.AdminRule>(() => permissions.Demand("ReportsRead"));
        Assert.Throws<FourChang.Api.Shared.Operations.AdminRule>(() => permissions.Demand("AuditExport"));
    }
    /// <summary>Chụp báo cáo qua HTTP bằng cùng DTO và định nghĩa mà UI hiển thị.</summary>
    private async Task<ReportSnapshot> Snapshot(ReportFilter filter) => (await fixture.Send("POST", "/api/admin/reports/snapshot", filter)).Deserialize<ReportSnapshot>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
}
