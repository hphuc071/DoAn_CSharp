using System.Security.Cryptography;
using System.Text.Json;
using FourChang.Domain.Entities.Inventory;
using FourChang.Presentation.Shared.Contracts.Customer;

namespace FourChang.Api.Features.Customer.Orders;

// Internal service contracts, not public HTTP DTOs. No SQL adapter or runtime registration exists.
public sealed record OrderLifecycleSnapshot(long CustomerId, string Channel, CustomerOrderDetail Detail,
    IReadOnlyList<GiuChoNguyenLieu> Holds, IReadOnlyList<TonKho> Stocks,
    bool BankReconciledThroughDeadline);
public sealed record CancelOrderIntent(long CustomerId, long OrderId, Guid Operation,
    string ExpectedVersion, string Reason);
public sealed record ExpireOrderIntent(long OrderId, Guid Operation, string ExpectedVersion);
public sealed record PlannedHoldRelease(long LineId, long IngredientId, decimal Quantity);
public sealed record PlannedRefund(long PaymentId, decimal Amount, string Status = "REQUESTED");
public sealed record OrderLifecyclePlan(string Code, long OrderId, string ExpectedVersion,
    string? TargetOrderStatus, IReadOnlyList<PlannedHoldRelease> Releases,
    IReadOnlyList<PlannedRefund> Refunds, bool ClosePaymentRequests)
{
    public bool Applicable => Code == "READY";
    // BR-17 explicitly leaves the SQL order status for expiry undecided. Never substitute CANCELLED.
    public bool RequiresSqlStatusMapping => Code == "EXPIRY_MAPPING_REQUIRED";
    public string? AuditAction { get; init; }
    public string? Reason { get; init; }
    public decimal? TargetShippingFee { get; init; }
    public bool RequiresTransactionalAuditAndOutbox => Applicable;
}

// Pure planner. Never mutates snapshots, releases live stock, changes payment status or creates rows.
public sealed class OrderLifecyclePlanner(TimeProvider clock)
{
    public OrderLifecyclePlan Cancel(OrderLifecycleSnapshot snapshot, CancelOrderIntent command)
    {
        var detail = snapshot.Detail;
        if (snapshot.Channel != "WEB" || command.CustomerId <= 0 || command.CustomerId != snapshot.CustomerId || command.OrderId != detail.Order.Id)
            return Reject(detail, "NOT_FOUND");
        var reason = command.Reason?.Trim() ?? "";
        if (command.OrderId <= 0 || command.Operation == Guid.Empty || reason.Length is < 3 or > 400 || reason.Any(char.IsControl))
            return Reject(detail, "INVALID_COMMAND");
        if (!VersionMatches(detail.Version, command.ExpectedVersion)) return Reject(detail, "VERSION_CONFLICT");
        if (detail.Order.Status != "PENDING_CONFIRMATION") return Reject(detail, "STATE_CONFLICT");
        return Prepare(snapshot, "CANCEL_ORDER", reason, "CANCELLED");
    }

    public OrderLifecyclePlan Expire(OrderLifecycleSnapshot snapshot, ExpireOrderIntent command)
    {
        var detail = snapshot.Detail;
        if (snapshot.Channel != "WEB" || command.OrderId <= 0 || command.Operation == Guid.Empty || command.OrderId != detail.Order.Id)
            return Reject(detail, "INVALID_COMMAND");
        if (!VersionMatches(detail.Version, command.ExpectedVersion)) return Reject(detail, "VERSION_CONFLICT");
        if (detail.Order.Status != "PENDING_PAYMENT") return Reject(detail, "STATE_CONFLICT");
        if (detail.ExpiresUtc is not { } deadline) return Reject(detail, "RECONCILIATION_REQUIRED");
        if (clock.GetUtcNow().UtcDateTime < deadline) return Reject(detail, "NOT_DUE");
        // Database silence is not proof that no money arrived. A trusted adapter must reconcile first.
        if (!snapshot.BankReconciledThroughDeadline || detail.Payments.Count != 1 ||
            detail.Payments.Any(x => x.Status is not ("PENDING" or "PARTIAL") ||
                x.ExpiresUtc != deadline || x.Verified >= x.Requested) || detail.Remaining <= 0)
            return Reject(detail, "RECONCILIATION_REQUIRED");
        return Prepare(snapshot, "EXPIRE_ORDER", "Hết hạn chờ thanh toán đã được đối soát.", null);
    }

