using System.Text.Json;
using System.Security.Claims;
using FourChang.Api.Features.Admin.Menu;
using FourChang.Api.Shared.Operations;
using FourChang.Domain.Entities.Menu;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.Recipes;

/// <summary>ADM-04: quản trị phiên bản BOM; tính decimal và giữ đơn/giữ chỗ đã chốt bất biến.</summary>
public sealed class RecipeService(CafeDbContext db)
{
    /// <summary>Đọc cả bản nháp, lên lịch và lịch sử cùng định mức cơ sở để quản lý tra cứu.</summary>
    public async Task<IReadOnlyList<RecipeView>> Read()
    {
        var recipes = await db.CongThuc.AsNoTracking().OrderByDescending(x => x.MaCongThuc).ToListAsync();
        var lines = await (from l in db.ChiTietCongThuc.AsNoTracking()
                           join n in db.NguyenLieu.AsNoTracking() on l.MaNguyenLieu equals n.MaNguyenLieu
                           join u in db.DonViTinh.AsNoTracking() on n.MaDonViCoSo equals u.MaDonVi
                           select new { l.MaCongThuc, Line = new RecipeLineView(l.MaNguyenLieu, n.TenNguyenLieu, u.KyHieu, l.DinhMucCoSo, l.LoaiDinhMuc, l.TyLeHaoHut) }).ToListAsync();
        return recipes.Select(r => new RecipeView(r.MaCongThuc, r.MaCauHinhMon, r.MaMonThem, r.SoPhienBan, r.TenCongThuc,
            r.TrangThai, r.HieuLucTu, r.HieuLucDen, r.GhiChu, r.PhienBanDuLieu, lines.Where(l => l.MaCongThuc == r.MaCongThuc).Select(l => l.Line).ToList())).ToList();
    }

    /// <summary>Chỉ sửa bản nháp chưa được tham chiếu; số phiên bản do server cấp theo cấu hình/topping.</summary>
    public Task<IResult> Save(long? id, RecipeInput input, HttpContext context) => Change(input.MaThaoTac, "SAVE", new { id, input }, context, async () =>
    {
        if ((input.MaCauHinhMon is null) == (input.MaMonThem is null)) throw new AdminRule(400, "Chọn đúng một cấu hình món hoặc topping.");
        if (input.MaCauHinhMon is not null && !await db.CauHinhMon.AnyAsync(x => x.MaCauHinhMon == input.MaCauHinhMon) ||
            input.MaMonThem is not null && !await db.MonThem.AnyAsync(x => x.MaMonThem == input.MaMonThem)) throw new AdminRule(400, "Cấu hình hoặc topping không tồn tại.");
        var recipe = id is null ? new CongThuc() : await Required(id.Value);
        var before = id is null ? null : JsonSerializer.Serialize(new { CongThuc = recipe, ThanhPhan = await db.ChiTietCongThuc.AsNoTracking().Where(x => x.MaCongThuc == id).ToListAsync() });
        if (id is not null)
        {
            AdminOperation.Version(input.PhienBanDuLieu, recipe.PhienBanDuLieu);
            if (recipe.TrangThai != "DRAFT" || await Used(recipe.MaCongThuc)) throw new AdminRule(409, "Bản đã công bố/được dùng không sửa trực tiếp; tạo phiên bản mới.");
            if (recipe.MaCauHinhMon != input.MaCauHinhMon || recipe.MaMonThem != input.MaMonThem) throw new AdminRule(409, "Không đổi đối tượng của phiên bản đã tạo; tạo nháp mới.");
        }
        var converted = await Convert(input.ThanhPhan);
        recipe.TenCongThuc = MenuValidation.Text(input.TenCongThuc, 150, "Tên công thức");
        recipe.GhiChu = MenuValidation.Optional(input.GhiChu, 500);
        if (id is null)
        {
            recipe.MaCauHinhMon = input.MaCauHinhMon; recipe.MaMonThem = input.MaMonThem; recipe.TrangThai = "DRAFT";
            recipe.SoPhienBan = 1 + (await db.CongThuc.Where(x => x.MaCauHinhMon == input.MaCauHinhMon && x.MaMonThem == input.MaMonThem).MaxAsync(x => (int?)x.SoPhienBan) ?? 0);
            recipe.MaQuanLyTao = long.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            db.CongThuc.Add(recipe); await db.SaveChangesAsync();
        }
        else
        {
            db.ChiTietCongThuc.RemoveRange(await db.ChiTietCongThuc.Where(x => x.MaCongThuc == id).ToListAsync());
            await db.SaveChangesAsync(); // Xóa dòng nháp trong cùng transaction, tránh đụng khóa ghép khi thay định mức.
        }
        foreach (var line in converted) { line.MaCongThuc = recipe.MaCongThuc; db.ChiTietCongThuc.Add(line); }
        // Chạm header để rowversion thay đổi ngay cả khi chỉ sửa các thành phần.
        recipe.ThoiDiemCapNhat = DateTime.UtcNow;
        db.Entry(recipe).Property(x => x.ThoiDiemCapNhat).IsModified = true;
        await db.SaveChangesAsync();
        return Result(recipe, before);
    });

