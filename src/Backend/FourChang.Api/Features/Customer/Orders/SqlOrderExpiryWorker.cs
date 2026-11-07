using System.Data;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using FourChang.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Customer.Orders;

public sealed record ExpiryBankQuery(long OrderId, long PaymentId, string? Reference, DateTime DeadlineUtc,
    string PaymentVersion, decimal VerifiedAmount);
public sealed record ExpiryBankProof(ExpiryBankQuery Query, DateTime ReconciledThroughUtc,
    decimal TotalReceived, bool HasUnresolvedTransfers);
public interface IExpiryBankReconciliation
{
    Task<ExpiryBankProof?> ReadAsync(ExpiryBankQuery query, CancellationToken ct);
}
public sealed class UnavailableExpiryBankReconciliation : IExpiryBankReconciliation
{
    public Task<ExpiryBankProof?> ReadAsync(ExpiryBankQuery query, CancellationToken ct) => Task.FromResult<ExpiryBankProof?>(null);
}

// No hosted-service registration and no public route. This is a gated SQL one-shot adapter,
// not the pure/mock worker; it cannot treat an unmigrated DB or absent bank evidence as ready.
public sealed class SqlOrderExpiryWorker(CafeDbContext db, IConfiguration config,
    IExpiryBankReconciliation bank, TimeProvider clock)
{
    public async Task<IReadOnlyList<string>> RunOnceAsync(CancellationToken ct)
    {
        if (!OrderIntegrationSafety.Enabled(config, "ExpiryEnabled")) return ["EXPIRY_DISABLED"];
        if (!bool.TryParse(config["Orders:ExpiryMappingApproved"], out var mapping) || !mapping)
            return ["EXPIRY_MAPPING_REQUIRED"];
        if (!bool.TryParse(config["Orders:ExpiryConsumersValidated"], out var consumers) || !consumers)
            return ["EXPIRY_CONSUMERS_REQUIRED"];
        if (!await SchemaReadyAsync(ct)) return ["EXPIRY_SCHEMA_REQUIRED"];
        var now = clock.GetUtcNow().UtcDateTime;
        var candidates = await db.DonHang.AsNoTracking().Where(x => x.KenhDatHang == "WEB" &&
            x.TrangThai == "PENDING_PAYMENT" && x.HetHanLuc <= now).OrderBy(x => x.HetHanLuc).Take(10).ToListAsync(ct);
        var results = new List<string>();
        foreach (var candidate in candidates)
        {
            var requests = await db.ThanhToan.AsNoTracking().Where(x => x.MaDonHang == candidate.MaDonHang).ToListAsync(ct);
            if (requests.Count != 1 || candidate.HetHanLuc is not { } deadline ||
                requests[0].HetHanLuc != deadline || requests[0].PhuongThuc != "BANK_TRANSFER")
            { results.Add("RECONCILIATION_REQUIRED"); break; }
            var payment = requests[0];
            if (string.IsNullOrWhiteSpace(payment.MaThamChieuChuyenKhoan))
            { results.Add("EXPIRY_BANK_REFERENCE_REQUIRED"); break; }
            var query = new ExpiryBankQuery(candidate.MaDonHang, payment.MaThanhToan, payment.MaThamChieuChuyenKhoan,
                DateTime.SpecifyKind(deadline, DateTimeKind.Utc), Convert.ToBase64String(payment.PhienBanDuLieu), payment.SoTienDaXacMinh);
            // Provider lookup stays outside the transaction. Runtime default source always returns null.
            var proof = await bank.ReadAsync(query, ct);
            if (proof is null || proof.Query != query || proof.HasUnresolvedTransfers ||
                proof.ReconciledThroughUtc.Kind != DateTimeKind.Utc || proof.ReconciledThroughUtc > clock.GetUtcNow().UtcDateTime ||
                proof.ReconciledThroughUtc < query.DeadlineUtc || proof.TotalReceived != payment.SoTienDaXacMinh)
            { results.Add("BANK_RECONCILIATION_REQUIRED"); break; }
            // System refund/cancellation actors are not supported by current CHECK constraints.
            // Never impersonate the Customer/Admin to make a partial-paid expiry pass.
            if (proof.TotalReceived != 0) { results.Add("PARTIAL_EXPIRY_ACTOR_POLICY_REQUIRED"); break; }
            var operation = new Guid(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("4chang.expire." + candidate.MaDonHang)).AsSpan(0, 16));
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (!await OrderIntegrationSafety.LockAsync(db, tx, "4chang.order." + operation.ToString("N"), ct) ||
                !await OrderIntegrationSafety.LockAsync(db, tx, "4chang.orderstate." + candidate.MaDonHang, ct))
            { results.Add("EXPIRY_BUSY"); break; }
            if (await db.NhatKyHeThong.AnyAsync(x => x.MaThaoTac == operation, ct))
            { results.Add("EXPIRY_ALREADY_RECORDED_REVIEW"); break; }
            if (await db.ThanhToan.AnyAsync(x => x.MaThaoTac == operation, ct))
            { results.Add("EXPIRY_OPERATION_CONFLICT"); break; }
            var order = await db.DonHang.SingleAsync(x => x.MaDonHang == candidate.MaDonHang, ct);
            var current = await db.ThanhToan.SingleAsync(x => x.MaThanhToan == payment.MaThanhToan, ct);
            if (order.KenhDatHang != "WEB" || order.TrangThai != "PENDING_PAYMENT" ||
                !order.PhienBanDuLieu.AsSpan().SequenceEqual(candidate.PhienBanDuLieu) || order.HetHanLuc != deadline ||
                !current.PhienBanDuLieu.AsSpan().SequenceEqual(payment.PhienBanDuLieu) || current.SoTienDaXacMinh != 0 ||
                current.TrangThai != "PENDING" || clock.GetUtcNow().UtcDateTime < query.DeadlineUtc)
            { results.Add("EXPIRY_VERSION_OR_STATE_CONFLICT"); break; }
            if (await db.HoSoHoanTien.AnyAsync(x => x.MaDonHang == order.MaDonHang, ct) ||
                await db.GiaoDichNganHang.AnyAsync(x => x.MaThanhToan == current.MaThanhToan, ct))
            { results.Add("EXPIRY_FINANCIAL_REVIEW_REQUIRED"); break; }
            var lines = await db.ChiTietDonHang.Where(x => x.MaDonHang == order.MaDonHang).ToListAsync(ct);
            if (lines.Count == 0 || lines.Any(x => x.SoLuongDat <= 0 || x.SoLuongDaHuy != 0 || x.SoLuongBatDauPha != 0 || x.SoLuongPhaXong != 0))
            { results.Add("EXPIRY_ITEM_REVIEW_REQUIRED"); break; }
            var ids = lines.Select(x => x.MaChiTietDonHang).ToArray();
            var holds = await db.GiuChoNguyenLieu.Where(x => ids.Contains(x.MaChiTietDonHang)).ToListAsync(ct);
            if (holds.Count == 0 || ids.Any(id => !holds.Any(x => x.MaChiTietDonHang == id)))
            { results.Add("EXPIRY_HOLDS_REQUIRED"); break; }
            now = clock.GetUtcNow().UtcDateTime;
            var released = await OrderInventoryRelease.ReleaseAllUnconsumedAsync(db, holds, "EXPIRED", now, ct);
            if (released is not null) { results.Add("EXPIRY_INVENTORY_CONFLICT"); break; }
            // Preserve item/price snapshots; do not fake Customer cancellation rows or completion time.
            order.TrangThai = "EXPIRED";
            current.TrangThai = "EXPIRED";
            var receipt = new { orderId = order.MaDonHang, status = "EXPIRED", expiredAtUtc = now, amountVerified = 0 };
            db.NhatKyHeThong.Add(OrderAuditOutbox.Stamp(OrderIntegrationSafety.SystemAudit(operation, "EXPIRE_ORDER",
                order.MaDonHang.ToString(System.Globalization.CultureInfo.InvariantCulture), OrderIntegrationSafety.Hash(query), receipt, now)));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            results.Add("EXPIRED");
        }
        return results;
    }

    private async Task<bool> SchemaReadyAsync(CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT definition FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.DonHang') AND name=N'CK_DonHang_7' AND is_disabled=0 AND is_not_trusted=0";
            return IsApprovedConstraint(await command.ExecuteScalarAsync(ct) as string);
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
    public static bool IsApprovedConstraint(string? definition)
    {
        if (definition is null) return false;
        var normalized = Regex.Replace(definition, @"[\s\[\]\(\)]", "").ToUpperInvariant();
        var tokens = Regex.Matches(normalized, "'([^']*)'").Select(x => x.Groups[1].Value).ToArray();
        var expected = FourChang.Presentation.Shared.Contracts.Customer.CustomerOrderStates.Orders.Append("EXPIRED").ToHashSet(StringComparer.Ordinal);
        if (tokens.Length != expected.Count || !tokens.ToHashSet(StringComparer.Ordinal).SetEquals(expected)) return false;
        var shape = Regex.Replace(normalized, "'[^']*'", "VALUE");
        return Regex.IsMatch(shape, @"\ATRANGTHAIINVALUE(?:,VALUE){11}\z") ||
            Regex.IsMatch(shape, @"\ATRANGTHAI=VALUE(?:ORTRANGTHAI=VALUE){11}\z");
    }
}