    private static OrderLifecyclePlan Prepare(OrderLifecycleSnapshot snapshot, string action, string reason, string? targetStatus)
    {
        var detail = snapshot.Detail;
        if (detail.Lines.Count == 0 || detail.ShippingFee < 0 || detail.Refunds.Count != 0 ||
            detail.Lines.Any(x => x.Id <= 0 || x.Quantity <= 0 || x.Cancelled != 0 || x.Started != 0 || x.Prepared != 0 ||
                x.BasePrice < 0 || x.Toppings.Any(t => t.Portions <= 0 || t.Price < 0)) ||
            detail.Lines.Select(x => x.Id).Distinct().Count() != detail.Lines.Count ||
            detail.Payments.Select(x => x.Id).Distinct().Count() != detail.Payments.Count ||
            detail.Payments.Any(x => x.Id <= 0 || x.Method != "BANK_TRANSFER" || x.Verified < 0 || x.Requested < 0 ||
                !CustomerOrderStates.Payments.Contains(x.Status)))
            return Reject(detail, "RECONCILIATION_REQUIRED");
        var lineIds = detail.Lines.Select(x => x.Id).ToHashSet();
        if (snapshot.Holds.Count == 0 || snapshot.Holds.Any(x => !lineIds.Contains(x.MaChiTietDonHang) ||
                x.SoLuongGiuCho < 0 || x.SoLuongDaGiaiPhong < 0 || x.SoLuongDaTieuHao != 0 ||
                x.SoLuongDaGiaiPhong > x.SoLuongGiuCho || x.TrangThai is not ("RESERVED" or "PARTIAL" or "RELEASED" or "EXPIRED") ||
                (x.TrangThai is "RELEASED" or "EXPIRED") && x.SoLuongDaGiaiPhong != x.SoLuongGiuCho) ||
            snapshot.Holds.GroupBy(x => (x.MaChiTietDonHang, x.MaNguyenLieu)).Any(x => x.Count() != 1) ||
            lineIds.Any(id => !snapshot.Holds.Any(x => x.MaChiTietDonHang == id)) ||
            snapshot.Stocks.GroupBy(x => x.MaNguyenLieu).Any(x => x.Count() != 1))
            return Reject(detail, "INVENTORY_CONFLICT");
        var releases = snapshot.Holds.Select(x => new PlannedHoldRelease(x.MaChiTietDonHang,
            x.MaNguyenLieu, x.SoLuongGiuCho - x.SoLuongDaGiaiPhong)).Where(x => x.Quantity > 0).ToArray();
        foreach (var group in releases.GroupBy(x => x.IngredientId))
        {
            var stock = snapshot.Stocks.SingleOrDefault(x => x.MaNguyenLieu == group.Key);
            if (stock is null || stock.TongGiuCho < group.Sum(x => x.Quantity)) return Reject(detail, "INVENTORY_CONFLICT");
        }
        return new(targetStatus is null ? "EXPIRY_MAPPING_REQUIRED" : "READY", detail.Order.Id, detail.Version, targetStatus, releases,
            detail.Payments.Where(x => x.Verified > 0).Select(x => new PlannedRefund(x.Id, x.Verified)).ToArray(), true)
        { AuditAction = action, Reason = reason, TargetShippingFee = 0 };
    }

    private static bool VersionMatches(string actual, string expected) =>
        !string.IsNullOrWhiteSpace(actual) && actual == expected;
    private static OrderLifecyclePlan Reject(CustomerOrderDetail detail, string code) =>
        new(code, detail.Order.Id, detail.Version, null, [], [], false);

    // Stable payload, including expected version. An unresolved retry must reuse this exact command.
    public static string Fingerprint(CancelOrderIntent command) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(command)));
}

// Implementations MUST check owner before replay and atomically: lookup operation/hash, re-read versions,
// validate all rows, apply plan + payment closure + item cancellation + stock + REQUESTED refunds +
// immutable operation receipt/audit/outbox. Expiry with RequiresSqlStatusMapping MUST NOT be persisted.
// No SQL implementation is supplied or activated here.
public interface IOrderLifecycleTransaction
{
    Task<OrderLifecyclePlan> CancelAsync(CancelOrderIntent command, CancellationToken ct);
    Task<OrderLifecyclePlan> ExpireAsync(ExpireOrderIntent command, CancellationToken ct);
}

public interface IOrderExpiryCandidates
{
    Task<IReadOnlyList<ExpireOrderIntent>> ReadReconciledAsync(DateTime nowUtc, CancellationToken ct);
}
public sealed record OrderExpiryWorkerOptions(bool Enabled = false);

// Explicit one-shot orchestration, NOT BackgroundService/IHostedService. Not registered in Program.
// Setting a configuration key cannot start it; production adapters and SQL validation remain absent.
public sealed class OrderExpiryWorker(OrderExpiryWorkerOptions options, TimeProvider clock,
    IOrderExpiryCandidates candidates, IOrderLifecycleTransaction transaction)
{
    public async Task<IReadOnlyList<OrderLifecyclePlan>> RunOnceAsync(CancellationToken ct)
    {
        if (!options.Enabled) return [];
        var commands = await candidates.ReadReconciledAsync(clock.GetUtcNow().UtcDateTime, ct);
        var results = new List<OrderLifecyclePlan>();
        foreach (var command in commands)
        {
            ct.ThrowIfCancellationRequested();
            var result = await transaction.ExpireAsync(command, ct);
            results.Add(result);
            if (!result.Applicable && result.Code != "REPLAY") break; // Fail closed; no automatic retry.
        }
        return results;
    }
}
