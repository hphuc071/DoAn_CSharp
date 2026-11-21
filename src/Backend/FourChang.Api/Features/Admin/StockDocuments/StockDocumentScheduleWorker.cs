using FourChang.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.StockDocuments;

/// <summary>Chỉ thực hiện phiếu đã xác nhận và đến ngày UTC+07; FAILED chờ Admin xử lý, không tự thử lại.</summary>
public sealed class StockDocumentScheduleWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<StockDocumentScheduleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
                using var reader = scopes.CreateScope();
                var db = reader.ServiceProvider.GetRequiredService<CafeDbContext>();
                // Phiếu nhập đến hạn cùng ngày thực hiện trước phiếu xuất, thứ tự ổn định theo ID.
                var ids = await db.PhieuKho.AsNoTracking().Where(x => x.TrangThai == "SCHEDULED" && x.NgayThucHien <= today)
                    .OrderBy(x => x.NgayThucHien).ThenBy(x => x.LoaiPhieu == "RECEIPT" ? 0 : 1).ThenBy(x => x.MaPhieuKho)
                    .Select(x => x.MaPhieuKho).Take(50).ToListAsync(stoppingToken);
                foreach (var id in ids)
                {
                    try
                    {
                        using var scope = scopes.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<StockDocumentService>().ApplyScheduled(id, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception ex) { logger.LogWarning("Chưa ghi sổ được phiếu kho {Id}; sẽ kiểm tra lại. Loại lỗi {Type}", id, ex.GetType().Name); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Chưa đọc được lịch phiếu kho. Loại lỗi {Type}", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
