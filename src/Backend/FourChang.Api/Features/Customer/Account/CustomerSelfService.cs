using FourChang.Api.Shared.Audit;
using FourChang.Api.Shared.Customers;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using FourChang.Domain.Entities.Accounts;
using FourChang.Domain.Entities.Audit;
using FourChang.Infrastructure.Database.Context;
using FourChang.Infrastructure.Security;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.EntityFrameworkCore;
namespace FourChang.Api.Features.Customer.Account;

// UC-WEB-03: chỉ sửa hồ sơ của Customer đang đăng nhập. Admin vẫn dùng service ADM-01 riêng.
// Mỗi lệnh dùng version + transaction + audit; không thêm bảng, không sửa snapshot của đơn cũ.
public sealed class CustomerSelfService(CafeDbContext db)
{
    private sealed class Rule(int code, string message) : Exception(message) { public int Code => code; }
    private static long Owner(HttpContext c) => long.Parse(c.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private static void Version(byte[]? input, byte[] current) { if (input is not { Length: 8 }) throw new Rule(400, "Thiếu phiên bản dữ liệu."); if (!input.SequenceEqual(current)) throw new Rule(409, "Dữ liệu đã thay đổi. Tải lại để đối chiếu trước khi lưu."); }
    private async Task<CustomerSelfProfile> Profile(long id)
    {
        var u = await db.TaiKhoanKhachHang.AsNoTracking().SingleAsync(x => x.MaKhachHang == id);
        var addresses = await db.DiaChiKhachHang.AsNoTracking().Where(x => x.MaKhachHang == id && x.DangSuDung).OrderByDescending(x => x.LaDiaChiMacDinh).ThenBy(x => x.MaDiaChi)
            .Select(x => new CustomerAddress(x.MaDiaChi, x.NhanDiaChi, x.TenNguoiNhan, x.DienThoaiNguoiNhan, x.DiaChiChiTiet, x.ViDo, x.KinhDo, x.GhiChuGiaoHang, x.LaDiaChiMacDinh, x.PhienBanDuLieu)).ToListAsync();
        return new(u.MaKhachHang, u.TenDangNhapChuanHoa, u.HoTen, u.EmailChuanHoa, u.SoDienThoaiChuanHoa, u.PhienBanDuLieu, addresses);
    }
    public async Task<IResult> Get(HttpContext context) => Results.Ok(await Profile(Owner(context)));
    public Task<IResult> Save(SaveSelfProfile input, HttpContext context) => Execute(input.MaThaoTac, "CUSTOMER_PROFILE_UPDATED", input, input.PhienBanDuLieu, context, async u =>
    {
        var invalid = CustomerRules.Validate(new(input.MaThaoTac, u.TenDangNhapChuanHoa, input.HoTen, input.Email, input.SoDienThoai, null), false);
        if (invalid is not null) throw new Rule(400, invalid);
        var email = CustomerRules.Normalize(input.Email, true); var phone = CustomerRules.Normalize(input.SoDienThoai);
        if (await db.TaiKhoanKhachHang.AnyAsync(x => x.MaKhachHang != u.MaKhachHang && (email != null && x.EmailChuanHoa == email || phone != null && x.SoDienThoaiChuanHoa == phone))) throw new Rule(409, "Email hoặc số điện thoại đã được sử dụng.");
        u.HoTen = input.HoTen.Trim(); u.EmailChuanHoa = email; u.SoDienThoaiChuanHoa = phone;
    });
    public Task<IResult> Address(long? id, SaveSelfAddress input, HttpContext context) => Execute(input.MaThaoTac, "CUSTOMER_ADDRESS_SAVED", new { id, input }, input.PhienBanHoSo, context, async u =>
    {
        if (string.IsNullOrWhiteSpace(input.TenNguoiNhan) || input.TenNguoiNhan.Trim().Length > 120 || input.DienThoaiNguoiNhan is null || !Regex.IsMatch(input.DienThoaiNguoiNhan.Trim(), @"^\+?[0-9]{9,15}$") || string.IsNullOrWhiteSpace(input.DiaChiChiTiet) || input.DiaChiChiTiet.Trim().Length > 500 || input.NhanDiaChi?.Length > 50 || input.GhiChuGiaoHang?.Length > 500 || input.ViDo is < -90 or > 90 || input.KinhDo is < -180 or > 180 || (input.ViDo is null) != (input.KinhDo is null)) throw new Rule(400, "Tên người nhận, số điện thoại, địa chỉ hoặc tọa độ không hợp lệ.");
        var a = id is null ? new DiaChiKhachHang { MaKhachHang = u.MaKhachHang, DangSuDung = true } : await db.DiaChiKhachHang.SingleOrDefaultAsync(x => x.MaDiaChi == id && x.MaKhachHang == u.MaKhachHang && x.DangSuDung) ?? throw new Rule(404, "Không tìm thấy địa chỉ của bạn.");
        if (id is not null) Version(input.PhienBanDiaChi, a.PhienBanDuLieu);
        if (input.LaDiaChiMacDinh)
        {
            // Bỏ cờ mặc định cũ trước để không vi phạm unique filtered index trong cùng transaction.
            var previous = await db.DiaChiKhachHang.Where(x => x.MaKhachHang == u.MaKhachHang && x.DangSuDung && x.LaDiaChiMacDinh && x.MaDiaChi != a.MaDiaChi).ToListAsync();
            foreach (var item in previous) item.LaDiaChiMacDinh = false; await db.SaveChangesAsync();
        }
        a.NhanDiaChi = CustomerRules.Normalize(input.NhanDiaChi); a.TenNguoiNhan = input.TenNguoiNhan.Trim(); a.DienThoaiNguoiNhan = input.DienThoaiNguoiNhan.Trim(); a.DiaChiChiTiet = input.DiaChiChiTiet.Trim(); a.ViDo = input.ViDo; a.KinhDo = input.KinhDo; a.GhiChuGiaoHang = CustomerRules.Normalize(input.GhiChuGiaoHang); a.LaDiaChiMacDinh = input.LaDiaChiMacDinh;
        if (id is null) db.DiaChiKhachHang.Add(a);
    });
    public Task<IResult> Remove(long id, RemoveSelfAddress input, HttpContext context) => Execute(input.MaThaoTac, "CUSTOMER_ADDRESS_REMOVED", new { id, input }, input.PhienBanHoSo, context, async u =>
    {
        var a = await db.DiaChiKhachHang.SingleOrDefaultAsync(x => x.MaDiaChi == id && x.MaKhachHang == u.MaKhachHang && x.DangSuDung) ?? throw new Rule(404, "Không tìm thấy địa chỉ của bạn."); Version(input.PhienBanDiaChi, a.PhienBanDuLieu);
        // Bỏ địa chỉ khỏi sổ bằng trạng thái có sẵn; giữ bản ghi và snapshot để không phá lịch sử đơn.
        a.DangSuDung = false; a.LaDiaChiMacDinh = false;
    });
    public Task<IResult> Password(ChangeSelfPassword input, HttpContext context) => Execute(input.MaThaoTac, "CUSTOMER_PASSWORD_CHANGED", input, input.PhienBanDuLieu, context, async u =>
    {
        if (input.MatKhauHienTai is null || !CustomerRules.ValidPassword(input.MatKhauMoi) || input.MatKhauMoi != input.XacNhanMatKhau) throw new Rule(400, "Mật khẩu mới phải có 10–128 ký tự và xác nhận khớp.");
        if (!Passwords.Verify(input.MatKhauHienTai, u.MatKhauBam))
        {
            // E3: lưu sự kiện thất bại an toàn, không ghi mật khẩu hoặc digest mật khẩu hiện tại.
            var failed = AuditFactory.Create("CUSTOMER", u.MaKhachHang, "CUSTOMER_PASSWORD_DENIED", "TaiKhoanKhachHang", u.MaKhachHang); failed.KetQua = "DENIED"; db.NhatKyHeThong.Add(failed); await db.SaveChangesAsync();
            throw new Rule(400, "Mật khẩu hiện tại không đúng.");
        }
        if (Passwords.Verify(input.MatKhauMoi, u.MatKhauBam)) throw new Rule(400, "Mật khẩu mới phải khác mật khẩu hiện tại.");
        u.MatKhauBam = Passwords.Hash(input.MatKhauMoi); u.BatBuocDoiMatKhau = false; u.SoLanDangNhapSai = 0; u.KhoaDenLuc = null;
        var sessions = await db.PhienDangNhap.Where(x => x.MaKhachHang == u.MaKhachHang && x.ThuHoiLuc == null).ToListAsync();
        foreach (var s in sessions) { s.ThuHoiLuc = DateTime.UtcNow; s.LyDoThuHoi = "CUSTOMER_PASSWORD_CHANGED"; }
    }, password: true);
    // Cùng giao dịch và receipt như mật khẩu thường, nhưng chỉ nhận phiên hạn chế và không trả hồ sơ.
    public Task<IResult> TemporaryPassword(ChangeTemporaryPassword input, HttpContext context) => Execute(input.MaThaoTac, "CUSTOMER_TEMPORARY_PASSWORD_CHANGED", input, input.PhienBanDuLieu, context, async u =>
    {
        if (!CustomerRules.ValidPassword(input.MatKhauMoi) || input.MatKhauMoi != input.XacNhanMatKhau) throw new Rule(400, "Mật khẩu mới phải có 10–128 ký tự và xác nhận khớp.");
        if (Passwords.Verify(input.MatKhauMoi, u.MatKhauBam)) throw new Rule(400, "Mật khẩu mới phải khác mật khẩu tạm.");
        u.MatKhauBam = Passwords.Hash(input.MatKhauMoi); u.BatBuocDoiMatKhau = false; u.SoLanDangNhapSai = 0; u.KhoaDenLuc = null;
        var sessions = await db.PhienDangNhap.Where(x => x.MaKhachHang == u.MaKhachHang && x.ThuHoiLuc == null).ToListAsync();
        foreach (var session in sessions) { session.ThuHoiLuc = DateTime.UtcNow; session.LyDoThuHoi = "TEMPORARY_PASSWORD_CHANGED"; }
    }, password: true, temporary: true);
    private async Task<IResult> Execute(Guid operation, string action, object input, byte[] version, HttpContext context, Func<TaiKhoanKhachHang, Task> mutate, bool password = false, bool temporary = false)
    {
        if (operation == Guid.Empty) return Results.Problem(detail: "Thiếu mã thao tác.", statusCode: 400);
        var owner = Owner(context); var digest = CustomerAccessService.Digest(new { owner, action, input });
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable); await CustomerAccessService.Lock(db, operation);
        var old = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == operation);
        if (old is not null)
        {
            if (old.MaKhachHang != owner || old.HanhDong != action || old.MaBamYeuCau is null || !CryptographicOperations.FixedTimeEquals(old.MaBamYeuCau, digest)) return Results.Problem(detail: "Mã thao tác đã dùng với nội dung khác.", statusCode: 409);
            return Results.Ok(JsonSerializer.Deserialize<CustomerSelfReceipt>(old.KetQuaDaChotJson!));
        }
        try
        {
            var user = await db.TaiKhoanKhachHang.SingleAsync(x => x.MaKhachHang == owner); Version(version, user.PhienBanDuLieu);
            if (user.TrangThai != "ACTIVE" || (temporary ? !user.BatBuocDoiMatKhau : user.BatBuocDoiMatKhau)) return Results.Unauthorized();
            var before = password ? null : JsonSerializer.Serialize(await Profile(owner)); await mutate(user);
            // Mọi thay đổi sổ địa chỉ cũng tăng version hồ sơ, giúp hai thiết bị không ghi đè nhau.
            user.ThoiDiemCapNhat = DateTime.UtcNow; db.Entry(user).Property(x => x.ThoiDiemCapNhat).IsModified = true; await db.SaveChangesAsync();
            var result = new CustomerSelfReceipt(operation, password ? null : await Profile(owner), password);
            var audit = AuditFactory.Create("CUSTOMER", owner, action, "TaiKhoanKhachHang", owner); audit.TenNguoiThucHienDaChot = user.HoTen; audit.MaThaoTac = operation; audit.MaBamYeuCau = digest; audit.DuLieuTruocJson = before;
            audit.DuLieuSauJson = password ? JsonSerializer.Serialize(new { MaPhienNguon = context.User.FindFirst("session")!.Value }) : JsonSerializer.Serialize(result.Profile); audit.KetQuaDaChotJson = JsonSerializer.Serialize(result);
            db.NhatKyHeThong.Add(audit); await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(result);
        }
        catch (Rule ex)
        {
            // Chỉ commit sự kiện sai mật khẩu; các thay đổi nghiệp vụ khác luôn rollback khi lỗi.
            if (db.ChangeTracker.Entries<NhatKyHeThong>().Any(x => x.Entity.HanhDong == "CUSTOMER_PASSWORD_DENIED")) await tx.CommitAsync();
            return Results.Problem(detail: ex.Message, statusCode: ex.Code);
        }
    }
    public async Task<IResult> Operation(Guid operation, HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString(); if (!header.StartsWith("Bearer ", StringComparison.Ordinal)) return Results.Unauthorized();
        var hash = Passwords.TokenHash(header[7..]); var session = await db.PhienDangNhap.AsNoTracking().SingleOrDefaultAsync(x => x.MaLamMoiDaBam == hash && x.MaKhachHang != null && x.HetHanLuc > DateTime.UtcNow);
        if (session is null) return Results.Unauthorized();
        var audit = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == operation && x.MaKhachHang == session.MaKhachHang && x.LoaiDoiTuong == "TaiKhoanKhachHang" && x.HanhDong.StartsWith("CUSTOMER_"));
        var user = await db.TaiKhoanKhachHang.AsNoTracking().SingleOrDefaultAsync(x => x.MaKhachHang == session.MaKhachHang && x.TrangThai == "ACTIVE");
        if (user is null) return Results.Unauthorized();
        // Trước khi đổi thành công, phiên hạn chế chỉ được biết lệnh mới chưa có receipt, không đọc receipt hồ sơ.
        if (user.BatBuocDoiMatKhau) return session.ThuHoiLuc is null && session.PhamViPhien == "PASSWORD_CHANGE" && audit is null ? Results.NotFound() : Results.Unauthorized();
        if (session.ThuHoiLuc is not null)
        {
            // Sau đổi mật khẩu, token nguồn chỉ đọc receipt của lệnh đó trong 15 phút, không lấy lại quyền Customer.
            if (audit?.HanhDong is not ("CUSTOMER_PASSWORD_CHANGED" or "CUSTOMER_TEMPORARY_PASSWORD_CHANGED") || audit.PhatSinhLuc < DateTime.UtcNow.AddMinutes(-15) || audit.DuLieuSauJson is null) return Results.Unauthorized();
            using var source = JsonDocument.Parse(audit.DuLieuSauJson); if (source.RootElement.GetProperty("MaPhienNguon").GetString() != session.MaPhienDangNhap.ToString()) return Results.Unauthorized();
        }
        else if (session.PhamViPhien != "FULL") return Results.Unauthorized();
        return audit?.KetQuaDaChotJson is null ? Results.NotFound() : Results.Ok(JsonSerializer.Deserialize<CustomerSelfReceipt>(audit.KetQuaDaChotJson));
    }
}
