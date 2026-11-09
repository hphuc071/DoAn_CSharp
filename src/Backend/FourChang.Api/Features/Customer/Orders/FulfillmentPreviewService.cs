using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Customer.Orders;

public sealed class FulfillmentUnavailable(string code, int status = 503) : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

// Proposed QR protocol: a canonical base64url 256-bit random secret, SHA256 stored in Ban.
// Existing demo seeds contain random digests, NOT issued secrets. Approval + issuance/revocation
// tooling and SQL integration are required before enabling even the read-only lookup.
public sealed record QrTable(long Id, string Code, int Version, bool Active,
    DateTimeOffset? ExpiresAt, byte[] Digest);
public interface ITableQrLookup
{
    Task<QrTable?> FindAsync(byte[] digest, CancellationToken ct);
}
public sealed class SqlTableQrLookup(CafeDbContext db) : ITableQrLookup
{
    public async Task<QrTable?> FindAsync(byte[] digest, CancellationToken ct)
    {
        var row = await db.Ban.AsNoTracking().SingleOrDefaultAsync(x => x.MaBamQR == digest, ct);
        return row is null ? null : new(row.MaBan, row.MaBanHienThi, row.PhienBanQR,
            row.DangSuDung, row.QRHetHanLuc is { } expiry
                ? new DateTimeOffset(DateTime.SpecifyKind(expiry, DateTimeKind.Utc)) : null, row.MaBamQR);
    }
}

