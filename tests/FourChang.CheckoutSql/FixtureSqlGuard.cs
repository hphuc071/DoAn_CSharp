// Derived from V2 safety guard; independent fixed target, never loads V2 credentials/evidence.
using System.Collections.Concurrent;
using System.Data.Common;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Fixture-only execution guard. Never loads .env or accepts a caller-supplied target.
static class FixtureSqlGuard
{
    public const string Database = "FourChangUc06ConsentFixtureV4";
    public const string Server = "127.0.0.1,14339";
    public const string User = "uc06_consent";
    public static Guid RunId { get; private set; }
    static string hostname = "";
    static string containerId = "";
    static Guid databaseIdentity;
    static readonly object identityLock=new();
    public static ConcurrentDictionary<Guid, object> Sessions { get; } = new();
    public static void Load()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText("artifacts/uc-web-06/sql-consent-v4/container.json"));
        var g=doc.RootElement;
        Check(g.GetProperty("Name").GetString()=="fourchang-uc06-consent-fixture-v4" &&
            g.GetProperty("Project").GetString()=="fourchang-uc06-consent-fixture-v4" &&
            g.GetProperty("Ports").GetArrayLength()==1,"Wrong fixture container");
        var port=g.GetProperty("Ports")[0];
        Check(port.GetProperty("HostIp").GetString()=="127.0.0.1" && port.GetProperty("HostPort").GetString()=="14339","Wrong fixture port");
        var captured=g.GetProperty("CapturedUtc").GetDateTime();
        Check(captured<=DateTime.UtcNow && DateTime.UtcNow-captured<TimeSpan.FromMinutes(45),"Stale fixture Docker evidence");
        hostname=g.GetProperty("Hostname").GetString()??""; containerId=g.GetProperty("Id").GetString()??"";
        RunId=g.GetProperty("RunId").GetGuid();
        Check(hostname=="fc-uc06-consent-fixture-v4" && containerId.Length==64 && RunId!=Guid.Empty,"Fixture identity missing");
    }
    public static string Connection(bool admin=false,bool master=false)
    {
        var secret=Environment.GetEnvironmentVariable(admin?"UC06_FIXTURE_SA_PASSWORD":"UC06_FIXTURE_APP_PASSWORD");
        Check(!string.IsNullOrEmpty(secret),"Fixture credentials missing");
        return new SqlConnectionStringBuilder { DataSource=Server,InitialCatalog=master?"master":Database,
            UserID=admin?"sa":User,Password=secret,Encrypt=true,TrustServerCertificate=true,Pooling=false,
            ConnectTimeout=5,ConnectRetryCount=0,ApplicationName="FourChang.Uc06Consent.IsolatedFixture" }.ConnectionString;
    }
    public static async Task VerifyAsync(DbConnection connection,DbTransaction? tx=null,bool requireMarker=true,bool master=false,CancellationToken ct=default)
    {
        Target(connection,master);
        await using var cmd=connection.CreateCommand(); cmd.Transaction=tx; cmd.CommandTimeout=5;
        cmd.CommandText="SELECT DB_NAME(),CONVERT(nvarchar(128),SERVERPROPERTY('MachineName')),CONVERT(nvarchar(20),CONNECTIONPROPERTY('net_transport')),CONVERT(int,CONNECTIONPROPERTY('local_tcp_port')),@@SPID,(SELECT service_broker_guid FROM sys.databases WHERE name=DB_NAME())";
        Guid dbIdentity; int spid;
        await using(var r=await cmd.ExecuteReaderAsync(ct))
        {
            Check(await r.ReadAsync(ct) && r.GetString(0)==(master?"master":Database) && r.GetString(1)==hostname &&
                r.GetString(2)=="TCP" && r.GetInt32(3)==1433,"SQL effective identity/session mismatch");
            spid=Convert.ToInt32(r.GetValue(4)); dbIdentity=r.GetGuid(5); // @@SPID is SQL smallint.
        }
        if(requireMarker)
        {
            cmd.CommandText="SELECT CONVERT(nvarchar(100),value) FROM sys.extended_properties WHERE class=0 AND name=N'FourChang.Uc06Consent.FixtureRunId'";
            Check(Guid.TryParse((await cmd.ExecuteScalarAsync(ct))?.ToString(),out var marker) && marker==RunId,"Fixture marker mismatch");
            cmd.CommandText="SELECT (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0),(SELECT COUNT(*) FROM sys.foreign_keys),(SELECT COUNT(*) FROM sys.triggers WHERE parent_class=1),(SELECT COUNT(*) FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1)+(SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1),(SELECT definition FROM sys.check_constraints WHERE name='CK_DonHang_7')";
            await using(var schema=await cmd.ExecuteReaderAsync(ct))
            {
                Check(await schema.ReadAsync(ct) && schema.GetInt32(0)==35 && schema.GetInt32(1)==73 &&
                    schema.GetInt32(2)==11 && schema.GetInt32(3)==0 && !schema.IsDBNull(4) &&
                    !schema.GetString(4).Contains("EXPIRED",StringComparison.Ordinal),"Fixture schema guard failed");
            }
            lock(identityLock)
            {
                if(databaseIdentity==Guid.Empty) databaseIdentity=dbIdentity;
                Check(databaseIdentity==dbIdentity,"Fixture database identity changed during execution");
            }
        }
        Sessions[((SqlConnection)connection).ClientConnectionId]=new { clientConnectionId=((SqlConnection)connection).ClientConnectionId,containerId,server=Server,database=connection.Database,dbIdentity,spid,transport="TCP",markerChecked=requireMarker };
    }
    public static void Check(bool valid,string message) { if(!valid) throw new SafetyStop(message); }
    public static void Target(DbConnection connection,bool master=false)=>Check(connection is SqlConnection &&
        connection.DataSource==Server && connection.Database==(master?"master":Database),"Forbidden SQL target");
}

