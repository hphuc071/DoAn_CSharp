using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using FourChang.Api.Features.Customer.Orders;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FourChang.Tests;

[Trait("Safety", "NoDatabase")]
public sealed class DineInCreatePreparationTests
{
    private static readonly string[] RequiredGates =
    [
        "Orders:CustomerCreationEnabled",
        "Orders:SqlIntegrationValidated",
        "Orders:DineInCreationEnabled",
        "Orders:DineIn:BusinessApproved",
        "Orders:DineIn:SqlWriterValidated",
        "Orders:FulfillmentPreviewEnabled",
        "Orders:TableQrProtocolValidated",
        "Orders:TableQr:IssuanceSqlValidated"
    ];

    private static IConfigurationRoot Config(bool all = false) => new ConfigurationBuilder()
        .AddInMemoryCollection(RequiredGates.ToDictionary(x => x, x => (string?)(all ? "true" : "false")))
        .Build();

    private static DineInOrderCreateRequest Valid() => new(
        WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32)),
        new string('A', 60),
        [new OrderItemInput(1, 1, 50, 50, null, [])]);

    [Fact]
    public void Writer_is_off_by_default_and_each_independent_gate_is_required()
    {
        Assert.False(DineInOrderCreation.Ready(new ConfigurationBuilder().Build()));
        var config = Config(all: true);
        Assert.True(DineInOrderCreation.Ready(config)); // A pure policy check: never executes SQL.
        foreach (var key in RequiredGates)
        {
            config[key] = "false";
            Assert.False(DineInOrderCreation.Ready(config));
            config[key] = "Development";
            Assert.False(DineInOrderCreation.Ready(config));
            config[key] = "true";
        }
    }

    [Fact]
    public void Canonical_qr_proof_and_cart_are_required_before_any_writer()
    {
        var value = Valid();
        Assert.Null(DineInOrderCreation.Validate(value));
        Assert.Equal("DINE_IN_CART_INVALID", DineInOrderCreation.Validate(value with { Items = [] }));
        Assert.Equal("DINE_IN_CART_INVALID", DineInOrderCreation.Validate(value with
        { Items = [new OrderItemInput(1, 0, 50, 50, null, [])] }));
        Assert.Equal("DINE_IN_CART_INVALID", DineInOrderCreation.Validate(value with
        { Items = [new OrderItemInput(1, 1, 101, 50, null, [])] }));
        Assert.Equal("DINE_IN_CART_INVALID", DineInOrderCreation.Validate(value with
        { Items = [new OrderItemInput(1, 1, 50, 50, null, [new OrderToppingInput(2, 0)])] }));
        Assert.Equal("DINE_IN_NOTE_INVALID", DineInOrderCreation.Validate(value with { GhiChu = "bad\nline" }));
        Assert.Equal("DINE_IN_QR_INVALID", DineInOrderCreation.Validate(value with { QrToken = "TABLE-5" }));
        Assert.Equal("DINE_IN_QR_INVALID", DineInOrderCreation.Validate(value with { QrToken = value.QrToken + "\n" }));
        Assert.Equal("DINE_IN_QR_INVALID", DineInOrderCreation.Validate(value with { QrToken = new string('A', 43) + "=" }));
        Assert.Equal("DINE_IN_PROOF_INVALID", DineInOrderCreation.Validate(value with { PreviewProof = "fake" }));
        Assert.Equal("DINE_IN_PROOF_INVALID", DineInOrderCreation.Validate(value with { PreviewProof = new string('A', 8193) }));
    }

    [Fact]
    public void Separate_v2_fingerprint_binds_secret_proof_cart_and_note_without_changing_takeaway_shape()
    {
        var original = Valid();
        var fingerprint = DineInOrderCreation.Hash(original);
        Assert.Equal(fingerprint, DineInOrderCreation.Hash(original));
        Assert.NotEqual(Convert.ToHexString(fingerprint), Convert.ToHexString(DineInOrderCreation.Hash(original with
        { QrToken = Valid().QrToken })));
        Assert.NotEqual(Convert.ToHexString(fingerprint), Convert.ToHexString(DineInOrderCreation.Hash(original with
        { PreviewProof = new string('B', 60) })));
        Assert.NotEqual(Convert.ToHexString(fingerprint), Convert.ToHexString(DineInOrderCreation.Hash(original with
        { Items = [new OrderItemInput(1, 2, 50, 50, null, [])] })));
        Assert.NotEqual(Convert.ToHexString(fingerprint), Convert.ToHexString(DineInOrderCreation.Hash(original with
        { GhiChu = "Đổi món" })));
        var preview = DineInOrderCreation.ToPreview(original);
        Assert.Equal("DINE_IN", preview.Method);
        Assert.Equal(original.QrToken, preview.QrToken);
        Assert.Equal(original.PreviewProof, preview.PreviousProof);
        Assert.Null(preview.Address); Assert.Null(preview.Recipient);
        Assert.Equal(SHA256.HashData(WebEncoders.Base64UrlDecode(original.QrToken!)),
            DineInOrderCreation.Digest(original));

        var legacy = new CreateCustomerOrderRequest("TAKEAWAY", null, "Test", "0900000000",
            null, null, null, original.Items);
        Assert.DoesNotContain("thoiDiemHenNhan", JsonSerializer.Serialize(legacy), StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(Convert.ToHexString(DineInOrderCreation.Hash(original)),
            Convert.ToHexString(OrderIdempotency.Hash(legacy)));
    }

    [Fact]
    public async Task New_v2_route_rejects_off_before_database_or_preview_services_are_touched()
    {
        await using var db = new CafeDbContext(new DbContextOptionsBuilder<CafeDbContext>()
            .UseSqlServer("Server=must-not-connect.invalid;Database=OfflineOnly;Integrated Security=true").Options);
        var context = new DefaultHttpContext();
        context.User = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "6")], "Offline"));
        context.Request.Headers["Idempotency-Key"] = Guid.NewGuid().ToString("D");
        context.Request.Headers["X-Checkout-Accepted-Total"] = "60000";
        var config = Config(all: true);
        config["Orders:DineIn:SqlWriterValidated"] = "false";
        var result = await CustomerOrdersEndpoints.CreateDineInAsync(Valid(), context, db,
            null!, null!, TimeProvider.System, default, config);
        Assert.Equal(503, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.Equal(ConnectionState.Closed, db.Database.GetDbConnection().State);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Even_with_flags_enabled_http_without_tls_cannot_reach_database()
    {
        await using var db = new CafeDbContext(new DbContextOptionsBuilder<CafeDbContext>()
            .UseSqlServer("Server=must-not-connect.invalid;Database=OfflineOnly;Integrated Security=true").Options);
        var context = new DefaultHttpContext();
        var result = await CustomerOrdersEndpoints.CreateDineInAsync(Valid(), context, db,
            null!, null!, TimeProvider.System, default, Config(all: true));
        Assert.Equal(503, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(ConnectionState.Closed, db.Database.GetDbConnection().State);
    }

    [Fact]
    public async Task Legacy_create_stays_locked_for_dine_in_even_when_all_new_flags_are_true()
    {
        await using var db = new CafeDbContext(new DbContextOptionsBuilder<CafeDbContext>()
            .UseSqlServer("Server=must-not-connect.invalid;Database=OfflineOnly;Integrated Security=true").Options);
        var context = new DefaultHttpContext();
        context.User = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "6")], "Offline"));
        context.Request.Headers["Idempotency-Key"] = Guid.NewGuid().ToString("D");
        var input = new CreateCustomerOrderRequest("DINE_IN", "TABLE-FAKE", null, null,
            null, null, null, Valid().Items);
        var result = await CustomerOrdersEndpoints.CreateAsync(input, context, db, default, Config(all: true));
        Assert.Equal(503, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(ConnectionState.Closed, db.Database.GetDbConnection().State);
    }

    [Fact]
    public async Task Dine_in_writer_has_its_own_customer_authorized_post_route()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "OfflineMockTests" });
        builder.Services.AddScoped<CafeDbContext>(_ => throw new InvalidOperationException("No SQL permitted"));
        builder.Services.AddScoped<ICustomerOrderReader, CustomerOrderReader>();
        await using var app = builder.Build();
        app.MapCustomerOrders();
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints).OfType<RouteEndpoint>();
        var route = Assert.Single(endpoints, x => x.RoutePattern.RawText == "/api/customer/orders/dine-in");
        Assert.Equal("POST", Assert.Single(route.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods));
        Assert.Equal("Customer", Assert.Single(route.Metadata.GetOrderedMetadata<IAuthorizeData>()).Policy);
    }
}
