using System.Data.Common;
using System.Security.Claims;
using FourChang.Api.Features.Customer.Orders;
using FourChang.Domain.Entities.Audit;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace FourChang.Tests;

[Trait("Safety", "NoDatabase")]
public sealed class AcceptedTotalSafetyTests
{
    private static CreateCustomerOrderRequest Order => new("TAKEAWAY", null, "Synthetic customer",
        "0900000000", null, null, null, [new(1, 1, 100, 100, null, [])]);
    private static readonly CustomerOrderReceipt Receipt = new(7, "TEST-7", "PENDING_PAYMENT", 45000, 0, 45000,
        new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    private sealed class NoSql : DbConnectionInterceptor
    {
        public int Attempts;
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData data, InterceptionResult result, CancellationToken ct = default)
        { Attempts++; throw new InvalidOperationException("SQL forbidden"); }
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("-1")] [InlineData("NaN")]
    [InlineData("45000,00")] [InlineData("1e3")] [InlineData("45000,45000")]
    [InlineData("79228162514264337593543950336")]
    public async Task Missing_or_invalid_consent_is_rejected_before_sql_or_writes(string? value)
    {
        var monitor = new NoSql();
        await using var db = new CafeDbContext(new DbContextOptionsBuilder<CafeDbContext>()
            .UseSqlServer("Server=forbidden.invalid;Database=NoDatabase;Integrated Security=true")
            .AddInterceptors(monitor).Options);
        var context = new DefaultHttpContext();
        context.User = new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "6")], "Offline"));
        context.Request.Headers["Idempotency-Key"] = Guid.NewGuid().ToString();
        if (value is not null) context.Request.Headers["X-Checkout-Accepted-Total"] = value;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Orders:CustomerCreationEnabled"] = "true" }).Build();
        var result = await CustomerOrdersEndpoints.CreateAsync(Order, context, db, default, config);
        Assert.Equal(400, ((IStatusCodeHttpResult)result).StatusCode);
        Assert.Equal(0, monitor.Attempts); Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public void Multiple_consent_headers_are_not_merged_or_silently_selected()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Checkout-Accepted-Total"] = new StringValues(["45000", "45000"]);
        Assert.False(OrderIdempotency.TryReadAcceptedTotal(context, out _));
    }

    [Theory]
    [InlineData("0", 0)] [InlineData("45000.00", 45000)]
    public void Consent_uses_exact_decimal_values(string header, int expected)
    {
        var context = new DefaultHttpContext(); context.Request.Headers["X-Checkout-Accepted-Total"] = header;
        Assert.True(OrderIdempotency.TryReadAcceptedTotal(context, out var value)); Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData(45000, true)] [InlineData(44999, false)] [InlineData(45001, false)]
    public void Caller_total_never_overrides_authoritative_total(int accepted, bool matches)
        => Assert.Equal(matches, OrderIdempotency.MatchesAcceptedTotal(accepted, new(45000, 0, 45000)));

    private static NhatKyHeThong Audit() => OrderIdempotency.CreateAudit(6, Guid.NewGuid(),
        OrderIdempotency.Hash(Order), 7, Receipt, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Historical_payload_hash_and_original_receipt_replay_without_deadline_recalculation()
    {
        var audit = OrderAuditOutbox.Stamp(Audit());
        var result = OrderIdempotency.ReplayResult(audit, 6, OrderIdempotency.Hash(Order), 45000);
        Assert.Equal(200, ((IStatusCodeHttpResult)result).StatusCode);
        Assert.Equal(Receipt, ((IValueHttpResult)result).Value);
        // No new hash format/accepted-total column: original stored receipt proves consent amount.
        Assert.Equal(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Order)),
            audit.MaBamYeuCau);
    }

    [Theory]
    [InlineData("accepted-total")] [InlineData("quantity")] [InlineData("topping")] [InlineData("owner")]
    public void Changed_consent_payload_or_owner_conflicts_without_exposing_old_receipt(string change)
    {
        var order = Order;
        if (change == "quantity") order = order with { Items = [order.Items[0] with { SoLuong = 2 }] };
        if (change == "topping") order = order with { Items = [order.Items[0] with { Toppings = [new(2, 1)] }] };
        var result = OrderIdempotency.ReplayResult(Audit(), change == "owner" ? 8 : 6,
            OrderIdempotency.Hash(order), change == "accepted-total" ? 46000 : 45000);
        Assert.Equal(409, ((IStatusCodeHttpResult)result).StatusCode);
        Assert.IsNotType<CustomerOrderReceipt>(((IValueHttpResult)result).Value);
    }

    [Fact]
    public async Task Both_replay_decisions_reject_changed_total_for_the_same_committed_command()
    {
        // Exercises the actual pure decision shared by pre-lock and post-lock replay.
        // It does NOT claim SQL concurrent dispatcher/order execution was run.
        var audit = Audit(); var fingerprint = OrderIdempotency.Hash(Order);
        var results = await Task.WhenAll(new[] { 45000m, 46000m }.Select(total => Task.Run(() =>
            OrderIdempotency.ReplayResult(audit, 6, fingerprint, total))));
        Assert.Equal(200, ((IStatusCodeHttpResult)results[0]).StatusCode);
        Assert.Equal(409, ((IStatusCodeHttpResult)results[1]).StatusCode);
    }
}
