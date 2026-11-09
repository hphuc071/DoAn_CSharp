using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FourChang.Api.Features.Customer.Orders;
using FourChang.Api.Features.Customer.Payments;
using FourChang.Domain.Entities.Payments;

using FourChang.Infrastructure.Database.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

// Standalone fixture runner: no API/HTTP or main/historical SQL connections.
// Explicit provisioning is confined to the newly approved container14333; business tests require
// its guarded manifest. No EXPIRED migration, cleanup, reseed or automatic retry exists.
const string SchemaQuery = """
SELECT 'CHECK' AS kind,OBJECT_NAME(parent_object_id) AS tableName,name,definition,
    CONVERT(int,is_disabled) AS disabled,CONVERT(int,is_not_trusted) AS extra FROM sys.check_constraints
UNION ALL
SELECT 'TRIGGER',OBJECT_NAME(parent_id),name,OBJECT_DEFINITION(object_id),CONVERT(int,is_disabled),0
    FROM sys.triggers WHERE parent_class=1
UNION ALL
SELECT 'INDEX',OBJECT_NAME(i.object_id),i.name,
    CONCAT(i.filter_definition,'|',(SELECT STRING_AGG(CONVERT(nvarchar(max),CONCAT(c.name,':',ic.key_ordinal,':',ic.is_descending_key)),',') WITHIN GROUP(ORDER BY ic.key_ordinal)
        FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
        WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0)),
    CONVERT(int,i.is_disabled),CONVERT(int,i.is_unique)
    FROM sys.indexes i WHERE i.is_unique=1 AND OBJECT_SCHEMA_NAME(i.object_id)='dbo'
UNION ALL
SELECT 'FK',OBJECT_NAME(f.parent_object_id),f.name,
    CONCAT(OBJECT_SCHEMA_NAME(f.referenced_object_id),'.',OBJECT_NAME(f.referenced_object_id),'|',f.delete_referential_action,':',f.update_referential_action,'|',
        (SELECT STRING_AGG(CONVERT(nvarchar(max),CONCAT(COL_NAME(k.parent_object_id,k.parent_column_id),'=',COL_NAME(k.referenced_object_id,k.referenced_column_id))),',') WITHIN GROUP(ORDER BY k.constraint_column_id)
         FROM sys.foreign_key_columns k WHERE k.constraint_object_id=f.object_id)),
    CONVERT(int,f.is_disabled),CONVERT(int,f.is_not_trusted) FROM sys.foreign_keys f
UNION ALL
SELECT 'COLUMN',OBJECT_NAME(c.object_id),c.name,
    CONCAT(TYPE_NAME(c.user_type_id),':',c.max_length,':',c.precision,':',c.scale,':',c.is_nullable,':',c.is_identity,':',c.collation_name,':',OBJECT_DEFINITION(c.default_object_id)),0,0
    FROM sys.columns c WHERE OBJECT_SCHEMA_NAME(c.object_id)='dbo'
ORDER BY kind,tableName,name
""";

