using System.Data;
using FourChang.Api.Shared.Audit;
using FourChang.Api.Shared.Operations;
using FourChang.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.Recipes;

/// <summary>Kích hoạt/đóng công thức theo lịch đã duyệt; không sửa định mức hoặc các đơn đã chốt.</summary>
public sealed class RecipeScheduleWorker(IServiceScopeFactory scopes, ILogger<RecipeScheduleWorker> logger) : BackgroundService
{
    /// <summary>Kiểm tra lịch định kỳ, lỗi kết nối được thử lại thay vì đánh dấu đã chạy thành công.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await ApplyDue(scope.ServiceProvider.GetRequiredService<CafeDbContext>(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Chưa cập nhật được lịch BOM; sẽ thử lại. Loại lỗi {Type}", ex.GetType().Name); }
            try { await Task.Delay(2000, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Đổi trạng thái, ghi audit và outbox nguyên tử; unique ACTIVE được nhả trước kích hoạt bản mới.</summary>
    public static async Task ApplyDue(CafeDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (!await db.CongThuc.AnyAsync(x => (x.TrangThai == "SCHEDULED" && x.HieuLucTu <= now) || (x.TrangThai == "ACTIVE" && x.HieuLucDen <= now), ct)) return;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AdminOperation.Lock(db);
        var expired = await db.CongThuc.Where(x => x.TrangThai == "ACTIVE" && x.HieuLucDen <= now).ToListAsync(ct);
        foreach (var recipe in expired) recipe.TrangThai = "RETIRED";
        await db.SaveChangesAsync(ct);
        var due = await db.CongThuc.Where(x => x.TrangThai == "SCHEDULED" && x.HieuLucTu <= now).ToListAsync(ct);
        foreach (var recipe in due)
        {
            if (await db.CongThuc.AnyAsync(x => x.MaCongThuc != recipe.MaCongThuc && x.TrangThai == "ACTIVE" && x.MaCauHinhMon == recipe.MaCauHinhMon && x.MaMonThem == recipe.MaMonThem, ct))
                throw new AdminRule(409, "Lịch BOM xung đột; không tự chọn phiên bản thắng.");
            recipe.TrangThai = recipe.HieuLucDen <= now ? "RETIRED" : "ACTIVE";
        }
        foreach (var recipe in expired.Concat(due))
        {
            var operation = Guid.NewGuid();
            var audit = AuditFactory.Create("SYSTEM", 0, "RECIPE_SCHEDULE_APPLIED", "CongThuc", recipe.MaCongThuc);
            audit.MaThaoTac = operation; audit.DuLieuSauJson = System.Text.Json.JsonSerializer.Serialize(new { recipe.MaCongThuc, recipe.TrangThai, recipe.HieuLucTu, recipe.HieuLucDen });
            db.NhatKyHeThong.Add(audit);
            db.SuKienDongBo.Add(new() { MaSuKien = Guid.NewGuid(), MaThaoTac = operation, LoaiSuKien = "RECIPE_CHANGED", NoiDungJson = audit.DuLieuSauJson, PhatSinhLuc = now });
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
