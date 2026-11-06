using System.Text.Json;
using FourChang.Api.Shared.WorkSessions;
using FourChang.Domain.Entities.Menu;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;
namespace FourChang.Api.Features.Admin.Menu;

/// <summary>UC-ADM-03: mọi lệnh menu nguyên tử cùng audit/receipt/outbox; không ghi vào đơn cũ.</summary>
public sealed class MenuAdminService(CafeDbContext db, CatalogReadService catalog)
{
    /// <summary>Đọc dữ liệu quản trị gồm nháp/ẩn và phiên bản; UI không tự khai báo giá hoặc mã.</summary>
    public async Task<MenuAdminSnapshot> Read()
    {
        var cats = await db.DanhMucMon.AsNoTracking().OrderBy(x => x.ThuTuHienThi).ThenBy(x => x.MaDanhMuc).ToListAsync();
        var dishes = await db.Mon.AsNoTracking().OrderBy(x => x.ThuTuHienThi).ThenBy(x => x.MaMon).ToListAsync();
        var variants = await db.CauHinhMon.AsNoTracking().OrderBy(x => x.MaCauHinhMon).ToListAsync();
        var choices = await db.TuyChonPhaCheHopLe.AsNoTracking().Where(x => x.DangSuDung).ToListAsync();
        var links = await db.CauHinhMonThem.AsNoTracking().Where(x => x.DangSuDung).ToListAsync();
        var toppings = await db.MonThem.AsNoTracking().OrderBy(x => x.MaMonThem).ToListAsync();
        var banners = await db.Banner.AsNoTracking().OrderBy(x => x.ThuTuHienThi).ThenBy(x => x.MaBanner).ToListAsync();
        return new(cats.Select(x => new MenuCategory(x.MaDanhMuc, x.TenDanhMuc, x.MoTa, x.ThuTuHienThi, x.DaCongBo, x.DangSuDung, dishes.Count(m => m.MaDanhMuc == x.MaDanhMuc), x.PhienBanDuLieu)).ToList(),
            dishes.Select(x => new MenuDish(x.MaMon, x.MaDanhMuc, x.TenMon, x.MoTa, x.DuongDanAnh, x.ThuTuHienThi, x.DaCongBo, x.DangBan, x.CongBoTuLuc, x.PhienBanDuLieu)).ToList(),
            variants.Select(x => new MenuVariant(x.MaCauHinhMon, x.MaMon, x.KichCo, x.NhietDo, x.GiaBan, x.DangBan, x.LaMacDinh, x.PhienBanDuLieu, choices.Where(c => c.MaCauHinhMon == x.MaCauHinhMon).Select(c => new MenuChoice(c.PhanTramDuong, c.PhanTramDa, c.LaMacDinh)).ToList(), links.Where(c => c.MaCauHinhMon == x.MaCauHinhMon).Select(c => new MenuToppingLink(c.MaMonThem, c.GioiHanMoiLy)).ToList())).ToList(),
            toppings.Select(x => new MenuTopping(x.MaMonThem, x.TenMonThem, x.MoTa, x.GiaMoiPhan, x.GioiHanMacDinhMoiLy, x.DuongDanAnh, x.DangSuDung, x.PhienBanDuLieu)).ToList(),
            banners.Select(x => new MenuBanner(x.MaBanner, x.TenBanner, x.NoiDung, x.DuongDanAnh, x.LienKet, x.ViTri, x.ThuTuHienThi, x.BatDauLuc, x.KetThucLuc, x.DaCongBo, x.PhienBanDuLieu)).ToList());
    }