var evidence=new Dictionary<string,object?> { ["status"]="BLOCKED_FIXTURE",["writesAttempted"]=false };
var provision=args.SequenceEqual(new[]{"--provision"});
var guardOnly=args.SequenceEqual(new[]{"--guard-self-test"});
var identityOnly=args.SequenceEqual(new[]{"--identity-readonly"});
var output="artifacts/uc-web-07-08/"+(identityOnly?"sql-fixture-v2-identity.json":guardOnly?"sql-fixture-v2-guard-check.json":provision?"sql-fixture-v2-provision.json":"sql-integration-v2-run.json");
try
{
    FixtureSqlGuard.Load();
    if(identityOnly)
    {
        await using var sql=new SqlConnection(FixtureSqlGuard.Connection(admin:true,master:true));
        await sql.OpenAsync();await FixtureSqlGuard.VerifyAsync(sql,requireMarker:false,master:true);
        await using var cmd=sql.CreateCommand();cmd.CommandText="SELECT COUNT(*) FROM sys.databases WHERE database_id>4";
        var databases=Convert.ToInt32(await cmd.ExecuteScalarAsync()); evidence["userDatabases"]=databases;
        cmd.CommandText="SELECT @@SPID";
        await using(var reader=await cmd.ExecuteReaderAsync()) {await reader.ReadAsync();evidence["spidSqlType"]=reader.GetDataTypeName(0);evidence["spidClrType"]=reader.GetFieldType(0).Name;}
        if(databases==1)
        {
            await using var fixtureSql=new SqlConnection(FixtureSqlGuard.Connection(admin:true));await fixtureSql.OpenAsync();
            evidence["tableRows"]=await FixtureProvisioner.ReadRows(fixtureSql,"SELECT t.name,SUM(p.rows) AS rows FROM sys.tables t JOIN sys.partitions p ON p.object_id=t.object_id AND p.index_id IN(0,1) WHERE t.is_ms_shipped=0 GROUP BY t.name ORDER BY t.name");
            evidence["adminIdentity"]=await FixtureProvisioner.ReadRows(fixtureSql,"SELECT name,last_value FROM sys.identity_columns WHERE object_id=OBJECT_ID(N'dbo.TaiKhoanQuanLy')");
            evidence["exactCounts"]=await FixtureProvisioner.ReadRows(fixtureSql,"SELECT (SELECT COUNT_BIG(*) FROM dbo.TaiKhoanKhachHang) AS customers,(SELECT COUNT_BIG(*) FROM dbo.TaiKhoanQuanLy) AS admins,(SELECT COUNT_BIG(*) FROM dbo.DonHang) AS orders,(SELECT COUNT_BIG(*) FROM dbo.ThanhToan) AS payments,(SELECT COUNT_BIG(*) FROM dbo.NhatKyHeThong) AS audits,(SELECT COUNT_BIG(*) FROM dbo.GiaoDichNganHang) AS bankRows,(SELECT COUNT_BIG(*) FROM dbo.HoSoHoanTien) AS refunds,(SELECT COUNT_BIG(*) FROM dbo.GiuChoNguyenLieu) AS holds,(SELECT COUNT_BIG(*) FROM dbo.TonKho) AS stock,(SELECT COUNT_BIG(*) FROM dbo.DonViTinh) AS units,(SELECT COUNT_BIG(*) FROM dbo.DanhMucMon) AS categories");
            evidence["seedConstraints"]=await FixtureProvisioner.ReadRows(fixtureSql,"SELECT name,definition,is_disabled,is_not_trusted FROM sys.check_constraints WHERE name IN('CK_TaiKhoanKhachHang_3','CK_TaiKhoanQuanLy_1') ORDER BY name");
        }
        evidence["status"]="PASS_IDENTITY_READONLY";
    }
    else if(guardOnly)
    {
        var rejections=new List<object>();
        foreach(var target in new[]{("127.0.0.1,14330",FixtureSqlGuard.Database),("127.0.0.1,14331",FixtureSqlGuard.Database),
            ("127.0.0.1,14332",FixtureSqlGuard.Database),(FixtureSqlGuard.Server,"FourChangDb"),
            (FixtureSqlGuard.Server,"FourChangPhase3CTest"),(FixtureSqlGuard.Server,"FourChangUc0708Fixture")})
        {
            await using var forbidden=new SqlConnection(new SqlConnectionStringBuilder {DataSource=target.Item1,InitialCatalog=target.Item2}.ConnectionString);
            var rejected=false;
            try {await FixtureSqlGuard.VerifyAsync(forbidden);}
            catch(SafetyStop error) when(error.Message=="Forbidden SQL target") {rejected=true;}
            FixtureSqlGuard.Check(rejected,"Forbidden target did not fail before SQL connection");
            rejections.Add(new{server=target.Item1,database=target.Item2,rejectedBeforeOpen=true});
        }
        evidence["checks"]=rejections; evidence["status"]="PASS_GUARDS";
    }
    else if(provision)
    {
        evidence["writesAttempted"]=true;
        await FixtureProvisioner.CreateAsync(SchemaQuery,evidence);
        evidence["status"]="PASS_PROVISION";
    }
    else
    {
        FixtureSqlGuard.Check(args.Length==2 && args[0]=="--run","Usage: --provision OR --run fixture-manifest.json; never targets 14330/14331");
        var bytes=await File.ReadAllBytesAsync(args[1]);
        var hash=Convert.ToHexString(SHA256.HashData(bytes));
        FixtureSqlGuard.Check(Environment.GetEnvironmentVariable("UC0708_APPROVED_MANIFEST_SHA256")==hash,"Fixture manifest approval missing");
        var fixture=JsonSerializer.Deserialize<Fixture>(bytes)??throw new SafetyStop("Missing manifest");
        FixtureSqlGuard.Check(fixture.Database==FixtureSqlGuard.Database && fixture.RunId==FixtureSqlGuard.RunId,"Manifest scope mismatch");
        var connection=FixtureSqlGuard.Connection();
        await using var sql=new SqlConnection(connection); await sql.OpenAsync(); await FixtureSqlGuard.VerifyAsync(sql);
        var permission=await FixtureProvisioner.ReadRows(sql,"SELECT HAS_PERMS_BY_NAME(NULL,NULL,'CONTROL SERVER') AS serverControl,IS_ROLEMEMBER('db_owner') AS dbOwner");
        FixtureSqlGuard.Check(Convert.ToInt32(permission[0]["serverControl"])==0 && Convert.ToInt32(permission[0]["dbOwner"])==0,"Runner must use restricted fixture principal");
        var shape=await FixtureProvisioner.ReadRows(sql,SchemaQuery);
        FixtureSqlGuard.Check(shape.All(x=>Convert.ToInt32(x["disabled"])==0 && (x["kind"]?.ToString() is not ("CHECK" or "FK") || Convert.ToInt32(x["extra"])==0)),"Disabled/untrusted schema");
        FixtureSqlGuard.Check(Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(shape)))==fixture.SchemaSha256,"Schema fingerprint mismatch");
        await using var exclusive=sql.CreateCommand();
        exclusive.CommandText="DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'FourChang.Uc0708.Fixture',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0; SELECT @r;";
        FixtureSqlGuard.Check(Convert.ToInt32(await exclusive.ExecuteScalarAsync())>=0,"Fixture runner already active");
        var suite=new SqlSuite(connection,fixture,evidence);
        await suite.ValidateAsync();
        evidence["manifestSha256"]=hash; evidence["fixture"]=fixture;
        evidence["writesAttempted"]=true;
        await suite.RunAsync();
        evidence["status"]="PASS_SQL_FIXTURE";
        evidence["limitations"]="Trusted test event boundary only, not actual provider signature verification. EXPIRED DML BLOCKED_BR17; no HTTP/PDF/provider integration.";
    }
}
catch(Exception error)
{
    evidence["status"]=evidence["writesAttempted"] is true?"FAIL_SQL_FIXTURE":"BLOCKED_FIXTURE";
    evidence["errorType"]=error.GetType().Name;
    if(error is SafetyStop) evidence["reason"]=error.Message;
    var sql=error as SqlException ?? error.InnerException as SqlException;
    if(sql is not null) evidence["sqlError"]=new {sql.Number,sql.State,sql.Class,sql.LineNumber,sql.Procedure};
}
finally
{
    evidence["sessions"]=FixtureSqlGuard.Sessions.Values.ToArray();
    evidence["target"]="127.0.0.1:14333/"+FixtureSqlGuard.Database;
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    await File.WriteAllTextAsync(output,JsonSerializer.Serialize(evidence,new JsonSerializerOptions {WriteIndented=true}));
    Console.WriteLine(evidence["status"]+": fixture evidence saved; no automatic retry or cleanup.");
    Environment.ExitCode=evidence["status"]?.ToString()?.StartsWith("PASS",StringComparison.Ordinal)==true?0:2;
}
sealed class SafetyStop(string message) : Exception(message);
sealed record Fixture(string Database, Guid RunId, string SchemaSha256, long CustomerId, long OtherCustomerId, Dictionary<string, long> Orders);

