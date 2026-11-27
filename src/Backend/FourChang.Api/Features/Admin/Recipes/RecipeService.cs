// UC: UC-ADM-02
// Chức năng: Lưu công thức và lượng nguyên liệu theo cấu hình món hoặc phần topping.
// Cần nhớ: Đơn cũ giữ lượng nguyên liệu đã chốt, không tính lại bằng công thức vừa sửa.

using FourChang.Domain.Shared;
using System.Text.Json;
using System.Security.Claims;
using FourChang.Api.Features.Admin.Menu;
using FourChang.Api.Shared.Operations;
using FourChang.Domain.Entities.Menu;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.Recipes;

/// <summary>ADM-02: lưu công thức áp dụng ngay; tính decimal và giữ đơn/giữ chỗ đã chốt bất biến.</summary>
public sealed class RecipeService(CafeDbContext db)
{
    /// <summary>Đọc công thức hiện hành/nháp còn xử lý; bản đã thay thế chỉ giữ cho lịch sử đơn.</summary>
    public async Task<IReadOnlyList<RecipeView>> Read()
    {
        var recipes = await db.CongThuc.AsNoTracking().Where(x => x.TrangThai == "ACTIVE" || x.TrangThai == "DRAFT").OrderByDescending(x => x.MaCongThuc).ToListAsync();
        var variants = await (from v in db.CauHinhMon.AsNoTracking()
            join m in db.Mon.AsNoTracking() on v.MaMon equals m.MaMon
            select new { v.MaCauHinhMon, m.TenMon, v.KichCo, v.NhietDo }).ToDictionaryAsync(x => x.MaCauHinhMon);
        var toppings = await db.MonThem.AsNoTracking().ToDictionaryAsync(x => x.MaMonThem, x => x.TenMonThem);
        var lines = await (from l in db.ChiTietCongThuc.AsNoTracking()
                           join n in db.NguyenLieu.AsNoTracking() on l.MaNguyenLieu equals n.MaNguyenLieu
                           select new { l.MaCongThuc, Line = new RecipeLineView(l.MaNguyenLieu, n.TenNguyenLieu, n.DonViCoSo, l.DinhMucCoSo, l.LoaiDinhMuc, 0) }).ToListAsync();
        return recipes.Select(r => new RecipeView(r.MaCongThuc, r.MaCauHinhMon, r.MaMonThem, r.SoPhienBan, r.TenCongThuc,
            r.TrangThai, r.HieuLucTu, r.HieuLucDen, r.GhiChu, r.PhienBanDuLieu, lines.Where(l => l.MaCongThuc == r.MaCongThuc).Select(l => l.Line).ToList(),
            r.MaCauHinhMon is { } id ? variants.GetValueOrDefault(id)?.TenMon : toppings.GetValueOrDefault(r.MaMonThem ?? 0),
            r.MaCauHinhMon is { } v ? variants.GetValueOrDefault(v)?.KichCo : null,
            r.MaCauHinhMon is { } t ? variants.GetValueOrDefault(t)?.NhietDo : null, r.PhanTramDuongMacDinh)).ToList();
    }

    /// <summary>Lưu và áp dụng ngay; tự lưu bản mới bên trong khi cần giữ định mức lịch sử.</summary>
    public Task<IResult> Save(long? id, RecipeInput input, HttpContext context) =>
        Change(input.MaThaoTac, "SAVE", new { id, input }, context, () => SaveCore(id, input, context));

