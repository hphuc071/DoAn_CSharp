using FourChang.Presentation.Shared.Contracts.Customer;
namespace FourChang.Api.Features.Customer.Account;

// Chỉ định tuyến và áp quyền; service tự lấy chủ sở hữu từ token, không tin mã khách gửi lên.
public static class CustomerSelfEndpoints
{
    public static void MapCustomerSelf(this WebApplication app)
    {
        var group = app.MapGroup("/api/customer").RequireAuthorization("Customer");
        group.MapGet("/profile", (HttpContext c, CustomerSelfService s) => s.Get(c));
        group.MapPut("/profile", (SaveSelfProfile i, HttpContext c, CustomerSelfService s) => s.Save(i, c));
        group.MapPost("/addresses", (SaveSelfAddress i, HttpContext c, CustomerSelfService s) => s.Address(null, i, c));
        group.MapPut("/addresses/{id:long}", (long id, SaveSelfAddress i, HttpContext c, CustomerSelfService s) => s.Address(id, i, c));
        group.MapPost("/addresses/{id:long}/remove", (long id, RemoveSelfAddress i, HttpContext c, CustomerSelfService s) => s.Remove(id, i, c));
        group.MapPost("/password", (ChangeSelfPassword i, HttpContext c, CustomerSelfService s) => s.Password(i, c));
        app.MapGet("/api/customer/account/operations/{id:guid}", (Guid id, HttpContext c, CustomerSelfService s) => s.Operation(id, c));
    }
}