    /// <summary>Dùng transaction/chống trùng hiện có; thêm đúng một sự kiện trước commit cùng audit bắt buộc.</summary>
    private Task<IResult> Change(Guid operation, string kind, long? id, object input, HttpContext context, Func<Task<(long Id, byte[]? Version, string? Before)>> action) =>
        WorkforceOperation.Execute(db, operation, "MENU_" + kind, "ThucDon", new
        {
            kind,
            id,
            input
        }, context, async () =>
        {
            var result = await action();
            db.SuKienDongBo.Add(new()
            {
                MaSuKien = Guid.NewGuid(),
                MaThaoTac = operation,
                LoaiSuKien = "MENU_CHANGED",
                NoiDungJson = JsonSerializer.Serialize(new
                {
                    Loai = kind,
                    Ma = result.Id
                }),
                PhatSinhLuc = DateTime.UtcNow
            });
            return (result.Id, (object)new MenuSaveReceipt(kind, result.Id, result.Version), result.Before, null);
        });

    /// <summary>Thêm/sửa danh mục; cảnh báo tác động trước khi ẩn/ngừng danh mục có món.</summary>
    public Task<IResult> Category(long? id, MenuCategoryInput input, HttpContext context) => Change(input.MaThaoTac, "CATEGORY", id, input, context, async () =>
    {
        var item = id is null ? new DanhMucMon() : await db.DanhMucMon.SingleOrDefaultAsync(x => x.MaDanhMuc == id) ?? throw Missing();
        var before = id is null ? null : JsonSerializer.Serialize(item);
        if (id is not null)
            WorkforceOperation.Version(input.PhienBanDuLieu, item.PhienBanDuLieu);
        var name = MenuValidation.Text(input.TenDanhMuc, 100, "Tên danh mục");
        if (input.ThuTuHienThi < 0)
            throw Bad("Thứ tự không được âm.");
        if (await db.DanhMucMon.AnyAsync(x => x.TenDanhMuc == name && x.MaDanhMuc != item.MaDanhMuc))
            throw Bad("Tên danh mục đã tồn tại.");
        if (id is not null && ((item.DaCongBo && !input.DaCongBo) || (item.DangSuDung && !input.DangSuDung)) && await db.Mon.AnyAsync(x => x.MaDanhMuc == id) && !input.XacNhanTacDong)
            throw new WorkforceRule(409, "Danh mục có món chịu tác động. Xem số món và xác nhận trước khi ẩn/ngừng.");
        item.TenDanhMuc = name;
        item.MoTa = MenuValidation.Optional(input.MoTa, 500);
        item.ThuTuHienThi = input.ThuTuHienThi;
        item.DaCongBo = input.DaCongBo;
        item.DangSuDung = input.DangSuDung;
        if (id is null)
            db.DanhMucMon.Add(item);
        await db.SaveChangesAsync();
        return (item.MaDanhMuc, item.PhienBanDuLieu, before);
    });

    /// <summary>Món mới lưu nháp trước; chỉ công bố sau khi cấu hình/BOM/mặc định hợp lệ, tồn thiếu vẫn tạm hết.</summary>
    public Task<IResult> Dish(long? id, MenuDishInput input, HttpContext context) => Change(input.MaThaoTac, "DISH", id, input, context, async () =>
    {
        var item = id is null ? new Mon() : await db.Mon.SingleOrDefaultAsync(x => x.MaMon == id) ?? throw Missing();
        var before = id is null ? null : JsonSerializer.Serialize(item);
        if (id is not null)
            WorkforceOperation.Version(input.PhienBanDuLieu, item.PhienBanDuLieu);
        if (!await db.DanhMucMon.AnyAsync(x => x.MaDanhMuc == input.MaDanhMuc))
            throw Bad("Danh mục không tồn tại.");
        item.TenMon = MenuValidation.Text(input.TenMon, 150, "Tên món");
        item.MaDanhMuc = input.MaDanhMuc;
        if (await db.Mon.AnyAsync(x => x.MaDanhMuc == item.MaDanhMuc && x.TenMon == item.TenMon && x.MaMon != item.MaMon))
            throw Bad("Tên món đã có trong danh mục.");
        if (input.ThuTuHienThi < 0 || input.DuongDanAnh is { Length: > 0 } && !MenuValidation.Image(input.DuongDanAnh))
            throw Bad("Thứ tự hoặc ảnh không hợp lệ.");
        item.MoTa = MenuValidation.Optional(input.MoTa, 1000);
        item.DuongDanAnh = MenuValidation.Optional(input.DuongDanAnh, 500);
        item.ThuTuHienThi = input.ThuTuHienThi;
        item.CongBoTuLuc = input.CongBoTuLuc is { } date ? MenuValidation.Utc(date) : null;
        item.DaCongBo = input.DaCongBo;
        item.DangBan = input.DangBan;
        if (id is null)
            db.Mon.Add(item);
        await db.SaveChangesAsync();
        if (input.DaCongBo)
            await CanPublish(item.MaMon);
        return (item.MaMon, item.PhienBanDuLieu, before);
    });

