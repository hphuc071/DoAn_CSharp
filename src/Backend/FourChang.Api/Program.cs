using FourChang.Api.Features.Admin.Reports;
using FourChang.Api.Features.Admin.Invoices;
using FourChang.Api.Features.Admin.Refunds;
using FourChang.Api.Shared.Orders;
using FourChang.Api.Features.Admin.Orders;
using FourChang.Api.Features.Admin.StockDocuments;
using FourChang.Api.Features.Admin.Employees;
using FourChang.Api.Features.Admin.Menu;
using FourChang.Api.Features.Admin.Ingredients;
using FourChang.Api.Features.Admin.Recipes;
using FourChang.Api.Features.Admin.Inventory;
using FourChang.Api.Features.Staff.Data;
using FourChang.Api.Features.Customer.Orders;

using FourChang.Api.Features.Admin.Data;
using FourChang.Api.Features.Customer.Menu;
using FourChang.Api.Features.Staff.WorkSessions;
using FourChang.Api.Shared.Authentication;
using FourChang.Api.Features.Admin.Access;
using FourChang.Api.Features.Admin.Customers;
using FourChang.Api.Features.Customer.Account;
using FourChang.Api.Security;
using FourChang.Infrastructure.Configuration;
using FourChang.Infrastructure.Database.Context;
using FourChang.Infrastructure.Database.Initialization;
using FourChang.Infrastructure.Database.Validation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

