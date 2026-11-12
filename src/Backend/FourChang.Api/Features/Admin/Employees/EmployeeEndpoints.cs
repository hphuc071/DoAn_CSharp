using FourChang.Api.Shared.Operations;
using FourChang.Api.Shared.WorkSessions;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.Employees;

/// <summary>Định tuyến ADM-12; mọi endpoint đều yêu cầu phiên Admin đầy đủ.</summary>
public static class EmployeeEndpoints
{
    /// <summary>Giữ đường dẫn danh sách cũ cho các client hiện có; bổ sung tìm kiếm/chi tiết/receipt cho màn hình nghiệp vụ.</summary>
    public static void MapEmployees(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/employees").RequireAuthorization("Admin");
        group.MapGet("/", async (CafeDbContext db) => (await db.NhanVien.AsNoTracking().OrderBy(x => x.HoTen).ToListAsync()).Select(EmployeeService.View));
        group.MapGet("/search", (string? search, string? status, int? page, int? pageSize, EmployeeService service) =>
            service.Search(search, status, page ?? 1, pageSize ?? 20));
        group.MapGet("/{id:long}", (long id, EmployeeService service) => service.Details(id));
        group.MapPost("/", (EmployeeInput input, HttpContext context, EmployeeService service) => service.Save(null, input, context));
        group.MapPut("/{id:long}", (long id, EmployeeInput input, HttpContext context, EmployeeService service) => service.Save(id, input, context));
        group.MapPost("/{id:long}/status", (long id, EmployeeStatusInput input, HttpContext context, EmployeeService service) => service.Status(id, input, context));
        group.MapPost("/{id:long}/delete", (long id, DeleteEmployeeInput input, HttpContext context, EmployeeService service) => service.Delete(id, input, context));
        group.MapGet("/operations/{id:guid}", (Guid id, CafeDbContext db, HttpContext context) => AdminOperation.Receipt(db, id, "NhanVien", context));
    }
}
