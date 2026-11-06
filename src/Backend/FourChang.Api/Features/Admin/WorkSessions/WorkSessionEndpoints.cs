using FourChang.Api.Shared.WorkSessions;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Api.Features.Admin.WorkSessions;

/// <summary>HTTP contract ADM-13; Customer và Staff không thể dùng các lệnh xác nhận của Admin.</summary>
public static class WorkSessionEndpoints
{
    /// <summary>Đăng ký tìm kiếm, chi tiết, receipt, chốt và bàn giao; không có lệnh xóa/sửa giao dịch nguồn.</summary>
    public static void MapAdminWorkSessions(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/work-sessions").RequireAuthorization("Admin");
        group.MapGet("/", (long? employee, DateTime? from, DateTime? to, string? name, string? status, string? machine,
            int? page, int? pageSize, WorkSessionService service) => service.Search(employee, from, to, name, status, machine, page ?? 1, pageSize ?? 20));
        group.MapGet("/{id:long}", (long id, WorkSessionService service) => service.Details(id));
        group.MapPost("/{id:long}/resume", (long id, ResumeWorkSessionInput input, HttpContext context, WorkSessionService service) => service.Resume(id, input, context));
        group.MapPost("/{id:long}/close", (long id, CloseWorkSessionInput input, HttpContext context, WorkSessionService service) => service.Close(id, input, context));
        group.MapPost("/{id:long}/handover", (long id, HandoverWorkSessionInput input, HttpContext context, WorkSessionService service) => service.Handover(id, input, context));
        group.MapGet("/operations/{id:guid}", (Guid id, CafeDbContext db, HttpContext context) => WorkforceOperation.Receipt(db, id, "CaLamViec", context));
    }
}
