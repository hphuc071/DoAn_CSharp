using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Customer.Orders;

public sealed class OrderQuoteValidationException : Exception
{
    public OrderQuoteValidationException(string message) : base(message) { }
}

public static class OrderPricingService
{
    public static async Task<OrderPriceSummary> CalculateOrderTotalAsync(
        CafeDbContext db,
        string fulfillmentType,
        decimal? distanceKm,
        IReadOnlyCollection<OrderItemInput> items,
        CancellationToken ct = default)
    {
        // DINE_IN needs an authenticated table; DELIVERY needs a server-verified
        // distance and fee. Neither is ready in this preview. Fail closed.
        if (!string.Equals(fulfillmentType, "TAKEAWAY", StringComparison.OrdinalIgnoreCase))
            throw new OrderQuoteValidationException(
                "Hiện chỉ hỗ trợ báo giá mang đi; tại bàn và giao hàng chưa được xác minh.");
        if (distanceKm is not null)
            throw new OrderQuoteValidationException("Báo giá mang đi không nhận khoảng cách giao hàng.");

        if (items is null || items.Count is < 1 or > 50)
            throw new OrderQuoteValidationException("Giỏ hàng phải có từ 1 đến 50 dòng món.");

        if (items.Any(i => i is null || i.MaCauHinhMon <= 0 ||
            i.SoLuong is < 1 or > 99 || i.PhanTramDuong is < 0 or > 100 ||
            i.PhanTramDa is < 0 or > 100 ||
            (i.GhiChu?.Length ?? 0) > 500 || (i.Toppings?.Count ?? 0) > 10 ||
            (i.Toppings?.Any(t => t is null || t.MaMonThem <= 0 || t.SoPhanMoiLy < 1) ?? false)))
            throw new OrderQuoteValidationException("Mã món, số lượng hoặc cấu hình không hợp lệ.");

        var variantIds = items.Select(i => i.MaCauHinhMon).Distinct().ToList();
        var variants = await (
            from variant in db.CauHinhMon.AsNoTracking()
            join product in db.Mon.AsNoTracking() on variant.MaMon equals product.MaMon
            join category in db.DanhMucMon.AsNoTracking() on product.MaDanhMuc equals category.MaDanhMuc
            where variantIds.Contains(variant.MaCauHinhMon) && variant.DangBan &&
                  product.DangBan && product.DaCongBo && category.DangSuDung && category.DaCongBo
            select new { variant.MaCauHinhMon, variant.GiaBan }
        ).ToDictionaryAsync(x => x.MaCauHinhMon, x => x.GiaBan, ct);

        if (variants.Count != variantIds.Count)
            throw new OrderQuoteValidationException("Một số biến thể món không tồn tại hoặc đã ngừng bán.");

        var choices = await db.TuyChonPhaCheHopLe.AsNoTracking()
            .Where(x => variantIds.Contains(x.MaCauHinhMon) && x.DangSuDung)
            .Select(x => new { x.MaCauHinhMon, x.PhanTramDuong, x.PhanTramDa })
            .ToListAsync(ct);
        var validOptions = choices
            .Select(x => (x.MaCauHinhMon, x.PhanTramDuong, x.PhanTramDa))
            .ToHashSet();

        var toppingIds = items.SelectMany(i => i.Toppings ?? [])
            .Select(t => t.MaMonThem).Distinct().ToList();
        var toppingLinks = await (
            from link in db.CauHinhMonThem.AsNoTracking()
            join top in db.MonThem.AsNoTracking() on link.MaMonThem equals top.MaMonThem
            where variantIds.Contains(link.MaCauHinhMon) &&
                  toppingIds.Contains(link.MaMonThem) && link.DangSuDung && top.DangSuDung
            select new
            {
                link.MaCauHinhMon,
                link.MaMonThem,
                top.GiaMoiPhan,
                Limit = link.GioiHanMoiLy > 0 ? link.GioiHanMoiLy : top.GioiHanMacDinhMoiLy
            }
        ).ToListAsync(ct);
        var allowedToppings = toppingLinks
            .ToDictionary(x => (x.MaCauHinhMon, x.MaMonThem));

        decimal subtotal = 0m;
        foreach (var item in items)
        {
            if (!validOptions.Contains((item.MaCauHinhMon, item.PhanTramDuong, item.PhanTramDa)))
                throw new OrderQuoteValidationException(
                    $"Biến thể {item.MaCauHinhMon} không hỗ trợ tỷ lệ đường/đá đã chọn.");

            var tops = item.Toppings ?? [];
            if (tops.GroupBy(t => t.MaMonThem).Any(g => g.Count() > 1))
                throw new OrderQuoteValidationException("Không được gửi trùng một loại topping trong cùng dòng món.");

            decimal topPricePerCup = 0m;
            foreach (var top in tops)
            {
                if (!allowedToppings.TryGetValue((item.MaCauHinhMon, top.MaMonThem), out var allowed))
                    throw new OrderQuoteValidationException(
                        $"Topping {top.MaMonThem} không hợp lệ cho biến thể {item.MaCauHinhMon}.");
                if (allowed.Limit < 1 || top.SoPhanMoiLy > allowed.Limit)
                    throw new OrderQuoteValidationException(
                        $"Topping {top.MaMonThem} chỉ được tối đa {allowed.Limit} phần/ly.");
                if (allowed.GiaMoiPhan < 0m)
                    throw new OrderQuoteValidationException("Giá topping trong database không hợp lệ.");

                topPricePerCup += allowed.GiaMoiPhan * top.SoPhanMoiLy;
            }

            var basePrice = variants[item.MaCauHinhMon];
            if (basePrice < 0m)
                throw new OrderQuoteValidationException("Giá món trong database không hợp lệ.");
            try { subtotal = checked(subtotal + (basePrice + topPricePerCup) * item.SoLuong); }
            catch (OverflowException) { throw new OrderQuoteValidationException("Tổng tiền vượt giới hạn tính toán."); }
            // A SQL decimal(18,0) total cannot represent more than 18 integral digits.
            if (subtotal > 999999999999999999m)
                throw new OrderQuoteValidationException("Tổng tiền vượt giới hạn lưu trữ; vui lòng giảm giỏ hàng.");
        }

        // Only TAKEAWAY can be priced here, so shipping is unconditionally zero.
        return new OrderPriceSummary(subtotal, 0m, subtotal);
    }
}
