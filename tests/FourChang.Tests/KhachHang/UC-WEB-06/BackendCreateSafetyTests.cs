using System.Data.Common;
using System.Security.Claims;
using System.Text.Json;
using FourChang.Api.Features.Customer.Orders;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FourChang.Tests;

[Trait("Safety", "NoDatabase")]
public sealed class BackendCreateSafetyTests
{
    private sealed class BodyAvailable : IHttpRequestBodyDetectionFeature
    { public bool CanHaveBody => true; }
    private sealed class BlockSql : DbConnectionInterceptor
    {
        public int Attempts;
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData data, InterceptionResult result, CancellationToken ct = default)
        { Attempts++; throw new InvalidOperationException("SQL forbidden"); }
        public override InterceptionResult ConnectionOpening(DbConnection connection,
            ConnectionEventData data, InterceptionResult result)
        { Attempts++; throw new InvalidOperationException("SQL forbidden"); }
    }
    private static CreateCustomerOrderRequest Request() => new("TAKEAWAY", null,
        "Offline Customer", "0900000000", null, null, null, []);

    [Theory]
    [InlineData(null)] [InlineData("false")] [InlineData("invalid")] [InlineData("1")]
    public async Task Actual_backend_route_rejects_direct_post_before_sql_even_with_web_flag_and_customer(string? flag)
    {
        var monitor = new BlockSql();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["Orders:CustomerCreationEnabled"] = flag, ["Checkout:OrderCreationEnabled"] = "true" });
        builder.Services.AddDbContext<CafeDbContext>(o => o.UseSqlServer(
            "Server=forbidden.invalid;Database=NoDatabase;Integrated Security=true").AddInterceptors(monitor));
        builder.Services.AddScoped<ICustomerOrderReader, CustomerOrderReader>();
        await using var app = builder.Build(); // No Start/Run/Kestrel, no API Program or .env loader.
        app.MapCustomerOrders();
        var route = Assert.Single(((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>(), x => x.RoutePattern.RawText == "/api/customer/orders" &&
                x.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains("POST"));
        Assert.Equal("Customer", Assert.Single(route.Metadata.GetOrderedMetadata<IAuthorizeData>()).Policy);
        await using var scope = app.Services.CreateAsyncScope();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(Request());
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.User = new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "6")], "Offline"));
        context.Request.Method = "POST"; context.Request.Path = "/api/customer/orders";
        context.Request.ContentType = "application/json"; context.Request.ContentLength = bytes.Length;
        context.Request.Body = new MemoryStream(bytes);
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyAvailable());
        context.Request.Headers["Idempotency-Key"] = Guid.NewGuid().ToString();
        context.Response.Body = new MemoryStream();
        await route.RequestDelegate!(context);
        Assert.Equal(503, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        Assert.Contains("CUSTOMER_ORDER_CREATION_DISABLED", await new StreamReader(context.Response.Body).ReadToEndAsync());
        Assert.Equal(0, monitor.Attempts);
        Assert.Empty(scope.ServiceProvider.GetRequiredService<CafeDbContext>().ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(false, 401)] [InlineData(true, 400)]
    public async Task Explicit_backend_flag_keeps_customer_and_idempotency_validation(bool customer, int status)
    {
        var monitor = new BlockSql();
        await using var db = new CafeDbContext(new DbContextOptionsBuilder<CafeDbContext>()
            .UseSqlServer("Server=forbidden.invalid;Database=NoDatabase;Integrated Security=true")
            .AddInterceptors(monitor).Options);
        var context = new DefaultHttpContext();
        if (customer) context.User = new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "6")], "Offline"));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Orders:CustomerCreationEnabled"] = "true" }).Build();
        var result = await CustomerOrdersEndpoints.CreateAsync(Request(), context, db, default, config);
        Assert.Equal(status, ((IStatusCodeHttpResult)result).StatusCode);
        Assert.Equal(0, monitor.Attempts);
    }

    [Fact]
    public async Task Delivery_sql_store_rejects_all_calls_without_flags_before_opening_sql()
    {
        var monitor = new BlockSql();
        await using var db = new CafeDbContext(new DbContextOptionsBuilder<CafeDbContext>()
            .UseSqlServer("Server=forbidden.invalid;Database=NoDatabase;Integrated Security=true")
            .AddInterceptors(monitor).Options);
        var store = new SqlOrderOutboxDeliveryStore(db, new ConfigurationBuilder().Build());
        var lease = new OrderOutboxLease(1, Guid.NewGuid(), "{}");
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ClaimAsync(default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CompleteAsync(lease, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.FailAsync(lease, "DELIVERY_OR_ENVELOPE_FAILED", default));
        Assert.Equal(0, monitor.Attempts);
    }
}