    /// <summary>Sao chép bản đã công bố thành nháp mới, giữ nguyên mã công thức và snapshot của đơn cũ.</summary>
    public Task<IResult> Clone(long id, AdminDeleteInput input, HttpContext context) => Change(input.MaThaoTac, "CLONE", new { id, input }, context, async () =>
    {
        // Receipt được kiểm tra trong Change trước rowversion, nên retry sau khi nguồn đã đổi vẫn trả bản đã tạo.
        var old = await Required(id); AdminOperation.Version(input.PhienBanDuLieu, old.PhienBanDuLieu);
        var source = await db.ChiTietCongThuc.AsNoTracking().Where(x => x.MaCongThuc == id).ToListAsync();
        var copy = new CongThuc
        {
            MaCauHinhMon = old.MaCauHinhMon,
            MaMonThem = old.MaMonThem,
            TenCongThuc = old.TenCongThuc,
            GhiChu = old.GhiChu,
            TrangThai = "DRAFT",
            MaQuanLyTao = long.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            SoPhienBan = 1 + (await db.CongThuc.Where(x => x.MaCauHinhMon == old.MaCauHinhMon && x.MaMonThem == old.MaMonThem).MaxAsync(x => (int?)x.SoPhienBan) ?? 0)
        };
        db.CongThuc.Add(copy); await db.SaveChangesAsync();
        foreach (var line in source)
            db.ChiTietCongThuc.Add(new() { MaCongThuc = copy.MaCongThuc, MaNguyenLieu = line.MaNguyenLieu, LoaiDinhMuc = line.LoaiDinhMuc, DinhMucCoSo = line.DinhMucCoSo, TyLeHaoHut = line.TyLeHaoHut });
        await db.SaveChangesAsync(); return Result(copy, null);
    });

    /// <summary>Công bố ngay hoặc lên lịch; đóng hiệu lực phiên bản trước mà không sửa định mức lịch sử.</summary>
    public Task<IResult> Publish(long id, RecipePublishInput input, HttpContext context) => Change(input.MaThaoTac, "PUBLISH", new { id, input }, context, async () =>
    {
        var recipe = await Required(id); AdminOperation.Version(input.PhienBanDuLieu, recipe.PhienBanDuLieu);
        if (recipe.TrangThai != "DRAFT") throw new AdminRule(409, "Chỉ công bố bản nháp; phiên bản đã công bố không dùng lại lệnh mới.");
        var start = MenuValidation.Utc(input.HieuLucTu);
        DateTime? end = input.HieuLucDen is { } value ? MenuValidation.Utc(value) : null;
        var now = DateTime.UtcNow;
        if (end <= start || start < now.AddMinutes(-1)) throw new AdminRule(400, "Hiệu lực kết thúc phải sau bắt đầu; không công bố hồi tố.");
        if (start < now) start = now;
        var lines = await db.ChiTietCongThuc.Where(x => x.MaCongThuc == id).ToListAsync();
        await ValidatePublish(recipe, lines);
        var siblings = await db.CongThuc.Where(x => x.MaCongThuc != id && x.MaCauHinhMon == recipe.MaCauHinhMon && x.MaMonThem == recipe.MaMonThem
            && (x.TrangThai == "ACTIVE" || x.TrangThai == "SCHEDULED")).ToListAsync();
        // Bản đang ACTIVE được kết thúc tại mốc thay thế; lịch tương lai chồng nhau phải sửa trước.
        if (siblings.Any(x => x.TrangThai == "SCHEDULED" && (x.HieuLucDen == null || x.HieuLucDen > start) && (end == null || x.HieuLucTu < end)))
            throw new AdminRule(409, "Lịch công thức trùng phiên bản đã lên lịch; hãy ngừng/đổi lịch trước.");
        var before = JsonSerializer.Serialize(recipe);
        foreach (var old in siblings.Where(x => x.TrangThai == "ACTIVE" && (x.HieuLucDen == null || x.HieuLucDen > start)))
        {
            old.HieuLucDen = start;
            if (start <= now) old.TrangThai = "RETIRED";
        }
        await db.SaveChangesAsync(); // Nhả unique ACTIVE trước khi kích hoạt phiên bản kế tiếp.
        recipe.HieuLucTu = start; recipe.HieuLucDen = end;
        recipe.TrangThai = start <= now ? "ACTIVE" : "SCHEDULED";
        recipe.MaQuanLyDuyet = long.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await db.SaveChangesAsync();
        return Result(recipe, before);
    });

