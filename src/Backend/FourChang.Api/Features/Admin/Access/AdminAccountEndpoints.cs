using FourChang.Presentation.Shared.Contracts.Admin;
namespace FourChang.Api.Features.Admin.Access;

public static class AdminAccountEndpoints
{
    public static void MapAdminAccount(this WebApplication app)
    {
        app.MapGet("/api/admin/access/account", (AdminAccountService service) => service.GetProfile()).RequireAuthorization("Admin");
        // Chỉ phiên Admin đầy đủ hoặc phiên đổi mật khẩu của Admin; Customer và Staff không dùng được.
        app.MapGet("/api/auth/admin/password-context", (AdminAccountService service) => service.GetProfile()).RequireAuthorization("AdminPassword");
        app.MapPost("/api/auth/admin/password", (AdminPasswordInput input, HttpContext context, AdminAccountService service) => service.ChangePassword(input, context)).RequireAuthorization("AdminPassword");
        app.MapGet("/api/auth/admin/password-operations/{operation:guid}", (Guid operation, HttpContext context, AdminAccountService service) => service.PasswordReceipt(operation, context));
    }
}
