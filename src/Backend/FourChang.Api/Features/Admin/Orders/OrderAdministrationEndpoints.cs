using FourChang.Api.Shared.Operations;
using FourChang.Api.Shared.Orders;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Api.Features.Admin.Orders;

/// <summary>UC-ADM-07: Admin giám sát; POS gọi cùng service cho tiến trình, không được gọi ngoại lệ Admin.</summary>
public static class OrderAdministrationEndpoints
{
    /// <summary>Đăng ký truy vấn/phân trang, lệnh có version và địa chỉ có chấp thuận.</summary>
    public static void MapOrderAdministration(this WebApplication app)
    {
        var g = app.MapGroup("/api/admin/orders").RequireAuthorization("Admin");
        g.MapGet("", ([AsParameters] AdminOrderFilter filter, OrderAdministrationService service) => service.Read(filter));
        g.MapGet("/{id:long}", (long id, OrderAdministrationService service) => service.Details(id));
        g.MapPost("/{id:long}/actions", (long id, OrderActionInput input, HttpContext context, OrderWorkflowService service) => service.Execute(id, input, context));
        g.MapPost("/{id:long}/address", (long id, OrderAddressInput input, HttpContext context, OrderAdministrationService service) => service.Address(id, input, context));
        g.MapGet("/operations/{id:guid}", (Guid id, CafeDbContext db, HttpContext context) => AdminOperation.Receipt(db, id, "DonHang", context));
        app.MapPost("/api/pos/orders/{id:long}/actions", (long id, OrderActionInput input, HttpContext context, OrderWorkflowService service) => service.Execute(id, input, context)).RequireAuthorization("Staff");
    }
}
