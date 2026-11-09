using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using FourChang.Api.Features.Customer.Orders;
using Microsoft.Data.SqlClient;

// Preparation/read-only runner. No provisioning, migration, seed, worker or business DML.
// Running this executable cannot authorize BR-17 or activate any runtime flag.
const string database = "FourChangBr17MigrationFixture";
const string container = "fourchang-br17-migration-fixture";
const string host = "fc-br17-migration-fixture";
var scenarios = new[] {
    "M01 wrong DB/approval, untrusted CHECK, literal drift and nested migration transaction reject",
    "M02 baseline11 -> up12; only CK_DonHang_7 changes, no historical rows/backfill",
    "M03 down with zero EXPIRED succeeds; down with EXPIRED rejects without remapping",
    "E01 zero-paid overdue WEB only: EXPIRED order/payment, exact remaining hold release, one audit/journal",
    "E02 second run/restart: no extra release, payment, audit, refund or outbox",
    "E03 missing/stale/mismatched bank proof, reference, version or unresolved transfer: no writes",
    "E04 POS, future/no deadline, partial/full-paid, accepted/preparing/terminal: no timer mutation",
    "R01 payment wins order lock before expiry; expiry must not release a confirmed order",
    "R02 expiry wins; late verified transfer persists once, does not credit/resurrect EXPIRED",
    "R03 callback timestamp before deadline but decision after deadline: reconciliation per approved D2",
    "R04 cancellation vs expiry/payment; only owner PENDING_CONFIRMATION, exact refund REQUESTED",
    "R05 Create vs expiry/callback/cancel on shared ingredient and separate GUIDs; observe actual SQL waits",
    "R06 opposite audit/order lock order, SQL1205, lock timeout, cancellation: surface errors, no auto-retry",
    "A01 audit/outbox failure rolls back order/payment/release/stock; retained GUID reconciles unknown commit",
    "C01 readers/POS/Admin/report distinguish EXPIRED; no invoice generation or refund SUCCEEDED"
};
try
{
    if (args.SequenceEqual(new[] { "--plan" }))
    {
        var states = FourChang.Presentation.Shared.Contracts.Customer.CustomerOrderStates.Orders;
        var baseline = "TrangThai IN (" + string.Join(",", states.Select(s => "'" + s + "'")) + ")";
        Require(BaselineReady(baseline) &&
            BaselineReady(string.Join(" OR ", states.Select(s => "TrangThai='" + s + "'"))) &&
            !BaselineReady(baseline.Replace("'DRAFT'", "'DRAFT','EXPIRED'")) &&
            !BaselineReady(baseline.Replace("'DRAFT'", "'D R A F T'")), "Offline stage parser regression.");
        Console.WriteLine(JsonSerializer.Serialize(new {
            status = "PREPARED_NOT_EXECUTED_BLOCKED_BR17", target = "127.0.0.1:14340/" + database,
            sqlOpened = false, migrationApplied = false, scenarios,
            note = "Read README. Future write orchestration/provisioning is deliberately not implemented here."
        }, new JsonSerializerOptions { WriteIndented = true }));
        return;
    }
    Require(args.Length == 3 && args[0] == "--preflight" && args[1] == "--approval",
        "Use --plan, or separately approved --preflight --approval <local JSON>. No writes are supported.");
    var approval = JsonSerializer.Deserialize<Approval>(await File.ReadAllTextAsync(args[2]))
        ?? throw new InvalidOperationException("Approval evidence missing.");
    Require(approval.Status == "APPROVED_FIXTURE_READ_PREFLIGHT" &&
        !string.IsNullOrWhiteSpace(approval.Approver) && !string.IsNullOrWhiteSpace(approval.DecisionReference) &&
        approval.ApprovedUtc <= DateTimeOffset.UtcNow && approval.ApprovedUtc > DateTimeOffset.UtcNow.AddDays(-1) &&
        approval.Decisions is not null && Enumerable.Range(1, 6).All(i =>
            approval.Decisions.TryGetValue("D" + i, out var decision) && decision == "APPROVED") &&
        approval.Stage is "Baseline11" or "Migrated12" && approval.ContainerId.Length == 64 &&
        approval.RunId != Guid.Empty, "BR-17 / fixture read approval incomplete; no SQL opened.");
    var up = await File.ReadAllBytesAsync("docs/database/proposals/BR17-EXPIRED.up.sql");
    var down = await File.ReadAllBytesAsync("docs/database/proposals/BR17-EXPIRED.down.sql");
    Require(approval.UpSha256 == Convert.ToHexString(SHA256.HashData(up)) &&
        approval.DownSha256 == Convert.ToHexString(SHA256.HashData(down)), "Reviewed migration hashes changed.");
    // Inspect only. No Docker start/create/stop or SQL credentials in shell arguments/output.
    var info = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true,
        UseShellExecute = false, CreateNoWindow = true };
    info.ArgumentList.Add("inspect"); info.ArgumentList.Add(approval.ContainerId);
    using var process = Process.Start(info) ?? throw new InvalidOperationException("Docker unavailable.");
    var outputTask = process.StandardOutput.ReadToEndAsync();
    var errorTask = process.StandardError.ReadToEndAsync();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    try { await process.WaitForExitAsync(timeout.Token); }
    catch { if (!process.HasExited) process.Kill(); throw; }
    var output = await outputTask; await errorTask;
    Require(process.ExitCode == 0, "Container identity not proven.");
    using var docker = JsonDocument.Parse(output);
    var item = docker.RootElement[0];
    var config = item.GetProperty("Config");
    var ports = item.GetProperty("NetworkSettings").GetProperty("Ports").GetProperty("1433/tcp");
    Require(item.GetProperty("Id").GetString() == approval.ContainerId &&
        item.GetProperty("Name").GetString() == "/" + container && config.GetProperty("Hostname").GetString() == host &&
        config.GetProperty("Labels").GetProperty("com.docker.compose.project").GetString() == container &&
        config.GetProperty("Labels").GetProperty("fourchang.fixture.runid").GetString() == approval.RunId.ToString() &&
        item.GetProperty("State").GetProperty("Running").GetBoolean() && ports.GetArrayLength() == 1 &&
        ports[0].GetProperty("HostIp").GetString() == "127.0.0.1" &&
        ports[0].GetProperty("HostPort").GetString() == "14340", "Wrong fixture isolation.");
    var connectionText = Environment.GetEnvironmentVariable("BR17_FIXTURE_READ_CONNECTION");
    Require(!string.IsNullOrWhiteSpace(connectionText), "Read-only fixture credential missing.");
    var builder = new SqlConnectionStringBuilder(connectionText);
    Require(builder.DataSource == "127.0.0.1,14340" && builder.InitialCatalog == database &&
        builder.AttachDBFilename.Length == 0 && builder.FailoverPartner.Length == 0,
        "Forbidden SQL target; historical ports/databases cannot be used.");
    builder.Pooling = false; builder.ConnectRetryCount = 0; builder.ConnectTimeout = 5;
    builder.ApplicationName = "FourChang.Br17.ReadOnlyPreflight";
    await using var sql = new SqlConnection(builder.ConnectionString);
    await sql.OpenAsync();
    await using var command = sql.CreateCommand(); command.CommandTimeout = 10;
    command.CommandText = """
SELECT DB_NAME(),CONVERT(nvarchar(128),SERVERPROPERTY('MachineName')),
 CONVERT(nvarchar(20),CONNECTIONPROPERTY('net_transport')),CONVERT(int,CONNECTIONPROPERTY('local_tcp_port')),
 (SELECT service_broker_guid FROM sys.databases WHERE name=DB_NAME()),
 (SELECT CONVERT(nvarchar(100),value) FROM sys.extended_properties WHERE class=0 AND name=N'FourChang.Br17.FixtureRunId'),
 HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','INSERT'),HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','UPDATE'),
 HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','DELETE'),HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','ALTER'),
 HAS_PERMS_BY_NAME('dbo','SCHEMA','INSERT'),HAS_PERMS_BY_NAME('dbo','SCHEMA','UPDATE'),
 HAS_PERMS_BY_NAME('dbo','SCHEMA','DELETE'),HAS_PERMS_BY_NAME('dbo','SCHEMA','ALTER')
""";
    await using (var reader = await command.ExecuteReaderAsync())
    {
        Require(await reader.ReadAsync() && reader.GetString(0) == database && reader.GetString(1) == host &&
            reader.GetString(2) == "TCP" && reader.GetInt32(3) == 1433 && reader.GetGuid(4) == approval.DatabaseIdentity &&
            !reader.IsDBNull(5) && Guid.Parse(reader.GetString(5)) == approval.RunId &&
            Enumerable.Range(6, 8).All(i => !reader.IsDBNull(i) && reader.GetInt32(i) == 0),
            "Effective SQL identity/marker/read-only privilege check failed.");
    }
    command.CommandText = """
SELECT (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0),
 (SELECT COUNT(*) FROM sys.foreign_keys),(SELECT COUNT(*) FROM sys.triggers WHERE parent_class=1),
 (SELECT COUNT(*) FROM sys.check_constraints),
 (SELECT COUNT(*) FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1)+
 (SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1),
 (SELECT definition FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.DonHang') AND name=N'CK_DonHang_7')
""";
    await using (var reader = await command.ExecuteReaderAsync())
    {
        Require(await reader.ReadAsync() && reader.GetInt32(0) == 35 && reader.GetInt32(1) == 73 &&
            reader.GetInt32(2) == 11 && reader.GetInt32(3) == 113 && reader.GetInt32(4) == 0 && !reader.IsDBNull(5),
            "Canonical schema metadata missing/drifted.");
        var definition = reader.GetString(5);
        // Reuse the exact runtime parser for both SQL Server OR-normalized and IN forms.
        // Adding a synthetic token in memory must produce exactly the approved twelve;
        // an already migrated baseline, literal drift or extra expression still fails.
        Require(approval.Stage == "Migrated12" ? SqlOrderExpiryWorker.IsApprovedConstraint(definition) : BaselineReady(definition),
            "Wrong status CHECK for approved stage.");
    }
    Console.WriteLine(JsonSerializer.Serialize(new { status = "PASS_READ_PREFLIGHT_ONLY", approval.ContainerId,
        approval.RunId, approval.Stage, migrationsExecuted = 0, businessWrites = 0,
        note = "Full definition fingerprint and scenario execution still required; this is not PASS_SQL_EXPIRY." }));
}
catch (Exception error)
{
    Console.Error.WriteLine("BLOCKED_BR17_PREFLIGHT: " + error.GetType().Name + ". No migration or business write attempted.");
    Environment.ExitCode = 1;
}
static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
static bool BaselineReady(string definition) =>
    SqlOrderExpiryWorker.IsApprovedConstraint("(" + definition + ") OR [TrangThai]='EXPIRED'") ||
    Regex.IsMatch(definition, @"\bIN\s*\(", RegexOptions.IgnoreCase) &&
        SqlOrderExpiryWorker.IsApprovedConstraint(Regex.Replace(definition, @"\bIN\s*\(",
            "IN ('EXPIRED',", RegexOptions.IgnoreCase));
sealed record Approval(string Status, string Approver, string DecisionReference, DateTimeOffset ApprovedUtc,
    Dictionary<string,string> Decisions, string Stage, string ContainerId, Guid RunId, Guid DatabaseIdentity,
    string UpSha256, string DownSha256);
