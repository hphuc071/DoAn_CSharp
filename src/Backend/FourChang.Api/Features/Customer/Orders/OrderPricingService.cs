using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Customer.Orders;

public static class OrderPricingService
{
    public static decimal CalculateShippingFee(string fulfillmentType, decimal? distanceKm)
    {
        if (!string.Equals(fulfillmentType, "DELIVERY", StringComparison.OrdinalIgnoreCase))
            return 0m;

        var km = distanceKm ?? 2.4m;
        if (km <= 0m) return 0m;
        if (km <= 3m) return 15000m;
        return 15000m + decimal.Ceiling(km - 3m) * 5000m;
    }

    public static async Task<OrderPriceSummary> CalculateOrderTotalAsync(
        CafeDbContext db,
        string fulfillmentType,
        decimal? distanceKm,
        IReadOnlyCollection<OrderItemInput> items,
        CancellationToken ct = default)
    {
        var variantIds = items.Select(i => i.MaCauHinhMon).Distinct().ToList();
        var variants = await db.CauHinhMon.AsNoTracking()
            .Where(v => variantIds.Contains(v.MaCauHinhMon))
            .ToDictionaryAsync(v => v.MaCauHinhMon, v => v.GiaBan, ct);

        var toppingIds = items.SelectMany(i => i.Toppings ?? []).Select(t => t.MaMonThem).Distinct().ToList();
        var toppings = await db.MonThem.AsNoTracking()
            .Where(t => toppingIds.Contains(t.MaMonThem))
            .ToDictionaryAsync(t => t.MaMonThem, t => t.GiaMoiPhan, ct);

        decimal subtotal = 0m;
        foreach (var item in items)
        {
            if (!variants.TryGetValue(item.MaCauHinhMon, out var basePrice))
                continue;

            var qty = Math.Max(1, item.SoLuong);
            decimal toppingPerCup = 0m;
            foreach (var top in item.Toppings ?? [])
            {
                if (toppings.TryGetValue(top.MaMonThem, out var topPrice))
                    toppingPerCup += topPrice * Math.Clamp(top.SoPhanMoiLy, 1, 2);
            }

            subtotal += (basePrice + toppingPerCup) * qty;
        }

        var shipping = CalculateShippingFee(fulfillmentType, distanceKm);
        return new OrderPriceSummary(subtotal, shipping, subtotal + shipping);
    }
}
