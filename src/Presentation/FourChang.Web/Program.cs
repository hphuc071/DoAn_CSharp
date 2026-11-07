using FourChang.Web.Services;
using FourChang.Presentation.Shared.ApiClients;
using Microsoft.Extensions.FileProviders;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddCustomerServices();
builder.Services.AddSingleton<IPaymentQrSource, UnavailablePaymentQrSource>();
var app = builder.Build();
app.UseStaticFiles();
var assets = Path.Combine(AppContext.BaseDirectory,"Assets");
if(Directory.Exists(assets))app.UseStaticFiles(new StaticFileOptions{FileProvider=new PhysicalFileProvider(assets),RequestPath="/assets"});
// Thử thu hồi phiên đã đăng xuất khi trình duyệt kết nối lại; không chạm giỏ/bàn nháp.
app.Use(async(context,next)=>{if(context.Request.Cookies.ContainsKey("fourchang_pending_logout")){var pending=context.RequestServices.GetRequiredService<FourChang.Web.Security.PendingCustomerLogout>();var api=context.RequestServices.GetRequiredService<CafeApiClient>();await pending.Retry(context,api);}await next(context);});
app.MapRazorPages();
app.Run();
