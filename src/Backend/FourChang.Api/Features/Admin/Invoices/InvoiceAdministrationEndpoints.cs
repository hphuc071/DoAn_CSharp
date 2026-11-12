using FourChang.Api.Shared.Operations;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Api.Features.Admin.Invoices;

/// <summary>UC-ADM-08 không có route sửa/xóa bản gốc, mọi lệnh đều yêu cầu Admin.</summary>
public static class InvoiceAdministrationEndpoints
{
    /// <summary>Đăng ký tra cứu, chi tiết, điều chỉnh và nhật ký in/xuất độc lập với thanh toán.</summary>
    public static void MapInvoiceAdministration(this WebApplication app)
    {
        var g = app.MapGroup("/api/admin/invoices").RequireAuthorization("Admin");
        g.MapGet("", (string? search, DateTime? from, DateTime? to, long? shift, int? page, InvoiceAdministrationService s) => s.Read(search, from, to, shift, page ?? 1));
        g.MapGet("/{id:long}", (long id, InvoiceAdministrationService s) => s.Details(id));
        g.MapGet("/{id:long}/pdf", async (long id, HttpContext context, InvoicePdfService s) =>
        { context.Response.Headers.CacheControl = "no-store"; return Results.File(await s.Render(id), "application/pdf", $"invoice-{id}.pdf"); });
        g.MapPost("/{id:long}/adjustments", (long id, InvoiceAdjustmentInput i, HttpContext c, InvoiceAdministrationService s) => s.Adjust(id, i, c));
        g.MapPost("/{id:long}/documents/log", (long id, DocumentLogInput i, HttpContext c, InvoiceAdministrationService s) => s.Log(id, i, c));
        g.MapGet("/operations/{id:guid}", (Guid id, CafeDbContext db, HttpContext c) => AdminOperation.Receipt(db, id, "HoaDon", c));
    }
}
