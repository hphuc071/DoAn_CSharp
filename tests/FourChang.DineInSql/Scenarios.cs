using System.Data;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using FourChang.Api.Features.Customer.Orders;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

sealed record SqlSnapshot(long Orders,long Payments,long Audits,long Outbox,long Holds,long Bom,decimal Held,decimal Physical,string Rows);
static partial class ConsentRun
{
 public static async Task<SqlSnapshot> SnapshotAsync() {
  await using var sql=new SqlConnection(FixtureSqlGuard.Connection());await sql.OpenAsync();
  var rows=await FixtureProvisioner.ReadRows(sql,"""
SELECT (SELECT COUNT_BIG(*) FROM DonHang) orders,(SELECT COUNT_BIG(*) FROM ThanhToan) payments,
(SELECT COUNT_BIG(*) FROM NhatKyHeThong WHERE HanhDong='CREATE_CUSTOMER_ORDER') audits,
(SELECT COUNT_BIG(*) FROM NhatKyHeThong WHERE HanhDong='CREATE_CUSTOMER_ORDER' AND JSON_VALUE(DuLieuSauJson,'$.outbox.Kind')='order.created') outbox,
(SELECT COUNT_BIG(*) FROM GiuChoNguyenLieu) holds,(SELECT COUNT_BIG(*) FROM NguyenLieuChiTietDonHang) bom,
(SELECT SUM(TongGiuCho) FROM TonKho) held,(SELECT SUM(TonThucTe) FROM TonKho) physical,
(SELECT MaDonHang,MaDonHienThi,TrangThai,HetHanLuc,PhiGiaoHangBanDau FROM DonHang ORDER BY MaDonHang FOR JSON PATH) orderRows,
(SELECT MaThanhToan,MaDonHang,SoTienYeuCau,SoTienDaXacMinh,TrangThai,HetHanLuc FROM ThanhToan ORDER BY MaThanhToan FOR JSON PATH) paymentRows,
(SELECT MaChiTietDonHang,MaDonHang,GiaMonDaChot,SoLuongDat,SoLuongDaHuy FROM ChiTietDonHang ORDER BY MaChiTietDonHang FOR JSON PATH) lineRows,
(SELECT MaChiTietDonHang,MaMonThem,GiaMoiPhanDaChot,SoPhanMoiLy FROM MonThemChiTietDonHang ORDER BY MaChiTietDonHang,MaMonThem FOR JSON PATH) toppingRows,
(SELECT MaChiTietDonHang,MaNguyenLieu,DinhMucMoiLy FROM NguyenLieuChiTietDonHang ORDER BY MaChiTietDonHang,MaNguyenLieu FOR JSON PATH) bomRows,
(SELECT MaChiTietDonHang,MaNguyenLieu,SoLuongGiuCho,SoLuongDaTieuHao,SoLuongDaGiaiPhong,TrangThai FROM GiuChoNguyenLieu ORDER BY MaChiTietDonHang,MaNguyenLieu FOR JSON PATH) holdRows,
(SELECT MaThaoTac,MaDoiTuong,MaBamYeuCau,KetQuaDaChotJson,DuLieuSauJson FROM NhatKyHeThong WHERE HanhDong='CREATE_CUSTOMER_ORDER' ORDER BY MaThaoTac FOR JSON PATH) auditRows,
(SELECT COUNT(*) FROM GiaoDichNganHang) banks,(SELECT COUNT(*) FROM HoSoHoanTien) refunds,(SELECT COUNT(*) FROM LanChiHoanTien) payouts
""");
  var r=rows.Single();Check(Convert.ToInt32(r["banks"])==0&&Convert.ToInt32(r["refunds"])==0&&Convert.ToInt32(r["payouts"])==0,"No bank/refund/payout writes");
  return new(Convert.ToInt64(r["orders"]),Convert.ToInt64(r["payments"]),Convert.ToInt64(r["audits"]),Convert.ToInt64(r["outbox"]),Convert.ToInt64(r["holds"]),Convert.ToInt64(r["bom"]),Convert.ToDecimal(r["held"]),Convert.ToDecimal(r["physical"]),JsonSerializer.Serialize(rows));
 }
 static async Task EffectsAsync(string label,SqlSnapshot before,int orders,decimal held) {
  var after=await SnapshotAsync();Evidence[label]=new{before,after};
  Check(after.Orders==before.Orders+orders&&after.Payments==before.Payments+orders&&after.Audits==before.Audits+orders&&after.Outbox==before.Outbox+orders&&after.Holds==before.Holds+orders&&after.Bom==before.Bom+orders&&after.Held==before.Held+held&&after.Physical==before.Physical,label+" exact SQL effects");
  if(orders==0)Check(after==before,label+" all SQL rows unchanged");
 }
 static CustomerOrderReceipt ReceiptOf((HttpResponseMessage Response,string Body) r)=>JsonSerializer.Deserialize<CustomerOrderReceipt>(r.Body,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new SafetyStop("Missing receipt");
 static async Task VerifyOrderAsync(CustomerOrderReceipt receipt,decimal basePrice,int qty,int tops,decimal perCup) {
  await using var db=Db();var order=await db.DonHang.AsNoTracking().SingleAsync(x=>x.MaDonHang==receipt.MaDonHang);
  var line=await db.ChiTietDonHang.AsNoTracking().SingleAsync(x=>x.MaDonHang==order.MaDonHang);
  var payment=await db.ThanhToan.AsNoTracking().SingleAsync(x=>x.MaDonHang==order.MaDonHang);
  var topping=await db.MonThemChiTietDonHang.AsNoTracking().Where(x=>x.MaChiTietDonHang==line.MaChiTietDonHang).ToListAsync();
  var hold=await db.GiuChoNguyenLieu.AsNoTracking().SingleAsync(x=>x.MaChiTietDonHang==line.MaChiTietDonHang);
  var bom=await db.NguyenLieuChiTietDonHang.AsNoTracking().SingleAsync(x=>x.MaChiTietDonHang==line.MaChiTietDonHang);
  Check(order.TrangThai=="PENDING_PAYMENT"&&payment.TrangThai=="PENDING"&&payment.SoTienDaXacMinh==0,"Pending order/payment not PAID");
  Check(line.GiaMonDaChot==basePrice&&line.SoLuongDat==qty&&topping.Count==tops&&topping.All(t=>t.GiaMoiPhanDaChot==2000&&t.SoPhanMoiLy==1),"Price/topping/quantity snapshots consistent");
  Check(receipt.TongCong==(basePrice+topping.Sum(t=>t.GiaMoiPhanDaChot*t.SoPhanMoiLy))*qty&&receipt.TongCong==payment.SoTienYeuCau&&receipt.PhiGiaoHang==0,"Receipt/payment/line totals match");
  Check(bom.DinhMucMoiLy==perCup&&hold.SoLuongGiuCho==perCup*qty&&hold.SoLuongDaTieuHao==0&&hold.SoLuongDaGiaiPhong==0&&hold.TrangThai=="RESERVED","BOM and held quantity match once");
  Check(receipt.HetHanLuc.HasValue&&order.HetHanLuc.HasValue&&Math.Abs((receipt.HetHanLuc.Value-order.HetHanLuc.Value).TotalMilliseconds)<1&&order.HetHanLuc==payment.HetHanLuc,"Receipt/order/payment deadline matches datetime2 precision");
  var audit=await db.NhatKyHeThong.AsNoTracking().SingleAsync(x=>x.MaDoiTuong==order.MaDonHang.ToString()&&x.HanhDong=="CREATE_CUSTOMER_ORDER");
  var recorded=JsonSerializer.Deserialize<CustomerOrderReceipt>(audit.KetQuaDaChotJson!)!;
  Check(recorded==receipt&&OrderAuditOutbox.Read(audit.DuLieuSauJson!).Kind=="order.created","Durable original receipt/outbox journal matches");
  Check(Math.Abs((receipt.HetHanLuc.GetValueOrDefault()-audit.PhatSinhLuc.AddMinutes(15)).TotalMilliseconds)<1,"Configured 15 minutes from server audit clock");
 }
 static async Task ExecuteAsync(string sql,params SqlParameter[] parameters) {
  await using var c=new SqlConnection(FixtureSqlGuard.Connection());await c.OpenAsync();await FixtureSqlGuard.VerifyAsync(c);
  await using var cmd=c.CreateCommand();cmd.CommandText=sql;cmd.CommandTimeout=15;cmd.Parameters.AddRange(parameters);await cmd.ExecuteNonQueryAsync();
 }
 static async Task<object> ObserveWaitAsync() {
  for(int i=0;i<50;i++) {
   await using var c=new SqlConnection(FixtureSqlGuard.Connection(admin:true));await c.OpenAsync();
   var rows=await FixtureProvisioner.ReadRows(c,"SELECT r.session_id,r.blocking_session_id,r.wait_type,r.wait_time,r.transaction_id FROM sys.dm_exec_requests r JOIN sys.dm_exec_sessions s ON s.session_id=r.session_id WHERE r.blocking_session_id>0 AND s.program_name='FourChang.Uc06DineIn.IsolatedFixture'");
   if(rows.Count>0)return rows;
   await Task.Delay(100);
  }
  throw new SafetyStop("Expected SQL blocking wait not observed");
 }
 static async Task HttpAndSqlAsync(Fixture f) {
  await using var api=await ApiAsync();using var client=api.GetTestClient();client.Timeout=TimeSpan.FromSeconds(40);
  var a=await LoginAsync(client,"A");var b=await LoginAsync(client,"B");
  await using var web=await WebAsync(api);using var browser=web.GetTestClient();
  var webState=await WebLoginAsync(browser); // Real Razor login + CSRF before business baseline.
  var initial=await SnapshotAsync();Evidence["B0"]=initial;
  if(Environment.GetEnvironmentVariable("DINEIN_INDEPENDENT_ONLY")=="true") {
   Config["Orders:CustomerCreationEnabled"]="true";
   await ExecuteAsync("UPDATE dbo.CauHinhMon SET GiaBan=11000 WHERE MaCauHinhMon=@id",new SqlParameter("@id",Variant));
   await IndependentConcurrencyAsync(client,a);return;
  }
  var off=await Send(client,a,"/api/customer/orders",Payload(),Guid.NewGuid(),10000);
  Check(off.Response.StatusCode==HttpStatusCode.ServiceUnavailable,"Backend OFF direct POST503");await EffectsAsync("flagOff",initial,0,0);
  Config["Orders:CustomerCreationEnabled"]="true";
  foreach(var method in new[]{"DINE_IN","DELIVERY"}) {
   var x=await Send(client,a,"/api/customer/orders",Payload() with{HinhThucNhanMon=method},Guid.NewGuid(),10000);
   Check(x.Response.StatusCode==HttpStatusCode.ServiceUnavailable,"Unsupported "+method+" Create503");
  }
  var missing=await Send(client,a,"/api/customer/orders",Payload(),Guid.NewGuid());Check(missing.Response.StatusCode==HttpStatusCode.BadRequest,"Missing accepted total400");
  var wrong=await Send(client,a,"/api/customer/orders",Payload(),Guid.NewGuid(),10001);Check(wrong.Response.StatusCode==HttpStatusCode.Conflict,"Mismatched accepted total409");
  await EffectsAsync("invalidCommands",initial,0,0);
  var key=Guid.NewGuid();var exact=await WebCreateAsync(browser,webState,Payload(),key,10000,true);
  Check(exact.Response.StatusCode==HttpStatusCode.Created,"Real Customer Razor CSRF Create201");var receipt=ReceiptOf(exact);await VerifyOrderAsync(receipt,10000,1,0,10);await EffectsAsync("exact",initial,1,10);
  Evidence["exactReceipt"]=receipt;var committed=await SnapshotAsync();
  var replay=await Send(client,a,"/api/customer/orders",Payload(),key,10000);Check(replay.Response.StatusCode==HttpStatusCode.OK&&ReceiptOf(replay)==receipt,"Replay200 exact original receipt");
  Check((await Send(client,a,"/api/customer/orders",Payload(),key,11000)).Response.StatusCode==HttpStatusCode.Conflict,"Replay changed total409");
  Check((await Send(client,a,"/api/customer/orders",Payload(2),key,10000)).Response.StatusCode==HttpStatusCode.Conflict,"Replay changed payload409");
  Check((await Send(client,b,"/api/customer/orders",Payload(),key,10000)).Response.StatusCode==HttpStatusCode.Conflict,"Other Customer replay409");
  var historical=await Send(client,a,"/api/customer/orders",HistoricalPayload,HistoricalKey,10000);Check(historical.Response.StatusCode==HttpStatusCode.OK&&ReceiptOf(historical)==HistoricalReceipt,"Historical hash + past deadline replay unchanged");
  Check((await WebCreateAsync(browser,webState,Payload(),Guid.NewGuid(),10000,false)).Response.StatusCode==HttpStatusCode.BadRequest,"Razor missing CSRF400");
  Check((await Send(client,AdminToken,"/api/customer/orders",Payload(),Guid.NewGuid(),10000)).Response.StatusCode==HttpStatusCode.Forbidden,"Admin cannot call Customer Create403");
  using(var get=new HttpRequestMessage(HttpMethod.Get,"/api/admin/data/Ban")){get.Headers.Authorization=new("Bearer",AdminToken);Check((await client.SendAsync(get)).StatusCode==HttpStatusCode.OK,"Actual Admin table GET unaffected");}
  using(var get=new HttpRequestMessage(HttpMethod.Get,"/api/pos/data/DonHang")){get.Headers.Authorization=new("Bearer",a);Check((await client.SendAsync(get)).StatusCode==HttpStatusCode.Forbidden,"Actual POS policy rejects Customer403");}
  await EffectsAsync("allReplaysAndAuth",committed,0,0);
  // Same GUID overlap: first request holds real SQL locks while second waits.
  var gate=new SaveGate(1);SaveControl.Current=gate;var sameKey=Guid.NewGuid();var before=await SnapshotAsync();
  var first=Send(client,a,"/api/customer/orders",Payload(),sameKey,10000);await gate.Ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
  var second=Send(client,a,"/api/customer/orders",Payload(),sameKey,10000);
  try{Evidence["sameGuidWait"]=await ObserveWaitAsync();}finally{gate.Release.TrySetResult();}
  var same=await Task.WhenAll(first,second);SaveControl.Current=null;
  Check(same.Select(x=>(int)x.Response.StatusCode).Order().SequenceEqual(new[]{200,201})&&ReceiptOf(same[0])==ReceiptOf(same[1]),"Concurrent same GUID exactly201+200 same order");
  await VerifyOrderAsync(ReceiptOf(same[0]),10000,1,0,10);await EffectsAsync("sameGuid",before,1,10);
  // Price updater cannot pass SERIALIZABLE read locks before the order commits.
  gate=new(1);SaveControl.Current=gate;before=await SnapshotAsync();var priceCreate=Send(client,a,"/api/customer/orders",Payload(),Guid.NewGuid(),10000);await gate.Ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
  var update=ExecuteAsync("SET LOCK_TIMEOUT 10000; UPDATE dbo.CauHinhMon SET GiaBan=11000 WHERE MaCauHinhMon=@id",new SqlParameter("@id",Variant));
  try{Evidence["priceUpdateWait"]=await ObserveWaitAsync();}finally{gate.Release.TrySetResult();}
  var priced=await priceCreate;await update;SaveControl.Current=null;
  Check(priced.Response.StatusCode==HttpStatusCode.Created,"Price race Create201 with pre-update confirmed total");await VerifyOrderAsync(ReceiptOf(priced),10000,1,0,10);await EffectsAsync("priceRace",before,1,10);
  before=await SnapshotAsync();Check((await Send(client,a,"/api/customer/orders",Payload(),Guid.NewGuid(),10000)).Response.StatusCode==HttpStatusCode.Conflict,"Already changed menu requires reconfirm409");
  Check((await Send(client,a,"/api/customer/orders",Payload(2,true),Guid.NewGuid(),11000)).Response.StatusCode==HttpStatusCode.Conflict,"Changed cart/toppings/qty requires new total409");await EffectsAsync("staleCart",before,0,0);
  var topped=await Send(client,a,"/api/customer/orders",Payload(2,true),Guid.NewGuid(),26000);Check(topped.Response.StatusCode==HttpStatusCode.Created,"Exact changed cart total201");await VerifyOrderAsync(ReceiptOf(topped),11000,2,1,12);await EffectsAsync("topping",before,1,24);
  before=await SnapshotAsync();SaveControl.AuditFault=true;
  var failed=await Send(client,a,"/api/customer/orders",Payload(),Guid.NewGuid(),11000);SaveControl.AuditFault=false;
  Check(failed.Response.StatusCode==HttpStatusCode.InternalServerError&&failed.Body.Contains("FIXTURE_AUDIT_FAILURE"),"Required audit failure surfaced500");await EffectsAsync("auditRollback",before,0,0);
  await LockTimeoutAsync(client,a);
  await LockTimeoutAsync(client,a,true);
  Evidence["B1"]=await SnapshotAsync();
  await IndependentConcurrencyAsync(client,a);
  Evidence["B2"]=await SnapshotAsync();
 }
 static async Task LockTimeoutAsync(HttpClient client,string a,bool common=false) {
  var before=await SnapshotAsync();var operation=Guid.NewGuid();
  await using var c=new SqlConnection(FixtureSqlGuard.Connection());await c.OpenAsync();await using var tx=(SqlTransaction)await c.BeginTransactionAsync();await FixtureSqlGuard.VerifyAsync(c,tx);
  await using var lockCmd=c.CreateCommand();lockCmd.Transaction=tx;lockCmd.CommandText="DECLARE @n int; EXEC @n=sys.sp_getapplock @Resource=@key,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=0; SELECT @n";lockCmd.Parameters.AddWithValue("@key",common?"4chang.customerorder.create":"4chang.order."+operation.ToString("N"));Check(Convert.ToInt32(await lockCmd.ExecuteScalarAsync())>=0,"Harness acquired test operation lock");
  var timer=Stopwatch.StartNew();var pending=Send(client,a,"/api/customer/orders",Payload(),operation,11000);Evidence[common?"commonTimeoutWait":"timeoutWait"]=await ObserveWaitAsync();var result=await pending;timer.Stop();await tx.RollbackAsync();
  Evidence[common?"commonLockTimeout":"lockTimeout"]=new{http=(int)result.Response.StatusCode,elapsedMs=timer.ElapsedMilliseconds,operation};
  Check(result.Response.StatusCode==HttpStatusCode.Conflict&&timer.ElapsedMilliseconds>=9000&&timer.ElapsedMilliseconds<20000&&(!common||result.Body.Contains("CUSTOMER_ORDER_CREATE_BUSY")),"Application lock timeout bounded409, no automatic retry");await EffectsAsync(common?"commonLockTimeoutNoWrite":"lockTimeoutNoWrite",before,0,0);
 }
 static async Task IndependentConcurrencyAsync(HttpClient client,string a) {
  var before=await SnapshotAsync();var gate=new SaveGate(1);SaveControl.Current=gate;
  var first=Send(client,a,"/api/customer/orders",Payload(),Guid.NewGuid(),11000);await gate.Ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
  var second=Send(client,a,"/api/customer/orders",Payload(),Guid.NewGuid(),11000);var requests=new[]{first,second};
  try{Evidence["independentCreateWait"]=await ObserveWaitAsync();}finally{gate.Release.TrySetResult();}
  var outcomes=await Task.WhenAll(requests);SaveControl.Current=null;
  Evidence["independentConcurrentOutcomes"]=outcomes.Select(x=>new{http=(int)x.Response.StatusCode,sqlNumber=x.Response.Headers.TryGetValues("X-Fixture-Sql-Number",out var n)?n.Single():null}).ToArray();
  await using(var diagnostic=new SqlConnection(FixtureSqlGuard.Connection(admin:true))){await diagnostic.OpenAsync();Evidence["liveDeadlockGraph"]=await FixtureProvisioner.ReadRows(diagnostic,"SELECT CONVERT(nvarchar(max),e.query('.')) eventXml FROM sys.dm_xe_session_targets t JOIN sys.dm_xe_sessions s ON s.address=t.event_session_address CROSS APPLY (SELECT CAST(t.target_data AS xml) data) d CROSS APPLY d.data.nodes('/RingBufferTarget/event[@name=\"xml_deadlock_report\"]') n(e) WHERE s.name='system_health' AND t.target_name='ring_buffer'");}
  Evidence["independentFinalSnapshot"]=await SnapshotAsync();
  Check(((System.Collections.ICollection)Evidence["liveDeadlockGraph"]!).Count==0,"No SQL system_health deadlock after fix");
  Check(outcomes.All(x=>x.Response.StatusCode==HttpStatusCode.Created),"Independent Create concurrency no hidden deadlock/timeout");await EffectsAsync("independentConcurrency",before,2,20);
 }
}
