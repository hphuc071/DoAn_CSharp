using System.Text.Json;
using FourChang.Api.Features.Customer.Orders;
using FourChang.Domain.Entities.Audit;
using Microsoft.Extensions.Configuration;

namespace FourChang.Tests;

[Trait("Safety", "NoDatabase")]
public sealed class OrderOutboxSafetyTests
{
    private static NhatKyHeThong Audit(string action = "CANCEL_ORDER", string? order = "7") => new() {
        MaThaoTac = Guid.NewGuid(), HanhDong = action, MaDoiTuong = order, KetQua = "SUCCESS",
        PhatSinhLuc = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc),
        KetQuaDaChotJson = "{\"status\":\"original\"}", DuLieuSauJson = "{\"refundRequested\":10000}" };
    private static IConfiguration Enabled() => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["Orders:SqlIntegrationValidated"] = "true",
            ["Orders:OutboxDispatchEnabled"] = "true", ["Orders:OutboxSchemaValidated"] = "true" }).Build();

    [Theory]
    [InlineData("CREATE_CUSTOMER_ORDER", "order.created")]
    [InlineData("RECORD_BANK_TRANSFER", "bank.transfer.recorded")]
    [InlineData("CANCEL_ORDER", "order.cancelled")]
    [InlineData("EXPIRE_ORDER", "order.expired")]
    public void Journal_preserves_receipt_and_snapshot_and_has_stable_non_pii_event(string action, string kind)
    {
        var audit = Audit(action); var receipt = audit.KetQuaDaChotJson;
        OrderAuditOutbox.Stamp(audit);
        var message = OrderAuditOutbox.Read(audit.DuLieuSauJson!);
        Assert.Equal(audit.MaThaoTac, message.EventId); Assert.Equal(kind, message.Kind);
        Assert.Equal("order:7", message.Aggregate); Assert.Equal(receipt, audit.KetQuaDaChotJson);
        using var snapshot = JsonDocument.Parse(audit.DuLieuSauJson!);
        Assert.Equal(10000, snapshot.RootElement.GetProperty("refundRequested").GetInt32());
        Assert.DoesNotContain("refundRequested", JsonSerializer.Serialize(message));
        Assert.Throws<InvalidOperationException>(() => OrderAuditOutbox.Stamp(audit));
    }

    [Fact]
    public void Unmatched_bank_money_has_own_stream_without_fabricating_an_order_or_owner()
    {
        var audit = OrderAuditOutbox.Stamp(Audit("RECORD_BANK_TRANSFER", null));
        var item = OrderAuditOutbox.Read(audit.DuLieuSauJson!);
        Assert.Equal("bank:" + audit.MaThaoTac!.Value.ToString("N"), item.Aggregate);
        Assert.Throws<InvalidOperationException>(() => OrderAuditOutbox.Stamp(Audit("CANCEL_ORDER", null)));
    }

    [Theory]
    [InlineData("REFUND_SUCCEEDED")] [InlineData("UNKNOWN")]
    public void Does_not_invent_refund_success_or_events_for_unimplemented_writers(string action) =>
        Assert.Throws<InvalidOperationException>(() => OrderAuditOutbox.Stamp(Audit(action)));

    private sealed class DurableStoreFake : IOrderOutboxDeliveryStore
    {
        public OrderOutboxLease? Pending;
        public int Claims, Completions, Failures;
        public bool LoseAck;
        public Task<OrderOutboxLease?> ClaimAsync(CancellationToken ct) { Claims++; return Task.FromResult(Pending); }
        public Task CompleteAsync(OrderOutboxLease lease, CancellationToken ct)
        {
            if (LoseAck) { LoseAck = false; throw new IOException("lost ack"); }
            Completions++; Pending = null; return Task.CompletedTask;
        }
        public Task FailAsync(OrderOutboxLease lease, string code, CancellationToken ct)
        { Assert.Equal("DELIVERY_OR_ENVELOPE_FAILED", code); Failures++; return Task.CompletedTask; }
    }
    private sealed class Sink : IOrderOutboxSink
    {
        public HashSet<Guid> Applied = []; public int Calls; public bool Fail; public bool Cancel;
        public Task DeliverAsync(OrderOutboxEvent message, CancellationToken ct)
        {
            Calls++;
            if (Cancel) throw new OperationCanceledException(ct);
            if (Fail) throw new IOException("provider secret must not be persisted");
            Applied.Add(message.EventId); return Task.CompletedTask;
        }
    }
    private static DurableStoreFake Store() => new() { Pending = new(5, Guid.NewGuid(),
        OrderAuditOutbox.Stamp(Audit()).DuLieuSauJson!) };

    [Theory]
    [InlineData(null)] [InlineData("false")] [InlineData("true")]
    public async Task Dispatcher_default_or_unvalidated_never_claims_or_sends(string? flag)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Orders:OutboxDispatchEnabled"] = flag, ["ASPNETCORE_ENVIRONMENT"] = "Development" }).Build();
        var store = Store(); var sink = new Sink();
        Assert.Equal("OUTBOX_DISABLED", await new OrderOutboxDispatcher(config, store, sink).RunOnceAsync(default));
        Assert.Equal(0, store.Claims); Assert.Equal(0, sink.Calls);
    }

    [Fact]
    public async Task Delivery_failure_preserves_event_for_restart_and_retry()
    {
        var store = Store(); var original = store.Pending; var sink = new Sink { Fail = true };
        Assert.Equal("OUTBOX_RETRY_OR_DEAD", await new OrderOutboxDispatcher(Enabled(), store, sink).RunOnceAsync(default));
        Assert.Equal(original, store.Pending); Assert.Equal(0, store.Completions);
        sink.Fail = false;
        Assert.Equal("OUTBOX_DELIVERED", await new OrderOutboxDispatcher(Enabled(), store, sink).RunOnceAsync(default));
        Assert.Equal(1, store.Failures); Assert.Single(sink.Applied);
    }

    [Fact]
    public async Task Lost_ack_replays_same_event_id_and_idempotent_consumer_applies_once()
    {
        var store = Store(); store.LoseAck = true; var sink = new Sink();
        await new OrderOutboxDispatcher(Enabled(), store, sink).RunOnceAsync(default);
        Assert.NotNull(store.Pending); Assert.Equal(0, store.Completions);
        await new OrderOutboxDispatcher(Enabled(), store, sink).RunOnceAsync(default);
        Assert.Equal(2, sink.Calls); Assert.Single(sink.Applied); Assert.Equal(1, store.Completions);
    }

    [Fact]
    public async Task Unsupported_envelope_is_not_sent_or_acknowledged()
    {
        var store = Store(); store.Pending = store.Pending! with { AuditJson = "{}" }; var sink = new Sink();
        Assert.Equal("OUTBOX_RETRY_OR_DEAD", await new OrderOutboxDispatcher(Enabled(), store, sink).RunOnceAsync(default));
        Assert.Equal(0, sink.Calls); Assert.Equal(0, store.Completions); Assert.Equal(1, store.Failures);
    }

    [Fact]
    public async Task Cancellation_leaves_lease_for_expiry_without_false_ack()
    {
        var store = Store(); var sink = new Sink { Cancel = true };
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new OrderOutboxDispatcher(Enabled(), store, sink).RunOnceAsync(cancellation.Token));
        Assert.Equal(0, store.Completions); Assert.Equal(0, store.Failures); Assert.NotNull(store.Pending);
    }
}
