using FourChang.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
namespace FourChang.Api.Features.Admin.Menu;

/// <summary>Phát outbox vào change feed bền vững trong SQL; lease bảo vệ nhiều worker và phục hồi sau crash.</summary>
public sealed class MenuOutboxWorker(IServiceScopeFactory scopes, ILogger<MenuOutboxWorker> logger) : BackgroundService
{
    /// <summary>Worker chỉ cập nhật sự kiện đã commit; lỗi không sửa giá/menu hoặc suy đoán giao dịch chưa thành công.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await PublishBatch(scope.ServiceProvider.GetRequiredService<CafeDbContext>(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Outbox chưa phát được; sẽ thử lại. Loại lỗi: {Type}, mã SQL: {Code}", ex.GetType().Name, ex is Microsoft.Data.SqlClient.SqlException sql ? sql.Number : 0); }
            try
            {
                await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }
    }
    /// <summary>Nhận tối đa 10 sự kiện bằng UPDATE nguyên tử; xác nhận đúng token lease trước khi công bố cho feed.</summary>
    public static async Task PublishBatch(CafeDbContext db, CancellationToken ct)
    {
        var token = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var lease = now.AddSeconds(30);
        // Pool có thể trả lại connection từng chạy Serializable ở lệnh Admin; READPAST cần ReadCommitted rõ ràng.
        await db.Database.ExecuteSqlInterpolatedAsync($"SET TRANSACTION ISOLATION LEVEL READ COMMITTED; UPDATE TOP(10) dbo.SuKienDongBo WITH(UPDLOCK,READPAST,ROWLOCK) SET MaLanXuLy={token},KhoaDenLuc={lease},SoLanThu=SoLanThu+1 WHERE XuLyXongLuc IS NULL AND (ThuLaiLuc IS NULL OR ThuLaiLuc<={now}) AND (KhoaDenLuc IS NULL OR KhoaDenLuc<={now})", ct);
        // Feed đọc các hàng đã công bố từ SQL nên hoạt động cả khi API có nhiều instance.
        // Nếu dừng giữa hai lệnh, lease hết hạn cho phép worker khác nhận lại, không mất sự kiện.
        try
        {
            await db.SuKienDongBo.Where(x => x.MaLanXuLy == token && x.XuLyXongLuc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.XuLyXongLuc, now).SetProperty(x => x.KhoaDenLuc, (DateTime?)null).SetProperty(x => x.MaLanXuLy, (Guid?)null).SetProperty(x => x.LoiGanNhat, (string?)null), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            try
            {
                var retry = DateTime.UtcNow.AddSeconds(5);
                var reason = ex.GetType().Name;
                await db.SuKienDongBo.Where(x => x.MaLanXuLy == token && x.XuLyXongLuc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.ThuLaiLuc, retry).SetProperty(x => x.LoiGanNhat, reason).SetProperty(x => x.KhoaDenLuc, (DateTime?)null).SetProperty(x => x.MaLanXuLy, (Guid?)null), ct);
            }
            catch (Exception retryError) when (retryError is not OperationCanceledException) { /* DB chưa hồi phục: lease hết hạn cho worker khác nhận lại. */ }
            throw;
        }
    }
}

/// <summary>SSE từ feed SQL: sự kiện chỉ báo tải lại, không chứa giá hay dữ liệu nội bộ.</summary>
public static class MenuEventEndpoints
{
    /// <summary>Kết nối mới luôn tải lại snapshot; theo dõi cả mốc lịch công bố ngay cả khi không có lệnh ghi.</summary>
    public static void MapMenuEvents(this WebApplication app)
    {
        app.MapGet("/api/catalog/events", async (HttpContext context, CafeDbContext db) =>
        {
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers["X-Accel-Buffering"] = "no";
            string? previous = null;
            var heartbeat = 0;
            var ct = context.RequestAborted;
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var now = DateTime.UtcNow;
                    var events = await db.SuKienDongBo.LongCountAsync(x => x.XuLyXongLuc != null, ct);
                    var dishes = await db.Mon.CountAsync(x => x.DaCongBo && (x.CongBoTuLuc == null || x.CongBoTuLuc <= now), ct);
                    var banners = await db.Banner.Where(x => x.DaCongBo && x.BatDauLuc <= now && (x.KetThucLuc == null || x.KetThucLuc > now)).Select(x => x.MaBanner).ToListAsync(ct);
                    var revision = events + ":" + dishes + ":" + string.Join(',', banners.Order());
                    if (revision != previous)
                    {
                        await context.Response.WriteAsync("data: changed\n\n", ct);
                        previous = revision;
                        heartbeat = 0;
                    }
                    else if (++heartbeat >= 15)
                    {
                        await context.Response.WriteAsync(": keepalive\n\n", ct);
                        heartbeat = 0;
                    }
                    await context.Response.Body.FlushAsync(ct);
                    await Task.Delay(1000, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        });
    }
}
