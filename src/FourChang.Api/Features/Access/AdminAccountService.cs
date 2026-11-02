using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FourChang.Domain.Entities;
using FourChang.Infrastructure.Persistence;
using FourChang.Infrastructure.Security;
using FourChang.Presentation.Shared.Integration;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Access;

// Đăng nhập và đổi mật khẩu của đúng một Quản lý, không cấp tài khoản cho nhân viên.
public sealed class AdminAccountService(CafeDbContext db)
{
    private const int MinimumPasswordLength = 10;
    private const int MaximumPasswordLength = 128;
    private static IResult Error(int code, string text) => Results.Problem(detail: text, statusCode: code);
    private static IResult Denied() => Error(401, "Tên đăng nhập hoặc mật khẩu không đúng, hoặc tài khoản tạm khóa.");
    private static AdminAccessProfile Profile(TaiKhoanQuanLy user) => new(user.MaQuanLy,
        user.TenDangNhapChuanHoa, user.HoTen, user.TrangThai, user.BatBuocDoiMatKhau,
        user.DangNhapLanCuoiLuc, user.PhienBanDuLieu);

    public async Task<IResult> Login(SignIn input)
    {
        if (string.IsNullOrWhiteSpace(input.TenDangNhap) || input.TenDangNhap.Length > 100 ||
            input.MatKhau is null || input.MatKhau.Length > MaximumPasswordLength)
            return Error(400, "Thông tin đăng nhập không hợp lệ.");
        var name = input.TenDangNhap.Trim().ToLowerInvariant();
        // BR-07: khóa bản ghi trong transaction để hai lần thử sai đồng thời không làm mất bộ đếm.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var user = await db.TaiKhoanQuanLy.SingleOrDefaultAsync(x => x.MaQuanLy == 1 && x.TenDangNhapChuanHoa == name);
        if (user is null || user.TrangThai != "ACTIVE" || user.KhoaDenLuc > DateTime.UtcNow)
        {
            db.NhatKyHeThong.Add(new NhatKyHeThong { MaLienKetXuLy = Guid.NewGuid(), LoaiTacNhan = "SYSTEM",
                TenNguoiThucHienDaChot = "Xác thực hệ thống", HanhDong = "ADMIN_LOGIN_DENIED", LoaiDoiTuong = "TaiKhoanQuanLy",
                KetQua = "DENIED", PhatSinhLuc = DateTime.UtcNow });
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Denied();
        }
        if (!Passwords.Verify(input.MatKhau, user.MatKhauBam))
        {
            user.SoLanDangNhapSai++;
            if (user.SoLanDangNhapSai >= 5) user.KhoaDenLuc = DateTime.UtcNow.AddMinutes(15);
            var audit = AuthEndpoints.Audit("ADMIN", 1, "ADMIN_LOGIN_FAILED", "TaiKhoanQuanLy", 1);
            audit.KetQua = "DENIED"; audit.LyDo = "Thông tin xác thực không hợp lệ";
            audit.DuLieuSauJson = JsonSerializer.Serialize(new { user.SoLanDangNhapSai, user.KhoaDenLuc });
            db.NhatKyHeThong.Add(audit); await db.SaveChangesAsync(); await tx.CommitAsync(); return Denied();
        }
        user.SoLanDangNhapSai = 0; user.KhoaDenLuc = null; user.DangNhapLanCuoiLuc = DateTime.UtcNow;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        // Mật khẩu tạm chỉ mở phiên PASSWORD_CHANGE, không được mở Dashboard hoặc API Admin.
        db.PhienDangNhap.Add(new PhienDangNhap { MaQuanLy = 1, MaLamMoiDaBam = Passwords.TokenHash(token),
            PhamViPhien = user.BatBuocDoiMatKhau ? "PASSWORD_CHANGE" : "FULL", LoaiUngDung = "DESKTOP",
            HetHanLuc = user.BatBuocDoiMatKhau ? DateTime.UtcNow.AddMinutes(15) : DateTime.UtcNow.AddHours(8) });
        var success = AuthEndpoints.Audit("ADMIN", 1, "ADMIN_LOGIN_SUCCEEDED", "TaiKhoanQuanLy", 1);
        success.TenNguoiThucHienDaChot = user.HoTen; db.NhatKyHeThong.Add(success);
        await db.SaveChangesAsync(); await tx.CommitAsync();
        return Results.Ok(new SessionResult(token, "ADMIN", user.HoTen, 1, MustChangePassword: user.BatBuocDoiMatKhau));
    }

    // Chỉ thông tin cần đổi mật khẩu được trả cả cho phiên hạn chế; không trả danh sách thiết bị/quyền tại đây.
    public async Task<IResult> GetProfile() => Results.Ok(Profile(await db.TaiKhoanQuanLy.AsNoTracking().SingleAsync(x => x.MaQuanLy == 1)));

