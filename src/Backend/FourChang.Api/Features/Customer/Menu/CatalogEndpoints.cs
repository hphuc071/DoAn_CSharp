using System.Globalization;
using System.Text;
using FourChang.Api.Features.Admin.Menu;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Customer.Menu;

/// <summary>Giữ hợp đồng menu hiện có của nhóm; dùng chung bộ tính giá/khả dụng với ADM-02 và POS.</summary>
public static class CatalogEndpoints
{
    /// <summary>Đăng ký tìm kiếm và cấu hình món, không khai báo bảng giá hay khả dụng topping riêng.</summary>
    public static void MapCatalog(this WebApplication app)
    {
        app.MapGet("/api/menu", async (string? q, string? category, decimal? minPrice, decimal? maxPrice,
            bool? availableOnly, string? sort, CafeDbContext db, CatalogReadService catalog) =>
        {
            if (minPrice < 0 || maxPrice < 0 || minPrice > maxPrice) return Results.Problem(statusCode: 400, detail: "Khoảng giá lọc không hợp lệ.");
            var snapshot = await catalog.Read();
            var sold = await db.ChiTietDonHang.AsNoTracking().Join(db.DonHang.Where(x => x.TrangThai == "COMPLETED"), l => l.MaDonHang, o => o.MaDonHang,
                (l, o) => new { l.MaCauHinhMon, Quantity = l.SoLuongDat - l.SoLuongDaHuy }).ToListAsync();
            var items = snapshot.Mon.Where(x => x.CauHinh.Count > 0).Select(m =>
            {
                var v = m.CauHinh.FirstOrDefault(x => x.LaMacDinh && x.ConHang)
                    ?? m.CauHinh.FirstOrDefault(x => x.ConHang)
                    ?? m.CauHinh.FirstOrDefault(x => x.LaMacDinh) ?? m.CauHinh.First();
                var image = int.TryParse(m.DuongDanAnh?.Replace("sprite:", "", StringComparison.Ordinal), out var index) ? index : 0;
                return new MenuProduct(v.MaCauHinhMon, m.MaMon, m.TenMon, m.TenDanhMuc, v.GiaBan, image, m.CauHinh.Any(x => x.ConHang), m.CauHinh.Any(x => x.ConHang) ? "" : "Tạm hết");
            }).Where(x => string.IsNullOrWhiteSpace(category) || Normalize(x.Category) == Normalize(category))
              .Where(x => string.IsNullOrWhiteSpace(q) || Normalize(x.Name).Contains(Normalize(q), StringComparison.Ordinal) || Normalize(x.Category).Contains(Normalize(q), StringComparison.Ordinal))
              .Where(x => minPrice is null || x.Price >= minPrice).Where(x => maxPrice is null || x.Price <= maxPrice)
              .Where(x => availableOnly != true || x.Available);
            items = sort?.ToLowerInvariant() switch
            {
                "price_asc" => items.OrderBy(x => x.Price),
                "price_desc" => items.OrderByDescending(x => x.Price),
                "newest" => items.OrderByDescending(x => x.MaMon),
                "bestseller" => items.OrderByDescending(x => sold.Where(l => snapshot.Mon.Single(m => m.MaMon == x.MaMon).CauHinh.Any(v => v.MaCauHinhMon == l.MaCauHinhMon)).Sum(l => l.Quantity)).ThenBy(x => x.MaMon),
                _ => items
            };
            return Results.Ok(items.ToList());
        });
        app.MapGet("/api/menu/{product:long}/variants", async (long product, CatalogReadService catalog) =>
        {
            var snapshot = await catalog.Read();
            var dish = snapshot.Mon.SingleOrDefault(x => x.MaMon == product);
            if (dish is null || !dish.DangBan) return Results.Problem(statusCode: 404, detail: "Món chưa công bố hoặc ngừng phục vụ.");
            return Results.Ok(dish.CauHinh.Select(v => new MenuVariantItem(v.MaCauHinhMon, v.KichCo, v.NhietDo, v.GiaBan, v.ConHang,
                v.TuyChon.Select(c => new MenuBrewOption(c.PhanTramDuong, c.PhanTramDa)).ToList(),
                v.MonThem.Select(t => new MenuToppingItem(t.MaMonThem, t.TenMonThem, t.GiaMoiPhan, t.GioiHanMoiLy, t.ConHang)).ToList())).ToList());
        });
    }

    /// <summary>Chuẩn hóa tiếng Việt cho tìm kiếm; không thay đổi tên được lưu hoặc snapshot.</summary>
    private static string Normalize(string? value)
    {
        var result = new StringBuilder();
        foreach (var c in (value ?? "").Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) result.Append(c == 'đ' ? 'd' : c);
        return result.ToString().Normalize(NormalizationForm.FormC);
    }
}
