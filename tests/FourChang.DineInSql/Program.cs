using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using FourChang.Api.Features.Customer.Orders;
using FourChang.Api.Security;
using FourChang.Api.Shared.Authentication;
using FourChang.Domain.Entities.Accounts;
using FourChang.Domain.Entities.Audit;
using FourChang.Domain.Entities.Menu;
using FourChang.Domain.Entities.Orders;
using FourChang.Domain.Entities.Payments;
using FourChang.Infrastructure.Database.Context;
using FourChang.Infrastructure.Security;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Common;
using FourChang.Presentation.Shared.Contracts.Customer;
using FourChang.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

if(args.SequenceEqual(new[]{"--inspect-failure"})) await ConsentRun.InspectFailureAsync();
else await ConsentRun.RunAsync(args);
sealed class SafetyStop(string message) : Exception(message);
sealed class InjectedAuditFailure : Exception;
sealed record Fixture(string Database,Guid RunId,string SchemaSha256,long CustomerId,long OtherCustomerId,Dictionary<string,long> Orders);
static partial class ConsentRun
{
 public const string Dir="artifacts/uc-web-06/sql-dinein-v1/";
 public static readonly Dictionary<string,object?> Evidence=new(){["status"]="BLOCKED_ENVIRONMENT"};
 public static readonly List<object> Steps=[];
 public static readonly List<object> Errors=[];
 static SqlException? FindSql(Exception error){for(Exception? e=error;e is not null;e=e.InnerException)if(e is SqlException sql)return sql;return null;}
 public static readonly IConfigurationRoot Config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
  ["Orders:PendingPaymentTimeoutMinutes"]="15",["Orders:CustomerCreationEnabled"]="false"}).Build();
 public static long Variant,Topping,Recipe,Ingredient;
 public static Guid HistoricalKey;
 public static CustomerOrderReceipt HistoricalReceipt=null!;
 public static CreateCustomerOrderRequest HistoricalPayload=null!;
 public static void Check(bool value,string label){lock(Steps)Steps.Add(new{label,pass=value,utc=DateTime.UtcNow});if(!value)throw new SafetyStop(label);}
 public static CafeDbContext Db()=>new(new DbContextOptionsBuilder<CafeDbContext>().UseSqlServer(FixtureSqlGuard.Connection())
  .AddInterceptors(new FixtureConnectionGuard(),new FixtureCommandGuard()).Options);
 public static CreateCustomerOrderRequest Payload(int qty=1,bool topping=false)=>new("TAKEAWAY",null,"Synthetic receiver","0900000000",null,null,null,
  [new(Variant,qty,100,100,null,topping?[new(Topping,1)]:[])]);
 public static async Task RunAsync(string[] args)
 {
  // Refuse before finally/result persistence too: accidental direct execution must not
  // overwrite the evidence of a completed fixture or attempt a second business run.
  if(!args.SequenceEqual(new[]{"--run-once"})||File.Exists(Dir+"write-once.json")||File.Exists(Dir+"result.json"))
   throw new SafetyStop("Fixture already attempted or arguments invalid; evidence preserved, no SQL opened.");
  try {
   if(!args.SequenceEqual(new[]{"--run-once"}))throw new SafetyStop("Explicit run-once required");
   FixtureSqlGuard.Load();Check(!File.Exists(Dir+"write-once.json"),"No earlier write attempt");
   Evidence["runtime"]=new{pid=Environment.ProcessId,http="TestServer in-process; no TCP listener",
    apiSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(typeof(CustomerOrdersEndpoints).Assembly.Location))),
    webSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(typeof(FourChang.Web.Pages.Checkout.IndexModel).Assembly.Location)))};
   foreach(var p in Enumerable.Range(14330,11)) {
    await using var forbidden=new SqlConnection($"Server=127.0.0.1,{p};Database={FixtureSqlGuard.Database}");
    bool rejected=false;try{FixtureSqlGuard.Target(forbidden);}catch(SafetyStop){rejected=true;}Check(rejected,"Reject historical port "+p);
   }
   await File.WriteAllTextAsync(Dir+"write-once.json",JsonSerializer.Serialize(new{FixtureSqlGuard.RunId,utc=DateTime.UtcNow}));
   var f=await FixtureProvisioner.CreateAsync(SchemaFingerprint.SchemaQuery,Evidence);
   await CompleteSeedAsync(f);
   Evidence["F1"]=await SnapshotAsync();
   await DineInSqlAsync(f);
   Evidence["status"]="PASS_DINE_IN_SQL_FIXTURE_V1";
  } catch(Exception ex) {
   Evidence["status"]=ex is SafetyStop && !File.Exists(Dir+"manifest.json")?"BLOCKED_ENVIRONMENT":"FAIL";
   Evidence["errorType"]=ex.GetType().Name;Evidence["error"]=ex is SafetyStop?ex.Message:"Redacted; see exception type and SQL numbers";
   Evidence["stack"]=ex.StackTrace;
   var sql=ex as SqlException??ex.InnerException as SqlException;
   if(sql is not null)Evidence["sqlErrorNumbers"]=sql.Errors.Cast<SqlError>().Select(e=>e.Number).ToArray();
   Environment.ExitCode=1;
  } finally {
   SaveControl.Current?.Release.TrySetResult();
   Evidence["steps"]=Steps;Evidence["httpErrors"]=Errors;Evidence["sessions"]=FixtureSqlGuard.Sessions.Values;
   Evidence["transactionObservations"]=SaveControl.Observations;Evidence["target"]="127.0.0.1:14341/"+FixtureSqlGuard.Database;
   Evidence["expiredMigrationApplied"]=false;Evidence["bankProviderCalled"]=false;
   await File.WriteAllTextAsync(Dir+"result.json",JsonSerializer.Serialize(Evidence,new JsonSerializerOptions{WriteIndented=true}));
   Console.WriteLine(JsonSerializer.Serialize(new{status=Evidence["status"],assertions=Steps.Count,exit=Environment.ExitCode}));
  }
 }
 public static async Task ValidateSeedAsync(Fixture f) {
  await using var db=Db();Check(!await db.DonHang.AnyAsync()&&!await db.ThanhToan.AnyAsync()&&!await db.GiuChoNguyenLieu.AnyAsync(),"Minimal F1 seed has no orders");
  Check(await db.TonKho.SumAsync(x=>x.TonThucTe)==1000&&await db.TonKho.SumAsync(x=>x.TongGiuCho)==0,"Minimal F1 stock");
 }
 static async Task CompleteSeedAsync(Fixture f) {
  await using var db=Db();await using var tx=await db.Database.BeginTransactionAsync();
  Variant=await db.CauHinhMon.Select(x=>x.MaCauHinhMon).SingleAsync();Ingredient=await db.NguyenLieu.Select(x=>x.MaNguyenLieu).SingleAsync();
  Recipe=await db.CongThuc.Select(x=>x.MaCongThuc).SingleAsync();
  foreach(var customer in await db.TaiKhoanKhachHang.ToListAsync()){customer.TrangThai="ACTIVE";customer.MatKhauBam=Passwords.Hash(Environment.GetEnvironmentVariable("DINEIN_CUSTOMER_PASSWORD")!);}
  var admin=await db.TaiKhoanQuanLy.SingleAsync();admin.TrangThai="ACTIVE";
  var top=new MonThem{TenMonThem="Synthetic topping",GiaMoiPhan=2000,GioiHanMacDinhMoiLy=2,DangSuDung=true};db.Add(top);await db.SaveChangesAsync();Topping=top.MaMonThem;
  var recipe=new CongThuc{MaMonThem=Topping,SoPhienBan=1,TenCongThuc="Topping recipe",TrangThai="DRAFT",MaQuanLyTao=admin.MaQuanLy};db.Add(recipe);await db.SaveChangesAsync();
  db.Add(new ChiTietCongThuc{MaCongThuc=recipe.MaCongThuc,MaNguyenLieu=Ingredient,LoaiDinhMuc="FIXED",DinhMucCoSo=2});
  db.Add(new CauHinhMonThem{MaCauHinhMon=Variant,MaMonThem=Topping,DangSuDung=true,GioiHanMoiLy=2});await db.SaveChangesAsync();
  recipe.TrangThai="ACTIVE";recipe.MaQuanLyDuyet=admin.MaQuanLy;recipe.HieuLucTu=DateTime.UtcNow;
  var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));AdminToken=token;
  db.Add(new PhienDangNhap{MaQuanLy=admin.MaQuanLy,MaLamMoiDaBam=Passwords.TokenHash(token),PhamViPhien="FULL",LoaiUngDung="WEB",HetHanLuc=DateTime.UtcNow.AddHours(1)});
  // Historical-format receipt: old DTO SHA256 and expired deadline, no new consent column/hash.
  HistoricalKey=Guid.NewGuid();HistoricalPayload=Payload();var deadline=DateTime.UtcNow.AddHours(-1);
  var order=new DonHang{MaDonHienThi="UC06-HISTORICAL",MaKhachHang=f.CustomerId,KenhDatHang="WEB",HinhThucNhanMon="TAKEAWAY",TrangThai="PENDING_PAYMENT",HetHanLuc=deadline};
  db.Add(order);await db.SaveChangesAsync();
  var line=new ChiTietDonHang{MaDonHang=order.MaDonHang,MaCauHinhMon=Variant,MaCongThuc=Recipe,TenMonDaChot="Historical fixture beverage",KichCoDaChot="M",NhietDoDaChot="ICED",PhanTramDuong=100,PhanTramDa=100,GiaMonDaChot=10000,SoLuongDat=1};db.Add(line);await db.SaveChangesAsync();
  db.Add(new NguyenLieuChiTietDonHang{MaChiTietDonHang=line.MaChiTietDonHang,MaNguyenLieu=Ingredient,TenNguyenLieuDaChot="Fixture ingredient",DonViCoSoDaChot="g",DinhMucMoiLy=10});
  db.Add(new FourChang.Domain.Entities.Inventory.GiuChoNguyenLieu{MaChiTietDonHang=line.MaChiTietDonHang,MaNguyenLieu=Ingredient,SoLuongGiuCho=10,TrangThai="RESERVED",HetHanLuc=deadline});
  (await db.TonKho.SingleAsync()).TongGiuCho=10;
  db.Add(new ThanhToan{MaDonHang=order.MaDonHang,MaThanhToanHienThi="UC06-HISTORICAL-P",PhuongThuc="BANK_TRANSFER",SoTienYeuCau=10000,TrangThai="PENDING",MaThaoTac=HistoricalKey,HetHanLuc=deadline});
  HistoricalReceipt=new(order.MaDonHang,order.MaDonHienThi,"PENDING_PAYMENT",10000,0,10000,deadline);
  // SQL datetime2(3) and JSON original deadline must match exactly.
  HistoricalReceipt=HistoricalReceipt with{HetHanLuc=new DateTime(deadline.Ticks/TimeSpan.TicksPerMillisecond*TimeSpan.TicksPerMillisecond,DateTimeKind.Utc)};
  order.HetHanLuc=HistoricalReceipt.HetHanLuc;
  db.Add(OrderAuditOutbox.Stamp(new NhatKyHeThong{MaThaoTac=HistoricalKey,MaLienKetXuLy=HistoricalKey,LoaiTacNhan="CUSTOMER",MaKhachHang=f.CustomerId,TenNguoiThucHienDaChot="Synthetic A",HanhDong="CREATE_CUSTOMER_ORDER",LoaiDoiTuong="DonHang",MaDoiTuong=order.MaDonHang.ToString(),KetQua="SUCCESS",MaBamYeuCau=SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(HistoricalPayload)),KetQuaDaChotJson=JsonSerializer.Serialize(HistoricalReceipt),PhatSinhLuc=DateTime.UtcNow}));
  await db.SaveChangesAsync();await tx.CommitAsync();
 }
 static string AdminToken="";
 static async Task<WebApplication> ApiAsync() {
  var b=WebApplication.CreateBuilder();b.Configuration.Sources.Clear();b.Configuration.AddConfiguration(Config);b.Logging.ClearProviders();b.WebHost.UseTestServer();
  b.Services.AddDbContext<CafeDbContext>(o=>o.UseSqlServer(FixtureSqlGuard.Connection()).AddInterceptors(new FixtureConnectionGuard(),new FixtureCommandGuard(),new SaveControl()));
  b.Services.AddScoped<FourChang.Api.Features.Customer.Account.CustomerAccessService>();b.Services.AddScoped<FourChang.Api.Features.Admin.Access.AdminAccountService>();
  b.Services.AddScoped<FourChang.Api.Features.Customer.Account.CustomerSelfService>();
  b.Services.AddScoped<ICustomerOrderReader,CustomerOrderReader>();b.Services.AddSingleton(TimeProvider.System);b.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());b.Services.AddScoped<ITableQrLookup,SqlTableQrLookup>();b.Services.AddScoped<IFulfillmentCartPricer,FulfillmentCartPricer>();b.Services.AddSingleton<IDeliveryRouteSource,UnavailableDeliveryRouteSource>();b.Services.AddScoped<FulfillmentPreviewService>();b.Services.AddAuthentication("Session").AddScheme<AuthenticationSchemeOptions,SessionAuthentication>("Session",_=>{});
  b.Services.AddAuthorization(o=>{o.AddPolicy("Customer",p=>p.RequireRole("CUSTOMER"));o.AddPolicy("Admin",p=>p.RequireRole("ADMIN"));o.AddPolicy("Staff",p=>p.RequireRole("STAFF"));});
  b.Services.AddRateLimiter(o=>o.AddFixedWindowLimiter("auth",x=>{x.PermitLimit=100;x.Window=TimeSpan.FromMinutes(1);}));
  var app=b.Build();app.Use(async(c,next)=>{try{await next(c);}catch(Exception e){var sql=FindSql(e);lock(Errors)Errors.Add(new{type=e.GetType().Name,numbers=sql?.Errors.Cast<SqlError>().Select(x=>x.Number).ToArray(),operation=c.Request.Headers["Idempotency-Key"].ToString()});if(sql is not null)c.Response.Headers["X-Fixture-Sql-Number"]=sql.Number.ToString();c.Response.StatusCode=e is DbUpdateConcurrencyException or DbUpdateException?409:e is SqlException?503:500;await c.Response.WriteAsJsonAsync(new{code=e is InjectedAuditFailure?"FIXTURE_AUDIT_FAILURE":"FIXTURE_REDACTED_ERROR"});}});
  app.UseRateLimiter();app.UseAuthentication();app.UseAuthorization();app.MapAuth();app.MapCustomerOrders();FourChang.Api.Features.Admin.Tables.TableQrAdministration.MapTableQrAdministration(app);app.MapCustomerOrderDetails();
  FourChang.Api.Features.Admin.Data.DataEndpoints.MapData(app);
  FourChang.Api.Features.Staff.Data.StaffDataEndpoints.MapStaffData(app);
  FourChang.Api.Features.Customer.Account.CustomerSelfEndpoints.MapCustomerSelf(app);
  await app.StartAsync();return app;
 }
 static async Task<(HttpResponseMessage Response,string Body)> Send(HttpClient client,string? token,string path,object body,Guid? operation=null,decimal? total=null) {
  using var request=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(body)};
  if(token is not null)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
  if(operation.HasValue)request.Headers.Add("Idempotency-Key",operation.Value.ToString());
  if(total.HasValue)request.Headers.Add("X-Checkout-Accepted-Total",total.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
  var response=await client.SendAsync(request);return(response,await response.Content.ReadAsStringAsync());
 }
 static async Task<string> LoginAsync(HttpClient client,string who) {
  var login=await Send(client,null,"/api/auth/customer/login",new SignIn("UC06_CUSTOMER_"+who,Environment.GetEnvironmentVariable("DINEIN_CUSTOMER_PASSWORD")!,Guid.NewGuid()));
  Check(login.Response.StatusCode==HttpStatusCode.OK,"Real Customer login "+who);
  return JsonSerializer.Deserialize<SessionResult>(login.Body,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!.Token;
 }
}
