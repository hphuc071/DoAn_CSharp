using System.Security.Cryptography;
using System.Data;
using FourChang.Api.Features.Admin.Access;
using FourChang.Api.Features.Customer.Account;
using FourChang.Presentation.Shared.Contracts.Common;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.EntityFrameworkCore;
namespace FourChang.Api.Shared.Authentication;

public static class AuthEndpoints
{
    public static void MapAuth(this WebApplication app)
    {
        // UC-WEB-01/02 dùng service riêng để chia sẻ quy tắc với hồ sơ và tránh trộn quyền Admin.
        app.MapPost("/api/auth/customer/register", (RegisterCustomer i, CustomerAccessService s) => s.Register(i)).RequireRateLimiting("auth");
        app.MapGet("/api/auth/customer/registrations/{id:guid}", (Guid id, CustomerAccessService s) => s.Registration(id)).RequireRateLimiting("auth");
        app.MapPost("/api/auth/{role}/login", async (string role, SignIn input, CustomerAccessService customer, AdminAccountService admin) =>
            role == "admin" ? await admin.Login(input) : role == "customer" ? await customer.Login(input) : Results.BadRequest()).RequireRateLimiting("auth");
        app.MapPost("/api/auth/logout", async (HttpContext c, CustomerAccessService customer, AdminAccountService admin) =>
            c.User.IsInRole("ADMIN") || c.User.IsInRole("ADMIN_PASSWORD_CHANGE") ? await admin.Logout(c) : await customer.Logout(c));
    }
}
