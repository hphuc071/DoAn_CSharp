using FourChang.Domain.Shared;
using FourChang.Domain.Entities.Menu;
using FourChang.Domain.Entities.Inventory;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.EntityFrameworkCore;
namespace FourChang.Api.Features.Admin.Menu;

/// <summary>Một nguồn khả dụng BOM và giá cho Web/POS; bật thủ công không vượt trạng thái thiếu nguyên liệu.</summary>
public sealed class CatalogReadService(CafeDbContext db)
{
    private List<CongThuc>? recipes;
    private List<ChiTietCongThuc> lines = [];
    private List<NguyenLieu> ingredients = [];
    private List<TonKho> stocks = [];

    /// <summary>Bỏ snapshot trong request sau khi món mới vừa được sao chép đủ công thức.</summary>
    internal void ResetSnapshot() => recipes = null;

    /// <summary>Chụp dữ liệu đọc trong phạm vi request; không giữ cache giá/tồn qua các yêu cầu.</summary>
    private async Task Load()
    {
        if (recipes is not null)
            return;
        var now = DateTime.UtcNow;
        recipes = await db.CongThuc.AsNoTracking().Where(x => x.TrangThai == "ACTIVE" && x.HieuLucTu <= now && (x.HieuLucDen == null || x.HieuLucDen > now)).ToListAsync();
        lines = await db.ChiTietCongThuc.AsNoTracking().ToListAsync();
        ingredients = await db.NguyenLieu.AsNoTracking().ToListAsync();
        stocks = await db.TonKho.AsNoTracking().ToListAsync();
    }
    /// <summary>Chọn đúng một BOM có hiệu lực; mơ hồ hoặc thiếu quy đổi thì không đủ điều kiện công bố.</summary>
    private CongThuc? Recipe(long? variant, long? topping)
    {
        var matches = recipes!.Where(x => variant is not null ? x.MaCauHinhMon == variant : x.MaMonThem == topping).ToList();
        if (matches.Count != 1)
            return null;
        var recipe = matches[0];
        var requirements = lines.Where(x => x.MaCongThuc == recipe.MaCongThuc).ToList();
        if (requirements.Count == 0 || requirements.Any(l => !ingredients.Any(n => n.MaNguyenLieu == l.MaNguyenLieu && n.DangSuDung && n.DonViCoSo is "g" or "ml")))
            return null;
        return recipe;
    }
    /// <summary>UC-ADM-02 kiểm tra công thức hiện hành của cấu hình/topping.</summary>
    public async Task<bool> HasRecipe(long? variant, long? topping)
    {
        await Load();
        return Recipe(variant, topping) is not null;
    }

    /// <summary>Đường/đá lấy từ tổ hợp cấu trúc, ghi chú tự do không làm đổi định mức hoặc tồn.</summary>
    private bool Available(long? variant, long? topping, int sugar = 100, int ice = 100)
    {
        var recipe = Recipe(variant, topping);
        if (recipe is null)
            return false;
        foreach (var g in lines.Where(x => x.MaCongThuc == recipe.MaCongThuc).GroupBy(x => x.MaNguyenLieu))
        {
            var required = decimal.Ceiling(g.Sum(l => l.DinhMucCoSo * (l.LoaiDinhMuc == "SUGAR" ? sugar / 100m : l.LoaiDinhMuc == "ICE" ? ice / 100m : 1)) * 10000) / 10000;
            var n = ingredients.Single(x => x.MaNguyenLieu == g.Key);
            var stock = stocks.SingleOrDefault(x => x.MaNguyenLieu == g.Key);
            if (required > 0 && (!n.DangSuDung || stock is null || stock.TonThucTe - stock.TongGiuCho < required))
                return false;
        }
        return true;
    }
    /// <summary>Lọc trạng thái/lịch tại máy chủ; món bị tắt còn hiện hết hàng, nháp/ẩn không rò qua API cấu hình.</summary>
    public async Task<CatalogSnapshot> Read()
    {
        await Load();
        var now = DateTime.UtcNow;
        var cats = CafeOptions.Categories;
        var dishes = await db.Mon.AsNoTracking().Where(x => x.DaCongBo && (x.CongBoTuLuc == null || x.CongBoTuLuc <= now)).OrderBy(x => x.ThuTuHienThi).ThenBy(x => x.MaMon).ToListAsync();
        var ids = dishes.Select(x => x.MaMon).ToArray();
        var variants = await db.CauHinhMon.AsNoTracking().Where(x => ids.Contains(x.MaMon) && x.DangBan).ToListAsync();
        var tops = await db.MonThem.AsNoTracking().Where(x => x.DangSuDung).ToListAsync();
        var result = dishes.Select(m => new CatalogDish(m.MaMon, m.MaDanhMuc, m.LoaiMon, m.TenMon, m.MoTa, m.DuongDanAnh, m.ThuTuHienThi, m.DangBan,
            variants.Where(v => v.MaMon == m.MaMon).OrderByDescending(v => v.LaMacDinh).ThenBy(v => v.MaCauHinhMon).Select(v =>
            {
                var defaultSugar = Recipe(v.MaCauHinhMon, null)?.PhanTramDuongMacDinh ?? 50;
                var options = CafeOptions.BrewChoices(v.NhietDo).Select(x => new CatalogChoice(x.Sugar, x.Ice, x.Sugar == defaultSugar && x.Ice == (v.NhietDo == "HOT" ? 0 : 50), m.DangBan && Available(v.MaCauHinhMon, null, x.Sugar, x.Ice))).ToList();
                return new CatalogVariant(v.MaCauHinhMon, v.KichCo, v.NhietDo, v.GiaBan, v.LaMacDinh, options.Any(x => x.ConHang), options,
                    tops.Select(t => new CatalogTopping(t.MaMonThem, t.TenMonThem, t.GiaMoiPhan, t.GioiHanMacDinhMoiLy, Available(null, t.MaMonThem))).ToList());
            }).ToList())).ToList();
        var banners = await db.Banner.AsNoTracking().Where(x => x.DaCongBo && x.BatDauLuc <= now && (x.KetThucLuc == null || x.KetThucLuc > now)).OrderBy(x => x.ThuTuHienThi).ThenBy(x => x.MaBanner).ToListAsync();
        return new(result, banners.Select(x => new MenuBanner(x.MaBanner, x.TenBanner, x.NoiDung, x.DuongDanAnh, x.LienKet, x.ViTri, x.ThuTuHienThi, x.BatDauLuc, x.KetThucLuc, x.DaCongBo, x.PhienBanDuLieu)).ToList(), cats.Select(x => new CatalogCategory(x.Id, x.Name, (int)x.Id)).ToList());
    }
}
