using FourChang.Api.Shared.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using FourChang.Tests.Fixtures;
using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FourChang.Api.Features.Customer.Orders;
using FourChang.Api.Features.Customer.Payments;
using FourChang.Api.Security;
using FourChang.Domain.Entities.Accounts;
using FourChang.Domain.Entities.Audit;
using FourChang.Domain.Entities.Payments;
using FourChang.Infrastructure.Database.Context;
using FourChang.Infrastructure.Security;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Customer;
using FourChang.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

if (args.SequenceEqual(new[]{"--probe-session-clock"})) await SessionClockProbe.RunAsync();
else if (args.SequenceEqual(new[]{"--http-readonly-preflight"})) await HttpReadOnlyPreflight.RunAsync();
else await Closure.RunAsync(args);
sealed class SafetyStop(string message):Exception(message);
sealed record Fixture(string Database,Guid RunId,string SchemaSha256,long CustomerId,long OtherCustomerId,Dictionary<string,long> Orders);
static class Closure
{
 public const string Dir="artifacts/final-review/integration-closure-v2/";
 static readonly Dictionary<string,object?> Evidence=new(){["status"]="BLOCKED_SQL_OUTBOX",["writesAttempted"]=false};
 static readonly List<object> Steps=[];
 static readonly IConfiguration Config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
  ["Orders:SqlIntegrationValidated"]="true",["Orders:CancellationEnabled"]="true",["Orders:PaymentProcessingEnabled"]="true",
  ["Orders:OutboxDispatchEnabled"]="true",["Orders:OutboxSchemaValidated"]="true",["Bank:Provider"]="FIXTURE",
  ["Bank:ReceiverAccount"]="SYNTHETIC",["Orders:PendingPaymentTimeoutMinutes"]="15"}).Build();
 public static void Check(bool pass,string label){Steps.Add(new{label,pass,utc=DateTime.UtcNow});if(!pass)throw new SafetyStop(label);}
 public static CafeDbContext Db(params IInterceptor[] extra)=>new(new DbContextOptionsBuilder<CafeDbContext>()
  .UseSqlServer(FixtureSqlGuard.Connection()).AddInterceptors(new IInterceptor[]{new FixtureConnectionGuard(),new FixtureCommandGuard()}.Concat(extra)).Options);
 public static async Task RunAsync(string[] args)
 {
  try {
   if(!args.SequenceEqual(new[]{"--run-once"}))throw new SafetyStop("Explicit --run-once required");
   FixtureSqlGuard.Load();
   Check(!File.Exists(Dir+"write-once.json"),"No prior attempt on this fixture");
   foreach(var port in new[]{14330,14331,14332,14333,14334}) {
    await using var forbidden=new SqlConnection($"Server=127.0.0.1,{port};Database={FixtureSqlGuard.Database}");
    var rejected=false;try{FixtureSqlGuard.Target(forbidden);}catch(SafetyStop){rejected=true;}
    Check(rejected,"Reject historical port "+port+" before open");
   }
   await File.WriteAllTextAsync(Dir+"write-once.json",JsonSerializer.Serialize(new{FixtureSqlGuard.RunId,utc=DateTime.UtcNow}));
   Evidence["writesAttempted"]=true;
   var fixture=await FixtureProvisioner.CreateAsync(SchemaFingerprint.SchemaQuery,Evidence);
   await using(var sql=new SqlConnection(FixtureSqlGuard.Connection(admin:true))) {
    await sql.OpenAsync();await FixtureSqlGuard.VerifyAsync(sql);
    var migration=await File.ReadAllTextAsync("docs/database/proposals/ORDER-OUTBOX-DELIVERY.up.sql");
    Check(migration.Contains("IF DB_NAME()<>N'FourChangOutboxMigrationFixture'"),"Reviewed migration pin exact");
    Evidence["migrationSourceSha256"]=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(migration)));
    migration=migration.Replace("IF DB_NAME()<>N'FourChangOutboxMigrationFixture'","IF DB_NAME()<>N'FourChangOutboxFixtureV2'",StringComparison.Ordinal);
    Evidence["migrationSha256"]=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(migration)));
    await using var command=sql.CreateCommand();
    command.CommandText="EXEC sys.sp_set_session_context @key=N'ORDER_OUTBOX_MIGRATION_APPROVED',@value=1;\n"+migration;
    await command.ExecuteNonQueryAsync(); FixtureSqlGuard.DeliveryReady=true;await FixtureSqlGuard.VerifyAsync(sql);
    Evidence["migrationChecks"]=await FixtureProvisioner.ReadRows(sql,"SELECT name,is_disabled,is_not_trusted,definition FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.OrderOutboxDelivery') ORDER BY name");
   }
   var tokens=await SessionsAsync(fixture);
   Evidence["B0"]=await SnapshotAsync();
   await HttpAsync(fixture,tokens);
   Evidence["B1"]=await SnapshotAsync();
   await OutboxAsync(fixture);
   Evidence["B2"]=await SnapshotAsync();
   Evidence["status"]="PASS_SQL_AND_HTTP_INPROCESS";
  } catch(Exception error) {
   Evidence["status"]="FAIL_OR_BLOCKED_STOPPED_NO_RETRY";
   Evidence["errorType"]=error.GetType().Name;
   Evidence["error"]=error is SafetyStop?error.Message:"See redacted type/stack; no exception message copied (may contain secrets).";
   Evidence["errorStack"]=error.StackTrace;
   if (error.InnerException is SqlException sqlError)
    Evidence["sqlErrorsRedacted"]=sqlError.Errors.Cast<SqlError>().Select(x=>new {
     x.Number, x.State, x.Class, constraints=Regex.Matches(x.Message,@"\b(?:CK|FK|UQ|UX)_[A-Za-z0-9_]+\b").Select(m=>m.Value).Distinct().ToArray() }).ToArray();
   Environment.ExitCode=1;
  } finally {
   Evidence["steps"]=Steps; Evidence["sessions"]=FixtureSqlGuard.Sessions.Values;
   Evidence["target"]="127.0.0.1:14335/"+FixtureSqlGuard.Database;
   Evidence["expiredMigrationApplied"]=false;Evidence["provider"]="FAKE_VERIFIED_EVENT_ONLY";
   await File.WriteAllTextAsync(Dir+"result.json",JsonSerializer.Serialize(Evidence,new JsonSerializerOptions{WriteIndented=true}));
   Console.WriteLine(JsonSerializer.Serialize(new{status=Evidence["status"],steps=Steps.Count,exit=Environment.ExitCode}));
  }
 }
 public static async Task ValidateSeedAsync(Fixture f) {
  await using var db=Db();Check(await db.DonHang.CountAsync()==10 && await db.ThanhToan.CountAsync()==10 && await db.GiuChoNguyenLieu.CountAsync()==10,"F1 counts");
  Check(await db.DonHang.AllAsync(x=>x.MaKhachHang==f.CustomerId&&x.TrangThai=="PENDING_PAYMENT"),"F1 owner/state");
  Check(await db.TonKho.SumAsync(x=>x.TongGiuCho)==100 && await db.TonKho.SumAsync(x=>x.TonThucTe)==1000,"F1 holds/stock");
 }
 static async Task<Dictionary<string,string>> SessionsAsync(Fixture f) {
  // Fixture-only historical sessions: CafeDbContext intentionally stamps every Added row at now.
  // The interceptor restores the fixture historical creation time AFTER that production rule,
  // before SQL. No production context/schema is changed; this corrected seed has NOT been rerun.
  var tokens=new Dictionary<string,string>();await using var db=Db(new FixtureSessionClock());await using var tx=await db.Database.BeginTransactionAsync();
  var customers=await db.TaiKhoanKhachHang.ToListAsync();foreach(var c in customers){c.TrangThai="ACTIVE";c.MatKhauBam=Passwords.Hash(Environment.GetEnvironmentVariable("UC0708_CLOSURE_CUSTOMER_PASSWORD")??throw new SafetyStop("Fixture login password absent"));}
  var admin=await db.TaiKhoanQuanLy.SingleAsync();admin.TrangThai="ACTIVE";
  foreach(var label in new[]{"admin","revoked","expired"}) {
   var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));tokens[label]=token;
   db.PhienDangNhap.Add(new(){MaKhachHang=label=="admin"?null:label=="b"?f.OtherCustomerId:f.CustomerId,
    MaQuanLy=label=="admin"?admin.MaQuanLy:null,MaLamMoiDaBam=Passwords.TokenHash(token),PhamViPhien="FULL",LoaiUngDung="WEB",
    ThoiDiemTao=DateTime.UtcNow.AddHours(-2),HetHanLuc=label=="expired"?DateTime.UtcNow.AddHours(-1):DateTime.UtcNow.AddHours(1),
    ThuHoiLuc=label=="revoked"?DateTime.UtcNow:null});
  }
  await db.SaveChangesAsync();
  var stored=await db.PhienDangNhap.AsNoTracking().Select(x=>new{x.MaPhienDangNhap,x.ThoiDiemTao,x.HetHanLuc,x.ThuHoiLuc}).ToListAsync();
  Check(stored.Count==3&&stored.All(x=>x.HetHanLuc>x.ThoiDiemTao),"SQL session timestamps valid AFTER SaveChanges");
  Check(stored.Count(x=>x.HetHanLuc<DateTime.UtcNow)==1,"SQL actual expired session preserved");
  Evidence["storedSessionsAfterSave"]=stored;
  await tx.CommitAsync();return tokens;
 }
 static async Task<WebApplication> ApiAsync() {
  var b=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="ClosureFixture"});b.Configuration.Sources.Clear();b.Configuration.AddConfiguration(Config);
  b.Logging.ClearProviders();b.WebHost.UseTestServer();
  b.Services.AddScoped(_=>Db());b.Services.AddScoped<ICustomerOrderReader,CustomerOrderReader>();
  b.Services.AddScoped<FourChang.Api.Features.Customer.Account.CustomerAccessService>();
  b.Services.AddScoped<FourChang.Api.Features.Admin.Access.AdminAccountService>();
  b.Services.AddRateLimiter(o=>o.AddFixedWindowLimiter("auth",x=>{x.PermitLimit=20;x.Window=TimeSpan.FromMinutes(1);}));
  b.Services.AddAuthentication("Session").AddScheme<AuthenticationSchemeOptions,SessionAuthentication>("Session",_=>{});
  b.Services.AddAuthorization(o=>o.AddPolicy("Customer",p=>p.RequireRole("CUSTOMER")));
  var app=b.Build();app.Use(async(c,next)=>{try{await next(c);}catch(DbUpdateConcurrencyException){c.Response.StatusCode=409;}catch(DbUpdateException){c.Response.StatusCode=409;}catch(SqlException){c.Response.StatusCode=503;}});
  app.UseRateLimiter();app.UseAuthentication();app.UseAuthorization();app.MapAuth();app.MapCustomerOrders();app.MapCustomerOrderDetails();
  await app.StartAsync();return app;
 }
 static async Task<(HttpResponseMessage,string)> Send(HttpClient client,string? token,HttpMethod method,string path,object? body=null,Guid? key=null,decimal? total=null) {
  using var request=new HttpRequestMessage(method,path);if(token is not null)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
  if(body is not null)request.Content=JsonContent.Create(body);if(key.HasValue)request.Headers.Add("Idempotency-Key",key.Value.ToString());
  if(total.HasValue)request.Headers.Add("X-Checkout-Accepted-Total",total.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
  var response=await client.SendAsync(request);return(response,await response.Content.ReadAsStringAsync());
 }
 static async Task HttpAsync(Fixture f,Dictionary<string,string> tokens) {
  await using var api=await ApiAsync();using var client=api.GetTestClient();
  foreach(var who in new[]{"a","b"}) {
   var login=await Send(client,null,HttpMethod.Post,"/api/auth/customer/login",new FourChang.Presentation.Shared.Contracts.Common.SignIn("PH5_CUSTOMER_"+who.ToUpperInvariant(),Environment.GetEnvironmentVariable("UC0708_CLOSURE_CUSTOMER_PASSWORD")!,Guid.NewGuid()));
   Check(login.Item1.StatusCode==HttpStatusCode.OK,"HTTP real customer login "+who);
   var session=JsonSerializer.Deserialize<FourChang.Presentation.Shared.Contracts.Common.SessionResult>(login.Item2,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
   Check(session.Role=="CUSTOMER"&&session.Id==(who=="a"?f.CustomerId:f.OtherCustomerId),"HTTP login identity "+who);tokens[who]=session.Token;
  }
  await using var db=Db();var option=await db.CauHinhMon.Select(x=>x.MaCauHinhMon).SingleAsync();
  var payload=new CreateCustomerOrderRequest("TAKEAWAY",null,"Synthetic receiver","0900000000",null,null,null,[new(option,1,100,100,null,[])]);
  foreach(var label in new[]{"anonymous","invalid","revoked","expired","admin"}) {
   var r=await Send(client,label=="anonymous"?null:label=="invalid"?"invalid-fixture-token":tokens[label],HttpMethod.Post,"/api/customer/orders",payload,Guid.NewGuid(),10000);
   Check((int)r.Item1.StatusCode==(label=="admin"?403:401),"HTTP real auth "+label);
  }
  var before=await SnapshotAsync();api.Configuration["Checkout:OrderCreationEnabled"]="true";
  var off=await Send(client,tokens["a"],HttpMethod.Post,"/api/customer/orders",payload,Guid.NewGuid(),10000);
  Check(off.Item1.StatusCode==HttpStatusCode.ServiceUnavailable&&off.Item2.Contains("CUSTOMER_ORDER_CREATION_DISABLED"),"HTTP backend flag OFF despite Web ON");
  Check(before==await SnapshotAsync(),"Flag OFF unchanged SQL");
  api.Configuration["Orders:CustomerCreationEnabled"]="true";
  Check((await Send(client,tokens["a"],HttpMethod.Post,"/api/customer/orders",payload)).Item1.StatusCode==HttpStatusCode.BadRequest,"HTTP missing idempotency");
  var price=await Send(client,tokens["a"],HttpMethod.Post,"/api/customer/orders",payload,Guid.NewGuid(),10001);
  Check(price.Item1.StatusCode==HttpStatusCode.Conflict&&before==await SnapshotAsync(),"HTTP accepted total conflict no writes");
  var key=Guid.NewGuid();var create=await Send(client,tokens["a"],HttpMethod.Post,"/api/customer/orders",payload,key,10000);
  Evidence["createHttpStatus"]=(int)create.Item1.StatusCode;
  Check(create.Item1.StatusCode==HttpStatusCode.Created,"HTTP 201 real create");
  var receipt=JsonSerializer.Deserialize<CustomerOrderReceipt>(create.Item2,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;Evidence["createdOrder"]=receipt;
  var committed=await SnapshotAsync();
  var replay=await Send(client,tokens["a"],HttpMethod.Post,"/api/customer/orders",payload,key,10000);
  Check(replay.Item1.StatusCode==HttpStatusCode.OK&&JsonNode.DeepEquals(JsonNode.Parse(replay.Item2),JsonNode.Parse(create.Item2))&&committed==await SnapshotAsync(),"HTTP create replay exact receipt unchanged SQL");
  Check((await Send(client,tokens["b"],HttpMethod.Post,"/api/customer/orders",payload,key,10000)).Item1.StatusCode==HttpStatusCode.Conflict,"HTTP wrong owner create replay");
  Check((await Send(client,tokens["a"],HttpMethod.Post,"/api/customer/orders",payload with{GhiChu="changed"},key,10000)).Item1.StatusCode==HttpStatusCode.Conflict,"HTTP create payload conflict");
  var id=f.Orders["cancel"];await BankAsync("cancel",10000);
  Check((await Send(client,tokens["a"],HttpMethod.Get,$"/api/customer/orders/{id}")).Item1.StatusCode==HttpStatusCode.OK,"HTTP owner detail");
  Check((await Send(client,tokens["b"],HttpMethod.Get,$"/api/customer/orders/{id}")).Item1.StatusCode==HttpStatusCode.NotFound,"HTTP foreign detail denied");
  await WebAsync(api,id,tokens);
  var pending=f.Orders["partial"];var pendingVersion=Convert.ToBase64String(await db.DonHang.AsNoTracking().Where(x=>x.MaDonHang==pending).Select(x=>x.PhienBanDuLieu).SingleAsync());
  Check((await Send(client,tokens["a"],HttpMethod.Post,$"/api/customer/orders/{pending}/cancel",new CustomerOrderCancellation.CancelRequest("Synthetic cancellation",pendingVersion),Guid.NewGuid())).Item1.StatusCode==HttpStatusCode.Conflict,"HTTP cancel wrong state");
  var operation=Guid.NewGuid();var version=Convert.ToBase64String(await db.DonHang.AsNoTracking().Where(x=>x.MaDonHang==id).Select(x=>x.PhienBanDuLieu).SingleAsync());
  var cancel=new CustomerOrderCancellation.CancelRequest("Synthetic cancellation",version);
  Check((await Send(client,tokens["b"],HttpMethod.Post,$"/api/customer/orders/{id}/cancel",cancel,operation)).Item1.StatusCode==HttpStatusCode.NotFound,"HTTP foreign cancel denied");
  Check((await Send(client,tokens["a"],HttpMethod.Post,$"/api/customer/orders/{id}/cancel",cancel with{Reason=""},operation)).Item1.StatusCode==HttpStatusCode.BadRequest,"HTTP cancel reason required");
  Check((await Send(client,tokens["a"],HttpMethod.Post,$"/api/customer/orders/{id}/cancel",cancel with{ExpectedVersion=Convert.ToBase64String(new byte[8])},operation)).Item1.StatusCode==HttpStatusCode.Conflict,"HTTP stale cancel version");
  var cancelled=await Send(client,tokens["a"],HttpMethod.Post,$"/api/customer/orders/{id}/cancel",cancel,operation);Check(cancelled.Item1.StatusCode==HttpStatusCode.OK,"HTTP cancel 200");
  var after=await SnapshotAsync();var cr=await Send(client,tokens["a"],HttpMethod.Post,$"/api/customer/orders/{id}/cancel",cancel,operation);
  Check(cr.Item1.StatusCode==HttpStatusCode.OK&&JsonNode.DeepEquals(JsonNode.Parse(cr.Item2),JsonNode.Parse(cancelled.Item2))&&after==await SnapshotAsync(),"HTTP cancel replay unchanged");
  Check((await Send(client,tokens["a"],HttpMethod.Post,$"/api/customer/orders/{id}/cancel",cancel with{Reason="Different reason"},operation)).Item1.StatusCode==HttpStatusCode.Conflict,"HTTP cancel payload conflict");
  Check(await db.HoSoHoanTien.CountAsync(x=>x.MaDonHang==id&&x.TrangThai=="REQUESTED")==1&&!await db.LanChiHoanTien.AnyAsync(),"SQL refund requested no payout");
  Check(await db.TonKho.SumAsync(x=>x.TonThucTe)==1000&&await db.TonKho.SumAsync(x=>x.TongGiuCho)==100,"SQL create +10 cancel -10 physical unchanged");
  foreach(var path in new[]{$"/api/customer/orders/{id}/paid",$"/api/customer/orders/{id}/refund/succeeded"})
   Check((await Send(client,tokens["a"],HttpMethod.Post,path,new{})).Item1.StatusCode==HttpStatusCode.NotFound,"No customer financial success command");
  Check(await db.NhatKyHeThong.CountAsync(x=>x.MaThaoTac==key)==1&&await db.NhatKyHeThong.CountAsync(x=>x.MaThaoTac==operation)==1,"SQL journal once per create/cancel");
  Evidence["httpPipeline"]="TestServer; production SessionAuthentication + SQL sessions; no fake auth; API registration reused, host composed explicitly (not API Program/bootstrap)";
 }
 static async Task WebAsync(WebApplication api,long id,Dictionary<string,string> tokens) {
  var b=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(FourChang.Web.Pages.Orders.IndexModel).Assembly.GetName().Name,ContentRootPath=Path.GetFullPath("src/Presentation/FourChang.Web"),EnvironmentName="ClosureFixture"});
  b.Configuration.Sources.Clear();b.Logging.ClearProviders();b.WebHost.UseTestServer();b.Services.AddRazorPages();
  b.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());b.Services.AddSingleton<IPaymentQrSource,UnavailablePaymentQrSource>();
  b.Services.AddHttpClient<CafeApiClient>(c=>c.BaseAddress=new Uri("http://localhost")).ConfigurePrimaryHttpMessageHandler(()=>api.GetTestServer().CreateHandler());
  await using var web=b.Build();web.MapRazorPages();await web.StartAsync();using var client=web.GetTestClient();
  await LoginWebAsync(client,"a");
  using(var other=web.GetTestClient()){await LoginWebAsync(other,"b");Check((await other.GetAsync($"/Orders/{id}")).StatusCode==HttpStatusCode.NotFound,"HTTP real Web cookie B denied A order");}
  var get=await client.GetAsync($"/Orders/{id}");var html=await get.Content.ReadAsStringAsync();Check(get.StatusCode==HttpStatusCode.OK,"HTTP Razor detail via real authenticated API");
  var form=Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");Check(form.Success,"Razor antiforgery token issued");
  var noCsrf=await client.PostAsync($"/Orders/{id}?handler=Cancel",new FormUrlEncodedContent(new Dictionary<string,string>{{"reason","Synthetic cancellation"}}));Check(noCsrf.StatusCode==HttpStatusCode.BadRequest,"HTTP Web missing CSRF 400");
  // Retain the real login session and antiforgery cookies already issued by LoginWebAsync.
  var bad=await client.PostAsync($"/Orders/{id}?handler=Cancel",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken","invalid-fixture-csrf"},{"reason","Synthetic cancellation"}}));
  Check(bad.StatusCode==HttpStatusCode.BadRequest,"HTTP wrong CSRF 400");
  var valid=await client.PostAsync($"/Orders/{id}?handler=Cancel",new FormUrlEncodedContent(new Dictionary<string,string>{{"reason","Synthetic cancellation"},{"__RequestVerificationToken",WebUtility.HtmlDecode(form.Groups[1].Value)}}));
  Check(valid.StatusCode==HttpStatusCode.ServiceUnavailable,"HTTP valid CSRF Web cancellation remains locked");
 }
 static async Task<string> LoginWebAsync(HttpClient client,string who) {
  var get=await client.GetAsync("/Account/Login");Check(get.StatusCode==HttpStatusCode.OK,"HTTP login page "+who);
  var html=await get.Content.ReadAsStringAsync();var token=Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");Check(token.Success,"Login CSRF issued "+who);
  var cookies=get.Headers.GetValues("Set-Cookie").Select(x=>x.Split(';')[0]);
  client.DefaultRequestHeaders.Add("Cookie",string.Join("; ",cookies));
  var post=await client.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{["TenDangNhap"]="PH5_CUSTOMER_"+who.ToUpperInvariant(),["MatKhau"]=Environment.GetEnvironmentVariable("UC0708_CLOSURE_CUSTOMER_PASSWORD")!,["MaThaoTac"]=Guid.NewGuid().ToString(),["__RequestVerificationToken"]=WebUtility.HtmlDecode(token.Groups[1].Value)}));
  Check(post.StatusCode==HttpStatusCode.Found,"HTTP Web login redirect "+who);
  var cookie=post.Headers.GetValues("Set-Cookie").Single(x=>x.StartsWith("fourchang_session=",StringComparison.Ordinal));
  Check(cookie.Contains("httponly",StringComparison.OrdinalIgnoreCase),"Real login HttpOnly cookie "+who);
  var session=cookie.Split(';')[0];client.DefaultRequestHeaders.Remove("Cookie");client.DefaultRequestHeaders.Add("Cookie",session+"; "+string.Join("; ",cookies));return session;
 }
 static async Task BankAsync(string slot,decimal amount,params IInterceptor[] extra) {
  await using var db=Db(extra);var payment=await db.ThanhToan.AsNoTracking().SingleAsync(x=>x.MaThanhToanHienThi=="PH5-P-"+slot);
  var e=new VerifiedBankTransfer(new GiaoDichNganHang{TenNhaCungCap="FIXTURE",MaTaiKhoanNganHangQuan="SYNTHETIC",MaGiaoDichBenNgoai=Guid.NewGuid().ToString(),ChieuGiaoDich="IN",SoTien=amount,NoiDungChuyenKhoan=payment.MaThamChieuChuyenKhoan,ThoiDiemNganHang=DateTime.UtcNow.AddSeconds(-1)},"VND");
  var result=await new SqlBankPaymentService(db,Config,new Source(e),TimeProvider.System).ProcessAsync(new byte[]{1},new Dictionary<string,string>(),default);
  Check(((IStatusCodeHttpResult)result).StatusCode==200,"Verified FAKE event SQL "+slot);
 }
 sealed class Source(VerifiedBankTransfer e):IPaymentVerificationSource {public Task<VerifiedBankTransfer?> AuthenticateAsync(ReadOnlyMemory<byte>b,IReadOnlyDictionary<string,string> h,CancellationToken ct)=>Task.FromResult<VerifiedBankTransfer?>(e);}
 public static async Task<string> SnapshotAsync() {
  await using var sql=new SqlConnection(FixtureSqlGuard.Connection());await sql.OpenAsync();
  return JsonSerializer.Serialize(await FixtureProvisioner.ReadRows(sql,"SELECT (SELECT MaDonHang,TrangThai,PhienBanDuLieu FROM DonHang ORDER BY MaDonHang FOR JSON PATH) orders,(SELECT MaThanhToan,TrangThai,SoTienDaXacMinh FROM ThanhToan ORDER BY MaThanhToan FOR JSON PATH) payments,(SELECT COUNT(*) FROM NguyenLieuChiTietDonHang) bomRows,(SELECT COUNT(*) FROM GiaoDichNganHang) bankRows,(SELECT COUNT(*) FROM NhatKyHeThong) audits,(SELECT COUNT(*) FROM HoSoHoanTien) refunds,(SELECT COUNT(*) FROM LanChiHoanTien) payouts,(SELECT MaChiTietDonHang,MaNguyenLieu,TrangThai,SoLuongGiuCho,SoLuongDaGiaiPhong FROM GiuChoNguyenLieu ORDER BY MaChiTietDonHang,MaNguyenLieu FOR JSON PATH) holds,(SELECT TonThucTe,TongGiuCho FROM TonKho FOR JSON PATH) stock"));
 }
 // Outbox scenarios are defined separately, kept out of production registrations.
 static async Task OutboxAsync(Fixture f)=>await OutboxScenarios.RunAsync(f,Config,BankAsync,SnapshotAsync,Check);
}
