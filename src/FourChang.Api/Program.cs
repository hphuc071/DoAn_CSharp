using FourChang.Api.Features;
using FourChang.Api.Security;
using FourChang.Infrastructure.Configuration;
using FourChang.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

LocalEnvironment.Load();
try
{
    if(args.Contains("--init-db")){await DatabaseBootstrap.InitializeAsync();Console.WriteLine("DATABASE OK: đúng 34 bảng, dữ liệu khởi tạo và tài khoản ứng dụng đã sẵn sàng.");return;}
    if(args.Contains("--check-db")){await using var connection=new SqlConnection(DatabaseSettings.ConnectionString());await connection.OpenAsync();await DatabaseBootstrap.ValidateAsync(connection);Console.WriteLine("CHECK OK: đúng 34 bảng và 72 FK, kết nối bằng fourchang_app.");return;}
    if(args.Contains("--test-db")){await DatabaseChecks.RunAsync();return;}
    var builder=WebApplication.CreateBuilder(args);
    builder.Services.AddDbContext<CafeDbContext>(o=>o.UseSqlServer(DatabaseSettings.ConnectionString(),s=>s.CommandTimeout(30)));
    builder.Services.AddAuthentication("Session").AddScheme<AuthenticationSchemeOptions,SessionAuthentication>("Session",_=>{});
    builder.Services.AddAuthorization(options=>{options.AddPolicy("Admin",p=>p.RequireRole("ADMIN"));options.AddPolicy("Staff",p=>p.RequireRole("STAFF"));options.AddPolicy("Customer",p=>p.RequireRole("CUSTOMER"));});
    builder.Services.AddRateLimiter(o=>o.AddFixedWindowLimiter("auth",x=>{x.PermitLimit=20;x.Window=TimeSpan.FromMinutes(1);x.QueueLimit=0;}));
    var app=builder.Build();
    app.Use(async(context,next)=>
    {
        try{await next(context);}
        catch(DbUpdateConcurrencyException){await Problem(context,409,"Dữ liệu đã thay đổi. Vui lòng tải lại.");}
        catch(DbUpdateException){await Problem(context,409,"Dữ liệu trùng, tham chiếu không hợp lệ hoặc không đáp ứng quy tắc nghiệp vụ.");}
        catch(SqlException){await Problem(context,503,"Không truy cập được database hoặc ràng buộc dữ liệu chưa thỏa mãn.");}
        catch(Exception exception){app.Logger.LogError(exception,"Lỗi xử lý yêu cầu {Path}",context.Request.Path);await Problem(context,500,"Không thể xử lý yêu cầu. Vui lòng thử lại.");}
    });
    app.UseRateLimiter();app.UseAuthentication();app.UseAuthorization();
    app.MapGet("/health/live",()=>Results.Ok(new{status="ok"}));
    app.MapGet("/health/ready",async(CafeDbContext db)=>
    {
        await using var connection=new SqlConnection(DatabaseSettings.ConnectionString());await connection.OpenAsync();await DatabaseBootstrap.ValidateAsync(connection);return Results.Ok(new{status="ready",tables=db.Model.GetEntityTypes().Count()});
    });
    app.MapAuth();app.MapCatalog();app.MapWork();app.MapData();
    app.Run();
}
catch(Exception exception)
{
    Console.Error.WriteLine("Không khởi động được backend/database. Kiểm tra Docker, .env và hướng dẫn database. " + exception.GetType().Name);
    if(args.Any(a=>a is "--init-db" or "--check-db" or "--test-db"))Console.Error.WriteLine(exception.Message);
    Environment.ExitCode=1;
}
static async Task Problem(HttpContext context,int status,string message)
{
    if(context.Response.HasStarted)return;
    context.Response.StatusCode=status;await context.Response.WriteAsJsonAsync(new{detail=message,status});
}
