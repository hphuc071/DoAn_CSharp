using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using FourChang.Api.Shared.Audit;
using FourChang.Api.Shared.Customers;
using FourChang.Api.Shared.Operations;
using FourChang.Domain.Entities.Accounts;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.Customers;

/// <summary>Admin quản lý đúng bảng địa chỉ dùng chung với Web theo phạm vi được chủ dự án chấp thuận.</summary>
public sealed class CustomerAddressService(CafeDbContext db)
{
    /// <summary>Đọc hồ sơ và mọi địa chỉ của đúng khách; địa chỉ ngừng dùng vẫn giữ để bảo toàn lịch sử.</summary>
    private async Task<CustomerAddressBook> Book(TaiKhoanKhachHang user) => new(CustomerRules.View(user),
        await db.DiaChiKhachHang.AsNoTracking().Where(x => x.MaKhachHang == user.MaKhachHang)
            .OrderByDescending(x => x.DangSuDung).ThenByDescending(x => x.LaDiaChiMacDinh).ThenBy(x => x.MaDiaChi)
            .Select(x => new AdminCustomerAddress(x.MaDiaChi, x.NhanDiaChi, x.TenNguoiNhan, x.DienThoaiNguoiNhan, x.DiaChiChiTiet,
                x.ViDo, x.KinhDo, x.GhiChuGiaoHang, x.LaDiaChiMacDinh, x.DangSuDung, x.PhienBanDuLieu)).ToListAsync());

