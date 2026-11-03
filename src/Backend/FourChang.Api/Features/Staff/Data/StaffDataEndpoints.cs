using FourChang.Api.Shared.Data;
using System.Globalization;
using FourChang.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
namespace FourChang.Api.Features.Staff.Data;

public static class StaffDataEndpoints
{
    public static void MapStaffData(this WebApplication app)
    {
        app.MapGet("/api/pos/data/DonHang",async(CafeDbContext db)=>await TableReader.Read(db.DonHang)).RequireAuthorization("Staff");
        app.MapGet("/api/pos/data/HoaDon",async(CafeDbContext db)=>await TableReader.Read(db.HoaDon)).RequireAuthorization("Staff");
    }
}
