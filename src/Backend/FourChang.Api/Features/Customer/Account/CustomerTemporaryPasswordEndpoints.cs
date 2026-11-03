using System.Security.Claims;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.EntityFrameworkCore;
namespace FourChang.Api.Features.Customer.Account;

// WEB-02 / ADM-01: phiên mật khẩu tạm chỉ lấy version và đổi mật khẩu; chưa được đọc hồ sơ/đơn.
public static class CustomerTemporaryPasswordEndpoints
{
    public static void MapCustomerTemporaryPassword(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth/customer/temporary-password").RequireAuthorization("CustomerPasswordChange");
        group.MapGet("", async (HttpContext c, CafeDbContext db) =>
        {
            var id = long.Parse(c.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var user = await db.TaiKhoanKhachHang.AsNoTracking().SingleAsync(x => x.MaKhachHang == id);
            return Results.Ok(new { required = true, user.PhienBanDuLieu });
        });
        group.MapPost("", (ChangeTemporaryPassword input, HttpContext c, CustomerSelfService s) => s.TemporaryPassword(input, c));
    }
}
