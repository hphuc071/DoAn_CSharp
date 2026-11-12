using System.Text.Json;
using Microsoft.Data.SqlClient;
static partial class ConsentRun
{
 // Read-only diagnostic entry, no provisioning/seed/HTTP/SaveChanges or result overwrite.
 public static async Task InspectFailureAsync() {
  FixtureSqlGuard.Load();
  await using var sql=new SqlConnection(FixtureSqlGuard.Connection(admin:true));await sql.OpenAsync();await FixtureSqlGuard.VerifyAsync(sql);
  var events=await FixtureProvisioner.ReadRows(sql,"""
SELECT CONVERT(nvarchar(max),event_data) eventXml
FROM sys.fn_xe_file_target_read_file(N'/var/opt/mssql/log/system_health*.xel',NULL,NULL,NULL)
WHERE object_name=N'xml_deadlock_report'
""");
  var snapshot=await SnapshotAsync();
  await File.WriteAllTextAsync(Dir+"failure-diagnostics.json",JsonSerializer.Serialize(new{events,snapshot,sessions=FixtureSqlGuard.Sessions.Values},new JsonSerializerOptions{WriteIndented=true}));
  Console.WriteLine(JsonSerializer.Serialize(new{readOnly=true,deadlockEvents=events.Count,snapshot.Orders,snapshot.Payments,snapshot.Audits,snapshot.Held,snapshot.Physical}));
 }
}