    /// <summary>Ngừng bản có lịch sử bằng mốc hiệu lực; không sửa các snapshot hay tự hoàn nguyên liệu.</summary>
    public Task<IResult> Stop(long id, RecipeStopInput input, HttpContext context) => Change(input.MaThaoTac, "RETIRE", new { id, input }, context, async () =>
    {
        AdminOperation.Reason(input.LyDo);
        var recipe = await Required(id); AdminOperation.Version(input.PhienBanDuLieu, recipe.PhienBanDuLieu);
        if (recipe.TrangThai is not ("ACTIVE" or "SCHEDULED")) throw new AdminRule(409, "Chỉ ngừng phiên bản đang hiệu lực hoặc lên lịch.");
        var before = JsonSerializer.Serialize(recipe);
        recipe.TrangThai = "RETIRED";
        // Lịch chưa bắt đầu được hủy trạng thái, không đặt HieuLucDen trước HieuLucTu.
        if (recipe.HieuLucTu < DateTime.UtcNow) recipe.HieuLucDen = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (recipe.MaCongThuc, (object)new AdminSaveReceipt(recipe.MaCongThuc, recipe.PhienBanDuLieu), before, input.LyDo.Trim());
    });

    /// <summary>Chỉ xóa nháp chưa dùng; bản đã công bố được ngừng để giữ lịch sử.</summary>
    public Task<IResult> Delete(long id, AdminDeleteInput input, HttpContext context) => Change(input.MaThaoTac, "DELETE", new { id, input }, context, async () =>
    {
        var recipe = await Required(id); AdminOperation.Version(input.PhienBanDuLieu, recipe.PhienBanDuLieu);
        if (recipe.TrangThai != "DRAFT" || await Used(id)) throw new AdminRule(409, "Chỉ xóa nháp chưa được tham chiếu.");
        var before = JsonSerializer.Serialize(recipe);
        db.ChiTietCongThuc.RemoveRange(await db.ChiTietCongThuc.Where(x => x.MaCongThuc == id).ToListAsync());
        db.CongThuc.Remove(recipe); await db.SaveChangesAsync();
        return (id, (object)new AdminSaveReceipt(id, null, true), before, (string?)null);
    });