sealed class FixtureConnectionGuard:DbConnectionInterceptor
{
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,ConnectionEventData data,InterceptionResult result,CancellationToken ct=default)
    { FixtureSqlGuard.Target(connection); return ValueTask.FromResult(result); }
    public override InterceptionResult ConnectionOpening(DbConnection connection,ConnectionEventData data,InterceptionResult result)=>throw new SafetyStop("Synchronous fixture SQL opens are forbidden");
    public override Task ConnectionOpenedAsync(DbConnection connection,ConnectionEndEventData data,CancellationToken ct=default)=>FixtureSqlGuard.VerifyAsync(connection,ct:ct);
    public override void ConnectionOpened(DbConnection connection,ConnectionEndEventData data)=>throw new SafetyStop("Synchronous fixture SQL opens are forbidden");
}
sealed class FixtureCommandGuard:DbCommandInterceptor
{
    static Task Verify(DbCommand cmd,CancellationToken ct)=>FixtureSqlGuard.VerifyAsync(cmd.Connection!,cmd.Transaction,ct:ct);
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand cmd,CommandEventData data,InterceptionResult<DbDataReader> result,CancellationToken ct=default)
    { await Verify(cmd,ct); return result; }
    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand cmd,CommandEventData data,InterceptionResult<int> result,CancellationToken ct=default)
    { await Verify(cmd,ct); return result; }
    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand cmd,CommandEventData data,InterceptionResult<object> result,CancellationToken ct=default)
    { await Verify(cmd,ct); return result; }
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand cmd,CommandEventData data,InterceptionResult<DbDataReader> result)=>throw new SafetyStop("Synchronous fixture commands forbidden");
    public override InterceptionResult<int> NonQueryExecuting(DbCommand cmd,CommandEventData data,InterceptionResult<int> result)=>throw new SafetyStop("Synchronous fixture commands forbidden");
    public override InterceptionResult<object> ScalarExecuting(DbCommand cmd,CommandEventData data,InterceptionResult<object> result)=>throw new SafetyStop("Synchronous fixture commands forbidden");
}
