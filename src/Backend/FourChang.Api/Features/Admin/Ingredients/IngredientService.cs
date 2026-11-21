using FourChang.Domain.Shared;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using FourChang.Api.Features.Admin.Menu;
using FourChang.Api.Shared.Operations;
using FourChang.Domain.Entities.Inventory;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.Ingredients;

/// <summary>ADM-03: quản trị nguyên liệu với đơn vị g/ml cố định; mọi lệnh giữ tồn và lịch sử chứng từ nguyên vẹn.</summary>
public sealed class IngredientService(CafeDbContext db)
{
    /// <summary>Đọc hồ sơ và tham chiếu thực tế để khóa đổi đơn vị sau khi phát sinh dữ liệu.</summary>
    public async Task<IngredientSnapshot> Read()
    {
        var units = new[] { new UnitView(1,"g","Gam",true,[]), new UnitView(2,"ml","Mililit",true,[]) };
        var ingredients = await db.NguyenLieu.AsNoTracking().OrderBy(x => x.MaNguyenLieu).ToListAsync();
        var result = new List<IngredientView>();
        foreach (var item in ingredients)
            result.Add(new(item.MaNguyenLieu, item.TenNguyenLieu, item.MaDonViCoSo, item.TonAnToan, item.NguongCanhBao,
                item.MoTa, item.DangSuDung, item.PhienBanDuLieu,
                Array.Empty<ConversionInput>(),
                await References(item.MaNguyenLieu)));
        return new(result, units);
    }

    /// <summary>Lưu nguyên liệu cùng transaction/audit/outbox; không nhận số tồn từ client.</summary>
    public Task<IResult> Save(long? id, IngredientInput input, HttpContext context) => Change(input.MaThaoTac, "SAVE", new { id, input }, context, async () =>
    {
        var item = id is null ? new NguyenLieu() : await db.NguyenLieu.SingleOrDefaultAsync(x => x.MaNguyenLieu == id)
            ?? throw new AdminRule(404, "Không tìm thấy nguyên liệu.");
        var before = id is null ? null : JsonSerializer.Serialize(item);
        if (id is not null) AdminOperation.Version(input.PhienBanDuLieu, item.PhienBanDuLieu);
        var name = MenuValidation.Text(input.TenNguyenLieu, 150, "Tên nguyên liệu");
        if (await db.NguyenLieu.AnyAsync(x => x.TenNguyenLieu == name && x.MaNguyenLieu != item.MaNguyenLieu))
            throw new AdminRule(409, "Tên nguyên liệu đã tồn tại; hãy kiểm tra để tránh tạo trùng.");
        if (input.NguongCanhBao < 0 || !Scale4(input.NguongCanhBao))
            throw new AdminRule(400, "Ngưỡng cảnh báo không âm, tối đa 4 số thập phân.");
        if (input.MaDonViCoSo is not (1 or 2)) throw new AdminRule(400, "Chỉ dùng hai đơn vị mặc định đang hoạt động: Gam (g) hoặc Mililit (ml).");
        if (id is not null && item.MaDonViCoSo != input.MaDonViCoSo && await References(id.Value) > 0)
            throw new AdminRule(409, "Không đổi đơn vị cơ sở sau khi đã phát sinh BOM, tồn hoặc giao dịch.");
        // Tương thích payload cũ chỉ khi hệ số cơ sở bằng 1; không lưu thêm bảng quy đổi.
        if (input.QuyDoi is null || input.QuyDoi.Count > 1 || input.QuyDoi.Any(x => x.MaDonVi != input.MaDonViCoSo || x.HeSoVeDonViCoSo != 1))
            throw new AdminRule(400, "Không khai báo quy đổi: chỉ dùng chính đơn vị cơ sở với hệ số 1.");
        // Xác nhận đúng danh sách đã xem; mọi thay đổi công thức sau đó yêu cầu người dùng xem lại.
        if (id is not null && item.DangSuDung && !input.DangSuDung)
        {
            var impact = await Impact(id.Value);
            if (!string.Equals(input.PhienBanAnhHuong, impact.PhienBanAnhHuong, StringComparison.Ordinal))
                throw new AdminRule(409, "Cần xem và xác nhận ảnh hưởng hiện tại trước khi ngừng nguyên liệu.");
        }
        item.TenNguyenLieu = name; item.MaDonViCoSo = input.MaDonViCoSo;
        // Cột cũ vẫn tồn tại để tương thích schema; UC mới chỉ quản trị một ngưỡng cảnh báo.
        // Đặt bằng 0 trong thao tác lưu hồ sơ, không cập nhật hàng loạt hay thay đổi số dư kho.
        item.TonAnToan = 0; item.NguongCanhBao = input.NguongCanhBao;
        item.MoTa = MenuValidation.Optional(input.MoTa, 500); item.DangSuDung = input.DangSuDung;
        if (id is null) db.NguyenLieu.Add(item);
        await db.SaveChangesAsync();
        return (item.MaNguyenLieu, (object)new AdminSaveReceipt(item.MaNguyenLieu, item.PhienBanDuLieu), before, (string?)null);
    });

    /// <summary>UC tinh gọn chỉ ngừng/kích hoạt lại; không xóa hồ sơ hoặc lịch sử qua client cũ.</summary>
    public Task<IResult> Delete(long id, AdminDeleteInput input, HttpContext context) => Task.FromResult<IResult>(
        Results.Problem(statusCode: 409, detail: "Không xóa nguyên liệu. Chọn Ngừng sử dụng để giữ tồn kho và lịch sử."));

