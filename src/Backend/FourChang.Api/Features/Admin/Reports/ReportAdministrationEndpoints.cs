using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Api.Features.Admin.Reports;

/// <summary>UC-ADM-10: quyền Admin và quyền báo cáo/audit/xuất được kiểm tra ở phía máy chủ.</summary>
public static class ReportAdministrationEndpoints
{
    /// <summary>Đăng ký báo cáo, audit, snapshot và job xuất; không có API sửa/xóa audit.</summary>
    public static void MapReportAdministration(this WebApplication app)
    {
        var g = app.MapGroup("/api/admin/reports").RequireAuthorization("Admin");
        g.MapPost("/snapshot", (ReportFilter f, HttpContext c, ReportAdministrationService s) => s.Read(f, c));
        g.MapGet("/audit", ([AsParameters] AuditFilter f, HttpContext c, ReportAdministrationService s) => s.Audit(f, c));
        g.MapPost("/audit/snapshot", (AuditFilter f, HttpContext c, ReportAdministrationService s) => s.AuditSnapshot(f, c));
        g.MapGet("/exports", (HttpContext c, ReportExportService s) => s.List(c));
        g.MapPost("/exports", (ReportExportInput i, HttpContext c, ReportExportService s) => s.Start(i, c));
        g.MapGet("/exports/operations/{id:guid}", (Guid id, FourChang.Infrastructure.Database.Context.CafeDbContext db, HttpContext c) =>
            FourChang.Api.Shared.Operations.AdminOperation.Receipt(db, id, "ReportExport", c));
        g.MapGet("/exports/{id:guid}", async (Guid id, HttpContext c, ReportExportService s) => (await s.Status(id, c)).Job);
        g.MapGet("/exports/{id:guid}/file", (Guid id, HttpContext c, ReportExportService s) => s.Download(id, c));
        g.MapPost("/exports/{id:guid}/cancel", (Guid id, HttpContext c, ReportExportService s) => s.Cancel(id, c));
    }
}
