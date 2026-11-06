using FourChang.Domain.Entities.Inventory;
using FourChang.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Customer.Orders;

// Database writes remain owned by the caller. Must run inside the same
// SERIALIZABLE transaction as the order/payment state changes.
internal static class OrderInventoryRelease
{
    internal static async Task<string?> ReleaseAllUnconsumedAsync(
        CafeDbContext db,
        IReadOnlyCollection<GiuChoNguyenLieu> holds,
        string targetStatus,
        DateTime now,
        CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Inventory release requires an active transaction.");
        if (targetStatus is not ("RELEASED" or "EXPIRED"))
            throw new ArgumentOutOfRangeException(nameof(targetStatus));

        if (holds.Any(h => h.SoLuongGiuCho < 0m ||
            h.SoLuongDaGiaiPhong < 0m || h.SoLuongDaTieuHao != 0m ||
            h.SoLuongDaGiaiPhong > h.SoLuongGiuCho))
            return "Nguyên liệu đã sử dụng hoặc dữ liệu giữ kho không hợp lệ.";

        var releases = holds.Select(h => new
        {
            Hold = h,
            Remaining = h.SoLuongGiuCho - h.SoLuongDaGiaiPhong
        }).ToList();

        var ingredientIds = releases.Where(x => x.Remaining > 0m)
            .Select(x => x.Hold.MaNguyenLieu).Distinct().ToArray();
        var stocks = await db.TonKho
            .Where(s => ingredientIds.Contains(s.MaNguyenLieu))
            .ToDictionaryAsync(s => s.MaNguyenLieu, ct);

        foreach (var group in releases.GroupBy(x => x.Hold.MaNguyenLieu))
        {
            var amount = group.Sum(x => x.Remaining);
            if (amount < 0m || (amount > 0m &&
                (!stocks.TryGetValue(group.Key, out var stock) ||
                 stock.TongGiuCho < amount)))
                return "Số lượng giữ kho không khớp; cần kiểm tra lại.";
        }

        // Validate everything before changing the first tracked entity.
        foreach (var item in releases)
        {
            if (item.Remaining <= 0m) continue;
            item.Hold.SoLuongDaGiaiPhong += item.Remaining;
            item.Hold.TrangThai = targetStatus;
            item.Hold.ThoiDiemCapNhat = now;
            var stock = stocks[item.Hold.MaNguyenLieu];
            stock.TongGiuCho -= item.Remaining;
            stock.ThoiDiemCapNhat = now;
        }

        return null;
    }
}
