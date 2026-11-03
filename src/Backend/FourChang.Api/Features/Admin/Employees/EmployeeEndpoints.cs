using FourChang.Api.Shared.Audit;
using System.Data;
using System.Security.Claims;
using FourChang.Domain.Entities.Audit;
using FourChang.Domain.Entities.Employees;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Staff;
using Microsoft.EntityFrameworkCore;
namespace FourChang.Api.Features.Admin.Employees;

public static class EmployeeEndpoints
{
    // Quản lý hồ sơ nhân viên; không tạo tài khoản đăng nhập cá nhân.
    public static void MapEmployees(this WebApplication app)
    {
        app.MapGet("/api/admin/employees", async (CafeDbContext db) => await db.NhanVien.AsNoTracking().OrderBy(x => x.HoTen).Select(x => new Employee(x.MaNhanVien, x.HoTen, x.SoDienThoai, x.NgayVaoLam, x.TrangThai, x.PhienBanDuLieu)).ToListAsync()).RequireAuthorization("Admin");
        app.MapPost("/api/admin/employees", async (EmployeeInput input, HttpContext context, CafeDbContext db) => await SaveEmployee(null, input, context, db)).RequireAuthorization("Admin");
        app.MapPut("/api/admin/employees/{id:long}", async (long id, EmployeeInput input, HttpContext context, CafeDbContext db) => await SaveEmployee(id, input, context, db)).RequireAuthorization("Admin");
    }
    private static async Task<IResult> SaveEmployee(long? id, EmployeeInput input, HttpContext context, CafeDbContext db)
    {
        if (string.IsNullOrWhiteSpace(input.HoTen) || input.HoTen.Length > 120 || input.SoDienThoai?.Length > 20 || input.TrangThai is not ("ACTIVE" or "INACTIVE")) return Results.Problem(detail: "Thông tin nhân viên không hợp lệ.", statusCode: 400);
        await using var tx = await db.Database.BeginTransactionAsync();
        var employee = id is null ? new NhanVien() : await db.NhanVien.SingleOrDefaultAsync(x => x.MaNhanVien == id);
        if (employee is null) return Results.NotFound();
        if (id is not null && (input.PhienBanDuLieu is null || !input.PhienBanDuLieu.SequenceEqual(employee.PhienBanDuLieu))) return Results.Problem(detail: "Hồ sơ đã thay đổi. Tải lại trước khi lưu.", statusCode: 409);
        if (id is not null && input.TrangThai == "INACTIVE" && await db.CaLamViec.AnyAsync(x => x.MaNhanVien == id && x.KetThucLuc == null)) return Results.Problem(detail: "Cần kết thúc ca trước khi ngừng sử dụng hồ sơ nhân viên.", statusCode: 409);
        employee.HoTen = input.HoTen.Trim(); employee.SoDienThoai = string.IsNullOrWhiteSpace(input.SoDienThoai) ? null : input.SoDienThoai.Trim(); employee.NgayVaoLam = input.NgayVaoLam.Date; employee.TrangThai = input.TrangThai;
        if (id is null) db.NhanVien.Add(employee); await db.SaveChangesAsync();
        db.NhatKyHeThong.Add(AuditFactory.Create("ADMIN", long.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)!.Value), id is null ? "CREATE_EMPLOYEE" : "UPDATE_EMPLOYEE", "NhanVien", employee.MaNhanVien));
        await db.SaveChangesAsync(); await tx.CommitAsync();
        return Results.Ok(new Employee(employee.MaNhanVien, employee.HoTen, employee.SoDienThoai, employee.NgayVaoLam, employee.TrangThai, employee.PhienBanDuLieu));
    }
}