    /// <summary>Mô phỏng món và topping, gom nguyên liệu rồi làm tròn lên một lần; không giữ/trừ kho.</summary>
    public async Task<IReadOnlyList<RecipeDemand>> Simulate(RecipeSimulationInput input)
    {
        if (input.SoLy is < 1 or > 999 || !new[] { 0, 25, 50, 75, 100 }.Contains(input.PhanTramDuong) || !new[] { 0, 25, 50, 75, 100 }.Contains(input.PhanTramDa))
            throw new AdminRule(400, "Số ly hoặc mức đường/đá không hợp lệ.");
        var recipe = await Required(input.MaCongThuc);
        if (recipe.MaCauHinhMon is not null && !await db.TuyChonPhaCheHopLe.AnyAsync(x => x.MaCauHinhMon == recipe.MaCauHinhMon && x.DangSuDung && x.PhanTramDuong == input.PhanTramDuong && x.PhanTramDa == input.PhanTramDa))
            throw new AdminRule(400, "Cặp đường/đá không thuộc cấu hình; món nóng không tự được thêm đá.");
        var totals = new Dictionary<long, decimal>();
        await AddDemand(recipe.MaCongThuc, input.SoLy, input.PhanTramDuong, input.PhanTramDa, totals);
        if (input.MonThem is null || input.MonThem.Select(x => x.MaMonThem).Distinct().Count() != input.MonThem.Count) throw new AdminRule(400, "Danh sách topping không hợp lệ hoặc trùng.");
        foreach (var topping in input.MonThem)
        {
            if (recipe.MaCauHinhMon is null) throw new AdminRule(400, "Chỉ mô phỏng thêm topping khi chọn công thức cấu hình món.");
            var link = await db.CauHinhMonThem.AsNoTracking().SingleOrDefaultAsync(x => x.MaCauHinhMon == recipe.MaCauHinhMon && x.MaMonThem == topping.MaMonThem && x.DangSuDung);
            var top = await db.MonThem.AsNoTracking().SingleOrDefaultAsync(x => x.MaMonThem == topping.MaMonThem && x.DangSuDung);
            if (link is null || top is null || topping.SoPhanMoiLy < 1 || topping.SoPhanMoiLy > Math.Min(link.GioiHanMoiLy, top.GioiHanMacDinhMoiLy)) throw new AdminRule(400, "Topping không hỗ trợ hoặc vượt số phần mỗi ly.");
            var now = DateTime.UtcNow;
            var topRecipe = await db.CongThuc.AsNoTracking().SingleOrDefaultAsync(x => x.MaMonThem == topping.MaMonThem && x.TrangThai == "ACTIVE" && x.HieuLucTu <= now && (x.HieuLucDen == null || x.HieuLucDen > now))
                ?? throw new AdminRule(409, "Topping thiếu công thức hiệu lực.");
            await AddDemand(topRecipe.MaCongThuc, input.SoLy * topping.SoPhanMoiLy, input.PhanTramDuong, input.PhanTramDa, totals);
        }
        var data = await (from n in db.NguyenLieu.AsNoTracking()
                          join u in db.DonViTinh on n.MaDonViCoSo equals u.MaDonVi
                          join t in db.TonKho on n.MaNguyenLieu equals t.MaNguyenLieu into balance
                          from t in balance.DefaultIfEmpty()
                          select new { n.MaNguyenLieu, n.TenNguyenLieu, n.DangSuDung, u.KyHieu, Available = t == null ? (decimal?)null : t.TonThucTe - t.TongGiuCho - n.TonAnToan }).ToListAsync();
        return totals.Select(x =>
        {
            var n = data.Single(y => y.MaNguyenLieu == x.Key);
            var amount = decimal.Ceiling(x.Value * 10000m) / 10000m;
            return new RecipeDemand(x.Key, n.TenNguyenLieu, n.KyHieu, amount, n.Available, amount == 0 || n.DangSuDung && n.Available >= amount);
        }).ToList();
    }

    /// <summary>Áp dụng hệ số đúng loại dòng và hao hụt đúng một lần trước khi cộng chung nguyên liệu.</summary>
    private async Task AddDemand(long id, int quantity, int sugar, int ice, Dictionary<long, decimal> total)
    {
        var lines = await db.ChiTietCongThuc.AsNoTracking().Where(x => x.MaCongThuc == id).ToListAsync();
        if (lines.Count == 0) throw new AdminRule(409, "Công thức chưa có thành phần, không giả định tiêu hao bằng 0.");
        foreach (var line in lines)
        {
            var factor = line.LoaiDinhMuc == "SUGAR" ? sugar / 100m : line.LoaiDinhMuc == "ICE" ? ice / 100m : 1m;
            total[line.MaNguyenLieu] = total.GetValueOrDefault(line.MaNguyenLieu) + line.DinhMucCoSo * factor * quantity / (1m - line.TyLeHaoHut);
        }
    }

