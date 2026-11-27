// UC: UC-WEB-06
// Chức năng: Tính tiền món, topping và phí nhận món từ cấu hình khách chọn.
// Cần nhớ: Không lấy tổng tiền do giao diện gửi lên làm giá chính thức.

using FourChang.Domain.Shared;
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
        CancellationToken ct = default, bool simulateFulfillment = false)
    {
        // Phí mô phỏng chỉ được tính khi caller đã kiểm tra cờ Development; TAKEAWAY giữ nguyên.
        if (fulfillmentType != "TAKEAWAY" && !(simulateFulfillment && fulfillmentType is "DINE_IN" or "DELIVERY"))
            throw new OrderQuoteValidationException(
                "Hiện chỉ hỗ trợ báo giá mang đi; tại bàn và giao hàng chưa được xác minh.");

        if (items is null || items.Count is < 1 or > 50)
            throw new OrderQuoteValidationException("Giỏ hàng phải có từ 1 đến 50 dòng món.");

        if (items.Any(i => i is null || i.MaCauHinhMon <= 0 ||
            i.SoLuong is < 1 or > 99 || i.PhanTramDuong is < 0 or > 100 ||
            i.PhanTramDa is < 0 or > 100 ||
            (i.Toppings?.Count ?? 0) > 10 ||
            (i.Toppings?.Any(t => t is null || t.MaMonThem <= 0 || t.SoPhanMoiLy < 1) ?? false)))
            throw new OrderQuoteValidationException("Mã món, số lượng hoặc cấu hình không hợp lệ.");

        var variantIds = items.Select(i => i.MaCauHinhMon).Distinct().ToList();
        var variants = await (
            from variant in db.CauHinhMon.AsNoTracking()
            join product in db.Mon.AsNoTracking() on variant.MaMon equals product.MaMon
            where variantIds.Contains(variant.MaCauHinhMon) && variant.DangBan &&
                  product.DangBan && product.DaCongBo && (product.CongBoTuLuc == null || product.CongBoTuLuc <= DateTime.UtcNow)
            select new { variant.MaCauHinhMon, variant.GiaBan, variant.NhietDo }
        ).ToDictionaryAsync(x => x.MaCauHinhMon, ct);

        if (variants.Count != variantIds.Count)
            throw new OrderQuoteValidationException("Một số biến thể món không tồn tại hoặc đã ngừng bán.");

        var toppingIds = items.SelectMany(i => i.Toppings ?? [])
            .Select(t => t.MaMonThem).Distinct().ToList();
        // Topping dùng chung cho mọi món; giá và giới hạn luôn đọc từ máy chủ.
        var allowedToppings = await db.MonThem.AsNoTracking()
            .Where(x => toppingIds.Contains(x.MaMonThem) && x.DangSuDung)
            .Select(x => new { x.MaMonThem, x.GiaMoiPhan, Limit = x.GioiHanMacDinhMoiLy })
            .ToDictionaryAsync(x => x.MaMonThem, ct);

        decimal subtotal = 0m;
        foreach (var item in items)
        {
            if (!CafeOptions.ValidBrew(variants[item.MaCauHinhMon].NhietDo, item.PhanTramDuong, item.PhanTramDa))
                throw new OrderQuoteValidationException(
                    $"Biến thể {item.MaCauHinhMon} không hỗ trợ tỷ lệ đường/đá đã chọn.");

            var tops = item.Toppings ?? [];
            if (tops.GroupBy(t => t.MaMonThem).Any(g => g.Count() > 1))
                throw new OrderQuoteValidationException("Không được gửi trùng một loại topping trong cùng dòng món.");

            decimal topPricePerCup = 0m;
            foreach (var top in tops)
            {
                if (!allowedToppings.TryGetValue(top.MaMonThem, out var allowed))
                    throw new OrderQuoteValidationException(
                        $"Topping {top.MaMonThem} không hợp lệ cho biến thể {item.MaCauHinhMon}.");
                if (allowed.Limit < 1 || top.SoPhanMoiLy > allowed.Limit)
                    throw new OrderQuoteValidationException(
                        $"Topping {top.MaMonThem} chỉ được tối đa {allowed.Limit} phần/ly.");
                if (allowed.GiaMoiPhan < 0m)
                    throw new OrderQuoteValidationException("Giá topping trong database không hợp lệ.");

                topPricePerCup += allowed.GiaMoiPhan * top.SoPhanMoiLy;
            }

            var basePrice = variants[item.MaCauHinhMon].GiaBan;
            if (basePrice < 0m)
                throw new OrderQuoteValidationException("Giá món trong database không hợp lệ.");
            subtotal += (basePrice + topPricePerCup) * item.SoLuong;
        }

        // Only TAKEAWAY can be priced here, so shipping is unconditionally zero.
        var shipping = fulfillmentType == "DELIVERY" ? SimulatedFulfillment.DeliveryFee : 0m;
        return new OrderPriceSummary(subtotal, shipping, subtotal + shipping);
    }
}
