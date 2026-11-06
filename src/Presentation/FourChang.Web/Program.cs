using FourChang.Web.Services;
using FourChang.Presentation.Shared.ApiClients;
using Microsoft.Extensions.FileProviders;
// DLL có thể được chạy từ thư mục gốc bằng launcher. Dùng thư mục tài nguyên cạnh DLL
// để CSS/JS/logo không phụ thuộc working directory hoặc môi trường Development/Production.
var outputWebRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = Directory.Exists(outputWebRoot) ? outputWebRoot : null
});
builder.Services.AddRazorPages();
builder.Services.AddCustomerServices();
var app = builder.Build();
app.UseStaticFiles();
var assets = Path.Combine(AppContext.BaseDirectory,"Assets");
if(Directory.Exists(assets))app.UseStaticFiles(new StaticFileOptions{FileProvider=new PhysicalFileProvider(assets),RequestPath="/assets"});
// Thử thu hồi phiên đã đăng xuất khi trình duyệt kết nối lại; không chạm giỏ/bàn nháp.
app.Use(async(context,next)=>{if(context.Request.Cookies.ContainsKey("fourchang_pending_logout")){var pending=context.RequestServices.GetRequiredService<FourChang.Web.Security.PendingCustomerLogout>();var api=context.RequestServices.GetRequiredService<CafeApiClient>();await pending.Retry(context,api);}await next(context);});
app.MapCatalogProxy();
app.MapRazorPages();
app.Run();
