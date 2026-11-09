using System.Text.Json;
using System.Text.Json.Nodes;
using FourChang.Domain.Entities.Audit;

namespace FourChang.Api.Features.Customer.Orders;

// The immutable audit row is the event journal, written by the SAME SaveChanges/transaction.
// No second event table or in-memory publish. Existing receipt/hash fields remain unchanged.
public sealed record OrderOutboxEvent(int Version, Guid EventId, string Aggregate, string Kind,
    DateTime OccurredAtUtc);

public static class OrderAuditOutbox
{
    public static NhatKyHeThong Stamp(NhatKyHeThong audit)
    {
        if (audit.MaThaoTac is not { } id || id == Guid.Empty || audit.KetQua != "SUCCESS")
            throw new InvalidOperationException("A successful durable operation is required.");
        var kind = audit.HanhDong switch
        {
            "CREATE_CUSTOMER_ORDER" => "order.created",
            "RECORD_BANK_TRANSFER" => "bank.transfer.recorded",
            "CANCEL_ORDER" => "order.cancelled",
            "EXPIRE_ORDER" => "order.expired",
            _ => throw new InvalidOperationException("Unsupported order event producer.")
        };
        var aggregate = long.TryParse(audit.MaDoiTuong, out var orderId) && orderId > 0
            ? "order:" + orderId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : audit.HanhDong == "RECORD_BANK_TRANSFER" ? "bank:" + id.ToString("N")
            : throw new InvalidOperationException("An order aggregate is required.");
        var data = audit.DuLieuSauJson is null ? new JsonObject()
            : JsonNode.Parse(audit.DuLieuSauJson) as JsonObject
                ?? throw new InvalidOperationException("Audit snapshot must be an object.");
        if (data.ContainsKey("outbox")) throw new InvalidOperationException("Event already stamped.");
        data["outbox"] = JsonSerializer.SerializeToNode(new OrderOutboxEvent(1, id, aggregate, kind,
            DateTime.SpecifyKind(audit.PhatSinhLuc, DateTimeKind.Utc)));
        audit.DuLieuSauJson = data.ToJsonString();
        return audit;
    }

    public static OrderOutboxEvent Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        var item = document.RootElement.GetProperty("outbox").Deserialize<OrderOutboxEvent>();
        if (item is null || item.Version != 1 || item.EventId == Guid.Empty ||
            item.OccurredAtUtc.Kind != DateTimeKind.Utc ||
            item.Kind is not ("order.created" or "bank.transfer.recorded" or "order.cancelled" or "order.expired") ||
            !(item.Aggregate.StartsWith("order:", StringComparison.Ordinal) &&
                long.TryParse(item.Aggregate[6..], out var id) && id > 0 ||
              item.Kind == "bank.transfer.recorded" && item.Aggregate.StartsWith("bank:", StringComparison.Ordinal) &&
                Guid.TryParseExact(item.Aggregate[5..], "N", out var operation) && operation == item.EventId))
            throw new InvalidOperationException("Unsupported event envelope.");
        return item;
    }
}

public sealed record OrderOutboxLease(long AuditId, Guid Token, string AuditJson);

// Implementations MUST persist lease/retry/ack and fence acknowledgements by token/lease deadline.
public interface IOrderOutboxDeliveryStore
{
    Task<OrderOutboxLease?> ClaimAsync(CancellationToken ct);
    Task CompleteAsync(OrderOutboxLease lease, CancellationToken ct);
    Task FailAsync(OrderOutboxLease lease, string errorCode, CancellationToken ct);
}

// Trusted INTERNAL destination only: consumers dedupe EventId and fetch authorized snapshots.
// Never broadcast an audit/receipt to browsers or use an event as evidence of payment.
public interface IOrderOutboxSink
{
    Task DeliverAsync(OrderOutboxEvent message, CancellationToken ct);
}

// Explicit one-shot; intentionally NOT registered in DI, hosted services, or public routes.
public sealed class OrderOutboxDispatcher(IConfiguration config, IOrderOutboxDeliveryStore store,
    IOrderOutboxSink sink)
{
    public static bool Enabled(IConfiguration config) =>
        OrderIntegrationSafety.Enabled(config, "OutboxDispatchEnabled") &&
        bool.TryParse(config["Orders:OutboxSchemaValidated"], out var approved) && approved;

    public async Task<string> RunOnceAsync(CancellationToken ct)
    {
        if (!Enabled(config)) return "OUTBOX_DISABLED";
        var lease = await store.ClaimAsync(ct);
        if (lease is null) return "OUTBOX_EMPTY";
        try
        {
            var message = OrderAuditOutbox.Read(lease.AuditJson);
            await sink.DeliverAsync(message, ct);
            await store.CompleteAsync(lease, ct);
            return "OUTBOX_DELIVERED";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Lease expiry recovers after process death/cancellation; no false ACK.
            throw;
        }
        catch (Exception)
        {
            // Do not persist exception text: a provider may include tokens/PII in it.
            await store.FailAsync(lease, "DELIVERY_OR_ENVELOPE_FAILED", ct);
            return "OUTBOX_RETRY_OR_DEAD";
        }
    }
}