    /// <summary>
    /// UC-ADM-02 — Lưu bảng nguyên liệu của công thức món theo các cấu hình.
    ///
    /// Hàm này được dùng để:
    /// 1. Tìm các cấu hình size/nhiệt độ của món cần lưu công thức.
    /// 2. Đọc lượng nguyên liệu đã nhập cho các cấu hình.
    /// 3. Gọi phần lưu công thức cho từng cấu hình trong cùng một thao tác.
    ///
    /// Thông tin cần truyền vào:
    /// - input: Món và các định lượng đã nhập trên giao diện.
    /// - context: Thông tin phiên và quyền của người đang thao tác.
    ///
    /// Sau khi xử lý: Các công thức của món được lưu hoặc toàn thao tác bị từ chối.
    /// </summary>
    public Task<IResult> SaveDish(RecipeDishInput input, HttpContext context) => Change(input.MaThaoTac, "SAVE_DISH", input, context, async () =>
    {
        if (input.CauHinh is null || input.CauHinh.Count is < 1 or > 100 ||
            input.CauHinh.Any(x => x.CongThuc is null || x.CongThuc.ThanhPhan is null || x.CongThuc.MaMonThem is not null || x.CongThuc.MaCauHinhMon is null) ||
            input.CauHinh.Select(x => x.CongThuc.MaCauHinhMon).Distinct().Count() != input.CauHinh.Count)
            throw new AdminRule(400, "Bảng công thức không hợp lệ hoặc trùng size/nhiệt độ.");
        var variants = await db.CauHinhMon.Where(v => v.MaMon == input.MaMon).Select(v => v.MaCauHinhMon).ToListAsync();
        if (variants.Count == 0 || !variants.Order().SequenceEqual(input.CauHinh.Select(x => x.CongThuc.MaCauHinhMon!.Value).Order()))
            throw new AdminRule(409, "Cấu hình món đã thay đổi. Mở lại bảng công thức để cập nhật đủ size/nóng lạnh.");
        var before = new List<string?>();
        (long Id, object Result, string? Before, string? Reason) first = default;
        foreach (var entry in input.CauHinh)
        {
            var temperature = await db.CauHinhMon.Where(v => v.MaCauHinhMon == entry.CongThuc.MaCauHinhMon).Select(v => v.NhietDo).SingleAsync();
            if (temperature == "HOT" && entry.CongThuc.ThanhPhan.Any(l => l.LoaiDinhMuc == "ICE"))
                throw new AdminRule(400, "Công thức nóng không dùng dòng theo % đá.");
            var result = await SaveCore(entry.MaCongThuc, entry.CongThuc, context);
            if (first.Id == 0) first = result;
            before.Add(result.Before);
        }
        return (first.Id, first.Result!, JsonSerializer.Serialize(before), null);
    });

