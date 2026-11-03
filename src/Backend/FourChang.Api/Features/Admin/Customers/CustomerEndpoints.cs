using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Api.Features.Admin.Customers;

public static class CustomerEndpoints
{
    public static void MapAdminCustomers(this WebApplication app)
    {
        // BR-08: bảo vệ cả nhóm ở API; ẩn nút WinForms không đủ ngăn khách/POS gọi trực tiếp.
        var group = app.MapGroup("/api/admin/customers").RequireAuthorization("Admin");
        group.MapGet("", (string? search, int? page, int? pageSize, CustomerAccountService service) =>
            service.Search(search, page ?? 1, pageSize ?? 20));
        group.MapGet("/{id:long}", (long id, CustomerAccountService service) => service.Details(id));
        group.MapGet("/operations/{operation:guid}", (Guid operation, HttpContext context, CustomerAccountService service) => service.Operation(operation, context));
        group.MapPost("", (CustomerSaveInput input, HttpContext context, CustomerAccountService service) => service.Save(null, input, context));
        group.MapPut("/{id:long}", (long id, CustomerSaveInput input, HttpContext context, CustomerAccountService service) => service.Save(id, input, context));
        group.MapPost("/{id:long}/status", (long id, CustomerActionInput input, HttpContext context, CustomerAccountService service) => service.Act(id, "STATUS", input, context));
        group.MapPost("/{id:long}/reset-password", (long id, CustomerActionInput input, HttpContext context, CustomerAccountService service) => service.Act(id, "RESET_PASSWORD", input, context));
        group.MapDelete("/{id:long}", (long id, [Microsoft.AspNetCore.Mvc.FromBody] CustomerActionInput input, HttpContext context, CustomerAccountService service) => service.Act(id, "DELETE", input, context));
    }
}
