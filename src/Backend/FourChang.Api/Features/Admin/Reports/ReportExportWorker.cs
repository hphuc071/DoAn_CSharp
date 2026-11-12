using System.Text.Json;
using FourChang.Api.Shared.Audit;
using FourChang.Api.Shared.Documents;
using FourChang.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.Reports;

/// <summary>Tạo file ở background, khóa file ngăn hai worker làm cùng job; phục hồi QUEUED/RUNNING sau restart.</summary>
public sealed class ReportExportWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<ReportExportWorker> logger) : BackgroundService
{
    /// <summary>Polling có hủy; chỉ log mã lỗi, không ghi snapshot báo cáo hoặc thông tin ngân hàng vào log ứng dụng.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope(); var store = scope.ServiceProvider.GetRequiredService<ReportSnapshotStore>();
                if (Directory.Exists(store.Root))
                    foreach (var file in Directory.EnumerateFiles(store.Root, "*.job.json")) await Process(file, store, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Chưa xử lý được job báo cáo: {Type}", ex.GetType().Name); }
            try { await Task.Delay(750, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Chỉ công bố READY sau file hoàn chỉnh và audit bền vững; lỗi/hủy không đưa file dở cho người dùng.</summary>
    private async Task Process(string file, ReportSnapshotStore store, CancellationToken ct)
    {
        FileStream lease;
        try { lease = new FileStream(file + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { return; }
        await using var held = lease;
        var saved = await store.Load<StoredReportJob>(Path.GetFileName(file));
        if (saved is null || saved.Job.TrangThai is not ("QUEUED" or "RUNNING")) return;
        using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CafeDbContext>();
        if (!await db.NhatKyHeThong.AnyAsync(x => x.MaThaoTac == saved.Operation && x.MaQuanLy == saved.Owner && x.HanhDong == "REPORT_EXPORT_REQUEST", ct)) return;
        var cancel = Path.Combine(store.Root, saved.Job.MaCongViec.ToString("N") + ".cancel");
        if (File.Exists(cancel)) { await store.Write(Path.GetFileName(file), saved with { Job = saved.Job with { TrangThai = "CANCELLED" } }); return; }
        try
        {
            var data = await store.Read(saved.Job.MaSnapshot, saved.Owner);
            scope.ServiceProvider.GetRequiredService<ReportPermissions>().Demand(data.Audit ? "AuditExport" : "ReportsExport");
            await store.Write(Path.GetFileName(file), saved with { Job = saved.Job with { TrangThai = "RUNNING", TienDo = 20 } });
            var bytes = await Task.Run(() => saved.Job.DinhDang == "PDF" ? ReportDocumentWriter.Pdf(data.Snapshot, configuration["Reports:FontPath"], ct) : ReportDocumentWriter.Excel(data.Snapshot, ct), ct);
            ct.ThrowIfCancellationRequested();
            if (File.Exists(cancel)) { await store.Write(Path.GetFileName(file), saved with { Job = saved.Job with { TrangThai = "CANCELLED" } }); return; }
            var target = store.Output(saved.Job.MaCongViec, saved.Job.DinhDang); var temp = target + ".tmp";
            await File.WriteAllBytesAsync(temp, bytes, ct); File.Move(temp, target, true);
            var audit = AuditFactory.Create("SYSTEM", 0, "REPORT_EXPORT_READY", "ReportExport", 0);
            audit.MaDoiTuong = saved.Job.MaCongViec.ToString(); audit.TenNguoiThucHienDaChot = "Dịch vụ xuất báo cáo";
            audit.DuLieuSauJson = JsonSerializer.Serialize(new { saved.Owner, saved.Job.MaSnapshot, saved.Job.MocDuLieu, saved.Job.DinhDang, Bytes = bytes.Length });
            db.NhatKyHeThong.Add(audit); await db.SaveChangesAsync(ct);
            await store.Write(Path.GetFileName(file), saved with { Job = saved.Job with { TrangThai = "READY", TienDo = 100 } });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; } // Để RUNNING cho lần khởi động sau, không giả là thất bại chắc chắn.
        catch (Exception ex)
        {
            logger.LogWarning("Job xuất chưa hoàn tất: {Type}", ex.GetType().Name);
            await store.Write(Path.GetFileName(file), saved with { Job = saved.Job with { TrangThai = "FAILED", Loi = "Không tạo được tệp hoàn chỉnh. Kiểm tra nguồn/font/quyền lưu và thử lại." } });
        }
    }
}
