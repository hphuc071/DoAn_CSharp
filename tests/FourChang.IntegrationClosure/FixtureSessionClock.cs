using FourChang.Domain.Entities.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FourChang.Tests.Fixtures;

// Fixture only: historical session creation is applied AFTER CafeDbContext's now stamp.
// Shared source is linked into NoDatabase regression; production context is untouched.
internal sealed class FixtureSessionClock : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
        InterceptionResult<int> result, CancellationToken ct = default)
    {
        foreach (var entry in data.Context!.ChangeTracker.Entries<PhienDangNhap>()
            .Where(x => x.State == EntityState.Added))
        {
            entry.Entity.ThoiDiemTao = DateTime.UtcNow.AddHours(-2);
            if (entry.Entity.HetHanLuc <= entry.Entity.ThoiDiemTao)
                throw new InvalidOperationException("Fixture session expiry violates the real SQL rule.");
        }
        return ValueTask.FromResult(result);
    }
}