    /// <summary>Chụp công thức đang áp dụng/lên lịch và phiên bản để xác nhận ảnh hưởng trước khi ngừng.</summary>
    public async Task<IngredientImpact> Impact(long id)
    {
        var ingredient = await db.NguyenLieu.AsNoTracking().SingleOrDefaultAsync(n => n.MaNguyenLieu == id)
            ?? throw new AdminRule(404, "Không tìm thấy nguyên liệu.");
        var now = DateTime.UtcNow;
        var dependencies = await (from line in db.ChiTietCongThuc.AsNoTracking()
                                  join recipe in db.CongThuc.AsNoTracking() on line.MaCongThuc equals recipe.MaCongThuc
                                  where line.MaNguyenLieu == id && (recipe.TrangThai == "ACTIVE" || recipe.TrangThai == "SCHEDULED")
                                      && (recipe.HieuLucDen == null || recipe.HieuLucDen > now)
                                  orderby recipe.MaCongThuc, line.LoaiDinhMuc
                                  select new { recipe.MaCongThuc, recipe.MaCauHinhMon, recipe.MaMonThem,
                                      RecipeVersion = recipe.PhienBanDuLieu, LineVersion = line.PhienBanDuLieu }).ToListAsync();
        var variantIds = dependencies.Where(x => x.MaCauHinhMon.HasValue).Select(x => x.MaCauHinhMon!.Value).Distinct().ToArray();
        var variants = await (from variant in db.CauHinhMon.AsNoTracking()
                              join dish in db.Mon.AsNoTracking() on variant.MaMon equals dish.MaMon
                              where variantIds.Contains(variant.MaCauHinhMon)
                              select new { variant.MaCauHinhMon, dish.TenMon, variant.KichCo, variant.NhietDo }).ToListAsync();
        var toppingIds = dependencies.Where(x => x.MaMonThem.HasValue).Select(x => x.MaMonThem!.Value).Distinct().ToArray();
        var toppings = await db.MonThem.AsNoTracking().Where(t => toppingIds.Contains(t.MaMonThem)).ToListAsync();
        var labels = variants.Select(v => $"{v.TenMon} · {v.KichCo} · {(v.NhietDo == "HOT" ? "Nóng" : "Lạnh")}")
            .Concat(toppings.Select(t => "Topping: " + t.TenMonThem)).Distinct().Order().ToList();
        var fingerprint = JsonSerializer.Serialize(new { id, ingredient.PhienBanDuLieu, dependencies });
        return new(id, labels, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint))));
    }

    /// <summary>Giữ route tương thích nhưng từ chối quản lý đơn vị: Gam/Mililit là danh mục cố định.</summary>
    public Task<IResult> Unit(long? id, UnitInput input, HttpContext context) => FixedUnitsOnly();

    /// <summary>Không xóa đơn vị mặc định hoặc đơn vị có lịch sử, kể cả client cũ còn gửi lệnh.</summary>
    public Task<IResult> DeleteUnit(long id, AdminDeleteInput input, HttpContext context) => FixedUnitsOnly();

    /// <summary>Trả lỗi nghiệp vụ rõ ràng, không ghi dữ liệu/audit thành công cho tác vụ đã bỏ.</summary>
    private static Task<IResult> FixedUnitsOnly() => Task.FromResult<IResult>(Results.Problem(statusCode: 409,
        detail: "Đơn vị đã cố định là Gam (g) và Mililit (ml). Không còn chức năng thêm, sửa hoặc xóa đơn vị tính."));

    /// <summary>Kiểm tra độ chính xác trước khi SQL làm tròn, tránh âm thầm thay lượng người quản lý nhập.</summary>
    private static bool Scale4(decimal value) => decimal.Round(value, 4) == value && value <= 99999999999999.9999m;

    /// <summary>Đếm tham chiếu công thức, kho và đơn hàng để bảo vệ ý nghĩa đơn vị cơ sở đã sử dụng.</summary>
    private async Task<long> References(long id) => await db.ChiTietCongThuc.LongCountAsync(x => x.MaNguyenLieu == id)
        + await db.TonKho.LongCountAsync(x => x.MaNguyenLieu == id) + await db.ChiTietPhieuKho.LongCountAsync(x => x.MaNguyenLieu == id)
        + await db.NguyenLieuChiTietDonHang.LongCountAsync(x => x.MaNguyenLieu == id) + await db.BienDongKho.LongCountAsync(x => x.MaNguyenLieu == id);

    /// <summary>Ghi sự kiện thay đổi khả dụng cùng giao dịch; lỗi audit/outbox làm rollback dữ liệu danh mục.</summary>
    private Task<IResult> Change(Guid operation, string action, object input, HttpContext context,
        Func<Task<(long Id, object Result, string? Before, string? Reason)>> change) =>
        AdminOperation.Execute(db, operation, "INGREDIENT_" + action, "NguyenLieu", input, context, async () =>
        {
            var result = await change();
            db.SuKienDongBo.Add(new() { MaSuKien = Guid.NewGuid(), MaThaoTac = operation, LoaiSuKien = "INVENTORY_CONFIGURATION_CHANGED", NoiDungJson = JsonSerializer.Serialize(new { result.Id }), PhatSinhLuc = DateTime.UtcNow });
            return result;
        });
}
