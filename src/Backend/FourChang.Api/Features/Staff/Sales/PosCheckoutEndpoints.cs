using System.Security.Claims;
using FourChang.Api.Shared.Operations;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Staff;
using Microsoft.EntityFrameworkCore;
namespace FourChang.Api.Features.Staff.Sales;
public static class PosCheckoutEndpoints
{
    /// <summary>POS tạo/đọc đơn trong ca hiện tại; Customer và ca đã khóa không thể thu tiền.</summary>
    public static void MapPosCheckout(this WebApplication app)
    {
        var group = app.MapGroup("/api/pos").RequireAuthorization("Staff");
        group.MapGet("/tables", async (CafeDbContext db) => await db.Ban.AsNoTracking().Where(x => x.DangSuDung).Select(x => new PosDiningTable(x.MaBan, x.TenBan)).ToListAsync());
        group.MapPost("/checkout", (PosCheckoutInput input, HttpContext context, PosCheckoutService service) => service.Checkout(input, context));
        group.MapGet("/checkout/{id:long}", async (long id, HttpContext context, PosCheckoutService service) =>
        {
            try { return Results.Ok(await service.Read(id, long.Parse(context.User.FindFirstValue("shift")!))); }
            catch (AdminRule ex) { return Results.Problem(statusCode: ex.Status, detail: ex.Message); }
        });
    }
}
