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
    private HashSet<long> validUnits = [];
    private List<QuyDoiDonViNguyenLieu> conversions = [];

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
        validUnits = (await db.DonViTinh.AsNoTracking().Where(x => x.DangSuDung).Select(x => x.MaDonVi).ToListAsync()).ToHashSet();
        conversions = await db.QuyDoiDonViNguyenLieu.AsNoTracking().ToListAsync();
    }
    /// <summary>Chọn đúng một BOM có hiệu lực; mơ hồ hoặc thiếu quy đổi thì không đủ điều kiện công bố.</summary>
    private CongThuc? Recipe(long? variant, long? topping)
    {
        var matches = recipes!.Where(x => variant is not null ? x.MaCauHinhMon == variant : x.MaMonThem == topping).ToList();
        if (matches.Count != 1)
            return null;
        var recipe = matches[0];
        var requirements = lines.Where(x => x.MaCongThuc == recipe.MaCongThuc).ToList();
        if (requirements.Count == 0 || requirements.Any(l => !ingredients.Any(n => n.MaNguyenLieu == l.MaNguyenLieu && validUnits.Contains(n.MaDonViCoSo) && conversions.Any(c => c.MaNguyenLieu == n.MaNguyenLieu && c.MaDonVi == n.MaDonViCoSo && c.HeSoVeDonViCoSo == 1))))
            return null;
        return recipe;
    }
    /// <summary>ADM-03 chỉ kiểm tra liên kết; tạo và kích hoạt công thức thuộc ADM-05.</summary>
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
            var required = decimal.Ceiling(g.Sum(l => l.DinhMucCoSo * (l.LoaiDinhMuc == "SUGAR" ? sugar / 100m : l.LoaiDinhMuc == "ICE" ? ice / 100m : 1) / (1 - l.TyLeHaoHut)) * 10000) / 10000;
            var n = ingredients.Single(x => x.MaNguyenLieu == g.Key);
            var stock = stocks.SingleOrDefault(x => x.MaNguyenLieu == g.Key);
            if (required > 0 && (!n.DangSuDung || stock is null || stock.TonThucTe - stock.TongGiuCho - n.TonAnToan < required))
                return false;
        }
        return true;
    }
    /// <summary>Lọc trạng thái/lịch tại máy chủ; món bị tắt còn hiện hết hàng, nháp/ẩn không rò qua API cấu hình.</summary>
    public async Task<CatalogSnapshot> Read()
    {
        await Load();
        var now = DateTime.UtcNow;
        var cats = await db.DanhMucMon.AsNoTracking().Where(x => x.DaCongBo && x.DangSuDung).ToListAsync();
        var catIds = cats.Select(x => x.MaDanhMuc).ToArray();
        var dishes = await db.Mon.AsNoTracking().Where(x => x.DaCongBo && (x.CongBoTuLuc == null || x.CongBoTuLuc <= now) && catIds.Contains(x.MaDanhMuc)).OrderBy(x => x.ThuTuHienThi).ThenBy(x => x.MaMon).ToListAsync();
        var ids = dishes.Select(x => x.MaMon).ToArray();
        var variants = await db.CauHinhMon.AsNoTracking().Where(x => ids.Contains(x.MaMon) && x.DangBan).ToListAsync();
        var choices = await db.TuyChonPhaCheHopLe.AsNoTracking().Where(x => x.DangSuDung).ToListAsync();
        var links = await db.CauHinhMonThem.AsNoTracking().Where(x => x.DangSuDung).ToListAsync();
        var tops = await db.MonThem.AsNoTracking().Where(x => x.DangSuDung).ToListAsync();
        var result = dishes.Select(m => new CatalogDish(m.MaMon, m.MaDanhMuc, cats.Single(c => c.MaDanhMuc == m.MaDanhMuc).TenDanhMuc, m.TenMon, m.MoTa, m.DuongDanAnh, m.ThuTuHienThi, m.DangBan,
            variants.Where(v => v.MaMon == m.MaMon).OrderByDescending(v => v.LaMacDinh).ThenBy(v => v.MaCauHinhMon).Select(v =>
            {
                var options = choices.Where(x => x.MaCauHinhMon == v.MaCauHinhMon).Select(x => new CatalogChoice(x.PhanTramDuong, x.PhanTramDa, x.LaMacDinh, m.DangBan && Available(v.MaCauHinhMon, null, x.PhanTramDuong, x.PhanTramDa))).ToList();
                return new CatalogVariant(v.MaCauHinhMon, v.KichCo, v.NhietDo, v.GiaBan, v.LaMacDinh, options.Any(x => x.ConHang), options,
                    links.Where(l => l.MaCauHinhMon == v.MaCauHinhMon).Join(tops, l => l.MaMonThem, t => t.MaMonThem, (l, t) => new CatalogTopping(t.MaMonThem, t.TenMonThem, t.GiaMoiPhan, Math.Min(l.GioiHanMoiLy, t.GioiHanMacDinhMoiLy), Available(null, t.MaMonThem))).ToList());
            }).ToList())).ToList();
        var banners = await db.Banner.AsNoTracking().Where(x => x.DaCongBo && x.BatDauLuc <= now && (x.KetThucLuc == null || x.KetThucLuc > now)).OrderBy(x => x.ThuTuHienThi).ThenBy(x => x.MaBanner).ToListAsync();
        return new(result, banners.Select(x => new MenuBanner(x.MaBanner, x.TenBanner, x.NoiDung, x.DuongDanAnh, x.LienKet, x.ViTri, x.ThuTuHienThi, x.BatDauLuc, x.KetThucLuc, x.DaCongBo, x.PhienBanDuLieu)).ToList(), cats.OrderBy(x => x.ThuTuHienThi).ThenBy(x => x.MaDanhMuc).Select(x => new CatalogCategory(x.MaDanhMuc, x.TenDanhMuc, x.ThuTuHienThi)).ToList());
    }
}