    /// <summary>Cấu hình lưu giá gốc cùng các tổ hợp đường/đá và topping; không động vào snapshot đã bán.</summary>
    public Task<IResult> Variant(long? id, MenuVariantInput input, HttpContext context) => Change(input.MaThaoTac, "VARIANT", id, input, context, async () =>
    {
        var item = id is null ? new CauHinhMon() : await db.CauHinhMon.SingleOrDefaultAsync(x => x.MaCauHinhMon == id) ?? throw Missing();
        var before = id is null ? null : JsonSerializer.Serialize(new
        {
            item,
            choices = await db.TuyChonPhaCheHopLe.AsNoTracking().Where(x => x.MaCauHinhMon == id).ToListAsync(),
            links = await db.CauHinhMonThem.AsNoTracking().Where(x => x.MaCauHinhMon == id).ToListAsync()
        });
        if (id is not null)
            WorkforceOperation.Version(input.PhienBanDuLieu, item.PhienBanDuLieu);
        var dish = await db.Mon.SingleOrDefaultAsync(x => x.MaMon == input.MaMon) ?? throw Bad("Món không tồn tại.");
        if (id is not null && item.MaMon != input.MaMon)
            throw Bad("Không chuyển cấu hình sang món khác.");
        MenuValidation.Price(input.GiaBan);
        if (input.KichCo is not ("S" or "M" or "L") || input.NhietDo is not ("HOT" or "ICED"))
            throw Bad("Size/nóng lạnh không hợp lệ.");
        if (await db.CauHinhMon.AnyAsync(x => x.MaMon == input.MaMon && x.KichCo == input.KichCo && x.NhietDo == input.NhietDo && x.MaCauHinhMon != item.MaCauHinhMon))
            throw Bad("Tổ hợp size/nóng lạnh đã tồn tại.");
        if (id is not null && (item.KichCo != input.KichCo || item.NhietDo != input.NhietDo) && await db.CongThuc.AnyAsync(x => x.MaCauHinhMon == id))
            throw new WorkforceRule(409, "Cấu hình đã gắn BOM. Tạo cấu hình mới để không đổi ý nghĩa công thức cũ.");
        var choices = input.TuyChon ?? [];
        var links = input.MonThem ?? [];
        if (choices.Count > 25 || choices.Any(x => x.PhanTramDuong is not (0 or 25 or 50 or 75 or 100) || x.PhanTramDa is not (0 or 25 or 50 or 75 or 100) || input.NhietDo == "HOT" && x.PhanTramDa != 0) || choices.Select(x => (x.PhanTramDuong, x.PhanTramDa)).Distinct().Count() != choices.Count || choices.Count(x => x.LaMacDinh) != 1)
            throw Bad("Chọn tổ hợp đường/đá hợp lệ, không trùng và đúng một mặc định; nóng không có đá.");
        if (links.Select(x => x.MaMonThem).Distinct().Count() != links.Count || links.Any(x => x.GioiHanMoiLy <= 0))
            throw Bad("Topping trùng hoặc giới hạn không hợp lệ.");
        foreach (var link in links)
        {
            var top = await db.MonThem.AsNoTracking().SingleOrDefaultAsync(x => x.MaMonThem == link.MaMonThem) ?? throw Bad("Topping không tồn tại.");
            if (link.GioiHanMoiLy > top.GioiHanMacDinhMoiLy)
                throw Bad("Giới hạn vượt mức của topping.");
        }
        // Cập nhật rowversion của cấu hình khác khi chuyển mặc định để form cũ không ghi đè im lặng.
        if (input.LaMacDinh)
        {
            var previous = await db.CauHinhMon.Where(x => x.MaMon == input.MaMon && x.LaMacDinh && x.MaCauHinhMon != item.MaCauHinhMon).ToListAsync();
            foreach (var other in previous)
                other.LaMacDinh = false;
            await db.SaveChangesAsync();
        }
        item.MaMon = input.MaMon;
        item.KichCo = input.KichCo;
        item.NhietDo = input.NhietDo;
        item.GiaBan = input.GiaBan;
        item.DangBan = input.DangBan;
        item.LaMacDinh = input.LaMacDinh;
        if (id is null)
            db.CauHinhMon.Add(item);
        await db.SaveChangesAsync();
        // Các bảng tùy chọn không được đơn cũ tham chiếu; đơn giữ bản chụp đường/đá và topping riêng.
        await db.TuyChonPhaCheHopLe.Where(x => x.MaCauHinhMon == item.MaCauHinhMon).ExecuteDeleteAsync();
        await db.CauHinhMonThem.Where(x => x.MaCauHinhMon == item.MaCauHinhMon).ExecuteDeleteAsync();
        foreach (var choice in choices)
            db.TuyChonPhaCheHopLe.Add(new()
            {
                MaCauHinhMon = item.MaCauHinhMon,
                PhanTramDuong = choice.PhanTramDuong,
                PhanTramDa = choice.PhanTramDa,
                LaMacDinh = choice.LaMacDinh,
                DangSuDung = true
            });
        foreach (var link in links)
            db.CauHinhMonThem.Add(new()
            {
                MaCauHinhMon = item.MaCauHinhMon,
                MaMonThem = link.MaMonThem,
                GioiHanMoiLy = link.GioiHanMoiLy,
                DangSuDung = true
            });
        await db.SaveChangesAsync();
        if (dish.DaCongBo)
            await CanPublish(dish.MaMon);
        return (item.MaCauHinhMon, item.PhienBanDuLieu, before);
    });

