using System.Security.Claims;
using FourChang.Presentation.Shared.Contracts.Customer;

namespace FourChang.Api.Features.Customer.Orders;

public static class CustomerOrderReadEndpoints
{
    public static void MapCustomerOrderDetails(this WebApplication app) =>
        app.MapGet("/api/customer/orders/{orderId:long}", DetailAsync).RequireAuthorization("Customer");

    public static async Task<IResult> DetailAsync(long orderId, HttpContext context,
        ICustomerOrderReader reader, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!Owner(context, out var owner)) return Results.Unauthorized();
        if (orderId <= 0) return Results.NotFound();
        var result = await reader.DetailAsync(owner, orderId, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    public static bool Owner(HttpContext context, out long id) =>
        long.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out id) && id > 0;

    public static async Task<IResult> ListAsync(HttpContext context, ICustomerOrderReader reader,
        int? page, int? pageSize, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!Owner(context, out var owner)) return Results.Unauthorized();
        var filter = new CustomerOrderFilter(page ?? 1, pageSize ?? 10, status, fromUtc, toUtc);
        var config = context.Features.Get<Microsoft.AspNetCore.Http.Features.IServiceProvidersFeature>()?.RequestServices?.GetService<IConfiguration>();
        var expiryReads = bool.TryParse(config?["Orders:ExpiryStatusReadEnabled"], out var enabled) && enabled;
        if (filter.Validate(expiryReads) is { } error) return Results.BadRequest(new { message = error });
        return Results.Ok(await reader.ListAsync(owner, filter, ct));
    }
}
