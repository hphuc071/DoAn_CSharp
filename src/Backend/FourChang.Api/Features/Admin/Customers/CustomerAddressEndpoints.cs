using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Customer;

namespace FourChang.Api.Features.Admin.Customers;

/// <summary>Điểm tích hợp địa chỉ Admin đã được chấp thuận; không cho Customer hoặc Staff gọi thay quyền.</summary>
public static class CustomerAddressEndpoints
{
    /// <summary>Đăng ký sổ địa chỉ theo mã khách và các lệnh có version/receipt; ngừng dùng thay vì DELETE.</summary>
    public static void MapAdminCustomerAddresses(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/customers").RequireAuthorization("Admin");
        group.MapGet("/{owner:long}/addresses", (long owner, CustomerAddressService service) => service.Get(owner));
        group.MapPost("/{owner:long}/addresses", (long owner, SaveSelfAddress input, HttpContext context, CustomerAddressService service) => service.Save(owner, null, input, context));
        group.MapPut("/{owner:long}/addresses/{id:long}", (long owner, long id, SaveSelfAddress input, HttpContext context, CustomerAddressService service) => service.Save(owner, id, input, context));
        group.MapPost("/{owner:long}/addresses/{id:long}/default", (long owner, long id, AdminAddressAction input, HttpContext context, CustomerAddressService service) => service.Act(owner, id, "default", input, context));
        group.MapPost("/{owner:long}/addresses/{id:long}/deactivate", (long owner, long id, AdminAddressAction input, HttpContext context, CustomerAddressService service) => service.Act(owner, id, "deactivate", input, context));
        group.MapGet("/address-operations/{id:guid}", (Guid id, HttpContext context, CustomerAddressService service) => service.Receipt(id, context));
    }
}