    /// <summary>Endpoint chỉ đọc; snapshot transaction tránh trộn version hồ sơ và sổ địa chỉ từ hai thời điểm.</summary>
    public async Task<IResult> Get(long owner)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var user = await db.TaiKhoanKhachHang.AsNoTracking().SingleOrDefaultAsync(x => x.MaKhachHang == owner);
        if (user is null) return Results.NotFound();
        var result = await Book(user); await tx.CommitAsync(); return Results.Ok(result);
    }

    /// <summary>Thêm/sửa địa chỉ của đúng khách, không sửa snapshot địa chỉ của bất kỳ đơn hàng nào.</summary>
    public Task<IResult> Save(long owner, long? id, SaveSelfAddress input, HttpContext context) => Execute(owner, input.MaThaoTac,
        "ADMIN_ADDRESS_SAVED", new { owner, id, input }, input.PhienBanHoSo, context, async user =>
    {
        Validate(input);
        var item = id is null ? new DiaChiKhachHang { MaKhachHang = owner, DangSuDung = true }
            : await Address(owner, id.Value, input.PhienBanDiaChi);
        if (input.LaDiaChiMacDinh) await ClearDefault(owner, item.MaDiaChi);
        item.NhanDiaChi = Clean(input.NhanDiaChi); item.TenNguoiNhan = input.TenNguoiNhan.Trim(); item.DienThoaiNguoiNhan = input.DienThoaiNguoiNhan.Trim();
        item.DiaChiChiTiet = input.DiaChiChiTiet.Trim(); item.GhiChuGiaoHang = Clean(input.GhiChuGiaoHang);
        item.ViDo = input.ViDo; item.KinhDo = input.KinhDo; item.LaDiaChiMacDinh = input.LaDiaChiMacDinh; item.ThoiDiemCapNhat = DateTime.UtcNow;
        if (id is null) db.DiaChiKhachHang.Add(item);
    });

    /// <summary>Ngừng dùng hoặc đặt mặc định có rowversion; không xóa cứng hoặc tự khôi phục bản đã ngừng.</summary>
    public Task<IResult> Act(long owner, long id, string action, AdminAddressAction input, HttpContext context) => Execute(owner, input.MaThaoTac,
        action == "default" ? "ADMIN_ADDRESS_DEFAULT" : "ADMIN_ADDRESS_DEACTIVATED", new { owner, id, action, input }, input.PhienBanHoSo, context, async user =>
    {
        var item = await Address(owner, id, input.PhienBanDiaChi);
        if (action == "default") { await ClearDefault(owner, id); item.LaDiaChiMacDinh = true; }
        else { item.DangSuDung = false; item.LaDiaChiMacDinh = false; }
        item.ThoiDiemCapNhat = DateTime.UtcNow;
    });

    /// <summary>Kiểm tra chủ sở hữu cả khi Admin nhập trực tiếp URL, chặn dùng mã địa chỉ của khách khác.</summary>
    private async Task<DiaChiKhachHang> Address(long owner, long id, byte[]? version)
    {
        var item = await db.DiaChiKhachHang.SingleOrDefaultAsync(x => x.MaDiaChi == id && x.MaKhachHang == owner && x.DangSuDung)
            ?? throw new AdminRule(404, "Địa chỉ không tồn tại, đã ngừng dùng hoặc không thuộc khách hàng này.");
        AdminOperation.Version(version, item.PhienBanDuLieu); return item;
    }

    /// <summary>Bỏ mặc định cũ trước để unique filtered index luôn hợp lệ; transaction bảo đảm không lưu nửa chừng.</summary>
    private async Task ClearDefault(long owner, long except)
    {
        var previous = await db.DiaChiKhachHang.Where(x => x.MaKhachHang == owner && x.MaDiaChi != except && x.DangSuDung && x.LaDiaChiMacDinh).ToListAsync();
        foreach (var item in previous) item.LaDiaChiMacDinh = false;
        await db.SaveChangesAsync();
    }

    /// <summary>Chuẩn hóa trường tùy chọn; trường rỗng không bị ghi thành chuỗi khoảng trắng.</summary>
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Cùng giới hạn với sổ địa chỉ Web và cột SQL; tọa độ phải có cặp và đúng miền hợp lệ.</summary>
    private static void Validate(SaveSelfAddress input)
    {
        if (string.IsNullOrWhiteSpace(input.TenNguoiNhan) || input.TenNguoiNhan.Trim().Length > 120 || input.DienThoaiNguoiNhan is null ||
            !Regex.IsMatch(input.DienThoaiNguoiNhan.Trim(), @"^[0-9]{10}$") || string.IsNullOrWhiteSpace(input.DiaChiChiTiet) ||
            input.DiaChiChiTiet.Trim().Length > 500 || input.NhanDiaChi?.Length > 50 || input.GhiChuGiaoHang?.Length > 500 ||
            input.ViDo is < -90 or > 90 || input.KinhDo is < -180 or > 180 || (input.ViDo is null) != (input.KinhDo is null))
            throw new AdminRule(400, "Kiểm tra tên người nhận, điện thoại, địa chỉ và độ dài các trường.");
    }

    /// <summary>Version hồ sơ dùng chung với Web; audit, thay địa chỉ và receipt lưu nguyên tử, lỗi nào cũng rollback.</summary>
    private async Task<IResult> Execute(long owner, Guid operation, string action, object input, byte[] version,
        HttpContext context, Func<TaiKhoanKhachHang, Task> mutate)
    {
        if (operation == Guid.Empty) return Results.Problem(statusCode: 400, detail: "Thiếu mã thao tác.");
        var actor = long.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var digest = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { action, input }));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var resource = "ADMIN_ADDRESS:" + operation.ToString("N");
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r<0 THROW 51090, 'Operation lock timeout', 1;");
        var old = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == operation);
        if (old is not null)
        {
            if (old.MaQuanLy != actor || old.LoaiDoiTuong != "DiaChiKhachHang" || old.HanhDong != action || old.MaBamYeuCau is null || !CryptographicOperations.FixedTimeEquals(old.MaBamYeuCau, digest))
                return Results.Problem(statusCode: 409, detail: "Mã thao tác đã dùng với nội dung khác.");
            return Results.Content(old.KetQuaDaChotJson!, "application/json");
        }
        try
        {
            var user = await db.TaiKhoanKhachHang.SingleOrDefaultAsync(x => x.MaKhachHang == owner) ?? throw new AdminRule(404, "Không tìm thấy khách hàng.");
            AdminOperation.Version(version, user.PhienBanDuLieu);
            var before = JsonSerializer.Serialize(await Book(user)); await mutate(user);
            user.ThoiDiemCapNhat = DateTime.UtcNow; db.Entry(user).Property(x => x.ThoiDiemCapNhat).IsModified = true;
            await db.SaveChangesAsync();
            var result = await Book(user); var json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var audit = AuditFactory.Create("ADMIN", actor, action, "DiaChiKhachHang", owner);
            audit.TenNguoiThucHienDaChot = context.User.Identity?.Name ?? "Quản lý"; audit.MaThaoTac = operation; audit.MaBamYeuCau = digest;
            audit.DuLieuTruocJson = before; audit.DuLieuSauJson = json; audit.KetQuaDaChotJson = json;
            db.NhatKyHeThong.Add(audit); await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Content(json, "application/json");
        }
        catch (AdminRule error) { return Results.Problem(statusCode: error.Status, detail: error.Message); }
    }

    /// <summary>Tra kết quả sau timeout, giới hạn đúng người thực hiện và đúng loại đối tượng.</summary>
    public async Task<IResult> Receipt(Guid id, HttpContext context)
    {
        var actor = long.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var audit = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == id && x.MaQuanLy == actor && x.LoaiDoiTuong == "DiaChiKhachHang");
        return audit?.KetQuaDaChotJson is { } json ? Results.Content(json, "application/json") : Results.NotFound();
    }
}
