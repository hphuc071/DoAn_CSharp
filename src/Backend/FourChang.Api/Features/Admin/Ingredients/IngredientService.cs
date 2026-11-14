using FourChang.Domain.Shared;
using System.Text.Json;
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
    /// <summary>Đọc danh mục và tham chiếu thực tế để UI giải thích điều kiện ngừng/xóa.</summary>
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
        if (input.TonAnToan < 0 || input.NguongCanhBao < input.TonAnToan || !Scale4(input.TonAnToan) || !Scale4(input.NguongCanhBao))
            throw new AdminRule(400, "Ngưỡng không âm, tối đa 4 số lẻ; cảnh báo phải lớn hơn hoặc bằng tồn an toàn.");
        if (input.MaDonViCoSo is not (1 or 2)) throw new AdminRule(400, "Chỉ dùng hai đơn vị mặc định đang hoạt động: Gam (g) hoặc Mililit (ml).");
        if (id is not null && item.MaDonViCoSo != input.MaDonViCoSo && await References(id.Value) > 0)
            throw new AdminRule(409, "Không đổi đơn vị cơ sở sau khi đã phát sinh BOM, tồn hoặc giao dịch.");
        // Tương thích payload cũ chỉ khi hệ số cơ sở bằng 1; không lưu thêm bảng quy đổi.
        if (input.QuyDoi is null || input.QuyDoi.Count > 1 || input.QuyDoi.Any(x => x.MaDonVi != input.MaDonViCoSo || x.HeSoVeDonViCoSo != 1))
            throw new AdminRule(400, "Không khai báo quy đổi: chỉ dùng chính đơn vị cơ sở với hệ số 1.");
        if (!input.DangSuDung && await IsRequired(id))
            throw new AdminRule(409, "Nguyên liệu còn trong BOM hiệu lực/lên lịch hoặc giữ chỗ; xử lý ảnh hưởng trước khi ngừng.");
        item.TenNguyenLieu = name; item.MaDonViCoSo = input.MaDonViCoSo;
        item.TonAnToan = input.TonAnToan; item.NguongCanhBao = input.NguongCanhBao;
        item.MoTa = MenuValidation.Optional(input.MoTa, 500); item.DangSuDung = input.DangSuDung;
        if (id is null) db.NguyenLieu.Add(item);
        await db.SaveChangesAsync();
        return (item.MaNguyenLieu, (object)new AdminSaveReceipt(item.MaNguyenLieu, item.PhienBanDuLieu), before, (string?)null);
    });

    /// <summary>Xóa nguyên liệu chưa có dữ liệu nghiệp vụ; chỉ xóa cấu hình quy đổi sở hữu bởi chính nguyên liệu đó.</summary>
    public Task<IResult> Delete(long id, AdminDeleteInput input, HttpContext context) => Change(input.MaThaoTac, "DELETE", new { id, input }, context, async () =>
    {
        var item = await db.NguyenLieu.SingleOrDefaultAsync(x => x.MaNguyenLieu == id) ?? throw new AdminRule(404, "Không tìm thấy nguyên liệu.");
        AdminOperation.Version(input.PhienBanDuLieu, item.PhienBanDuLieu);
        if (await References(id) > 0) throw new AdminRule(409, "Nguyên liệu có BOM/tồn/giao dịch, chỉ được ngừng khi đã xử lý ảnh hưởng.");
        db.NguyenLieu.Remove(item); await db.SaveChangesAsync();
        return (id, (object)new AdminSaveReceipt(id, null, true), JsonSerializer.Serialize(item), (string?)null);
    });

    /// <summary>Giữ route tương thích nhưng từ chối quản lý đơn vị: Gam/Mililit là danh mục cố định.</summary>
    public Task<IResult> Unit(long? id, UnitInput input, HttpContext context) => FixedUnitsOnly();

    /// <summary>Không xóa đơn vị mặc định hoặc đơn vị có lịch sử, kể cả client cũ còn gửi lệnh.</summary>
    public Task<IResult> DeleteUnit(long id, AdminDeleteInput input, HttpContext context) => FixedUnitsOnly();

    /// <summary>Trả lỗi nghiệp vụ rõ ràng, không ghi dữ liệu/audit thành công cho tác vụ đã bỏ.</summary>
    private static Task<IResult> FixedUnitsOnly() => Task.FromResult<IResult>(Results.Problem(statusCode: 409,
        detail: "Đơn vị đã cố định là Gam (g) và Mililit (ml). Không còn chức năng thêm, sửa hoặc xóa đơn vị tính."));

    /// <summary>Kiểm tra độ chính xác trước khi SQL làm tròn, tránh âm thầm thay lượng người quản lý nhập.</summary>
    private static bool Scale4(decimal value) => decimal.Round(value, 4) == value && value <= 99999999999999.9999m;

    /// <summary>Tổng tham chiếu nghiệp vụ; quy đổi sở hữu không khiến mọi nguyên liệu mới đều mất quyền xóa.</summary>
    private async Task<long> References(long id) => await db.ChiTietCongThuc.LongCountAsync(x => x.MaNguyenLieu == id)
        + await db.TonKho.LongCountAsync(x => x.MaNguyenLieu == id) + await db.ChiTietPhieuKho.LongCountAsync(x => x.MaNguyenLieu == id)
        + await db.NguyenLieuChiTietDonHang.LongCountAsync(x => x.MaNguyenLieu == id) + await db.BienDongKho.LongCountAsync(x => x.MaNguyenLieu == id);

    /// <summary>BOM tương lai hoặc giữ chỗ chưa giải phóng phải được xử lý trước ngừng nguyên liệu.</summary>
    private async Task<bool> IsRequired(long? id) => id is not null && (await (from l in db.ChiTietCongThuc
                                                                               join r in db.CongThuc on l.MaCongThuc equals r.MaCongThuc
                                                                               where l.MaNguyenLieu == id && (r.TrangThai == "ACTIVE" || r.TrangThai == "SCHEDULED") && (r.HieuLucDen == null || r.HieuLucDen > DateTime.UtcNow)
                                                                               select l).AnyAsync()
        || await db.NguyenLieuChiTietDonHang.AnyAsync(x => x.MaNguyenLieu == id && x.SoLuongGiuCho > x.SoLuongDaTieuHao + x.SoLuongDaGiaiPhong));

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
