using System.Security.Cryptography;
using FourChang.Api.Features.Customer.Orders;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;

namespace FourChang.Tests;

// No DB, routes, bank or stock writes. A preview is not an order/hold integration proof.
[Trait("Safety", "NoDatabase")]
public sealed class FulfillmentPreviewTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.Parse("2026-11-07T00:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Tables : ITableQrLookup
    {
        public QrTable? Row;
        public int Calls;
        public Task<QrTable?> FindAsync(byte[] digest, CancellationToken ct) { Calls++; return Task.FromResult(Row); }
    }
    private sealed class Routes : IDeliveryRouteSource
    {
        public DeliveryRoute? Route;
        public Exception? Error;
        public int Calls;
        public Task<DeliveryRoute?> ResolveAsync(string address, decimal latitude, decimal longitude, CancellationToken ct)
        { Calls++; return Error is null ? Task.FromResult(Route) : Task.FromException<DeliveryRoute?>(Error); }
    }
    private sealed class Cart : IFulfillmentCartPricer
    {
        public decimal Subtotal = 45000;
        public int Calls;
        public Action? OnPrice;
        public Task<OrderPriceSummary> PriceAsync(List<OrderItemInput> items, CancellationToken ct)
        { Interlocked.Increment(ref Calls); OnPrice?.Invoke(); return Task.FromResult(new OrderPriceSummary(Subtotal, 0, Subtotal)); }
    }
    private sealed class Fixture
    {
        public readonly Clock Clock = new();
        public readonly Tables Tables = new();
        public readonly Routes Routes = new();
        public readonly Cart Cart = new();
        public readonly IConfigurationRoot Config;
        public readonly string Token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        public FulfillmentPreviewService Service { get; }
        public Fixture()
        {
            Config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                ["Orders:FulfillmentPreviewEnabled"] = "true", ["Orders:TableQrProtocolValidated"] = "true",
                ["Orders:FulfillmentPreviewLifetimeSeconds"] = "120",
                // Test policy only: the proposal is NOT a production default or approved deployment.
                ["Orders:DeliveryPolicy:Approved"] = "true", ["Orders:DeliveryPolicy:Version"] = "test-proposal-v1",
                ["Orders:DeliveryPolicy:BaseKm"] = "3", ["Orders:DeliveryPolicy:BaseFee"] = "15000",
                ["Orders:DeliveryPolicy:PerExtraKm"] = "5000", ["Orders:DeliveryPolicy:MaxKm"] = "10",
                ["Orders:DeliveryPolicy:FreeSubtotal"] = "300000", ["Orders:DeliveryPolicy:FreeWithinKm"] = "5",
                ["Orders:DeliveryRoute:Provider"] = "TEST-ROAD", ["Orders:DeliveryRoute:OriginVersion"] = "test-origin-v1",
                ["Orders:DeliveryRoute:Profile"] = "test-road-profile"
            }).Build();
            Tables.Row = new(12, "TEST-12", 4, true, Clock.Now.AddMinutes(1), SHA256.HashData(WebEncoders.Base64UrlDecode(Token)));
            Routes.Route = new(3.001m, Clock.Now.AddMinutes(1)) {
                Provider = "TEST-ROAD", OriginVersion = "test-origin-v1", Profile = "test-road-profile",
                Revision = "test-route-v1", Address = Delivery.Address, Latitude = Delivery.Latitude, Longitude = Delivery.Longitude
            };
            Service = new(Config, Tables, Routes, Cart, Clock, new EphemeralDataProtectionProvider());
        }
        public FulfillmentPreviewRequest Qr => new("DINE_IN", Token, null, null, null, null, null, [new(1, 1, 100, 100, null, [])]);
        public FulfillmentPreviewRequest Delivery => new("DELIVERY", null, "Test Recipient", "0900000000",
            "Test address, not a real customer", 10.123456m, 106.123456m, Qr.Items);
        public Task<FulfillmentPreview> Preview(FulfillmentPreviewRequest request, long owner = 6)
            => Service.PreviewAsync(owner, request, default);
    }

    [Theory]
    [InlineData(null)] [InlineData("false")] [InlineData("Development")] [InlineData("1")]
    public async Task Disabled_preview_never_calls_sql_lookup_route_or_cart(string? flag)
    {
        var f = new Fixture(); f.Config["Orders:FulfillmentPreviewEnabled"] = flag;
        var error = await Assert.ThrowsAsync<FulfillmentUnavailable>(() => f.Preview(f.Qr));
        Assert.Equal("FULFILLMENT_PREVIEW_DISABLED", error.Code);
        Assert.Equal(0, f.Tables.Calls + f.Routes.Calls + f.Cart.Calls);
    }
    [Fact]
    public async Task Qr_valid_refresh_preserves_table_owner_and_deadline_without_granting_create()
    {
        var f = new Fixture(); var a = await f.Preview(f.Qr);
        Assert.Equal("TEST-12", a.TableCode); Assert.Equal(0, a.Price.PhiGiaoHang); Assert.False(a.CreationEnabled);
        f.Clock.Now = f.Clock.Now.AddSeconds(10);
        var b = await f.Preview(f.Qr with { PreviousProof = a.Proof });
        Assert.Equal(a.ExpiresAt, b.ExpiresAt); Assert.Equal(a.Price, b.Price); Assert.Equal(4, f.Tables.Calls);
        var ex = await Assert.ThrowsAsync<FulfillmentUnavailable>(() => f.Preview(f.Qr with { PreviousProof = a.Proof }, 7));
        Assert.Equal(409, ex.Status); // A proof cannot be replayed using B, even at the same table.
        Assert.NotNull(await f.Preview(f.Qr, 7)); // B may independently scan; never sees A's order.
    }
    [Theory]
    [InlineData("malformed")] [InlineData("inactive")] [InlineData("expired")]
    [InlineData("missing")] [InlineData("forged")] [InlineData("protocol")] [InlineData("trailing-newline")]
    public async Task Qr_failures_are_closed_before_cart_pricing(string failure)
    {
        var f = new Fixture(); var input = f.Qr;
        switch (failure)
        {
            case "malformed": input = input with { QrToken = "TEST-12" }; break;
            case "trailing-newline": input = input with { QrToken = f.Token + "\n" }; break;
            case "inactive": f.Tables.Row = f.Tables.Row! with { Active = false }; break;
            case "expired": f.Tables.Row = f.Tables.Row! with { ExpiresAt = f.Clock.Now }; break;
            case "missing": f.Tables.Row = null; break;
            case "forged": f.Tables.Row = f.Tables.Row! with { Digest = new byte[32] }; break;
            case "protocol": f.Config["Orders:TableQrProtocolValidated"] = null; break;
        }
        await Assert.ThrowsAsync<FulfillmentUnavailable>(() => f.Preview(input)); Assert.Equal(0, f.Cart.Calls);
    }
    [Theory]
    [InlineData("table-version")] [InlineData("cart")] [InlineData("price")]
    [InlineData("expired-proof")] [InlineData("tampered-proof")] [InlineData("method")]
    public async Task Qr_previous_confirmation_cannot_be_silently_reused(string change)
    {
        var f = new Fixture(); var preview = await f.Preview(f.Qr);
        var input = f.Qr with { PreviousProof = preview.Proof };
        switch (change)
        {
            case "table-version": f.Tables.Row = f.Tables.Row! with { Version = 5 }; break;
            case "cart": input = input with { Items = [new(1, 2, 100, 100, null, [])] }; break;
            case "price": f.Cart.Subtotal++; break;
            case "expired-proof": f.Clock.Now = preview.ExpiresAt; break;
            case "tampered-proof": input = input with { PreviousProof = "tampered" }; break;
            case "method": input = f.Delivery with { PreviousProof = preview.Proof }; break;
        }
        var ex = await Assert.ThrowsAsync<FulfillmentUnavailable>(() => f.Preview(input)); Assert.Equal(409, ex.Status);
    }
    [Theory]
    [InlineData("3", 299999, 15000)] [InlineData("3.001", 299999, 20000)]
    [InlineData("5", 300000, 0)] [InlineData("5.001", 300000, 30000)] [InlineData("10", 45000, 50000)]
    public async Task Delivery_uses_verified_road_distance_and_explicit_test_policy(string km, int subtotal, int fee)
    {
        var f = new Fixture(); f.Cart.Subtotal = subtotal;
        f.Routes.Route = f.Routes.Route! with { Km = decimal.Parse(km, System.Globalization.CultureInfo.InvariantCulture) };
        var p = await f.Preview(f.Delivery);
        Assert.Null(p.TableCode); Assert.Equal(fee, p.Price.PhiGiaoHang);
        Assert.Equal(subtotal + fee, p.Price.TongCong); Assert.False(p.CreationEnabled);
    }
    [Theory]
    [InlineData("recipient")] [InlineData("phone")] [InlineData("address")] [InlineData("latitude")]
    [InlineData("longitude")] [InlineData("precision")] [InlineData("mixed-qr")]
    public async Task Invalid_delivery_contact_or_location_never_calls_external_source(string field)
    {
        var f = new Fixture(); var input = f.Delivery;
        input = field switch {
            "recipient" => input with { Recipient = "\n" }, "phone" => input with { Phone = "123" },
            "address" => input with { Address = "" }, "latitude" => input with { Latitude = null },
            "longitude" => input with { Longitude = 181 }, "precision" => input with { Latitude = 10.0000001m },
            _ => input with { QrToken = f.Token }
        };
        var ex = await Assert.ThrowsAsync<FulfillmentUnavailable>(() => f.Preview(input));
        Assert.Equal(400, ex.Status); Assert.Equal(0, f.Routes.Calls + f.Cart.Calls);
    }
    [Theory]
    [InlineData("unapproved")] [InlineData("missing-policy")] [InlineData("missing-source")]
    [InlineData("source-expired")] [InlineData("network")] [InlineData("timeout")]
    public async Task Delivery_never_falls_back_to_zero_shipping_or_sample_distance(string failure)
    {
        var f = new Fixture();
        switch (failure)
        {
            case "unapproved": f.Config["Orders:DeliveryPolicy:Approved"] = "false"; break;
            case "missing-policy": f.Config["Orders:DeliveryPolicy:BaseFee"] = null; break;
            case "missing-source": f.Routes.Route = null; break;
            case "source-expired": f.Routes.Route = f.Routes.Route! with { ExpiresAt = f.Clock.Now }; break;
            case "network": f.Routes.Error = new HttpRequestException(); break;
            case "timeout": f.Routes.Error = new TaskCanceledException(); break;
        }
        var ex = await Assert.ThrowsAsync<FulfillmentUnavailable>(() => f.Preview(f.Delivery));
        Assert.Equal(503, ex.Status); Assert.Equal(0, f.Cart.Calls);
    }
    [Theory]
    [InlineData("address")] [InlineData("coordinate")] [InlineData("policy")]
    [InlineData("distance")] [InlineData("same-total-policy-change")]
    public async Task Delivery_preview_binds_cart_location_policy_and_total(string change)
    {
        var f = new Fixture(); var p = await f.Preview(f.Delivery);
        var input = f.Delivery with { PreviousProof = p.Proof };
        switch (change)
        {
            case "address": input = input with { Address = "Different test address" }; break;
            case "coordinate": input = input with { Latitude = 11 }; break;
            case "policy": f.Config["Orders:DeliveryPolicy:Version"] = "test-v2"; break;
            case "distance": f.Routes.Route = f.Routes.Route! with { Km = 4.1m }; break;
            case "same-total-policy-change": f.Config["Orders:DeliveryPolicy:FreeSubtotal"] = "400000"; break;
        }
        var ex = await Assert.ThrowsAsync<FulfillmentUnavailable>(() => f.Preview(input)); Assert.Equal(409, ex.Status);
    }
    [Theory]
    [InlineData("0")] [InlineData("-1")] [InlineData("10.001")]
    public async Task Out_of_range_delivery_is_rejected(string km)
    {
        var f = new Fixture(); f.Routes.Route = f.Routes.Route! with { Km = decimal.Parse(km, System.Globalization.CultureInfo.InvariantCulture) };
        var ex = await Assert.ThrowsAsync<FulfillmentUnavailable>(() => f.Preview(f.Delivery));
        Assert.Equal("DELIVERY_OUT_OF_RANGE", ex.Code);
        Assert.Equal(0, f.Cart.Calls);
    }
    [Fact]
    public async Task Deadline_cannot_elapse_during_cart_pricing_and_still_yield_a_valid_preview()
    {
        var f = new Fixture(); f.Cart.OnPrice = () => f.Clock.Now = f.Clock.Now.AddMinutes(2);
        var ex = await Assert.ThrowsAsync<FulfillmentUnavailable>(() => f.Preview(f.Delivery));
        Assert.Equal("FULFILLMENT_RECONFIRM_REQUIRED", ex.Code);
    }
    [Fact]
    public async Task Rechecking_a_refreshed_route_still_enforces_the_original_deadline_after_pricing()
    {
        var f = new Fixture(); var preview = await f.Preview(f.Delivery);
        f.Routes.Route = f.Routes.Route! with { ExpiresAt = f.Clock.Now.AddMinutes(5) };
        f.Cart.OnPrice = () => f.Clock.Now = preview.ExpiresAt.AddSeconds(1);
        var ex = await Assert.ThrowsAsync<FulfillmentUnavailable>(() => f.Preview(f.Delivery with { PreviousProof = preview.Proof }));
        Assert.Equal("FULFILLMENT_RECONFIRM_REQUIRED", ex.Code);
    }
    [Fact]
    public async Task Two_tabs_can_preview_but_neither_obtains_write_authorization()
    {
        var f = new Fixture(); var results = await Task.WhenAll(f.Preview(f.Qr), f.Preview(f.Qr));
        Assert.All(results, x => Assert.False(x.CreationEnabled)); Assert.Equal(2, f.Cart.Calls);
        // No claim about SQL/Web Lock order creation is made by this read-only concurrency test.
    }

    [Theory]
    [InlineData("rotate")] [InlineData("revoke")] [InlineData("table")] [InlineData("digest")]
    public async Task Qr_changed_while_pricing_cannot_issue_a_stale_confirmation(string change)
    {
        var f = new Fixture();
        f.Cart.OnPrice = () => f.Tables.Row = change switch {
            "rotate" => f.Tables.Row! with { Version = 5 },
            "revoke" => f.Tables.Row! with { Active = false },
            "table" => f.Tables.Row! with { Id = 99, Code = "TEST-OTHER" },
            _ => f.Tables.Row! with { Digest = new byte[32] }
        };
        var ex = await Assert.ThrowsAsync<FulfillmentUnavailable>(() => f.Preview(f.Qr));
        Assert.Equal("FULFILLMENT_RECONFIRM_REQUIRED", ex.Code); Assert.Equal(409, ex.Status);
        Assert.Equal(2, f.Tables.Calls);
    }

    [Theory]
    [InlineData("provider")] [InlineData("origin")] [InlineData("profile")] [InlineData("revision")]
    [InlineData("address")] [InlineData("latitude")] [InlineData("longitude")]
    public async Task Route_must_bind_the_exact_configured_authority_and_requested_destination(string field)
    {
        var f = new Fixture(); var r = f.Routes.Route!;
        f.Routes.Route = field switch {
            "provider" => r with { Provider = "OTHER" }, "origin" => r with { OriginVersion = "OTHER" },
            "profile" => r with { Profile = "OTHER" }, "revision" => r with { Revision = null },
            "address" => r with { Address = "Another address" }, "latitude" => r with { Latitude = 11 },
            _ => r with { Longitude = null }
        };
        var ex = await Assert.ThrowsAsync<FulfillmentUnavailable>(() => f.Preview(f.Delivery));
        Assert.Equal("DELIVERY_ROUTE_BINDING_INVALID", ex.Code); Assert.Equal(502, ex.Status);
        Assert.Equal(0, f.Cart.Calls);
    }

    [Theory]
    [InlineData("Provider")] [InlineData("OriginVersion")] [InlineData("Profile")]
    public async Task Routing_configuration_is_explicit_and_required_before_calling_a_source(string field)
    {
        var f = new Fixture(); f.Config["Orders:DeliveryRoute:" + field] = null;
        var ex = await Assert.ThrowsAsync<FulfillmentUnavailable>(() => f.Preview(f.Delivery));
        Assert.Equal("DELIVERY_ROUTE_CONFIGURATION_REQUIRED", ex.Code);
        Assert.Equal(0, f.Routes.Calls + f.Cart.Calls);
    }

    [Theory]
    [InlineData("provider")] [InlineData("origin")] [InlineData("profile")] [InlineData("revision")]
    public async Task Same_distance_and_fee_do_not_hide_a_route_authority_or_revision_change(string field)
    {
        var f = new Fixture(); var first = await f.Preview(f.Delivery); var r = f.Routes.Route!;
        switch (field) {
            case "provider": f.Config["Orders:DeliveryRoute:Provider"] = "TEST-ROAD-v2"; f.Routes.Route = r with { Provider = "TEST-ROAD-v2" }; break;
            case "origin": f.Config["Orders:DeliveryRoute:OriginVersion"] = "test-origin-v2"; f.Routes.Route = r with { OriginVersion = "test-origin-v2" }; break;
            case "profile": f.Config["Orders:DeliveryRoute:Profile"] = "test-profile-v2"; f.Routes.Route = r with { Profile = "test-profile-v2" }; break;
            default: f.Routes.Route = r with { Revision = "test-route-v2" }; break;
        }
        var ex = await Assert.ThrowsAsync<FulfillmentUnavailable>(() => f.Preview(f.Delivery with { PreviousProof = first.Proof }));
        Assert.Equal("FULFILLMENT_RECONFIRM_REQUIRED", ex.Code); Assert.Equal(409, ex.Status);
    }

    [Theory]
    [InlineData("policy")] [InlineData("origin")] [InlineData("profile")]
    public async Task Configuration_changes_during_pricing_require_a_new_confirmation(string change)
    {
        var f = new Fixture(); f.Cart.OnPrice = () => f.Config[change switch {
            "policy" => "Orders:DeliveryPolicy:Version", "origin" => "Orders:DeliveryRoute:OriginVersion",
            _ => "Orders:DeliveryRoute:Profile"
        }] = "test-changed";
        var ex = await Assert.ThrowsAsync<FulfillmentUnavailable>(() => f.Preview(f.Delivery));
        Assert.Equal("FULFILLMENT_RECONFIRM_REQUIRED", ex.Code); Assert.Equal(409, ex.Status);
    }

    [Theory]
    [InlineData("fraction")] [InlineData("large-goods")] [InlineData("large-fee")]
    public async Task Preview_rejects_amounts_that_cannot_be_persisted_in_the_existing_money_contract(string change)
    {
        var f = new Fixture();
        if (change == "fraction") f.Cart.Subtotal = 0.5m;
        else if (change == "large-goods") f.Cart.Subtotal = 1000000000000000000m;
        else f.Config["Orders:DeliveryPolicy:BaseFee"] = "999999999999999999";
        var ex = await Assert.ThrowsAsync<FulfillmentUnavailable>(() => f.Preview(f.Delivery));
        Assert.Contains(ex.Code, new[] { "CART_PRICE_INVALID", "DELIVERY_POLICY_INVALID" });
    }

    [Fact]
    public async Task Fee_arithmetic_overflow_is_controlled_instead_of_becoming_an_unhandled_error()
    {
        var f = new Fixture(); f.Config["Orders:DeliveryPolicy:MaxKm"] = "79228162514264337593543950335";
        f.Config["Orders:DeliveryPolicy:PerExtraKm"] = "999999999999999999";
        f.Routes.Route = f.Routes.Route! with { Km = decimal.MaxValue };
        var ex = await Assert.ThrowsAsync<FulfillmentUnavailable>(() => f.Preview(f.Delivery));
        Assert.Equal("DELIVERY_POLICY_INVALID", ex.Code); Assert.Equal(503, ex.Status);
    }
}