    /// <summary>Topping có giá mỗi phần và giới hạn riêng; kích hoạt phải có BOM, thiếu tồn chỉ khóa khả dụng.</summary>
    public Task<IResult> Topping(long? id, MenuToppingInput input, HttpContext context) => Change(input.MaThaoTac, "TOPPING", id, input, context, async () =>
    {
        var item = id is null ? new MonThem() : await db.MonThem.SingleOrDefaultAsync(x => x.MaMonThem == id) ?? throw Missing();
        var before = id is null ? null : JsonSerializer.Serialize(item);
        if (id is not null)
            WorkforceOperation.Version(input.PhienBanDuLieu, item.PhienBanDuLieu);
        MenuValidation.Price(input.GiaMoiPhan);
        item.TenMonThem = MenuValidation.Text(input.TenMonThem, 100, "Tên topping");
        if (input.GioiHanMacDinhMoiLy <= 0 || input.DuongDanAnh is { Length: > 0 } && !MenuValidation.Image(input.DuongDanAnh))
            throw Bad("Giới hạn hoặc ảnh không hợp lệ.");
        if (await db.MonThem.AnyAsync(x => x.TenMonThem == item.TenMonThem && x.MaMonThem != item.MaMonThem))
            throw Bad("Tên topping đã tồn tại.");
        if (id is not null && await db.CauHinhMonThem.AnyAsync(x => x.MaMonThem == id && x.DangSuDung && x.GioiHanMoiLy > input.GioiHanMacDinhMoiLy))
            throw Bad("Cấu hình đang gắn giới hạn lớn hơn. Điều chỉnh liên kết trước khi giảm mức topping.");
        item.MoTa = MenuValidation.Optional(input.MoTa, 500);
        item.GiaMoiPhan = input.GiaMoiPhan;
        item.GioiHanMacDinhMoiLy = input.GioiHanMacDinhMoiLy;
        item.DuongDanAnh = MenuValidation.Optional(input.DuongDanAnh, 500);
        item.DangSuDung = input.DangSuDung;
        if (id is null)
            db.MonThem.Add(item);
        await db.SaveChangesAsync();
        if (item.DangSuDung && !await catalog.HasRecipe(null, item.MaMonThem))
            throw Bad("Thiếu BOM topping hợp lệ. Lưu ngừng sử dụng, gắn BOM tại ADM-05 rồi kích hoạt.");
        return (item.MaMonThem, item.PhienBanDuLieu, before);
    });

