using System.Security.Cryptography;
using System.Data;
using FourChang.Domain.Entities.Orders;
using FourChang.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
namespace FourChang.Api.Features.Customer.Orders;

public static class CustomerOrdersEndpoints
{
    public static void MapCustomerOrders(this WebApplication app)
    {
        app.MapGet("/api/customer/orders", async (HttpContext context, CafeDbContext db) =>
        {
            var id = long.Parse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            return await db.DonHang.AsNoTracking().Where(x => x.MaKhachHang == id).OrderByDescending(x => x.ThoiDiemTao).Take(100).Select(x => new { x.MaDonHang, x.MaDonHienThi, x.HinhThucNhanMon, x.TrangThai, x.ThoiDiemTao }).ToListAsync();
        }).RequireAuthorization("Customer");
    }
}
