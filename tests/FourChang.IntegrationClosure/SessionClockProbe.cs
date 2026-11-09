using System.Data.Common;
using System.Text.Json;
using FourChang.Domain.Entities.Accounts;
using FourChang.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Diagnostic for the real failed fixture prerequisite, not a simulated SQL PASS.
// Executes the production SaveChanges override, then aborts BEFORE opening any connection.
static class SessionClockProbe
{
    sealed class Observation : Exception;
    sealed class Capture : SaveChangesInterceptor
    {
        public DateTime EffectiveCreated, Expiry;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken ct = default)
        {
            var session = data.Context!.ChangeTracker.Entries<PhienDangNhap>().Single().Entity;
            EffectiveCreated = session.ThoiDiemTao; Expiry = session.HetHanLuc;
            throw new Observation();
        }
    }
    sealed class NoSql : DbConnectionInterceptor
    {
        public int Attempts;
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData data, InterceptionResult result, CancellationToken ct = default)
        { Attempts++; throw new InvalidOperationException("SQL forbidden in clock diagnostic."); }
    }
    public static async Task RunAsync()
    {
        var capture = new Capture(); var monitor = new NoSql(); var now = DateTime.UtcNow;
        await using var db = new CafeDbContext(new DbContextOptionsBuilder<CafeDbContext>()
            .UseSqlServer("Server=forbidden.invalid;Database=NoDatabase;Integrated Security=true")
            .AddInterceptors(capture, monitor).Options);
        db.PhienDangNhap.Add(new() { MaKhachHang = 1, MaLamMoiDaBam = new byte[32],
            PhamViPhien = "FULL", LoaiUngDung = "WEB", ThoiDiemTao = now.AddHours(-2), HetHanLuc = now.AddHours(-1) });
        try { await db.SaveChangesAsync(); throw new InvalidOperationException("Probe did not abort."); }
        catch (Observation) { }
        var evidence = new { status = "PASS_ROOT_CAUSE_CODE_PROBE_NOT_SQL", monitor.Attempts,
            suppliedCreatedUtc = now.AddHours(-2), effectiveCreatedUtc = capture.EffectiveCreated,
            expiryUtc = capture.Expiry, wouldViolateCkPhienDangNhap2 = capture.Expiry <= capture.EffectiveCreated };
        if (monitor.Attempts != 0 || !evidence.wouldViolateCkPhienDangNhap2)
            throw new InvalidOperationException("Clock explanation not proven.");
        await File.WriteAllTextAsync(Closure.Dir + "session-clock-probe.json",
            JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS: production timestamp override explains invalid historical session; zero SQL opens.");
    }
}