    /// <summary>Quy đổi định mức sang cơ sở đã khai báo; khóa ghép gồm nguyên liệu và loại định mức.</summary>
    private async Task<List<ChiTietCongThuc>> Convert(IReadOnlyList<RecipeLineInput>? input)
    {
        if (input is null || input.Count > 200 || input.GroupBy(x => new { x.MaNguyenLieu, x.LoaiDinhMuc }).Any(g => g.Count() > 1)) throw new AdminRule(400, "Thành phần không hợp lệ hoặc trùng nguyên liệu/loại định mức.");
        var result = new List<ChiTietCongThuc>();
        foreach (var line in input)
        {
            if (line.DinhMuc <= 0 || line.DinhMuc != decimal.Round(line.DinhMuc, 4) || line.TyLeHaoHut < 0 || line.TyLeHaoHut >= 1 || line.TyLeHaoHut != decimal.Round(line.TyLeHaoHut, 6) || line.LoaiDinhMuc is not ("FIXED" or "SUGAR" or "ICE")) throw new AdminRule(400, "Định mức dương, tối đa 4 số lẻ; hao hụt từ 0 đến dưới 1 và tối đa 6 số lẻ.");
            if (!await db.NguyenLieu.AnyAsync(x => x.MaNguyenLieu == line.MaNguyenLieu && x.DangSuDung) || !await db.DonViTinh.AnyAsync(x => x.MaDonVi == line.MaDonVi && x.DangSuDung)) throw new AdminRule(400, "Nguyên liệu/đơn vị không hoạt động.");
            var ingredient = await db.NguyenLieu.AsNoTracking().SingleAsync(x => x.MaNguyenLieu == line.MaNguyenLieu);
            if (line.MaDonVi != ingredient.MaDonViCoSo || !await db.DonViTinh.AnyAsync(x => x.MaDonVi == line.MaDonVi && (x.KyHieu == IngredientUnits.Gram || x.KyHieu == IngredientUnits.Milliliter)))
                throw new AdminRule(400, "Nhập định mức trực tiếp theo Gam/Mililit của nguyên liệu, không sử dụng đơn vị quy đổi.");
            var conversion = await db.QuyDoiDonViNguyenLieu.AsNoTracking().SingleOrDefaultAsync(x => x.MaNguyenLieu == line.MaNguyenLieu && x.MaDonVi == line.MaDonVi) ?? throw new AdminRule(400, "Thiếu quy đổi cho nguyên liệu và đơn vị đã chọn.");
            var amount = line.DinhMuc * conversion.HeSoVeDonViCoSo;
            if (amount <= 0 || amount > 99999999999999.9999m || amount != decimal.Round(amount, 4)) throw new AdminRule(400, "Định mức sau quy đổi vượt độ chính xác 4 số lẻ; hãy nhập theo đơn vị cơ sở.");
            result.Add(new() { MaNguyenLieu = line.MaNguyenLieu, LoaiDinhMuc = line.LoaiDinhMuc, DinhMucCoSo = amount, TyLeHaoHut = line.TyLeHaoHut });
        }
        return result;
    }

    /// <summary>Chỉ công bố khi thành phần, đơn vị và tập lựa chọn cấu trúc đầy đủ; tồn thiếu không sửa công thức.</summary>
    private async Task ValidatePublish(CongThuc recipe, List<ChiTietCongThuc> lines)
    {
        if (lines.Count == 0) throw new AdminRule(409, "Công thức thiếu thành phần.");
        foreach (var line in lines)
        {
            var n = await db.NguyenLieu.AsNoTracking().SingleOrDefaultAsync(x => x.MaNguyenLieu == line.MaNguyenLieu && x.DangSuDung);
            if (n is null || !await db.DonViTinh.AnyAsync(x => x.MaDonVi == n.MaDonViCoSo && x.DangSuDung) ||
                !await db.QuyDoiDonViNguyenLieu.AnyAsync(x => x.MaNguyenLieu == n.MaNguyenLieu && x.MaDonVi == n.MaDonViCoSo && x.HeSoVeDonViCoSo == 1)) throw new AdminRule(409, "Nguyên liệu hoặc quy đổi cơ sở không hợp lệ.");
        }
        if (recipe.MaCauHinhMon is not null && !await db.TuyChonPhaCheHopLe.AnyAsync(x => x.MaCauHinhMon == recipe.MaCauHinhMon && x.DangSuDung)) throw new AdminRule(409, "Cấu hình chưa có tập đường/đá hợp lệ.");
    }

    /// <summary>Đọc một công thức theo khóa; lỗi 404 là lỗi nghiệp vụ có thể hiển thị.</summary>
    private async Task<CongThuc> Required(long id) => await db.CongThuc.SingleOrDefaultAsync(x => x.MaCongThuc == id) ?? throw new AdminRule(404, "Không tìm thấy công thức.");
    /// <summary>Kiểm tra cả món chính và topping đã chốt để ngăn sửa/xóa công thức lịch sử.</summary>
    private async Task<bool> Used(long id) => await db.ChiTietDonHang.AnyAsync(x => x.MaCongThuc == id) || await db.MonThemChiTietDonHang.AnyAsync(x => x.MaCongThuc == id);
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