LocalEnvironment.Load();
try
{
    if (args.Contains("--init-db")) { await DatabaseBootstrap.InitializeAsync(); Console.WriteLine("DATABASE OK: đúng 37 bảng, dữ liệu khởi tạo và tài khoản ứng dụng đã sẵn sàng."); return; }
    if (args.Contains("--seed-customers"))
    {
        await using var sampleDb = new CafeDbContext(new DbContextOptionsBuilder<CafeDbContext>().UseSqlServer(DatabaseSettings.ConnectionString()).Options);
        var added = await CustomerSampleSeed.RunAsync(sampleDb);
        Console.WriteLine($"Đã thêm {added} khách hàng mẫu; dữ liệu hiện hữu được giữ nguyên.");
        return;
    }
    if (args.Contains("--check-db")) { await using var connection = new SqlConnection(DatabaseSettings.ConnectionString()); await connection.OpenAsync(); await DatabaseBootstrap.ValidateAsync(connection); Console.WriteLine("CHECK OK: đúng 37 bảng và 74 FK, kết nối bằng fourchang_app."); return; }
    if (args.Contains("--test-db")) { await DatabaseChecks.RunAsync(); return; }
    var builder = WebApplication.CreateBuilder(args);
    // EXPIRED đã được chốt và migration hỗ trợ; chỉ bật đọc, không tự bật worker hay xác minh ngân hàng.
    builder.Configuration["Orders:ExpiryStatusReadEnabled"] ??= "true";
    builder.Services.AddDbContext<CafeDbContext>(o => o.UseSqlServer(DatabaseSettings.ConnectionString(), s => s.CommandTimeout(30)));
    // ADM-01..05: các service dùng cùng DbContext trong một request và cùng nguồn giá/tồn.
    builder.Services.AddScoped<FourChang.Api.Features.Admin.Employees.EmployeeService>();
    builder.Services.AddScoped<FourChang.Api.Features.Admin.WorkSessions.WorkSessionService>();
    builder.Services.AddScoped<FourChang.Api.Features.Admin.WorkSessions.ShiftRecoveryService>();
    builder.Services.AddScoped<FourChang.Api.Features.Staff.WorkSessions.ShiftStaffService>();
    builder.Services.AddScoped<FourChang.Api.Features.Staff.Sales.PosCheckoutService>();
    builder.Services.AddScoped<FourChang.Api.Features.Payments.Simulation.PaymentSimulationService>();
    builder.Services.AddScoped<CustomerAccountService>();
    builder.Services.AddScoped<CustomerAddressService>();
    builder.Services.AddScoped<CatalogReadService>();
    builder.Services.AddScoped<MenuAdminService>();
    builder.Services.AddScoped<IngredientService>();
    builder.Services.AddScoped<RecipeService>();
    builder.Services.AddScoped<InventoryService>();
    builder.Services.AddHostedService<MenuOutboxWorker>();
    builder.Services.AddHostedService<RecipeScheduleWorker>();
    builder.Services.AddScoped<CustomerAccessService>();
    builder.Services.AddScoped<CustomerSelfService>();
    builder.Services.AddScoped<ICustomerOrderReader, CustomerOrderReader>();
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddDataProtection();
    builder.Services.AddScoped<ITableQrLookup, SqlTableQrLookup>();
    builder.Services.AddSingleton<IDeliveryRouteSource, UnavailableDeliveryRouteSource>();
    builder.Services.AddScoped<IFulfillmentCartPricer, FulfillmentCartPricer>();
    builder.Services.AddScoped<FulfillmentPreviewService>();
    builder.Services.AddScoped<FourChang.Api.Features.Customer.Payments.SqlBankPaymentService>();
    builder.Services.AddSingleton<IExpiryBankReconciliation, UnavailableExpiryBankReconciliation>();
    builder.Services.AddScoped<SqlOrderExpiryWorker>(); // Explicit invocation only; never AddHostedService.
    builder.Services.AddSingleton<IInvoiceDocumentSource, UnavailableInvoiceDocumentSource>();
    builder.Services.AddSingleton<FourChang.Api.Features.Customer.Payments.IPaymentVerificationSource,
        FourChang.Api.Features.Customer.Payments.UnavailablePaymentVerificationSource>();
    builder.Services.AddScoped<AdminAccountService>();
    builder.Services.AddScoped<PosDeviceService>();
    builder.Services.AddAuthentication("Session").AddScheme<AuthenticationSchemeOptions, SessionAuthentication>("Session", _ => { });
    builder.Services.AddAuthorization(options => { options.AddPolicy("Admin", p => p.RequireRole("ADMIN")); options.AddPolicy("AdminPassword", p => p.RequireRole("ADMIN", "ADMIN_PASSWORD_CHANGE")); options.AddPolicy("Staff", p => p.RequireRole("STAFF")); options.AddPolicy("Customer", p => p.RequireRole("CUSTOMER")); options.AddPolicy("CustomerPasswordChange", p => p.RequireRole("CUSTOMER_PASSWORD_CHANGE")); });
    builder.Services.AddRateLimiter(o => o.AddFixedWindowLimiter("auth", x => { x.PermitLimit = 20; x.Window = TimeSpan.FromMinutes(1); x.QueueLimit = 0; }));
    builder.Services.AddScoped<StockDocumentService>();
    builder.Services.AddScoped<OrderFinancials>();
    builder.Services.AddScoped<OrderWorkflowService>();
    builder.Services.AddScoped<OrderAdministrationService>();
    builder.Services.AddScoped<RefundAdministrationService>();
    builder.Services.AddScoped<InvoiceAdministrationService>();
    builder.Services.AddScoped<ReportSnapshotStore>();
    builder.Services.AddScoped<ReportPermissions>();
    builder.Services.AddScoped<ReportAdministrationService>();
    builder.Services.AddScoped<ReportExportService>();
    builder.Services.AddHostedService<ReportExportWorker>();
    builder.Services.AddScoped<InvoicePdfService>();
    builder.Services.AddScoped<IncomingReconciliationService>();
    builder.Services.AddScoped<FourChang.Api.Shared.Orders.CancellationWorkflow>();
var app = builder.Build();
    app.Use(async (context, next) =>
    {
        try { await next(context); }
        catch (FourChang.Api.Shared.Operations.AdminRule error) { await Problem(context, error.Status, error.Message); }
        catch (DbUpdateConcurrencyException) { await Problem(context, 409, "Dữ liệu đã thay đổi. Vui lòng tải lại."); }
        catch (DbUpdateException) { await Problem(context, 409, "Dữ liệu trùng, tham chiếu không hợp lệ hoặc không đáp ứng quy tắc nghiệp vụ."); }
        catch (SqlException) { await Problem(context, 503, "Không truy cập được database hoặc ràng buộc dữ liệu chưa thỏa mãn."); }
        catch (Exception exception) { app.Logger.LogError(exception, "Lỗi xử lý yêu cầu {Path}", context.Request.Path); await Problem(context, 500, "Không thể xử lý yêu cầu. Vui lòng thử lại."); }
    });
    app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
    // Nhóm endpoint Admin tự kiểm tra quyền; không thay các API Web/POS đã được nhóm phát triển.
    app.MapOrderAdministration();
FourChang.Api.Features.Staff.Orders.StaffOrderEndpoints.MapStaffOrders(app); app.MapRefundAdministration(); app.MapInvoiceAdministration(); app.MapReportAdministration(); app.MapStockDocuments(); app.MapAdminMenu(); app.MapAdminCustomerAddresses(); app.MapIngredients(); app.MapRecipes(); app.MapInventory();
    app.MapMenuEvents();
    app.MapGet("/api/catalog", async (CatalogReadService catalog) => Results.Ok(await catalog.Read()));
    app.MapCustomerOrderDetails();
    app.MapCustomerInvoiceDocuments();
    app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
    app.MapGet("/health/ready", async (CafeDbContext db) =>
    {
        await using var connection = new SqlConnection(DatabaseSettings.ConnectionString()); await connection.OpenAsync(); await DatabaseBootstrap.ValidateAsync(connection); return Results.Ok(new { status = "ready", tables = db.Model.GetEntityTypes().Count() });
    });
    FourChang.Api.Features.Staff.Sales.PosCheckoutEndpoints.MapPosCheckout(app);
    FourChang.Api.Features.Payments.Simulation.PaymentSimulationEndpoints.MapPaymentSimulation(app);
    app.MapAdminAccount(); app.MapPosDevices(); app.MapAuth(); app.MapCustomerOrders(); app.MapCatalog(); app.MapWork(); app.MapEmployees();
    FourChang.Api.Features.Admin.WorkSessions.WorkSessionEndpoints.MapAdminWorkSessions(app);
    FourChang.Api.Features.Staff.WorkSessions.ReconciliationEndpoints.MapReconciliation(app); app.MapData(); app.MapStaffData(); app.MapAdminCustomers(); app.MapCustomerTemporaryPassword(); app.MapCustomerSelf();
    app.Run();
}
catch (Exception exception)
{
    Console.Error.WriteLine("Không khởi động được backend/database. Kiểm tra Docker, .env và hướng dẫn database. " + exception.GetType().Name);
    if (args.Any(a => a is "--init-db" or "--check-db" or "--test-db")) Console.Error.WriteLine(exception.Message);
    Environment.ExitCode = 1;
}
static async Task Problem(HttpContext context, int status, string message)
{
    if (context.Response.HasStarted) return;
    context.Response.StatusCode = status; await context.Response.WriteAsJsonAsync(new { detail = message, status });
}