    /// <summary>Banner chỉ công bố khi ảnh/liên kết đã kiểm tra và lịch chưa hết; lỗi rollback giữ bản công bố cũ.</summary>
    public Task<IResult> Banner(long? id, MenuBannerInput input, HttpContext context) => Change(input.MaThaoTac, "BANNER", id, input, context, async () =>
    {
        var item = id is null ? new Banner() : await db.Banner.SingleOrDefaultAsync(x => x.MaBanner == id) ?? throw Missing();
        var before = id is null ? null : JsonSerializer.Serialize(item);
        if (id is not null)
            WorkforceOperation.Version(input.PhienBanDuLieu, item.PhienBanDuLieu);
        if (!MenuValidation.Image(input.DuongDanAnh) || !MenuValidation.Link(input.LienKet) || input.ViTri != "HOME_HERO" || input.ThuTuHienThi < 0)
            throw Bad("Ảnh, liên kết, vị trí hoặc thứ tự banner không hợp lệ.");
        var start = MenuValidation.Utc(input.BatDauLuc);
        var end = input.KetThucLuc is { } date ? MenuValidation.Utc(date) : (DateTime?)null;
        if (end <= start || input.DaCongBo && end <= DateTime.UtcNow)
            throw Bad("Lịch banner không hợp lệ hoặc đã hết hạn.");
        item.TenBanner = MenuValidation.Text(input.TenBanner, 150, "Tên banner");
        item.NoiDung = MenuValidation.Optional(input.NoiDung, 1000);
        item.DuongDanAnh = input.DuongDanAnh;
        item.LienKet = MenuValidation.Optional(input.LienKet, 500);
        item.ViTri = input.ViTri;
        item.ThuTuHienThi = input.ThuTuHienThi;
        item.BatDauLuc = start;
        item.KetThucLuc = end;
        item.DaCongBo = input.DaCongBo;
        if (id is null)
            db.Banner.Add(item);
        await db.SaveChangesAsync();
        return (item.MaBanner, item.PhienBanDuLieu, before);
    });