    /// <summary>
    /// UC-ADM-02 — Lưu nguyên liệu và định lượng của một công thức.
    ///
    /// Hàm này được dùng để:
    /// 1. Kiểm tra công thức áp dụng cho cấu hình món hoặc topping nào.
    /// 2. Nếu sửa thì kiểm tra công thức hiện tại và bản dữ liệu mới nhất.
    /// 3. Kiểm tra mức đường mặc định, nguyên liệu, đơn vị và lượng dùng.
    /// 4. Lưu công thức và xử lý bản công thức đang áp dụng theo điều kiện trong hàm.
    ///
    /// Thông tin cần truyền vào:
    /// - id: Khóa công thức cần sửa; để trống nếu thêm công thức mới.
    /// - input: Cấu hình món hoặc topping, tên công thức, mức đường và danh sách nguyên liệu.
    /// - context: Thông tin phiên và quyền của người đang thao tác.
    ///
    /// Cần nhớ: Đơn đã đặt giữ lượng nguyên liệu đã chốt, không lấy công thức vừa sửa để tính lại đơn cũ.
    /// </summary>
    internal async Task<(long Id, object Result, string? Before, string? Reason)> SaveCore(long? id, RecipeInput input, HttpContext context)
    {

        // Bước 1: Công thức phải áp dụng cho đúng một cấu hình món hoặc một topping.
        if ((input.MaCauHinhMon is null) == (input.MaMonThem is null))
            throw new AdminRule(400, "Chọn đúng một cấu hình món hoặc topping.");
        if (input.MaCauHinhMon is { } variant && !await db.CauHinhMon.AnyAsync(x => x.MaCauHinhMon == variant) ||
            input.MaMonThem is { } topping && !await db.MonThem.AnyAsync(x => x.MaMonThem == topping))
            throw new AdminRule(400, "Cấu hình hoặc topping không tồn tại.");
        var siblings = await db.CongThuc.Where(x => x.MaCauHinhMon == input.MaCauHinhMon && x.MaMonThem == input.MaMonThem).ToListAsync();
        CongThuc? current = null;
        if (id is { } recipeId)
        {
            current = await Required(recipeId);
            AdminOperation.Version(input.PhienBanDuLieu, current.PhienBanDuLieu);
            if (current.MaCauHinhMon != input.MaCauHinhMon || current.MaMonThem != input.MaMonThem)
                throw new AdminRule(409, "Không đổi đối tượng áp dụng của công thức đã lưu.");
            if (current.TrangThai is not ("ACTIVE" or "DRAFT"))
                throw new AdminRule(409, "Công thức đã được thay thế. Tải lại trước khi sửa.");
        }
        else if (siblings.Any(x => x.TrangThai is "ACTIVE" or "DRAFT" or "SCHEDULED"))
            throw new AdminRule(409, "Đối tượng đã có công thức. Chọn sửa công thức hiện tại.");

        // Kiểm toàn bộ trước khi thay hiệu lực; lỗi không để món đang bán mất công thức cũ.
        if (input.PhanTramDuongMacDinh is not (0 or 25 or 50 or 75 or 100))
            throw new AdminRule(400, "Mức đường mặc định chỉ có 0%,25%,50%,75%,100%.");

        // Bước 2: Kiểm tra từng nguyên liệu và lượng dùng trước khi thay công thức đang áp dụng.
        var lines = await Convert(input.ThanhPhan);
        if (lines.Count == 0) throw new AdminRule(400, "Công thức phải có ít nhất một nguyên liệu.");
        var name = MenuValidation.Text(input.TenCongThuc, 150, "Tên công thức");
        var note = MenuValidation.Optional(input.GhiChu, 500);
        var before = current is null ? null : JsonSerializer.Serialize(new { CongThuc = current,
            ThanhPhan = await db.ChiTietCongThuc.AsNoTracking().Where(x => x.MaCongThuc == current.MaCongThuc).ToListAsync() });
        var now = DateTime.UtcNow;
        var actor = long.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        // Giữ nguyên dòng định mức đã công bố; chỉ đóng hiệu lực và tạo bản kế tiếp tự động.
        foreach (var old in siblings.Where(x => x.TrangThai is "ACTIVE" or "SCHEDULED"))
        {
            old.TrangThai = "RETIRED";
            if (old.HieuLucTu < now) old.HieuLucDen = now;
        }
        await db.SaveChangesAsync();
        var recipe = current?.TrangThai == "DRAFT" ? current : new CongThuc
        {
            MaCauHinhMon = input.MaCauHinhMon, MaMonThem = input.MaMonThem,
            SoPhienBan = siblings.Select(x => x.SoPhienBan).DefaultIfEmpty(0).Max() + 1,
            TrangThai = "DRAFT", MaQuanLyTao = actor
        };
        recipe.TenCongThuc = name; recipe.GhiChu = note; recipe.PhanTramDuongMacDinh = input.PhanTramDuongMacDinh;
        if (recipe.MaCongThuc == 0) { db.CongThuc.Add(recipe); await db.SaveChangesAsync(); }
        else
        {
            db.ChiTietCongThuc.RemoveRange(await db.ChiTietCongThuc.Where(x => x.MaCongThuc == recipe.MaCongThuc).ToListAsync());
            await db.SaveChangesAsync();
        }
        foreach (var line in lines) { line.MaCongThuc = recipe.MaCongThuc; db.ChiTietCongThuc.Add(line); }
        await db.SaveChangesAsync(); // Trigger chỉ cho ghi dòng khi header vẫn DRAFT.
        recipe.TrangThai = "ACTIVE"; recipe.HieuLucTu = now; recipe.HieuLucDen = null;
        recipe.MaQuanLyDuyet = actor; recipe.ThoiDiemCapNhat = now;
        await db.SaveChangesAsync();
        return Result(recipe, before);
    }

