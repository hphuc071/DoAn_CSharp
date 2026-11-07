using System.Globalization;
using System.Text;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Customer.Menu;

public static class CatalogEndpoints
{
    public static void MapCatalog(this WebApplication app)
    {
        app.MapGet("/api/menu", async (
            string? q,
            string? category,
            decimal? minPrice,
            decimal? maxPrice,
            bool? availableOnly,
            string? sort,
            CafeDbContext db) =>
        {
            if ((minPrice.HasValue && minPrice.Value < 0) ||
                (maxPrice.HasValue && maxPrice.Value < 0) ||
                (minPrice.HasValue && maxPrice.HasValue && minPrice.Value > maxPrice.Value))
            {
                return Results.Problem(detail: "Khoảng giá lọc không hợp lệ.", statusCode: 400);
            }

            var variants = await (
                from v in db.CauHinhMon.AsNoTracking()
                join m in db.Mon.AsNoTracking() on v.MaMon equals m.MaMon
                join c in db.DanhMucMon.AsNoTracking() on m.MaDanhMuc equals c.MaDanhMuc
                where m.DaCongBo && c.DaCongBo && c.DangSuDung
                select new
                {
                    v.MaCauHinhMon,
                    v.MaMon,
                    m.TenMon,
                    c.TenDanhMuc,
                    v.GiaBan,
                    v.KichCo,
                    v.NhietDo,
                    m.DuongDanAnh,
                    m.ThuTuHienThi,
                    Enabled = v.DangBan && m.DangBan
                }).ToListAsync();

            var now = DateTime.UtcNow;
            var recipes = await db.CongThuc.AsNoTracking()
                .Where(x => x.TrangThai == "ACTIVE" && x.MaCauHinhMon != null && x.HieuLucTu <= now && (x.HieuLucDen == null || x.HieuLucDen > now))
                .ToListAsync();
            var requirements = await db.ChiTietCongThuc.AsNoTracking().ToListAsync();
            var stocks = await (
                from n in db.NguyenLieu.AsNoTracking()
                join t in db.TonKho.AsNoTracking() on n.MaNguyenLieu equals t.MaNguyenLieu
                select new { n.MaNguyenLieu, n.DangSuDung, Available = t.TonThucTe - t.TongGiuCho - n.TonAnToan }
            ).ToDictionaryAsync(x => x.MaNguyenLieu);
            var choices = await db.TuyChonPhaCheHopLe.AsNoTracking().Where(x => x.DangSuDung).ToListAsync();

            bool Available(long variant)
            {
                var recipe = recipes.FirstOrDefault(x => x.MaCauHinhMon == variant);
                if (recipe is null) return false;
                var lines = requirements.Where(x => x.MaCongThuc == recipe.MaCongThuc).ToList();
                return lines.Count > 0 && choices.Where(x => x.MaCauHinhMon == variant).Any(choice =>
                    lines.GroupBy(x => x.MaNguyenLieu).All(g =>
                    {
                        var required = decimal.Ceiling(g.Sum(l => l.DinhMucCoSo *
                            (l.LoaiDinhMuc == "SUGAR" ? choice.PhanTramDuong / 100m :
                             l.LoaiDinhMuc == "ICE" ? choice.PhanTramDa / 100m : 1m) / (1m - l.TyLeHaoHut)) * 10000m) / 10000m;
                        return required == 0 || (stocks.TryGetValue(g.Key, out var stock) && stock.DangSuDung && stock.Available >= required);
                    }));
            }

            var completedCounts = await (
                from d in db.ChiTietDonHang.AsNoTracking()
                join o in db.DonHang.AsNoTracking() on d.MaDonHang equals o.MaDonHang
                join v in db.CauHinhMon.AsNoTracking() on d.MaCauHinhMon equals v.MaCauHinhMon
                where o.TrangThai == "COMPLETED"
                group d by v.MaMon into g
                select new { MaMon = g.Key, SoldQty = g.Sum(x => x.SoLuongDat) }
            ).ToDictionaryAsync(x => x.MaMon, x => x.SoldQty);

            var products = variants.GroupBy(x => x.MaMon).OrderBy(g => g.First().ThuTuHienThi).Select(g =>
            {
                var item = g.OrderBy(x => x.KichCo == "M" ? 0 : 1).ThenBy(x => x.NhietDo == "ICED" ? 0 : 1).First();
                var available = g.Any(v => v.Enabled && Available(v.MaCauHinhMon));
                var image = int.TryParse(item.DuongDanAnh?.Replace("sprite:", "", StringComparison.Ordinal), out var index) ? index : 0;
                return new MenuProduct(item.MaCauHinhMon, item.MaMon, item.TenMon, item.TenDanhMuc, item.GiaBan, image, available, available ? "" : "Tạm hết");
            }).ToList();

            if (!string.IsNullOrWhiteSpace(category))
            {
                var normCat = NormalizeVietnamese(category);
                products = products.Where(p => NormalizeVietnamese(p.Category) == normCat).ToList();
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var normQuery = NormalizeVietnamese(q);
                products = products.Where(p =>
                    NormalizeVietnamese(p.Name).Contains(normQuery, StringComparison.Ordinal) ||
                    NormalizeVietnamese(p.Category).Contains(normQuery, StringComparison.Ordinal)).ToList();
            }

            if (minPrice.HasValue) products = products.Where(p => p.Price >= minPrice.Value).ToList();
            if (maxPrice.HasValue) products = products.Where(p => p.Price <= maxPrice.Value).ToList();
            if (availableOnly == true) products = products.Where(p => p.Available).ToList();

            products = (sort?.ToLowerInvariant()) switch
            {
                "price_asc" => products.OrderBy(p => p.Price).ToList(),
                "price_desc" => products.OrderByDescending(p => p.Price).ToList(),
                "bestseller" => products.OrderByDescending(p => completedCounts.GetValueOrDefault(p.MaMon, 0)).ThenBy(p => p.MaMon).ToList(),
                "newest" => products.OrderByDescending(p => p.MaMon).ToList(),
                _ => products
            };

            return Results.Ok(products);
        });

        app.MapGet("/api/menu/{product:long}/variants", async (long product, CafeDbContext db) =>
        {
            var mon = await db.Mon.AsNoTracking().FirstOrDefaultAsync(m => m.MaMon == product);
            if (mon is null || !mon.DaCongBo || !mon.DangBan)
                return Results.Problem(detail: "Món hiện ngừng phục vụ.", statusCode: 404);

            var now = DateTime.UtcNow;
            var variants = await db.CauHinhMon.AsNoTracking()
                .Where(x => x.MaMon == product && x.DangBan)
                .ToListAsync();

            var variantIds = variants.Select(v => v.MaCauHinhMon).ToList();

            var choices = await db.TuyChonPhaCheHopLe.AsNoTracking()
                .Where(x => variantIds.Contains(x.MaCauHinhMon) && x.DangSuDung)
                .ToListAsync();

            var toppings = await (
                from ct in db.CauHinhMonThem.AsNoTracking()
                join mt in db.MonThem.AsNoTracking() on ct.MaMonThem equals mt.MaMonThem
                where variantIds.Contains(ct.MaCauHinhMon) && ct.DangSuDung && mt.DangSuDung
                select new
                {
                    ct.MaCauHinhMon,
                    mt.MaMonThem,
                    mt.TenMonThem,
                    mt.GiaMoiPhan,
                    GioiHanMoiLy = ct.GioiHanMoiLy > 0 ? ct.GioiHanMoiLy : mt.GioiHanMacDinhMoiLy
                }).ToListAsync();

            var recipes = await db.CongThuc.AsNoTracking()
                .Where(x => x.TrangThai == "ACTIVE" && x.HieuLucTu <= now && (x.HieuLucDen == null || x.HieuLucDen > now))
                .ToListAsync();
            var requirements = await db.ChiTietCongThuc.AsNoTracking().ToListAsync();
            var stocks = await (
                from n in db.NguyenLieu.AsNoTracking()
                join t in db.TonKho.AsNoTracking() on n.MaNguyenLieu equals t.MaNguyenLieu
                select new { n.MaNguyenLieu, n.DangSuDung, Available = t.TonThucTe - t.TongGiuCho - n.TonAnToan }
            ).ToDictionaryAsync(x => x.MaNguyenLieu);

            bool IsVariantAvailable(long variantId)
            {
                var recipe = recipes.FirstOrDefault(x => x.MaCauHinhMon == variantId);
                if (recipe is null) return false;
                var lines = requirements.Where(x => x.MaCongThuc == recipe.MaCongThuc).ToList();
                return lines.Count > 0 && choices.Where(x => x.MaCauHinhMon == variantId).Any(choice =>
                    lines.GroupBy(x => x.MaNguyenLieu).All(g =>
                    {
                        var required = decimal.Ceiling(g.Sum(l => l.DinhMucCoSo *
                            (l.LoaiDinhMuc == "SUGAR" ? choice.PhanTramDuong / 100m :
                             l.LoaiDinhMuc == "ICE" ? choice.PhanTramDa / 100m : 1m) / (1m - l.TyLeHaoHut)) * 10000m) / 10000m;
                        return required == 0 || (stocks.TryGetValue(g.Key, out var stock) && stock.DangSuDung && stock.Available >= required);
                    }));
            }

            return Results.Ok(variants.Select(v => new MenuVariantItem(
                v.MaCauHinhMon,
                v.KichCo,
                v.NhietDo,
                v.GiaBan,
                IsVariantAvailable(v.MaCauHinhMon),
                choices.Where(c => c.MaCauHinhMon == v.MaCauHinhMon)
                       .Where(c => !string.Equals(v.NhietDo, "HOT", StringComparison.OrdinalIgnoreCase) || c.PhanTramDa == 0)
                       .Select(c => new MenuBrewOption(c.PhanTramDuong, c.PhanTramDa))
                       .ToList(),
                toppings.Where(t => t.MaCauHinhMon == v.MaCauHinhMon)
                        .Select(t => new MenuToppingItem(t.MaMonThem, t.TenMonThem, t.GiaMoiPhan, t.GioiHanMoiLy, true))
                        .ToList()
            )).ToList());
        });
    }

    private static string NormalizeVietnamese(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var normalized = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c is 'đ' or 'Đ' ? 'd' : c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