sealed class SqlSuite(string connection, Fixture fixture, Dictionary<string, object?> evidence)
{
    static readonly string[] Slots = FixtureProvisioner.Slots;
    readonly List<object> steps = [];
    int paymentCalls, cancellationCalls;
    readonly DateTime now = DateTime.UtcNow;
    readonly IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
    {
        ["Orders:SqlIntegrationValidated"]="true", ["Orders:PaymentProcessingEnabled"]="true", ["Orders:CancellationEnabled"]="true",
        ["Bank:Provider"]="PHASE5_TEST", ["Bank:ReceiverAccount"]="PHASE5_FIXTURE_ONLY"
    }).Build();
    CafeDbContext Db(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<CafeDbContext>()
        .UseSqlServer(connection, x => x.CommandTimeout(15)).AddInterceptors(new FixtureConnectionGuard(),new FixtureCommandGuard()).AddInterceptors(interceptors).Options);
    public async Task ValidateAsync()
    {
        Check(fixture.CustomerId > 0 && fixture.OtherCustomerId > 0 && fixture.CustomerId != fixture.OtherCustomerId, "Two distinct fixture Customers required");
        Check(fixture.Orders.Keys.Order().SequenceEqual(Slots.Order()) && fixture.Orders.Values.Distinct().Count()==Slots.Length, "Exact isolated scenario order set required");
        await using var db = Db();
        Check(await db.DonHang.CountAsync()==Slots.Length && await db.TaiKhoanKhachHang.CountAsync()==2, "Historical/non-fixture rows present");
        Check(await db.TaiKhoanKhachHang.AllAsync(x=>x.TenDangNhapChuanHoa.StartsWith("PH5_")) &&
            await db.TaiKhoanKhachHang.AnyAsync(x=>x.MaKhachHang==fixture.OtherCustomerId), "Synthetic Customer markers missing");
        Check(!await db.NhatKyHeThong.AnyAsync() && !await db.GiaoDichNganHang.AnyAsync() && !await db.HoSoHoanTien.AnyAsync() &&
            !await db.LanChiHoanTien.AnyAsync() && !await db.HoaDon.AnyAsync() && !await db.HuyMon.AnyAsync(), "Fixture already used or contains financial history");
        foreach (var id in fixture.Orders.Values)
        {
            var order=await db.DonHang.SingleAsync(x=>x.MaDonHang==id);
            var requests=await db.ThanhToan.Where(x=>x.MaDonHang==id).ToListAsync();
            Check(order.MaKhachHang==fixture.CustomerId && order.KenhDatHang=="WEB" && order.HinhThucNhanMon=="TAKEAWAY" &&
                order.TrangThai=="PENDING_PAYMENT" && order.MaDonHienThi.StartsWith("PH5-",StringComparison.Ordinal) &&
                order.HetHanLuc>now.AddMinutes(10) && requests.Count==1 && requests[0].TrangThai=="PENDING" &&
                requests[0].SoTienDaXacMinh==0 && requests[0].SoTienYeuCau>1 && requests[0].HetHanLuc==order.HetHanLuc &&
                requests[0].MaThamChieuChuyenKhoan?.StartsWith("PH5-",StringComparison.Ordinal)==true, "Fixture state/deadline invalid");
            var detail=await new CustomerOrderReader(db).DetailAsync(fixture.CustomerId,id,default);
            Check(detail is not null && detail.CurrentTotal==requests[0].SoTienYeuCau && detail.Lines.Count>0, "Fixture price snapshot mismatch");
        }
        var holds=await db.GiuChoNguyenLieu.AsNoTracking().ToListAsync();
        var lines=await db.ChiTietDonHang.AsNoTracking().ToListAsync();
        var bom=await db.NguyenLieuChiTietDonHang.AsNoTracking().ToListAsync();
        Check(holds.Count>0 && holds.All(x=>x.SoLuongGiuCho>0 && x.SoLuongDaGiaiPhong==0 && x.SoLuongDaTieuHao==0 && x.TrangThai=="RESERVED") &&
            lines.All(l=>holds.Any(h=>h.MaChiTietDonHang==l.MaChiTietDonHang)), "Fresh fixture holds required");
        Check(bom.Count==holds.Count && holds.All(h=>bom.Any(b=>b.MaChiTietDonHang==h.MaChiTietDonHang && b.MaNguyenLieu==h.MaNguyenLieu &&
            b.DinhMucMoiLy>0 && h.SoLuongGiuCho==b.DinhMucMoiLy*lines.Single(l=>l.MaChiTietDonHang==h.MaChiTietDonHang).SoLuongDat)),"Immutable BOM/hold snapshot mismatch");
        var stock=await db.TonKho.AsNoTracking().ToListAsync();
        Check(stock.All(s=>s.TongGiuCho==holds.Where(h=>h.MaNguyenLieu==s.MaNguyenLieu).Sum(h=>h.SoLuongGiuCho)) &&
            holds.All(h=>stock.Any(s=>s.MaNguyenLieu==h.MaNguyenLieu)), "Fixture stock/holds mismatch");
        evidence["B0"]=await Snapshot(); evidence["steps"]=steps;
    }
    public async Task RunAsync()
    {
        await SeedRegressionAsync();
        // Stub authentication failure must cause no SQL mutation, including when flags are enabled.
        var before=await Snapshot();
        await Pay("exact", null, 401); Same(before,await Snapshot(),"Unauthenticated boundary modified SQL");
        var exact=await Transfer("exact",1m);
        var wrong=exact with { Transaction = Copy(exact.Transaction,"WRONG_ACCOUNT") };
        await Pay("exact",wrong,401); Same(before,await Snapshot(),"Wrong receiver modified SQL");
        var originalPaymentReceipt=await Pay("exact",exact,200); await State("exact","PENDING_CONFIRMATION","VERIFIED");
        before=await Snapshot(); var replayPaymentReceipt=await Pay("exact",exact,200); Same(before,await Snapshot(),"Duplicate credited twice");
        Check(originalPaymentReceipt==replayPaymentReceipt,"Payment replay receipt differs");
        var conflicting=exact with { Transaction=Copy(exact.Transaction,amount:exact.Transaction.SoTien+1) };
        await Pay("exact",conflicting,409); Same(before,await Snapshot(),"Changed duplicate modified SQL");
        await Pay("partial",await Transfer("partial",0.5m),200); await State("partial","PENDING_PAYMENT","PARTIAL");
        var excessReceipt=await Pay("excess",await Transfer("excess",1m,1),200); await State("excess","PENDING_CONFIRMATION","VERIFIED");
        using(var receipt=JsonDocument.Parse(excessReceipt)) Check(receipt.RootElement.GetProperty("code").GetString()=="EXCESS_REQUIRES_RECONCILIATION","Excess obligation not surfaced");
        var unmatched=await Transfer("wrong-reference",1m); unmatched.Transaction.NoiDungChuyenKhoan="PH5-NO-MATCH-"+fixture.RunId;
        await Pay("wrong-reference",unmatched,200); await State("wrong-reference","PENDING_PAYMENT","PENDING");
        await Unallocated(GuidFor("wrong-reference"));
        var late=await Transfer("late",1m);
        await using(var db=Db())
        {
            var p=await db.ThanhToan.SingleAsync(x=>x.MaDonHang==fixture.Orders["late"]);
            var afterDeadline=DateTime.SpecifyKind(p.HetHanLuc!.Value,DateTimeKind.Utc).AddSeconds(1);
            await Pay("late",late,200,clock:new FixedClock(afterDeadline)); // On-time money, delayed processing.
            late.Transaction.MaGiaoDichBenNgoai=GuidFor("late-money").ToString("N");
            late.Transaction.ThoiDiemNganHang=afterDeadline;
            await Pay("late",late,200,clock:new FixedClock(afterDeadline.AddSeconds(1))); // Actual late money.
        }
        await State("late","PENDING_PAYMENT","PENDING");
        await Unallocated(GuidFor("late")); await Unallocated(GuidFor("late-money"));
        await ConcurrentDuplicate();
        before=await Snapshot(); await PaymentRollback(await Transfer("payment-rollback",1m)); Same(before,await Snapshot(),"Payment audit failure did not rollback");
        evidence["paymentGroupAfter"]=await Snapshot();
        foreach(var slot in new[]{"cancel","cancel-rollback","cancel-race"}) await Pay(slot,await Transfer(slot,1m),200);
        before=await Snapshot(); await Cancel("cancel",fixture.OtherCustomerId,GuidFor("wrong-owner"),404); Same(before,await Snapshot(),"Wrong owner modified SQL");
        await Cancel("partial",fixture.CustomerId,GuidFor("wrong-state"),409); Same(before,await Snapshot(),"Wrong state modified SQL");
        await Cancel("cancel",fixture.CustomerId,GuidFor("missing-reason"),400,reason:""); Same(before,await Snapshot(),"Missing reason modified SQL");
        await Cancel("cancel",fixture.CustomerId,GuidFor("bad-version"),400,"AQ=="); Same(before,await Snapshot(),"Malformed version modified SQL");
        await Cancel("cancel",fixture.CustomerId,GuidFor("stale-version"),409,Convert.ToBase64String(new byte[8])); Same(before,await Snapshot(),"Stale version modified SQL");
        var version=await Version("cancel"); var key=GuidFor("cancel");
        var inventoryBefore=await Inventory();
        var originalCancellationReceipt=await Cancel("cancel",fixture.CustomerId,key,200,version);
        await CancellationEffects("cancel");
        var inventoryAfter=await Inventory();
        foreach(var stock in inventoryBefore)
        {
            Check(inventoryAfter[stock.Key].Physical==stock.Value.Physical &&
                inventoryAfter[stock.Key].Held==stock.Value.Held-stock.Value.CancelRelease,"Physical stock/release delta mismatch");
        }
        before=await Snapshot(); var replayCancellationReceipt=await Cancel("cancel",fixture.CustomerId,key,200,version); Same(before,await Snapshot(),"Cancel replay released/refunded twice");
        Check(originalCancellationReceipt==replayCancellationReceipt,"Cancellation replay receipt differs");
        await Cancel("cancel",fixture.CustomerId,key,409,version,"Changed fixture reason"); Same(before,await Snapshot(),"Changed cancellation payload modified SQL");
        before=await Snapshot(); await CancellationRollback(); Same(before,await Snapshot(),"Cancellation audit failure did not rollback");
        await AcceptanceRace();
        evidence["cancellationGroupAfter"]=await Snapshot();
        evidence["B1"]=await Snapshot();
        evidence["callBudget"]=new{paymentCalls,cancellationCalls,newOrders=0,staffUpdates=1};
        evidence["expiry"]="NOT_RUN: no EXPIRED migration; schema/planning review only";
    }
    async Task SeedRegressionAsync()
    {
        var before=await Snapshot(); var prefix="PH5_NEG_"+fixture.RunId.ToString("N");long inserted=0;
        try
        {
            await using var db=Db();await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var valid=new FourChang.Domain.Entities.Accounts.TaiKhoanKhachHang {TenDangNhapChuanHoa=prefix+"_OK",HoTen="Synthetic rollback regression",EmailChuanHoa=prefix.ToLowerInvariant()+"@example.invalid",MatKhauBam=Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),TrangThai="LOCKED"};
            db.Add(valid);await db.SaveChangesAsync();inserted=valid.MaKhachHang;
            Check(inserted>0,"Database did not allocate regression Customer ID");
            db.Add(new FourChang.Domain.Entities.Accounts.TaiKhoanKhachHang {TenDangNhapChuanHoa=prefix+"_BAD",HoTen="Synthetic missing contact",MatKhauBam=Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),TrangThai="LOCKED"});
            await db.SaveChangesAsync();throw new SafetyStop("Missing-contact seed unexpectedly passed SQL CHECK");
        }
        catch(DbUpdateException error) when(error.InnerException is SqlException {Number:547} sql && sql.Message.Contains("CK_TaiKhoanKhachHang_3",StringComparison.Ordinal)) {Check(inserted>0,"Expected prior INSERT missing");}
        await using(var db=Db()) {Check(await db.TaiKhoanKhachHang.CountAsync()==2 && !await db.TaiKhoanKhachHang.AnyAsync(x=>x.TenDangNhapChuanHoa.StartsWith(prefix)),"Missing-contact regression did not roll back earlier INSERT");}
        Same(before,await Snapshot(),"Seed regression changed fixture financial/inventory snapshot");
        steps.Add(new{kind="seed-required-contact-rollback",sqlCheck="CK_TaiKhoanKhachHang_3",sqlError=547,priorInsertedCustomerId=inserted,customerRowsAfter=2,priorInsertRolledBack=true});
    }
    async Task<VerifiedBankTransfer> Transfer(string slot,decimal factor,decimal extra=0)
    {
        await using var db=Db(); var p=await db.ThanhToan.SingleAsync(x=>x.MaDonHang==fixture.Orders[slot]);
        return new(new GiaoDichNganHang { TenNhaCungCap="PHASE5_TEST",MaTaiKhoanNganHangQuan="PHASE5_FIXTURE_ONLY",
            MaGiaoDichBenNgoai=GuidFor(slot).ToString("N"),ChieuGiaoDich="IN",SoTien=decimal.Floor(p.SoTienYeuCau*factor)+extra,
            NoiDungChuyenKhoan=p.MaThamChieuChuyenKhoan,ThoiDiemNganHang=now.AddSeconds(-1)},"VND");
    }
    async Task<string> Pay(string slot,VerifiedBankTransfer? transfer,int expected,TimeProvider? clock=null)
    {
        Check(++paymentCalls<=16,"Payment call budget exceeded");
        var b=await Snapshot(); await using var db=Db();
        var result=await new SqlBankPaymentService(db,config,new TestSource(transfer),clock??new FixedClock(now)).ProcessAsync(new byte[]{1},new Dictionary<string,string>(),default);
        Check((result as IStatusCodeHttpResult)?.StatusCode==expected,"Unexpected payment result");
        steps.Add(new{kind="payment",slot,status=expected,before=b,after=await Snapshot()});
        return Payload(result);
    }
    async Task<string> Cancel(string slot,long customer,Guid key,int expected,string? version=null,string reason="Phase5 isolated fixture")
    {
        Check(++cancellationCalls<=10,"Cancellation call budget exceeded");
        var b=await Snapshot(); await using var db=Db();
        var result=await CustomerOrderCancellation.CancelAsync(fixture.Orders[slot],new(reason,version??await Version(slot)),Context(customer,key),db,default,config);
        Check((result as IStatusCodeHttpResult)?.StatusCode==expected,"Unexpected cancellation result");
        steps.Add(new{kind="cancel",slot,status=expected,before=b,after=await Snapshot()});
        return Payload(result);
    }
    async Task ConcurrentDuplicate()
    {
        Check((paymentCalls+=2)<=16,"Payment call budget exceeded");
        var b=await Snapshot(); var transfer=await Transfer("concurrent",1m);
        var barrier=new TrustedEventBarrier(transfer); var start=DateTime.UtcNow;
        async Task<string> Send()
        {
            await using var db=Db(); var result=await new SqlBankPaymentService(db,config,barrier,new FixedClock(now))
                .ProcessAsync(new byte[]{1},new Dictionary<string,string>(),default);
            Check((result as IStatusCodeHttpResult)?.StatusCode==200,"Concurrent duplicate status mismatch"); return Payload(result);
        }
        var replies=await Task.WhenAll(Send(),Send());
        Check(replies[0]==replies[1] && barrier.Arrivals==2,"Concurrent duplicate receipt/barrier mismatch");
        await State("concurrent","PENDING_CONFIRMATION","VERIFIED");
        await using var check=Db(); var external=transfer.Transaction.MaGiaoDichBenNgoai;
        Check(await check.GiaoDichNganHang.CountAsync(x=>x.MaGiaoDichBenNgoai==external)==1,"Concurrent duplicate ledger rows");
        steps.Add(new{kind="concurrent-duplicate",statuses=new[]{200,200},startedUtc=start,completedUtc=DateTime.UtcNow,
            authenticatedEventArrivals=barrier.Arrivals,identicalReceipts=true,before=b,after=await Snapshot()});
    }
    async Task PaymentRollback(VerifiedBankTransfer transfer)
    {
        Check(++paymentCalls<=16,"Payment call budget exceeded");
        var fault=new InvalidAudit(GuidFor("payment-rollback-marker")); await using var db=Db(fault);
        try { await new SqlBankPaymentService(db,config,new TestSource(transfer),new FixedClock(now)).ProcessAsync(new byte[]{1},new Dictionary<string,string>(),default); throw new SafetyStop("Expected SQL audit constraint failure absent"); }
        catch(DbUpdateException ex) when(ex.InnerException is SqlException { Number:547 } sql && sql.Message.Contains("CK_NhatKyHeThong_4",StringComparison.Ordinal)) { Check(fault.Applied && fault.MarkerInserted,"Fault/SQL rollback marker not applied"); }
        steps.Add(new{kind="payment-rollback",result="Audit CHECK547; preceding SQL audit marker INSERT rolled back; fresh snapshot equality required"});
    }
    async Task CancellationRollback()
    {
        Check(++cancellationCalls<=10,"Cancellation call budget exceeded");
        var fault=new InvalidAudit(GuidFor("cancel-rollback-marker")); var version=await Version("cancel-rollback"); await using var db=Db(fault);
        try { await CustomerOrderCancellation.CancelAsync(fixture.Orders["cancel-rollback"],new("Phase5 isolated fixture",version),Context(fixture.CustomerId,GuidFor("cancel-rollback")),db,default,config); throw new SafetyStop("Expected SQL audit constraint failure absent"); }
        catch(DbUpdateException ex) when(ex.InnerException is SqlException { Number:547 } sql && sql.Message.Contains("CK_NhatKyHeThong_4",StringComparison.Ordinal)) { Check(fault.Applied && fault.MarkerInserted,"Fault/SQL rollback marker not applied"); }
        steps.Add(new{kind="cancel-rollback",result="Audit CHECK547; preceding SQL audit marker INSERT rolled back; fresh snapshot equality required"});
    }
    async Task AcceptanceRace()
    {
        Check(++cancellationCalls<=10,"Cancellation call budget exceeded");
        var b=await Snapshot(); var version=await Version("cancel-race"); await using var staff=Db();
        await using var cancelDb=Db(); await cancelDb.Database.OpenConnectionAsync();
        var cancelPid=((SqlConnection)cancelDb.Database.GetDbConnection()).ServerProcessId;
        await using var tx=await staff.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var staffPid=((SqlConnection)staff.Database.GetDbConnection()).ServerProcessId;
        var order=await staff.DonHang.SingleAsync(x=>x.MaDonHang==fixture.Orders["cancel-race"]);
        order.TrangThai="ACCEPTED"; order.TiepNhanLuc=now; await staff.SaveChangesAsync();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var cancelTask=CustomerOrderCancellation.CancelAsync(fixture.Orders["cancel-race"],new("Phase5 isolated fixture",version),
            Context(fixture.CustomerId,GuidFor("cancel-race")),cancelDb,timeout.Token,config);
        var committed=false;
        try
        {
            // Prove SQL actually blocked cancellation on the uncommitted staff row, not merely
            // Task scheduling. Only SELECT observation is polled; command itself is never retried.
            await using var monitor=new SqlConnection(connection); await monitor.OpenAsync(); await FixtureSqlGuard.VerifyAsync(monitor);
            var blocked=false;
            for(var attempt=0;attempt<30 && !blocked;attempt++)
            {
                await using var command=monitor.CreateCommand();
                command.CommandText="SELECT COUNT(*) FROM sys.dm_exec_requests WHERE session_id=@cancel AND blocking_session_id=@staff AND wait_type LIKE 'LCK_M_%'";
                command.Parameters.AddWithValue("@cancel",cancelPid); command.Parameters.AddWithValue("@staff",staffPid);
                blocked=Convert.ToInt32(await command.ExecuteScalarAsync())==1;
                if(!blocked) await Task.Delay(100);
            }
            Check(blocked,"SQL lock contention not observed; do not claim concurrency PASS");
            await tx.CommitAsync(); committed=true;
            var result=await cancelTask;
            Check((result as IStatusCodeHttpResult)?.StatusCode==409,"Concurrent cancellation did not reject stale version");
        }
        finally
        {
            if(!committed) { await tx.RollbackAsync(); timeout.Cancel(); try { await cancelTask; } catch(OperationCanceledException) { } }
        }
        await State("cancel-race","ACCEPTED","VERIFIED");
        steps.Add(new{kind="accept-vs-cancel",result="Observed SQL lock wait; staff wins; cancellation 409",before=b,after=await Snapshot(),limitation="Direct EF staff state update, not staff HTTP endpoint"});
    }
    async Task CancellationEffects(string slot)
    {
        await using var db=Db(); var id=fixture.Orders[slot];
        Check((await db.DonHang.SingleAsync(x=>x.MaDonHang==id)).TrangThai=="CANCELLED","Cancellation state missing");
        var lines=await db.ChiTietDonHang.Where(x=>x.MaDonHang==id).ToListAsync();
        Check(lines.All(x=>x.SoLuongDaHuy==x.SoLuongDat),"Cancelled quantities mismatch");
        var ids=lines.Select(x=>x.MaChiTietDonHang).ToArray(); var holds=await db.GiuChoNguyenLieu.Where(x=>ids.Contains(x.MaChiTietDonHang)).ToListAsync();
        Check(holds.Count>0 && holds.All(x=>x.SoLuongDaGiaiPhong==x.SoLuongGiuCho && x.SoLuongDaTieuHao==0 && x.TrangThai=="RELEASED"),"Release quantities mismatch");
        var paid=await db.ThanhToan.SingleAsync(x=>x.MaDonHang==id); var refunds=await db.HoSoHoanTien.Where(x=>x.MaDonHang==id).ToListAsync();
        Check(refunds.Count==1 && refunds[0].TrangThai=="REQUESTED" && refunds[0].SoTienYeuCau==paid.SoTienDaXacMinh,"Refund obligation mismatch");
        Check(!await db.LanChiHoanTien.AnyAsync(),"Fixture must never pay refunds");
        Check(await db.NhatKyHeThong.CountAsync(x=>x.MaThaoTac==GuidFor("cancel"))==1 &&
            await db.HuyMon.CountAsync(x=>ids.Contains(x.MaChiTietDonHang))==lines.Count,"Cancellation audit/item history mismatch");
    }
    async Task<Dictionary<long,(decimal Physical,decimal Held,decimal CancelRelease)>> Inventory()
    {
        await using var db=Db(); var id=fixture.Orders["cancel"];
        var ids=await db.ChiTietDonHang.Where(x=>x.MaDonHang==id).Select(x=>x.MaChiTietDonHang).ToArrayAsync();
        var holds=await db.GiuChoNguyenLieu.AsNoTracking().Where(x=>ids.Contains(x.MaChiTietDonHang)).ToListAsync();
        return (await db.TonKho.AsNoTracking().ToListAsync()).ToDictionary(x=>x.MaNguyenLieu,
            x=>(x.TonThucTe,x.TongGiuCho,holds.Where(h=>h.MaNguyenLieu==x.MaNguyenLieu).Sum(h=>h.SoLuongGiuCho-h.SoLuongDaGiaiPhong)));
    }
    async Task State(string slot,string orderState,string paymentState)
    {
        await using var db=Db(); var id=fixture.Orders[slot];
        var payment=await db.ThanhToan.SingleAsync(x=>x.MaDonHang==id);
        var ledger=await db.GiaoDichNganHang.Where(x=>x.MaThanhToan==payment.MaThanhToan && x.ChieuGiaoDich=="IN" &&
            x.TrangThaiXacMinh=="VERIFIED").SumAsync(x=>(decimal?)x.SoTien)??0;
        Check((await db.DonHang.SingleAsync(x=>x.MaDonHang==id)).TrangThai==orderState &&
            payment.TrangThai==paymentState && payment.SoTienDaXacMinh==ledger,"Order/payment/ledger state mismatch");
        Check(paymentState switch { "PENDING"=>ledger==0,"PARTIAL"=>ledger>0 && ledger<payment.SoTienYeuCau,
            "VERIFIED"=>ledger>=payment.SoTienYeuCau,_=>false },"Verified amount boundary mismatch");
    }
    async Task Unallocated(Guid transaction)
    {
        await using var db=Db(); var external=transaction.ToString("N");
        var bank=await db.GiaoDichNganHang.SingleAsync(x=>x.MaGiaoDichBenNgoai==external);
        Check(bank.MaThanhToan is null && bank.TrangThaiXacMinh=="VERIFIED","Late/unmatched bank truth must remain unallocated");
    }
    async Task<string> Version(string slot) { await using var db=Db(); return Convert.ToBase64String((await db.DonHang.AsNoTracking().SingleAsync(x=>x.MaDonHang==fixture.Orders[slot])).PhienBanDuLieu); }
    async Task<string> Snapshot()
    {
        await using var db=Db();
        return JsonSerializer.Serialize(new {
            orders=await db.DonHang.AsNoTracking().OrderBy(x=>x.MaDonHang).Select(x=>new{x.MaDonHang,x.TrangThai,x.PhiGiaoHangHienTai,x.HoanTatLuc,x.PhienBanDuLieu}).ToListAsync(),
            payments=await db.ThanhToan.AsNoTracking().OrderBy(x=>x.MaThanhToan).Select(x=>new{x.MaThanhToan,x.TrangThai,x.SoTienDaXacMinh,x.HetHanLuc,x.PhienBanDuLieu}).ToListAsync(),
            lines=await db.ChiTietDonHang.AsNoTracking().OrderBy(x=>x.MaChiTietDonHang).Select(x=>new{x.MaChiTietDonHang,x.SoLuongDaHuy,x.PhienBanDuLieu}).ToListAsync(),
            bom=await db.NguyenLieuChiTietDonHang.AsNoTracking().OrderBy(x=>x.MaChiTietDonHang).ThenBy(x=>x.MaNguyenLieu).Select(x=>new{x.MaChiTietDonHang,x.MaNguyenLieu,x.DinhMucMoiLy,x.DonViCoSoDaChot}).ToListAsync(),
            holds=await db.GiuChoNguyenLieu.AsNoTracking().OrderBy(x=>x.MaChiTietDonHang).ThenBy(x=>x.MaNguyenLieu).Select(x=>new{x.MaChiTietDonHang,x.MaNguyenLieu,x.SoLuongGiuCho,x.SoLuongDaGiaiPhong,x.SoLuongDaTieuHao,x.TrangThai,x.PhienBanDuLieu}).ToListAsync(),
            stock=await db.TonKho.AsNoTracking().OrderBy(x=>x.MaNguyenLieu).Select(x=>new{x.MaNguyenLieu,x.TonThucTe,x.TongGiuCho,x.PhienBanDuLieu}).ToListAsync(),
            audit=await db.NhatKyHeThong.AsNoTracking().OrderBy(x=>x.MaNhatKy).Select(x=>new{x.MaNhatKy,x.MaThaoTac,x.HanhDong,x.MaBamYeuCau,x.KetQuaDaChotJson}).ToListAsync(),
            banks=await db.GiaoDichNganHang.AsNoTracking().OrderBy(x=>x.MaGiaoDichNganHang).Select(x=>new{x.MaGiaoDichNganHang,x.MaGiaoDichBenNgoai,x.SoTien,x.MaThanhToan,x.TrangThaiXacMinh,x.MaBamNoiDung}).ToListAsync(),
            refunds=await db.HoSoHoanTien.AsNoTracking().OrderBy(x=>x.MaHoanTien).Select(x=>new{x.MaHoanTien,x.SoTienYeuCau,x.TrangThai}).ToListAsync(),
            cancellations=await db.HuyMon.CountAsync(),payouts=await db.LanChiHoanTien.CountAsync()
        });
    }
    Guid GuidFor(string slot)=>new(SHA256.HashData(Encoding.UTF8.GetBytes(fixture.RunId+":"+slot)).AsSpan(0,16));
    static void Check(bool condition,string message) { if(!condition) throw new SafetyStop(message); }
    static void Same(string before,string after,string message)=>Check(before==after,message);
    static DefaultHttpContext Context(long customer,Guid key)
    {
        var context=new DefaultHttpContext { User=new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier,customer.ToString())],"IsolatedSqlFixture")) };
        context.Request.Headers["Idempotency-Key"]=key.ToString(); return context;
    }
    static GiaoDichNganHang Copy(GiaoDichNganHang b,string? account=null,decimal? amount=null)=>new() {TenNhaCungCap=b.TenNhaCungCap,MaTaiKhoanNganHangQuan=account??b.MaTaiKhoanNganHangQuan,MaGiaoDichBenNgoai=b.MaGiaoDichBenNgoai,ChieuGiaoDich=b.ChieuGiaoDich,SoTien=amount??b.SoTien,NoiDungChuyenKhoan=b.NoiDungChuyenKhoan,ThoiDiemNganHang=b.ThoiDiemNganHang};
    static string Payload(IResult result)=>result is Microsoft.AspNetCore.Http.HttpResults.ContentHttpResult content ? content.ResponseContent??"" : JsonSerializer.Serialize((result as IValueHttpResult)?.Value);
    sealed class TestSource(VerifiedBankTransfer? value):IPaymentVerificationSource { public Task<VerifiedBankTransfer?> AuthenticateAsync(ReadOnlyMemory<byte> b,IReadOnlyDictionary<string,string> h,CancellationToken ct)=>Task.FromResult(value); }
    sealed class TrustedEventBarrier(VerifiedBankTransfer value):IPaymentVerificationSource
    {
        readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously); int arrivals;
        public int Arrivals=>arrivals;
        public async Task<VerifiedBankTransfer?> AuthenticateAsync(ReadOnlyMemory<byte> b,IReadOnlyDictionary<string,string> h,CancellationToken ct)
        { if(Interlocked.Increment(ref arrivals)==2) ready.TrySetResult(); await ready.Task.WaitAsync(TimeSpan.FromSeconds(5),ct); return value; }
    }
    sealed class FixedClock(DateTime utc):TimeProvider { public override DateTimeOffset GetUtcNow()=>new(utc,TimeSpan.Zero); }
    sealed class InvalidAudit(Guid marker):SaveChangesInterceptor
    {
        public bool Applied,MarkerInserted;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {
            var db=data.Context!;
            var tx=db.Database.CurrentTransaction??throw new SafetyStop("Audit fault requires active service transaction");
            var connection=db.Database.GetDbConnection();
            var rawTx=Microsoft.EntityFrameworkCore.Storage.DbContextTransactionExtensions.GetDbTransaction(tx);
            await FixtureSqlGuard.VerifyAsync(connection,rawTx,ct:ct);
            await using var cmd=connection.CreateCommand();cmd.Transaction=rawTx;
            cmd.CommandText="INSERT dbo.NhatKyHeThong(MaLienKetXuLy,MaThaoTac,LoaiTacNhan,TenNguoiThucHienDaChot,HanhDong,LoaiDoiTuong,KetQua,PhatSinhLuc) VALUES(@marker,@marker,'SYSTEM',N'Fixture','FIXTURE_ROLLBACK_MARKER','Fixture','SUCCESS',SYSUTCDATETIME()); SELECT COUNT(*) FROM dbo.NhatKyHeThong WHERE MaThaoTac=@marker;";
            var parameter=cmd.CreateParameter();parameter.ParameterName="@marker";parameter.Value=marker;cmd.Parameters.Add(parameter);
            MarkerInserted=Convert.ToInt32(await cmd.ExecuteScalarAsync(ct))==1;
            foreach(var row in data.Context!.ChangeTracker.Entries<FourChang.Domain.Entities.Audit.NhatKyHeThong>().Where(x=>x.State==EntityState.Added)) { row.Entity.KetQuaDaChotJson="NOT_JSON"; Applied=true; }
            return result;
        }
    }
}