    /// <summary>Kiểm định mức nhập trực tiếp theo g/ml; khóa ghép gồm nguyên liệu và loại định mức.</summary>
    private async Task<List<ChiTietCongThuc>> Convert(IReadOnlyList<RecipeLineInput>? input)
    {
        if (input is null || input.Count > 200 || input.GroupBy(x => new { x.MaNguyenLieu, x.LoaiDinhMuc }).Any(g => g.Count() > 1)) throw new AdminRule(400, "Thành phần không hợp lệ hoặc trùng nguyên liệu/loại định mức.");
        var result = new List<ChiTietCongThuc>();
        foreach (var line in input)
        {
            if (line.DinhMuc <= 0 || line.DinhMuc != decimal.Round(line.DinhMuc, 4) || line.TyLeHaoHut != 0 || line.LoaiDinhMuc is not ("FIXED" or "SUGAR" or "ICE")) throw new AdminRule(400, "Lượng phải dương, tối đa 4 số lẻ. Nhập lượng thực dùng, không tính hao hụt riêng.");
            if (!await db.NguyenLieu.AnyAsync(x => x.MaNguyenLieu == line.MaNguyenLieu && x.DangSuDung)) throw new AdminRule(400, "Nguyên liệu/đơn vị không hoạt động.");
            var ingredient = await db.NguyenLieu.AsNoTracking().SingleAsync(x => x.MaNguyenLieu == line.MaNguyenLieu);
            if (line.MaDonVi != ingredient.MaDonViCoSo)
                throw new AdminRule(400, "Nhập định mức trực tiếp theo Gam/Mililit của nguyên liệu, không sử dụng đơn vị quy đổi.");
            var amount = line.DinhMuc;
            if (amount <= 0 || amount > 99999999999999.9999m || amount != decimal.Round(amount, 4)) throw new AdminRule(400, "Định mức sau quy đổi vượt độ chính xác 4 số lẻ; hãy nhập theo đơn vị cơ sở.");
            result.Add(new() { MaNguyenLieu = line.MaNguyenLieu, LoaiDinhMuc = line.LoaiDinhMuc, DinhMucCoSo = amount });
        }
        return result;
    }

    /// <summary>Đọc một công thức theo khóa; lỗi 404 là lỗi nghiệp vụ có thể hiển thị.</summary>
    private async Task<CongThuc> Required(long id) => await db.CongThuc.SingleOrDefaultAsync(x => x.MaCongThuc == id) ?? throw new AdminRule(404, "Không tìm thấy công thức.");
    /// <summary>Đóng gói receipt và snapshot trước thay đổi, không trả entity để lộ dữ liệu không cần thiết.</summary>
    private static (long, object, string?, string?) Result(CongThuc recipe, string? before) => (recipe.MaCongThuc, new AdminSaveReceipt(recipe.MaCongThuc, recipe.PhienBanDuLieu), before, null);
    /// <summary>Transaction dùng chung cho audit, chống trùng, BOM và sự kiện thay đổi khả dụng.</summary>
    private Task<IResult> Change(Guid operation, string action, object input, HttpContext context, Func<Task<(long Id, object Result, string? Before, string? Reason)>> change) =>
        AdminOperation.Execute(db, operation, "RECIPE_" + action, "CongThuc", input, context, async () =>
        {
            var result = await change();
            db.SuKienDongBo.Add(new() { MaSuKien = Guid.NewGuid(), MaThaoTac = operation, LoaiSuKien = "RECIPE_CHANGED", NoiDungJson = JsonSerializer.Serialize(new { result.Id }), PhatSinhLuc = DateTime.UtcNow });
            return result;
        });
}
