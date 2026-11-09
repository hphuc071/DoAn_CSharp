using System;
using System.Collections.Generic;
using System.Linq;

namespace FourChang.Api.Features.Customer.Orders;

// BR-23/BR-24: calculate with decimal before rounding, then round the
// TOTAL demand of each ingredient up to 4 decimal places exactly once.
// Allocate the resulting 0.0001-unit quantities to order lines so that
// sum(GiuChoNguyenLieu.SoLuongGiuCho) == TonKho.TongGiuCho increase.
internal static class OrderBomAllocation
{
    private const decimal Scale = 10000m;

    internal sealed record Result(
        IReadOnlyList<Dictionary<long, decimal>> ReservedByLine,
        Dictionary<long, decimal> TotalByIngredient);

    internal static decimal RoundUp4(decimal amount)
    {
        if (amount < 0m)
            throw new ArgumentOutOfRangeException(nameof(amount));
        return decimal.Ceiling(amount * Scale) / Scale;
    }

    internal static Result Calculate(
        IReadOnlyList<Dictionary<long, decimal>> rawPerCupByLine,
        IReadOnlyList<int> quantities)
    {
        if (rawPerCupByLine.Count != quantities.Count)
            throw new InvalidOperationException("Số dòng BOM và số lượng món không khớp.");

        var reservedByLine = Enumerable.Range(0, rawPerCupByLine.Count)
            .Select(_ => new Dictionary<long, decimal>()).ToList();
        var totalByIngredient = new Dictionary<long, decimal>();

        for (var i = 0; i < quantities.Count; i++)
        {
            if (quantities[i] <= 0 || rawPerCupByLine[i].Values.Any(x => x < 0m))
                throw new InvalidOperationException("Định mức hoặc số lượng món không hợp lệ.");
        }

        foreach (var ingredientId in rawPerCupByLine
                     .SelectMany(x => x.Keys).Distinct().OrderBy(x => x))
        {
            var shares = new List<(int Index, decimal FloorUnits, decimal FractionalUnits)>();
            decimal rawTotal = 0m;
            decimal floorSumUnits = 0m;
            for (var i = 0; i < rawPerCupByLine.Count; i++)
            {
                if (!rawPerCupByLine[i].TryGetValue(ingredientId, out var perCup) || perCup <= 0m)
                    continue;

                var rawLineTotal = perCup * quantities[i];
                var scaled = rawLineTotal * Scale;
                var floorUnits = decimal.Floor(scaled);
                rawTotal += rawLineTotal;
                floorSumUnits += floorUnits;
                shares.Add((i, floorUnits, scaled - floorUnits));
            }
            if (shares.Count == 0)
                continue;

            var targetUnits = decimal.Ceiling(rawTotal * Scale);
            var extra = targetUnits - floorSumUnits;
            if (extra < 0m || extra > shares.Count || extra != decimal.Truncate(extra))
                throw new InvalidOperationException("Không thể phân bổ lượng làm tròn của BOM.");

            // Largest remainder method; tie-break by original line position.
            var extraIndices = shares.OrderByDescending(x => x.FractionalUnits)
                .ThenBy(x => x.Index).Take((int)extra)
                .Select(x => x.Index).ToHashSet();

            decimal allocatedTotal = 0m;
            foreach (var share in shares)
            {
                var held = (share.FloorUnits + (extraIndices.Contains(share.Index) ? 1m : 0m)) / Scale;
                // A positive-need line cannot silently become a zero-stock hold.
                // If this occurs, the schema/rounding policy must be revisited.
                if (held <= 0m)
                    throw new InvalidOperationException(
                        "Có dòng nguyên liệu quá nhỏ để giữ riêng với độ chính xác 4 chữ số.");
                reservedByLine[share.Index][ingredientId] = held;
                allocatedTotal += held;
            }

            var roundedTotal = targetUnits / Scale;
            if (allocatedTotal != roundedTotal)
                throw new InvalidOperationException("Tổng giữ chỗ không khớp BOM đã chốt.");
            totalByIngredient[ingredientId] = roundedTotal;
        }

        return new Result(reservedByLine, totalByIngredient);
    }
}