    /// <summary>Xóa chỉ khi chưa có quan hệ phụ thuộc, luôn kiểm tra lại trong transaction có phiên bản.</summary>
    public Task<IResult> Delete(string kind, long id, MenuDeleteInput input, HttpContext context) => Change(input.MaThaoTac, "DELETE_" + kind, id, input, context, async () =>
    {
        object item;
        byte[] version;
        bool used;
        switch (kind)
        {
            case "categories":
                var c = await db.DanhMucMon.SingleOrDefaultAsync(x => x.MaDanhMuc == id) ?? throw Missing();
                item = c;
                version = c.PhienBanDuLieu;
                used = await db.Mon.AnyAsync(x => x.MaDanhMuc == id);
                break;
            case "dishes":
                var m = await db.Mon.SingleOrDefaultAsync(x => x.MaMon == id) ?? throw Missing();
                item = m;
                version = m.PhienBanDuLieu;
                used = await db.CauHinhMon.AnyAsync(x => x.MaMon == id);
                break;
            case "variants":
                var v = await db.CauHinhMon.SingleOrDefaultAsync(x => x.MaCauHinhMon == id) ?? throw Missing();
                item = v;
                version = v.PhienBanDuLieu;
                used = await db.CongThuc.AnyAsync(x => x.MaCauHinhMon == id) || await db.ChiTietDonHang.AnyAsync(x => x.MaCauHinhMon == id);
                if (!used)
                {
                    var dish = await db.Mon.SingleAsync(x => x.MaMon == v.MaMon);
                    if (dish.DaCongBo)
                        throw new WorkforceRule(409, "Gỡ công bố món trước khi xóa cấu hình.");
                    await db.TuyChonPhaCheHopLe.Where(x => x.MaCauHinhMon == id).ExecuteDeleteAsync();
                    await db.CauHinhMonThem.Where(x => x.MaCauHinhMon == id).ExecuteDeleteAsync();
                }
                break;
            case "toppings":
                var t = await db.MonThem.SingleOrDefaultAsync(x => x.MaMonThem == id) ?? throw Missing();
                item = t;
                version = t.PhienBanDuLieu;
                used = await db.CongThuc.AnyAsync(x => x.MaMonThem == id) || await db.CauHinhMonThem.AnyAsync(x => x.MaMonThem == id) || await db.MonThemChiTietDonHang.AnyAsync(x => x.MaMonThem == id);
                break;
            case "banners":
                var b = await db.Banner.SingleOrDefaultAsync(x => x.MaBanner == id) ?? throw Missing();
                item = b;
                version = b.PhienBanDuLieu;
                used = false;
                break;
            default:
                throw Bad("Loại dữ liệu không hợp lệ.");
        }
        WorkforceOperation.Version(input.PhienBanDuLieu, version);
        if (used)
            throw new WorkforceRule(409, "Dữ liệu đã có tham chiếu. Hãy ẩn/ngừng thay vì xóa.");
        var before = JsonSerializer.Serialize(item);
        db.Remove(item);
        await db.SaveChangesAsync();
        return (id, null, before);
    });

    /// <summary>Điều kiện công bố không dựa trên cờ bật bán: đủ mặc định/giá/BOM/quy đổi; tồn thiếu được báo tạm hết.</summary>
    private async Task CanPublish(long dish)
    {
        var variants = await db.CauHinhMon.AsNoTracking().Where(x => x.MaMon == dish).ToListAsync();
        if (variants.Count == 0 || variants.Count(x => x.LaMacDinh) != 1 || !variants.Single(x => x.LaMacDinh).DangBan)
            throw Bad("Cần cấu hình mặc định đang bật và đủ giá trước khi công bố.");
        foreach (var v in variants.Where(x => x.DangBan))
        {
            if (!await catalog.HasRecipe(v.MaCauHinhMon, null))
                throw Bad("Thiếu BOM/quy đổi hợp lệ. Lưu nháp và hoàn thiện tại ADM-05.");
            if (await db.TuyChonPhaCheHopLe.CountAsync(x => x.MaCauHinhMon == v.MaCauHinhMon && x.DangSuDung && x.LaMacDinh) != 1)
                throw Bad("Cấu hình cần đúng một cặp đường/đá mặc định.");
        }
    }
    /// <summary>Lỗi nhập liệu được trả rõ cho UI để giữ nháp sửa, không trả lỗi SQL/bí mật.</summary>
    private static WorkforceRule Bad(string text) => new(400, text);
    /// <summary>Đối tượng đã bị xóa phải trả 404 thay vì tạo thay thế không có sự đồng ý.</summary>
    private static WorkforceRule Missing() => new(404, "Dữ liệu không còn tồn tại. Tải lại danh sách.");
}
