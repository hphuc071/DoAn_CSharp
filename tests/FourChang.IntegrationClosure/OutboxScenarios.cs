using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FourChang.Api.Features.Customer.Orders;
using FourChang.Domain.Entities.Audit;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

static class OutboxScenarios
{
 static IConfiguration config=null!;
 static async Task<OrderOutboxLease?> Claim(){await using var db=Closure.Db();return await new SqlOrderOutboxDeliveryStore(db,config).ClaimAsync(default);}
 static async Task Ack(OrderOutboxLease lease){await using var db=Closure.Db();await new SqlOrderOutboxDeliveryStore(db,config).CompleteAsync(lease,default);}
 static async Task<string> Dispatch(){await using var db=Closure.Db();return await new OrderOutboxDispatcher(config,new SqlOrderOutboxDeliveryStore(db,config),new SqlInboxSink()).RunOnceAsync(default);}
 static async Task Expire(OrderOutboxLease lease){await using var db=Closure.Db();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.OrderOutboxDelivery SET LeaseUntilUtc=DATEADD(second,-1,SYSUTCDATETIME()) WHERE AuditId={lease.AuditId} AND LeaseToken={lease.Token}");}
 static async Task<long> Effects(Guid eventId){await using var db=Closure.Db();var id=SqlInboxSink.Operation(eventId);return await db.NhatKyHeThong.CountAsync(x=>x.MaThaoTac==id&&x.HanhDong=="FIXTURE_INBOX_EFFECT");}
 public static async Task RunAsync(Fixture fixture,IConfiguration settings,Func<string,decimal,IInterceptor[],Task> bank,Func<Task<string>> snapshot,Action<bool,string> check)
 {
  config=settings;
  // Drain only committed real-business envelopes first. No data deletion or fake status changes.
  for(var i=0;i<20;i++){var status=await Dispatch();if(status=="OUTBOX_EMPTY")break;check(status=="OUTBOX_DELIVERED","SQL initial journal delivered "+i);if(i==19)throw new SafetyStop("Unexpected event budget");}
  await using(var db=Closure.Db())check(await db.NhatKyHeThong.CountAsync(x=>x.DuLieuSauJson!=null&&x.DuLieuSauJson.Contains("\"outbox\""))==3,"SQL create/payment/cancel each one journal");
  // Two committed payments in the SAME aggregate, no memory-only test event.
  await bank("partial",5000,[]);await bank("partial",5000,[]);
  await using(var failureDb=Closure.Db())check(await new OrderOutboxDispatcher(config,new SqlOrderOutboxDeliveryStore(failureDb,config),new FailingSink()).RunOnceAsync(default)=="OUTBOX_RETRY_OR_DEAD","SQL transient send failure recorded");
  await using(var retryDb=Closure.Db())await retryDb.Database.ExecuteSqlRawAsync("UPDATE dbo.OrderOutboxDelivery SET NextAttemptUtc=DATEADD(second,-1,SYSUTCDATETIME()) WHERE Status='PENDING'");
  var contenders=await Task.WhenAll(Task.Run(Claim),Task.Run(Claim));
  check(contenders.Count(x=>x is not null)==1,"SQL two dispatchers only one live lease for aggregate");
  var first=contenders.Single(x=>x is not null)!;var evt=OrderAuditOutbox.Read(first.AuditJson);
  await new SqlInboxSink().DeliverAsync(evt,default); // Simulated crash AFTER durable sink effect, BEFORE ACK.
  check(await Effects(evt.EventId)==1,"SQL durable inbox effect before lost ACK");
  await Expire(first);var retry=await Claim();
  check(retry is not null&&retry.AuditId==first.AuditId&&retry.Token!=first.Token,"SQL expired lease replay same event new token");
  var fenced=false;try{await Ack(first);}catch(InvalidOperationException ex)when(ex.Message=="OUTBOX_LEASE_LOST"){fenced=true;}
  check(fenced,"SQL stale lease cannot ACK replacement");
  await new SqlInboxSink().DeliverAsync(OrderAuditOutbox.Read(retry!.AuditJson),default);await Ack(retry);
  check(await Effects(evt.EventId)==1,"SQL duplicate delivery durable effect once");
  var second=await Claim();check(second is not null&&second.AuditId>first.AuditId&&OrderAuditOutbox.Read(second.AuditJson).Aggregate==evt.Aggregate,"SQL next state follows ACK of previous state");
  await new SqlInboxSink().DeliverAsync(OrderAuditOutbox.Read(second!.AuditJson),default);await Ack(second);
  // Crash BEFORE sink delivery, restart different store/context.
  await bank("late",1000,[]);var before=await Claim();check(before is not null,"SQL pre-delivery lease acquired");
  await Expire(before!);var restarted=await Claim();check(restarted is not null&&restarted.AuditId==before!.AuditId,"SQL crash before delivery recovered");
  await new SqlInboxSink().DeliverAsync(OrderAuditOutbox.Read(restarted!.AuditJson),default);await Ack(restarted);
  // JSON remains schema-valid but envelope version is unsupported; mutation ONLY in fixture interceptor.
  await bank("concurrent",1000,[new PoisonEnvelope()]);await bank("concurrent",1000,[]);
  for(var i=1;i<=5;i++) {
   check(await Dispatch()=="OUTBOX_RETRY_OR_DEAD","SQL poison retry "+i);
   await using var db=Closure.Db();await db.Database.ExecuteSqlRawAsync("UPDATE dbo.OrderOutboxDelivery SET NextAttemptUtc=DATEADD(second,-1,SYSUTCDATETIME()) WHERE Status='PENDING'");
  }
  await using(var sql=new SqlConnection(FixtureSqlGuard.Connection())) {
   await sql.OpenAsync();var rows=await FixtureProvisioner.ReadRows(sql,"SELECT AuditId,Status,Attempts,LastErrorCode FROM dbo.OrderOutboxDelivery WHERE Status='DEAD'");
   check(rows.Count==1&&Convert.ToInt32(rows[0]["Attempts"])==5,"SQL poison DEAD after five attempts");
   await File.WriteAllTextAsync(Closure.Dir+"dead-letter.json",JsonSerializer.Serialize(rows));
  }
  check(await Claim() is null,"SQL poison blocks later event of same aggregate");
  await bank("exact",1000,[]);check(await Dispatch()=="OUTBOX_DELIVERED","SQL unrelated aggregate progresses despite poison");
  // Expected business rollback; check real preceding marker INSERT rolls back as well.
  var state=await snapshot();var marker=Guid.NewGuid();var interceptor=new AuditFailure(marker);var rolledBack=false;
  try{await bank("payment-rollback",10000,[interceptor]);}
  catch(DbUpdateException ex)when(ex.InnerException is SqlException se&&se.Number==547){rolledBack=true;}
  check(rolledBack&&interceptor.MarkerInserted&&state==await snapshot(),"SQL business/audit/event rollback including prior INSERT");
  await using(var db=Closure.Db())check(!await db.NhatKyHeThong.AnyAsync(x=>x.MaThaoTac==marker),"SQL rollback marker absent");
  check(await Claim() is null,"SQL rollback creates no dispatchable event");
  await using(var sql=new SqlConnection(FixtureSqlGuard.Connection())) {
   await sql.OpenAsync();var rows=await FixtureProvisioner.ReadRows(sql,"SELECT d.AuditId,d.Status,d.Attempts,d.DeliveredAtUtc,d.LastErrorCode,JSON_VALUE(a.DuLieuSauJson,'$.outbox.Aggregate') AggregateKey,JSON_VALUE(a.DuLieuSauJson,'$.outbox.Kind') Kind FROM dbo.OrderOutboxDelivery d JOIN dbo.NhatKyHeThong a ON a.MaNhatKy=d.AuditId ORDER BY d.AuditId");
   await File.WriteAllTextAsync(Closure.Dir+"delivery-final.json",JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
  }
 }
 sealed class FailingSink:IOrderOutboxSink {public Task DeliverAsync(OrderOutboxEvent e,CancellationToken ct)=>throw new IOException("Fixture transient local sink failure");}
 sealed class SqlInboxSink:IOrderOutboxSink
 {
  public static Guid Operation(Guid id)=>new(SHA256.HashData(Encoding.UTF8.GetBytes("fixture.inbox."+id)).AsSpan(0,16));
  public async Task DeliverAsync(OrderOutboxEvent message,CancellationToken ct) {
   await using var db=Closure.Db();await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);var operation=Operation(message.EventId);
   var resource="fixture.inbox."+message.EventId.ToString("N");
   await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000; IF @r<0 THROW 51400,'Fixture inbox lock unavailable',1;",ct);
   if(!await db.NhatKyHeThong.AnyAsync(x=>x.MaThaoTac==operation,ct)) {
    db.NhatKyHeThong.Add(new NhatKyHeThong{MaLienKetXuLy=operation,MaThaoTac=operation,LoaiTacNhan="SYSTEM",TenNguoiThucHienDaChot="Fixture inbox (no financial effects)",HanhDong="FIXTURE_INBOX_EFFECT",LoaiDoiTuong="FixtureEvent",MaDoiTuong=message.EventId.ToString(),KetQua="SUCCESS",PhatSinhLuc=DateTime.UtcNow});await db.SaveChangesAsync(ct);
   }
   await tx.CommitAsync(ct);
  }
 }
 sealed class PoisonEnvelope:SaveChangesInterceptor {
  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default) {
   foreach(var e in data.Context!.ChangeTracker.Entries<NhatKyHeThong>().Where(x=>x.State==EntityState.Added)) {
    var node=JsonNode.Parse(e.Entity.DuLieuSauJson!)!;node["outbox"]!["Version"]=99;e.Entity.DuLieuSauJson=node.ToJsonString();
   }return ValueTask.FromResult(result);
  }
 }
 sealed class AuditFailure(Guid marker):SaveChangesInterceptor {
  public bool MarkerInserted;
  public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default) {
   await data.Context!.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.NhatKyHeThong(MaLienKetXuLy,MaThaoTac,LoaiTacNhan,TenNguoiThucHienDaChot,HanhDong,LoaiDoiTuong,KetQua,PhatSinhLuc) VALUES({marker},{marker},'SYSTEM',N'Fixture','ROLLBACK_MARKER','Fixture','SUCCESS',SYSUTCDATETIME())",ct);MarkerInserted=true;
   foreach(var e in data.Context.ChangeTracker.Entries<NhatKyHeThong>().Where(x=>x.State==EntityState.Added))e.Entity.KetQuaDaChotJson="NOT_JSON";
   return result;
  }
 }
}
