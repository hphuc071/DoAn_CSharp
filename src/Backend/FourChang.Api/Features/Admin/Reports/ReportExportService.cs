using System.Security.Claims;
using System.Text.Json;
using FourChang.Api.Shared.Operations;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.Reports;

/// <summary>Job xuất gắn Admin/snapshot, hỗ trợ polling, hủy và kiểm tra quyền lại khi tải tệp hoàn chỉnh.</summary>
public sealed class ReportExportService(CafeDbContext db, ReportSnapshotStore store, ReportPermissions permissions)
{
    /// <summary>Đăng ký job chống trùng; worker chỉ nhận metadata có audit request đã commit.</summary>
    public async Task<IResult> Start(ReportExportInput input, HttpContext context)
    {
        var actor = Actor(context); var snapshot = await store.Read(input.MaSnapshot, actor);
        permissions.Demand(snapshot.Audit ? "AuditExport" : "ReportsExport");
        if (input.DinhDang is not ("PDF" or "EXCEL")) throw new AdminRule(400, "Chỉ xuất Excel hoặc PDF.");
        return await AdminOperation.Execute(db, input.MaThaoTac, "REPORT_EXPORT_REQUEST", "ReportExport", input, context, async () =>
        {
            var job = new ReportExportJob(Guid.NewGuid(), "QUEUED", 0, null, input.MaSnapshot, input.DinhDang, snapshot.Snapshot.MocDuLieu);
            await store.Write(job.MaCongViec.ToString("N") + ".job.json", new StoredReportJob(actor, input.MaThaoTac, job));
            return (0L, (object)job, (string?)null, "Xuất " + (snapshot.Audit ? "nhật ký" : "báo cáo") + " theo snapshot");
        });
    }

    /// <summary>Chỉ chủ job đã được đăng ký trong audit mới được xem tiến độ; không đọc file người khác bằng Guid đoán.</summary>
    public async Task<StoredReportJob> Status(Guid id, HttpContext context)
    {
        var job = await store.Load<StoredReportJob>(id.ToString("N") + ".job.json") ?? throw new AdminRule(404, "Không tìm thấy job xuất.");
        if (job.Owner != Actor(context) || !await db.NhatKyHeThong.AnyAsync(x => x.MaThaoTac == job.Operation && x.MaQuanLy == job.Owner && x.HanhDong == "REPORT_EXPORT_REQUEST"))
            throw new AdminRule(404, "Không tìm thấy job xuất.");
        var snapshot = await store.Read(job.Job.MaSnapshot, job.Owner); permissions.Demand(snapshot.Audit ? "AuditExport" : "ReportsExport");
        return File.Exists(Path.Combine(store.Root, id.ToString("N") + ".cancel")) ? job with { Job=job.Job with { TrangThai="CANCELLED" } } : job;
    }

    /// <summary>Kiểm tra quyền, chủ sở hữu, trạng thái và tệp đầy đủ ở thời điểm tải, không trả tệp temp.</summary>
    public async Task<IResult> Download(Guid id, HttpContext context)
    {
        var stored = await Status(id, context);
        if (stored.Job.TrangThai != "READY" || File.Exists(Path.Combine(store.Root, id.ToString("N") + ".cancel"))) throw new AdminRule(409, "Tệp chưa sẵn sàng hoặc job đã hủy.");
        var file = store.Output(id, stored.Job.DinhDang);
        if (!File.Exists(file)) throw new AdminRule(503, "Không đọc được tệp đã xuất; không coi là tệp rỗng.");
        var bytes = await File.ReadAllBytesAsync(file);
        var audit = FourChang.Api.Shared.Audit.AuditFactory.Create("ADMIN", Actor(context), "REPORT_EXPORT_DOWNLOAD", "ReportExport", 0);
        audit.MaDoiTuong = id.ToString(); audit.TenNguoiThucHienDaChot = context.User.Identity?.Name ?? "Quản lý";
        audit.DuLieuSauJson = JsonSerializer.Serialize(new { stored.Job.MaSnapshot, stored.Job.MocDuLieu, stored.Job.DinhDang });
        db.NhatKyHeThong.Add(audit); await db.SaveChangesAsync(); context.Response.Headers.CacheControl = "no-store";
        return Results.File(bytes, stored.Job.DinhDang == "PDF" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "4CHANG-" + id.ToString("N") + (stored.Job.DinhDang == "PDF" ? ".pdf" : ".xlsx"));
    }

    /// <summary>Đánh dấu yêu cầu hủy; worker kiểm tra trước công bố file, UI không báo hủy là xuất thành công.</summary>
    public async Task<IResult> Cancel(Guid id, HttpContext context)
    {
        var job = await Status(id, context);
        var audit = FourChang.Api.Shared.Audit.AuditFactory.Create("ADMIN", Actor(context), "REPORT_EXPORT_CANCEL", "ReportExport", 0);
        audit.MaDoiTuong = id.ToString(); audit.TenNguoiThucHienDaChot = context.User.Identity?.Name ?? "Quản lý";
        db.NhatKyHeThong.Add(audit); await db.SaveChangesAsync();
        await File.WriteAllTextAsync(Path.Combine(store.Root, id.ToString("N") + ".cancel"), DateTime.UtcNow.ToString("O"));
        return Results.Ok(job.Job with { TrangThai = "CANCELLED" });
    }
    /// <summary>Khôi phục danh sách job của phiên Admin sau khi đóng cửa sổ; chỉ trả job đã commit audit và còn quyền.</summary>
    public async Task<IReadOnlyList<ReportExportJob>> List(HttpContext context)
    {
        var result = new List<ReportExportJob>();
        if (!Directory.Exists(store.Root)) return result;
        var actor = Actor(context);
        var operations = await db.NhatKyHeThong.Where(x => x.MaQuanLy == actor && x.HanhDong == "REPORT_EXPORT_REQUEST").Select(x => x.MaThaoTac).ToListAsync();
        foreach (var file in Directory.EnumerateFiles(store.Root, "*.job.json").OrderByDescending(File.GetLastWriteTimeUtc).Take(500))
        {
            var stored = await store.Load<StoredReportJob>(Path.GetFileName(file));
            if (stored is null || stored.Owner != actor || !operations.Contains(stored.Operation)) continue;
            try
            {
                var snapshot = await store.Read(stored.Job.MaSnapshot, actor);
                permissions.Demand(snapshot.Audit ? "AuditExport" : "ReportsExport");
                result.Add(File.Exists(Path.Combine(store.Root, stored.Job.MaCongViec.ToString("N") + ".cancel")) ? stored.Job with { TrangThai="CANCELLED" } : stored.Job);
            }
            catch (AdminRule ex) when (ex.Status is 403 or 404) { }
        }
        return result;
    }

    /// <summary>Lấy Admin từ phiên, không nhận mã người yêu cầu trong request body.</summary>
    private static long Actor(HttpContext context) => long.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
