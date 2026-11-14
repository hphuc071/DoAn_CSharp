using FourChang.Api.Shared.Operations;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.Refunds;

/// <summary>UC-ADM-09: chỉ Admin được yêu cầu, duyệt, ghi lần chi và xác minh sao kê ở các bước riêng.</summary>
public static class RefundAdministrationEndpoints
{
    /// <summary>Giữ các route này riêng Admin. Staff chi tiền mặt đã duyệt qua endpoint counter-refunds, không được tự tăng thu/hạn mức.</summary>
    public static void MapRefundAdministration(this WebApplication app)
    {
        var g = app.MapGroup("/api/admin/refunds").RequireAuthorization("Admin");
        g.MapGet("", (RefundAdministrationService s) => s.Read());
        g.MapGet("/{id:long}", (long id, RefundAdministrationService s) => s.Details(id));
        g.MapPost("", (RefundRequestInput i, HttpContext c, RefundAdministrationService s) => s.Save(null, i, c));
        g.MapPut("/{id:long}", (long id, RefundRequestInput i, HttpContext c, RefundAdministrationService s) => s.Save(id, i, c));
        g.MapPost("/{id:long}/decision", (long id, RefundDecisionInput i, HttpContext c, RefundAdministrationService s) => s.Decide(id, i, c));
        g.MapPost("/{id:long}/attempts", (long id, RefundAttemptInput i, HttpContext c, RefundAdministrationService s) => s.Begin(id, i, c));
        g.MapPost("/attempts/{id:long}/verify", (long id, RefundVerificationInput i, HttpContext c, RefundAdministrationService s) => s.Verify(id, i, c));
        g.MapGet("/operations/{id:guid}", (Guid id, CafeDbContext db, HttpContext c) => AdminOperation.Receipt(db, id, "HoSoHoanTien", c));
        g.MapPost("/incoming/reconcile", (IncomingReconciliationInput i, HttpContext c, IncomingReconciliationService s) => s.Match(i, c));
        g.MapGet("/incoming/{id:long}", async (long id, CafeDbContext db) =>
        {
            var payment = await db.ThanhToan.Where(x => x.MaThanhToan == id && x.PhuongThuc == "BANK_TRANSFER")
                .Select(x => new IncomingPaymentView(x.MaThanhToan, x.MaDonHang, x.SoTienYeuCau, x.SoTienDaXacMinh, x.TrangThai, x.PhienBanDuLieu)).SingleOrDefaultAsync();
            return payment is null ? Results.NotFound() : Results.Ok(payment);
        });
    }
}