// Implementations must use a configured store origin + road-routing source, not straight-line
// distance or caller-supplied km/fee. No provider has been selected or registered for production.
public sealed record DeliveryRoute(decimal Km, DateTimeOffset ExpiresAt)
{
    // Filled by a trusted server adapter, never copied from a fee/km supplied by the caller.
    // These identify the configured origin/profile and the exact destination resolved.
    public string? Provider { get; init; }
    public string? OriginVersion { get; init; }
    public string? Profile { get; init; }
    public string? Revision { get; init; }
    public string? Address { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
}
public interface IDeliveryRouteSource
{
    Task<DeliveryRoute?> ResolveAsync(string address, decimal latitude, decimal longitude, CancellationToken ct);
}
public sealed class UnavailableDeliveryRouteSource : IDeliveryRouteSource
{
    public Task<DeliveryRoute?> ResolveAsync(string address, decimal latitude, decimal longitude, CancellationToken ct)
        => Task.FromResult<DeliveryRoute?>(null);
}
public interface IFulfillmentCartPricer
{
    Task<OrderPriceSummary> PriceAsync(List<OrderItemInput> items, CancellationToken ct);
}
public sealed class FulfillmentCartPricer(CafeDbContext db) : IFulfillmentCartPricer
{
    // Goods pricing and menu validation stay in the tested calculator; shipping is computed below.
    public Task<OrderPriceSummary> PriceAsync(List<OrderItemInput> items, CancellationToken ct)
        => OrderPricingService.CalculateOrderTotalAsync(db, "TAKEAWAY", null, items, ct);
}

// All policy values are explicit, with no proposed fees silently selected as runtime defaults.
public sealed record DeliveryFeePolicy(string Version, decimal BaseKm, decimal BaseFee,
    decimal PerExtraKm, decimal MaxKm, decimal FreeSubtotal, decimal FreeWithinKm)
{
    internal const decimal MaxMoney = 999999999999999999m; // SQL decimal(18,0), not a fee default.
    internal static bool ValidMoney(decimal value) => value >= 0 && value <= MaxMoney && decimal.Truncate(value) == value;
    public static DeliveryFeePolicy Read(IConfiguration config)
    {
        const string prefix = "Orders:DeliveryPolicy:";
        if (!FulfillmentPreviewService.On(config, prefix + "Approved"))
            throw new FulfillmentUnavailable("DELIVERY_POLICY_APPROVAL_REQUIRED");
        decimal Value(string key) => decimal.TryParse(config[prefix + key],
            System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture,
            out var value) ? value : throw new FulfillmentUnavailable("DELIVERY_POLICY_INVALID");
        var policy = new DeliveryFeePolicy(config[prefix + "Version"] ?? "", Value("BaseKm"),
            Value("BaseFee"), Value("PerExtraKm"), Value("MaxKm"), Value("FreeSubtotal"), Value("FreeWithinKm"));
        if (string.IsNullOrWhiteSpace(policy.Version) || policy.Version.Length > 80 || policy.Version.Any(char.IsControl) ||
            policy.BaseKm <= 0 || policy.MaxKm < policy.BaseKm || policy.BaseFee < 0 || policy.PerExtraKm < 0 ||
            policy.FreeSubtotal <= 0 || policy.FreeWithinKm <= 0 || policy.FreeWithinKm > policy.MaxKm ||
            !ValidMoney(policy.BaseFee) || !ValidMoney(policy.PerExtraKm) || !ValidMoney(policy.FreeSubtotal))
            throw new FulfillmentUnavailable("DELIVERY_POLICY_INVALID");
        return policy;
    }
    public decimal Fee(decimal km, decimal subtotal)
    {
        if (km <= 0 || km > MaxKm) throw new FulfillmentUnavailable("DELIVERY_OUT_OF_RANGE", 400);
        if (!ValidMoney(subtotal)) throw new FulfillmentUnavailable("CART_PRICE_INVALID", 502);
        decimal fee;
        try { fee = subtotal >= FreeSubtotal && km <= FreeWithinKm ? 0m
            : checked(BaseFee + decimal.Ceiling(decimal.Max(0, km - BaseKm)) * PerExtraKm); }
        catch (OverflowException) { throw new FulfillmentUnavailable("DELIVERY_POLICY_INVALID"); }
        if (!ValidMoney(fee)) throw new FulfillmentUnavailable("DELIVERY_POLICY_INVALID");
        return fee;
    }
}

public sealed class FulfillmentPreviewService(IConfiguration config, ITableQrLookup tables,
    IDeliveryRouteSource routes, IFulfillmentCartPricer cart, TimeProvider clock, IDataProtectionProvider protection)
{
    private sealed record BoundPreview(long Owner, string RequestHash, string ContextHash,
        OrderPriceSummary Price, DateTimeOffset ExpiresAt);
    public static bool On(IConfiguration config, string key)
        => bool.TryParse(config[key], out var enabled) && enabled;
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private static bool ValidTable(QrTable? table, byte[] digest, DateTimeOffset now) =>
        table is { Id: > 0, Version: > 0, Active: true, Digest.Length: 32 } &&
        !string.IsNullOrWhiteSpace(table.Code) &&
        CryptographicOperations.FixedTimeEquals(table.Digest, digest) &&
        (table.ExpiresAt is null || table.ExpiresAt > now);
    private static string TableContext(QrTable table) => Hash(new { table.Id, table.Code, table.Version, table.ExpiresAt });
    private (string Provider, string OriginVersion, string Profile) RouteAuthority()
    {
        string Required(string key)
        {
            var value = config["Orders:DeliveryRoute:" + key];
            if (string.IsNullOrWhiteSpace(value) || value.Length > 120 || value.Any(char.IsControl))
                throw new FulfillmentUnavailable("DELIVERY_ROUTE_CONFIGURATION_REQUIRED");
            return value;
        }
        return (Required("Provider"), Required("OriginVersion"), Required("Profile"));
    }

    public async Task<FulfillmentPreview> PreviewAsync(long owner, FulfillmentPreviewRequest input, CancellationToken ct)
    {
        if (owner <= 0) throw new FulfillmentUnavailable("CUSTOMER_REQUIRED", 401);
        // No source, SQL or provider call while preparation is OFF. This gate never enables Create.
        if (!On(config, "Orders:FulfillmentPreviewEnabled")) throw new FulfillmentUnavailable("FULFILLMENT_PREVIEW_DISABLED");
        if (input.Method is not ("DINE_IN" or "DELIVERY") || input.Items is null || input.Items.Count is < 1 or > 50 ||
            input.Items.Any(i => i is null || i.MaCauHinhMon <= 0 || i.SoLuong is < 1 or > 99 ||
                i.PhanTramDuong is < 0 or > 100 || i.PhanTramDa is < 0 or > 100 ||
                (i.GhiChu?.Length ?? 0) > 500 || (i.Toppings?.Count ?? 0) > 10 ||
                (i.Toppings?.Any(t => t is null || t.MaMonThem <= 0 || t.SoPhanMoiLy < 1) ?? false)))
            throw new FulfillmentUnavailable("FULFILLMENT_INPUT_INVALID", 400);
        if (!int.TryParse(config["Orders:FulfillmentPreviewLifetimeSeconds"], out var seconds) || seconds is < 1 or > 3600)
            throw new FulfillmentUnavailable("FULFILLMENT_PREVIEW_LIFETIME_REQUIRED");
        var now = clock.GetUtcNow();
        var expires = now.AddSeconds(seconds);
        var requestHash = Hash(input with { PreviousProof = null });
        var protector = protection.CreateProtector("FourChang.Fulfillment.ReadOnlyPreview.v1");
        BoundPreview? previous = null;
        if (input.PreviousProof is not null)
        {
            if (input.PreviousProof.Length > 8192) throw new FulfillmentUnavailable("FULFILLMENT_PROOF_INVALID", 409);
            try { previous = JsonSerializer.Deserialize<BoundPreview>(protector.Unprotect(input.PreviousProof)); }
            catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
            { throw new FulfillmentUnavailable("FULFILLMENT_PROOF_INVALID", 409); }
            if (previous is null || previous.Owner != owner || previous.RequestHash != requestHash || previous.ExpiresAt <= now)
                throw new FulfillmentUnavailable("FULFILLMENT_RECONFIRM_REQUIRED", 409);
        }
        string? tableCode = null;
        string contextHash;
        decimal fee = 0;
        DeliveryFeePolicy? policy = null;
        DeliveryRoute? route = null;
        byte[]? qrDigest = null;
        (string Provider, string OriginVersion, string Profile)? authority = null;
        if (input.Method == "DINE_IN")
        {
            if (!On(config, "Orders:TableQrProtocolValidated")) throw new FulfillmentUnavailable("TABLE_QR_PROTOCOL_REQUIRED");
            if (input.Address is not null || input.Latitude is not null || input.Longitude is not null ||
                input.Recipient is not null || input.Phone is not null ||
                input.QrToken is null || !Regex.IsMatch(input.QrToken, @"\A[A-Za-z0-9_-]{43}\z"))
                throw new FulfillmentUnavailable("TABLE_QR_INVALID", 400);
            var secret = WebEncoders.Base64UrlDecode(input.QrToken);
            if (secret.Length != 32 || WebEncoders.Base64UrlEncode(secret) != input.QrToken)
                throw new FulfillmentUnavailable("TABLE_QR_INVALID", 400);
            var digest = SHA256.HashData(secret);
            var table = await tables.FindAsync(digest, ct);
            if (!ValidTable(table, digest, now))
                throw new FulfillmentUnavailable("TABLE_QR_INVALID", 400);
            if (table!.ExpiresAt is { } deadline && deadline < expires) expires = deadline;
            tableCode = table.Code; // Only the server lookup determines the table.
            qrDigest = digest;
            contextHash = TableContext(table);
        }
        else
        {
            if (input.QrToken is not null || string.IsNullOrWhiteSpace(input.Recipient) || input.Recipient.Length > 120 ||
                input.Recipient.Any(char.IsControl) || !Regex.IsMatch(input.Phone ?? "", @"^\+?[0-9]{9,15}\z") ||
                string.IsNullOrWhiteSpace(input.Address) || input.Address.Length > 500 || input.Address.Any(char.IsControl) ||
                input.Latitude is null or < -90 or > 90 || input.Longitude is null or < -180 or > 180 ||
                decimal.Round(input.Latitude.Value, 6) != input.Latitude.Value ||
                decimal.Round(input.Longitude.Value, 6) != input.Longitude.Value)
                throw new FulfillmentUnavailable("DELIVERY_CONTACT_OR_LOCATION_INVALID", 400);
            policy = DeliveryFeePolicy.Read(config);
            authority = RouteAuthority();
            try { route = await routes.ResolveAsync(input.Address, input.Latitude.Value, input.Longitude.Value, ct); }
            catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException && !ct.IsCancellationRequested)
            { throw new FulfillmentUnavailable("DELIVERY_ROUTE_UNAVAILABLE"); }
            if (route is null || route.ExpiresAt <= now) throw new FulfillmentUnavailable("DELIVERY_ROUTE_UNAVAILABLE");
            if (route.Provider != authority.Value.Provider || route.OriginVersion != authority.Value.OriginVersion ||
                route.Profile != authority.Value.Profile || string.IsNullOrWhiteSpace(route.Revision) ||
                route.Revision.Length > 120 || route.Revision.Any(char.IsControl) ||
                route.Address != input.Address || route.Latitude != input.Latitude || route.Longitude != input.Longitude)
                throw new FulfillmentUnavailable("DELIVERY_ROUTE_BINDING_INVALID", 502);
            if (route.Km <= 0 || route.Km > policy.MaxKm)
                throw new FulfillmentUnavailable("DELIVERY_OUT_OF_RANGE", 400);
            if (route.ExpiresAt < expires) expires = route.ExpiresAt;
            contextHash = Hash(new { policy, route.Km, route.Provider, route.OriginVersion, route.Profile, route.Revision });
        }
        var goods = await cart.PriceAsync(input.Items, ct);
        if (goods is null || !DeliveryFeePolicy.ValidMoney(goods.TienHang) || goods.PhiGiaoHang != 0 || goods.TongCong != goods.TienHang)
            throw new FulfillmentUnavailable("CART_PRICE_INVALID", 502);
        if (policy is not null) fee = policy.Fee(route!.Km, goods.TienHang);
        var price = new OrderPriceSummary(goods.TienHang, fee, checked(goods.TienHang + fee));
        if (!DeliveryFeePolicy.ValidMoney(price.TongCong)) throw new FulfillmentUnavailable("CART_PRICE_INVALID", 502);
        // Awaited pricing can outlive a rotation/revocation or configuration change. Verify again
        // before issuing even a read-only proof. This is not an atomic SQL Create authorization.
        if (!On(config, "Orders:FulfillmentPreviewEnabled")) throw new FulfillmentUnavailable("FULFILLMENT_PREVIEW_DISABLED");
        if (qrDigest is not null)
        {
            if (!On(config, "Orders:TableQrProtocolValidated")) throw new FulfillmentUnavailable("TABLE_QR_PROTOCOL_REQUIRED");
            var live = await tables.FindAsync(qrDigest, ct);
            if (!ValidTable(live, qrDigest, clock.GetUtcNow()) || TableContext(live!) != contextHash)
                throw new FulfillmentUnavailable("FULFILLMENT_RECONFIRM_REQUIRED", 409);
        }
        else if (policy != DeliveryFeePolicy.Read(config) || authority != RouteAuthority())
            throw new FulfillmentUnavailable("FULFILLMENT_RECONFIRM_REQUIRED", 409);
        if (previous is not null && previous.ExpiresAt < expires) expires = previous.ExpiresAt;
        if (expires <= clock.GetUtcNow()) throw new FulfillmentUnavailable("FULFILLMENT_RECONFIRM_REQUIRED", 409);
        // Re-read the live source on every verification: rotation/revocation or changed route/policy/price
        // requires explicit reconfirmation, never silently accepts a different total.
        if (previous is not null && (previous.ContextHash != contextHash || previous.Price != price))
            throw new FulfillmentUnavailable("FULFILLMENT_RECONFIRM_REQUIRED", 409);
        var proof = protector.Protect(JsonSerializer.Serialize(new BoundPreview(owner, requestHash, contextHash, price, expires)));
        return new(input.Method, tableCode, price, expires, proof); // CreationEnabled always false.
    }
}
