using FourChang.Api.Features.Admin.Employees;
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

using FourChang.Api.Features.Admin.Menu;
LocalEnvironment.Load();
try
{
    if (args.Contains("--init-db")) { await DatabaseBootstrap.InitializeAsync(); Console.WriteLine("DATABASE OK: đúng 37 bảng, dữ liệu khởi tạo và tài khoản ứng dụng đã sẵn sàng."); return; }
    if (args.Contains("--check-db")) { await using var connection = new SqlConnection(DatabaseSettings.ConnectionString()); await connection.OpenAsync(); await DatabaseBootstrap.ValidateAsync(connection); Console.WriteLine("CHECK OK: đúng 37 bảng và 73 FK, kết nối bằng fourchang_app."); return; }
    if (args.Contains("--test-db")) { await DatabaseChecks.RunAsync(); return; }
    // Công cụ nạp dữ liệu mẫu chủ động cho đồ án, không cung cấp endpoint HTTP và không thay đổi schema.
    if (args.Contains("--seed-people"))
    {
        await using var connection = new SqlConnection(DatabaseSettings.ConnectionString());
        await connection.OpenAsync(); await DatabaseBootstrap.ValidateAsync(connection);
        await using var db = new CafeDbContext(new DbContextOptionsBuilder<CafeDbContext>().UseSqlServer(DatabaseSettings.ConnectionString()).Options);
        var result = await PeopleSampleSeed.RunAsync(db);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        return;
    }

    // Lệnh bảo trì chỉ dùng khi chủ dự án yêu cầu thay bộ mẫu; luôn sao lưu trước khi xóa.
    if (args.Contains("--reset-people-samples"))
    {
        if (args.Length != 3 || args[1] != "--confirm-database" || args[2] != DatabaseSettings.DatabaseName)
            throw new InvalidOperationException("Cần xác định đúng database bằng --confirm-database <tên-database>.");
        await using var connection = new SqlConnection(DatabaseSettings.ConnectionString(true));
        await connection.OpenAsync(); await DatabaseBootstrap.ValidateAsync(connection);
        if (connection.Database != args[2]) throw new InvalidOperationException("Database kết nối không khớp yêu cầu.");
        var backup = $"/var/opt/mssql/data/{connection.Database}_before_people_reset_{Guid.NewGuid():N}.bak";
        await using (var command = connection.CreateCommand())
        {
            command.CommandTimeout = 120;
            command.CommandText = "BACKUP DATABASE [" + connection.Database.Replace("]", "]]") + "] TO DISK=@file WITH COPY_ONLY,CHECKSUM;";
            command.Parameters.AddWithValue("@file", backup); await command.ExecuteNonQueryAsync();
            // Kiểm tra bản sao đọc được trước khi thay dữ liệu; lỗi backup/verify phải dừng trước mọi lệnh DELETE.
            command.CommandText = "RESTORE VERIFYONLY FROM DISK=@file WITH CHECKSUM;";
            await command.ExecuteNonQueryAsync();
        }
        await using var db = new CafeDbContext(new DbContextOptionsBuilder<CafeDbContext>().UseSqlServer(DatabaseSettings.ConnectionString(true)).Options);
        var result = await PeopleSampleReset.RunAsync(db);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { Database = connection.Database, Backup = backup, Result = result }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        return;
    }
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddDbContext<CafeDbContext>(o => o.UseSqlServer(DatabaseSettings.ConnectionString(), s => s.CommandTimeout(30)));
    builder.Services.AddScoped<FourChang.Api.Features.Admin.Employees.EmployeeService>();
    builder.Services.AddScoped<FourChang.Api.Features.Admin.WorkSessions.WorkSessionService>();
    builder.Services.AddScoped<MenuAdminService>();
    builder.Services.AddScoped<CatalogReadService>();
    builder.Services.AddHostedService<MenuOutboxWorker>();
    builder.Services.AddScoped<CustomerAddressService>();
    builder.Services.AddScoped<CustomerAccountService>();
    builder.Services.AddScoped<CustomerAccessService>();
    builder.Services.AddScoped<CustomerSelfService>();
    builder.Services.AddScoped<AdminAccountService>();
    builder.Services.AddScoped<PosDeviceService>();
    builder.Services.AddAuthentication("Session").AddScheme<AuthenticationSchemeOptions, SessionAuthentication>("Session", _ => { });
    builder.Services.AddAuthorization(options => { options.AddPolicy("Admin", p => p.RequireRole("ADMIN")); options.AddPolicy("AdminPassword", p => p.RequireRole("ADMIN", "ADMIN_PASSWORD_CHANGE")); options.AddPolicy("Staff", p => p.RequireRole("STAFF")); options.AddPolicy("Customer", p => p.RequireRole("CUSTOMER")); options.AddPolicy("CustomerPasswordChange", p => p.RequireRole("CUSTOMER_PASSWORD_CHANGE")); });
    builder.Services.AddRateLimiter(o => o.AddFixedWindowLimiter("auth", x => { x.PermitLimit = 20; x.Window = TimeSpan.FromMinutes(1); x.QueueLimit = 0; }));
    var app = builder.Build();
    app.Use(async (context, next) =>
    {
        try { await next(context); }
        catch (DbUpdateConcurrencyException) { await Problem(context, 409, "Dữ liệu đã thay đổi. Vui lòng tải lại."); }
        catch (DbUpdateException) { await Problem(context, 409, "Dữ liệu trùng, tham chiếu không hợp lệ hoặc không đáp ứng quy tắc nghiệp vụ."); }
        catch (SqlException) { await Problem(context, 503, "Không truy cập được database hoặc ràng buộc dữ liệu chưa thỏa mãn."); }
        catch (Exception exception) { app.Logger.LogError(exception, "Lỗi xử lý yêu cầu {Path}", context.Request.Path); await Problem(context, 500, "Không thể xử lý yêu cầu. Vui lòng thử lại."); }
    });
    app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
    app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
    app.MapGet("/health/ready", async (CafeDbContext db) =>
    {
        await using var connection = new SqlConnection(DatabaseSettings.ConnectionString()); await connection.OpenAsync(); await DatabaseBootstrap.ValidateAsync(connection); return Results.Ok(new { status = "ready", tables = db.Model.GetEntityTypes().Count() });
    });
    app.MapAdminMenu(); app.MapMenuEvents();
    app.MapAdminAccount(); app.MapPosDevices(); app.MapAuth(); app.MapCustomerOrders(); app.MapCatalog(); app.MapWork(); app.MapEmployees(); FourChang.Api.Features.Admin.WorkSessions.WorkSessionEndpoints.MapAdminWorkSessions(app); FourChang.Api.Features.Staff.WorkSessions.ReconciliationEndpoints.MapReconciliation(app); app.MapData(); app.MapStaffData(); app.MapAdminCustomers(); app.MapAdminCustomerAddresses(); app.MapCustomerTemporaryPassword(); app.MapCustomerSelf();
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
