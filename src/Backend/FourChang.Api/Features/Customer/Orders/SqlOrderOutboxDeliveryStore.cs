using System.Data;
using FourChang.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FourChang.Api.Features.Customer.Orders;

// One internal delivery pipeline. Requires the separate REVIEW-ONLY delivery-state migration.
// No cursor over global audit identity: identity allocation is not SQL commit order.
// Prior unacknowledged events block their aggregate, including poison/dead events.
public sealed class SqlOrderOutboxDeliveryStore(CafeDbContext db, IConfiguration config)
    : IOrderOutboxDeliveryStore
{
    private void Guard()
    {
        if (!OrderOutboxDispatcher.Enabled(config))
            throw new InvalidOperationException("OUTBOX_DISABLED");
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Delivery must not run inside a business transaction.");
    }

    public async Task<OrderOutboxLease?> ClaimAsync(CancellationToken ct)
    {
        Guard();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await OrderIntegrationSafety.LockAsync(db, tx, "4chang.outbox.delivery.v1", ct))
            return null;
        // Keep every write on EF's command/interceptor path (also permits fixture write guards).
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE dbo.OrderOutboxDelivery SET Status='DEAD', LeaseToken=NULL, LeaseUntilUtc=NULL,
                LastErrorCode='LEASE_RETRY_LIMIT'
            WHERE Status='LEASED' AND Attempts>=5 AND LeaseUntilUtc<=SYSUTCDATETIME();
            """, ct);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = tx.GetDbTransaction();
        command.CommandText = """
            SELECT TOP(1) a.MaNhatKy, a.DuLieuSauJson
            FROM dbo.NhatKyHeThong a
            LEFT JOIN dbo.OrderOutboxDelivery d ON d.AuditId=a.MaNhatKy
            WHERE a.KetQua='SUCCESS' AND JSON_QUERY(a.DuLieuSauJson,'$.outbox') IS NOT NULL
              AND JSON_VALUE(a.DuLieuSauJson,'$.outbox.Aggregate') IS NOT NULL
              AND (d.AuditId IS NULL OR (d.Attempts<5 AND
                  ((d.Status='PENDING' AND d.NextAttemptUtc<=SYSUTCDATETIME()) OR
                   (d.Status='LEASED' AND d.LeaseUntilUtc<=SYSUTCDATETIME()))))
              AND NOT EXISTS (
                SELECT 1 FROM dbo.NhatKyHeThong p
                LEFT JOIN dbo.OrderOutboxDelivery pd ON pd.AuditId=p.MaNhatKy
                WHERE p.MaNhatKy<a.MaNhatKy AND p.KetQua='SUCCESS'
                  AND JSON_VALUE(p.DuLieuSauJson,'$.outbox.Aggregate')=
                      JSON_VALUE(a.DuLieuSauJson,'$.outbox.Aggregate')
                  AND (pd.AuditId IS NULL OR pd.Status<>'SENT'))
            ORDER BY a.MaNhatKy;
            """;
        long auditId = 0;
        string json = "";
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            { auditId = reader.GetInt64(0); json = reader.GetString(1); }
        }
        if (auditId == 0) { await tx.CommitAsync(ct); return null; }
        var token = Guid.NewGuid();
        // Global claim lock plus SERIALIZABLE protects the insert/lease transition; not a business lock.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            IF NOT EXISTS (SELECT 1 FROM dbo.OrderOutboxDelivery WHERE AuditId={auditId})
                INSERT dbo.OrderOutboxDelivery(AuditId,Status,Attempts,NextAttemptUtc)
                VALUES({auditId},'PENDING',0,SYSUTCDATETIME());
            UPDATE dbo.OrderOutboxDelivery SET Status='LEASED', Attempts=Attempts+1,
                LeaseToken={token}, LeaseUntilUtc=DATEADD(second,120,SYSUTCDATETIME())
            WHERE AuditId={auditId};
            """, ct);
        await tx.CommitAsync(ct);
        return new(auditId, token, json);
    }

    public async Task CompleteAsync(OrderOutboxLease lease, CancellationToken ct)
    {
        Guard();
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.OrderOutboxDelivery SET Status='SENT', DeliveredAtUtc=SYSUTCDATETIME(),
                LeaseToken=NULL,LeaseUntilUtc=NULL,LastErrorCode=NULL
            WHERE AuditId={lease.AuditId} AND Status='LEASED' AND LeaseToken={lease.Token}
                AND LeaseUntilUtc>SYSUTCDATETIME();
            """, ct);
        if (changed != 1) throw new InvalidOperationException("OUTBOX_LEASE_LOST");
    }

    public async Task FailAsync(OrderOutboxLease lease, string errorCode, CancellationToken ct)
    {
        Guard();
        // Fixed codes only, never exception details/raw audit/receiver credentials.
        if (errorCode != "DELIVERY_OR_ENVELOPE_FAILED") throw new ArgumentException("Unknown error code.");
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.OrderOutboxDelivery SET Status=CASE WHEN Attempts>=5 THEN 'DEAD' ELSE 'PENDING' END,
                NextAttemptUtc=DATEADD(second,CASE WHEN Attempts<5 THEN Attempts*Attempts*10 ELSE 600 END,SYSUTCDATETIME()),
                LeaseToken=NULL,LeaseUntilUtc=NULL,LastErrorCode={errorCode}
            WHERE AuditId={lease.AuditId} AND Status='LEASED' AND LeaseToken={lease.Token}
                AND LeaseUntilUtc>SYSUTCDATETIME();
            """, ct);
        if (changed != 1) throw new InvalidOperationException("OUTBOX_LEASE_LOST");
    }
}
