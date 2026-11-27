// UC: UC-ADM-08
// Chức năng: Khai báo API quản lý và đối chiếu ca.
// Cần nhớ: Đọc hàm từ bước kiểm tra dữ liệu đến bước trả kết quả; các quyết định nghiệp vụ do Backend kiểm tra.

using FourChang.Api.Shared.Operations;
using FourChang.Api.Shared.WorkSessions;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Api.Features.Admin.WorkSessions;

/// <summary>HTTP contract UC-ADM-08; Customer và Staff không thể dùng các lệnh xác nhận của Admin.</summary>
public static class WorkSessionEndpoints
{
    /// <summary>Đăng ký tìm kiếm, chi tiết, receipt, chốt và bàn giao; không có lệnh xóa/sửa giao dịch nguồn.</summary>
    public static void MapAdminWorkSessions(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/work-sessions").RequireAuthorization("Admin");
        group.MapGet("/", (long? employee, DateTime? from, DateTime? to, string? name, string? status, string? machine, string? search,
            int? page, int? pageSize, WorkSessionService service) => service.Search(employee, from, to, name, status, machine, page ?? 1, pageSize ?? 20, search));
        group.MapGet("/{id:long}", (long id, WorkSessionService service) => service.Details(id));
        group.MapPost("/{id:long}/close", (long id, CloseWorkSessionInput input, HttpContext context, WorkSessionService service) => service.Close(id, input, context));
        group.MapPost("/{id:long}/handover", (long id, HandoverWorkSessionInput input, HttpContext context, WorkSessionService service) => service.Handover(id, input, context));
        group.MapPost("/{id:long}/recovery", (long id, IssueShiftRecoveryInput input, HttpContext context, ShiftRecoveryService service) => service.Issue(id, input, context));
        group.MapGet("/operations/{id:guid}", (Guid id, CafeDbContext db, HttpContext context) => AdminOperation.Receipt(db, id, "CaLamViec", context));
    }
}
