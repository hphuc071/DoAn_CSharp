using FourChang.Domain.Entities.Accounts;
using FourChang.Infrastructure.Database.Context;
using FourChang.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FourChang.Tests;

[Trait("Safety", "NoDatabase")]
public sealed class SessionFixtureClockTests
{
    private sealed class StopBeforeSql : Exception;
    private sealed class Capture : SaveChangesInterceptor
    {
        public bool Valid;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken ct = default)
        {
            var session = data.Context!.ChangeTracker.Entries<PhienDangNhap>().Single().Entity;
            Valid = session.HetHanLuc > session.ThoiDiemTao;
            throw new StopBeforeSql();
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Checks_timestamp_after_real_context_stamp_not_the_assigned_creation_time(bool correctedFixture)
    {
        var capture = new Capture();
        var interceptors = correctedFixture ? new IInterceptor[] { new FixtureSessionClock(), capture } : [capture];
        await using var db = new CafeDbContext(new DbContextOptionsBuilder<CafeDbContext>()
            .UseSqlServer("Server=forbidden.invalid;Database=NoDatabase;Integrated Security=true")
            .AddInterceptors(interceptors).Options);
        var now = DateTime.UtcNow;
        db.PhienDangNhap.Add(new() { MaKhachHang = 99, MaLamMoiDaBam = new byte[32],
            LoaiUngDung = "WEB", PhamViPhien = "FULL", ThoiDiemTao = now.AddHours(-2), HetHanLuc = now.AddHours(-1) });
        await Assert.ThrowsAsync<StopBeforeSql>(() => db.SaveChangesAsync());
        Assert.Equal(correctedFixture, capture.Valid);
    }
}