    public async Task<IResult> ChangePassword(AdminPasswordInput input, HttpContext context)
    {
        if (input.MaThaoTac == Guid.Empty || input.PhienBanDuLieu is not { Length: 8 }) return Error(400, "Thiếu mã thao tác hoặc phiên bản tài khoản.");
        if (input.MatKhauHienTai is null || input.MatKhauHienTai.Length > MaximumPasswordLength ||
            input.MatKhauMoi is not { Length: >= MinimumPasswordLength and <= MaximumPasswordLength } || input.MatKhauMoi != input.XacNhanMatKhau)
            return Error(400, "Mật khẩu mới phải có 10–128 ký tự và xác nhận khớp.");
        var sessionId = long.Parse(context.User.FindFirst("session")!.Value);
        var secret = Environment.GetEnvironmentVariable("FOURCHANG_POS_KEY") ?? throw new InvalidOperationException("Thiếu khóa máy chủ.");
        var digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), JsonSerializer.SerializeToUtf8Bytes(input));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var resource = "UC-ADM-02:PASSWORD:" + input.MaThaoTac.ToString("N");
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r<0 THROW 51090, 'Operation lock timeout', 1;");
        var previous = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == input.MaThaoTac);
        if (previous is not null)
        {
            if (previous.HanhDong != "ADMIN_PASSWORD_CHANGED" || previous.MaQuanLy != 1 || previous.MaBamYeuCau is null ||
                !CryptographicOperations.FixedTimeEquals(previous.MaBamYeuCau, digest)) return Error(409, "Mã thao tác đã dùng với nội dung khác.");
            return Results.Ok(JsonSerializer.Deserialize<AdminPasswordReceipt>(previous.KetQuaDaChotJson!));
        }
        var user = await db.TaiKhoanQuanLy.SingleAsync(x => x.MaQuanLy == 1);
        if (!input.PhienBanDuLieu.SequenceEqual(user.PhienBanDuLieu))
            return Results.Conflict(new { detail = "Thông tin tài khoản đã thay đổi. Tải lại để đối chiếu.", current = Profile(user) });
        if (!Passwords.Verify(input.MatKhauHienTai, user.MatKhauBam)) return Error(400, "Mật khẩu hiện tại không đúng.");
        if (Passwords.Verify(input.MatKhauMoi, user.MatKhauBam)) return Error(400, "Mật khẩu mới phải khác mật khẩu hiện tại.");
        var before = JsonSerializer.Serialize(new { user.MaQuanLy, user.BatBuocDoiMatKhau });
        user.MatKhauBam = Passwords.Hash(input.MatKhauMoi); user.BatBuocDoiMatKhau = false;
        user.SoLanDangNhapSai = 0; user.KhoaDenLuc = null;
        var now = DateTime.UtcNow;
        var sessions = await db.PhienDangNhap.Where(x => x.MaQuanLy == 1 && x.ThuHoiLuc == null).ToListAsync();
        foreach (var session in sessions) { session.ThuHoiLuc = now; session.LyDoThuHoi = "ADMIN_PASSWORD_CHANGED"; }
        var result = new AdminPasswordReceipt(input.MaThaoTac, true, now);
        var audit = AuthEndpoints.Audit("ADMIN", 1, "ADMIN_PASSWORD_CHANGED", "TaiKhoanQuanLy", 1);
        audit.TenNguoiThucHienDaChot = user.HoTen; audit.MaThaoTac = input.MaThaoTac; audit.MaBamYeuCau = digest;
        audit.DuLieuTruocJson = before;
        audit.DuLieuSauJson = JsonSerializer.Serialize(new { user.MaQuanLy, user.BatBuocDoiMatKhau, MaPhienNguon = sessionId });
        audit.KetQuaDaChotJson = JsonSerializer.Serialize(result); db.NhatKyHeThong.Add(audit);
        // BR-29/E6: đổi mật khẩu, thu hồi phiên và audit cùng commit; không sửa bất kỳ ca POS nào.
        await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(result);
    }

    public async Task<IResult> PasswordReceipt(Guid operation, HttpContext context)
    {
        // Phiên bị thu hồi sau đổi mật khẩu chỉ được đọc biên nhận của chính lệnh đó trong 15 phút.
        // Không khôi phục quyền Admin hoặc cho phép token cũ gọi bất kỳ nghiệp vụ nào khác.
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.Ordinal)) return Results.Unauthorized();
        var hash = Passwords.TokenHash(header[7..]);
        var session = await db.PhienDangNhap.AsNoTracking().SingleOrDefaultAsync(x => x.MaLamMoiDaBam == hash && x.MaQuanLy == 1 && x.HetHanLuc > DateTime.UtcNow);
        if (session is null) return Results.Unauthorized();
        var audit = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == operation && x.HanhDong == "ADMIN_PASSWORD_CHANGED" && x.MaQuanLy == 1);
        if (audit is null) return session.ThuHoiLuc is null ? Results.NotFound() : Results.Unauthorized();
        if (audit.PhatSinhLuc < DateTime.UtcNow.AddMinutes(-15) || audit.DuLieuSauJson is null) return Results.Unauthorized();
        using var data = JsonDocument.Parse(audit.DuLieuSauJson);
        if (data.RootElement.GetProperty("MaPhienNguon").GetInt64() != session.MaPhienDangNhap) return Results.Unauthorized();
        return Results.Ok(JsonSerializer.Deserialize<AdminPasswordReceipt>(audit.KetQuaDaChotJson!));
    }

    public async Task<IResult> Logout(HttpContext context)
    {
        var sessionId = long.Parse(context.User.FindFirst("session")!.Value);
        await using var tx = await db.Database.BeginTransactionAsync();
        var session = await db.PhienDangNhap.SingleAsync(x => x.MaPhienDangNhap == sessionId && x.MaQuanLy == 1);
        session.ThuHoiLuc = DateTime.UtcNow; session.LyDoThuHoi = "LOGOUT";
        var audit = AuthEndpoints.Audit("ADMIN", 1, "ADMIN_LOGOUT", "PhienDangNhap", sessionId);
        audit.TenNguoiThucHienDaChot = context.User.Identity?.Name ?? "Quản lý"; db.NhatKyHeThong.Add(audit);
        await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(new { message = "Đã đăng xuất" });
    }
}
