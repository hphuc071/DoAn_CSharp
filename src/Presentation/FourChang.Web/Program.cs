using FourChang.Web.Services;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
    // EXPIRED đã được chốt và migration hỗ trợ; chỉ bật đọc, không tự bật worker hay xác minh ngân hàng.
    builder.Configuration["Orders:ExpiryStatusReadEnabled"] ??= "true";
builder.Services.AddRazorPages();
builder.Services.AddCustomerServices();
builder.Services.AddSingleton<IPaymentQrSource, UnavailablePaymentQrSource>();
var app = builder.Build();
app.UseStaticFiles();
var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
if (Directory.Exists(assets)) app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(assets), RequestPath = "/assets" });

// Thử thu hồi phiên đã đăng xuất khi trình duyệt kết nối lại; không chạm giỏ/bản nháp.
app.Use(async (context, next) =>
{
    if (context.Request.Cookies.ContainsKey("fourchang_pending_logout"))
    {
        var pending = context.RequestServices.GetRequiredService<FourChang.Web.Security.PendingCustomerLogout>();
        var api = context.RequestServices.GetRequiredService<CafeApiClient>();
        await pending.Retry(context, api);
    }
    await next(context);
});

app.MapGet("/api/menu/{product:long}/variants", async (long product, CafeApiClient api) =>
{
    try { return Results.Ok(await api.GetMenuVariantsAsync(product)); }
    catch (CafeApiException ex) { return Results.Problem(ex.Message, statusCode: (int?)ex.StatusCode ?? 500); }
});

app.MapPost("/api/customer/orders/quote", async (CreateCustomerOrderRequest req, CafeApiClient api) =>
{
    try { return Results.Ok(await api.QuoteCustomerOrderAsync(req)); }
    catch (CafeApiException ex) { return Results.Problem(ex.Message, statusCode: (int?)ex.StatusCode ?? 500); }
});

// The legacy browser Create proxy cannot preserve the owned, CSRF-protected Checkout
// command contract. Keep its route fail-closed; writes use the Razor Checkout handler.
app.MapPost("/api/customer/orders", () =>
    Results.Json(new { code = "CHECKOUT_HANDLER_REQUIRED" }, statusCode: 503));

app.MapCatalogUpdates();
app.MapRazorPages();
app.Run();
