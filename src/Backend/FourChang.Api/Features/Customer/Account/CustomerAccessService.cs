using FourChang.Api.Shared.Audit;
using FourChang.Api.Shared.Customers;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FourChang.Domain.Entities.Accounts;
using FourChang.Domain.Entities.Audit;
using FourChang.Infrastructure.Database.Context;
using FourChang.Infrastructure.Security;
using FourChang.Presentation.Shared.Contracts.Common;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.EntityFrameworkCore;
namespace FourChang.Api.Features.Customer.Account;

// UC-WEB-01/02: đăng ký, đăng nhập và đăng xuất Customer.
// Dùng cùng bảng tài khoản với ADM-01 để khóa/reset có hiệu lực ngay tại mọi API.
public sealed class CustomerAccessService(CafeDbContext db)
{
    private static IResult Error(int status, string text) => Results.Problem(detail: text, statusCode: status);
    internal static byte[] Digest(object input) => HMACSHA256.HashData(Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("FOURCHANG_POS_KEY") ?? throw new InvalidOperationException("Thiếu bí mật máy chủ.")), JsonSerializer.SerializeToUtf8Bytes(input));
    internal static async Task Lock(CafeDbContext db, Guid id)
    {
        // Khóa theo mã thao tác trong transaction: hai yêu cầu lặp cùng lúc chỉ được commit một lần.
        var resource = "CUSTOMER-ACCOUNT:" + id.ToString("N");
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51090,'Operation lock timeout',1;");
    }
    public async Task<IResult> Register(RegisterCustomer input)
    {
        if (input.MaThaoTac == Guid.Empty) return Error(400, "Thiếu mã thao tác đăng ký.");
        var invalid = CustomerRules.Validate(new(input.MaThaoTac, input.TenDangNhap, input.HoTen, input.Email, input.SoDienThoai, input.MatKhau), true);
        if (invalid is not null) return Error(400, invalid);
        if (input.MatKhau != input.XacNhanMatKhau) return Error(400, "Xác nhận mật khẩu chưa khớp.");
        var digest = Digest(input);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable); await Lock(db, input.MaThaoTac);
        var old = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == input.MaThaoTac);
        if (old is not null)
        {
            if (old.HanhDong != "REGISTER_CUSTOMER" || old.MaBamYeuCau is null || !CryptographicOperations.FixedTimeEquals(old.MaBamYeuCau, digest)) return Error(409, "Mã thao tác đã dùng với nội dung khác.");
            return Results.Ok(JsonSerializer.Deserialize<RegistrationReceipt>(old.KetQuaDaChotJson!));
        }
        var username = CustomerRules.Normalize(input.TenDangNhap, true)!; var email = CustomerRules.Normalize(input.Email, true); var phone = CustomerRules.Normalize(input.SoDienThoai);
        if (await db.TaiKhoanKhachHang.AnyAsync(x => x.TenDangNhapChuanHoa == username || email != null && x.EmailChuanHoa == email || phone != null && x.SoDienThoaiChuanHoa == phone)) return Error(409, "Định danh đã được sử dụng. Vui lòng đăng nhập hoặc liên hệ hỗ trợ.");
        var user = new TaiKhoanKhachHang { TenDangNhapChuanHoa = username, HoTen = input.HoTen.Trim(), EmailChuanHoa = email, SoDienThoaiChuanHoa = phone, MatKhauBam = Passwords.Hash(input.MatKhau), TrangThai = "ACTIVE" };
        db.TaiKhoanKhachHang.Add(user); await db.SaveChangesAsync();
        var result = new RegistrationReceipt(input.MaThaoTac, user.MaKhachHang);
        var audit = AuditFactory.Create("CUSTOMER", user.MaKhachHang, "REGISTER_CUSTOMER", "TaiKhoanKhachHang", user.MaKhachHang);
        audit.TenNguoiThucHienDaChot = user.HoTen; audit.MaThaoTac = input.MaThaoTac; audit.MaBamYeuCau = digest; audit.KetQuaDaChotJson = JsonSerializer.Serialize(result);
        db.NhatKyHeThong.Add(audit); await db.SaveChangesAsync(); await tx.CommitAsync();
        // Đặc tả yêu cầu chuyển sang đăng nhập. Đăng ký không tự phát token hay quyền nội bộ.
        return Results.Ok(result);
    }
    public async Task<IResult> Registration(Guid operation)
    {
        var audit = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == operation && x.HanhDong == "REGISTER_CUSTOMER");
        // Mã ngẫu nhiên chỉ trả receipt tối thiểu; không có tên, email, mật khẩu hoặc token.
        return audit is null ? Results.NotFound() : Results.Ok(JsonSerializer.Deserialize<RegistrationReceipt>(audit.KetQuaDaChotJson!));
    }
    public async Task<IResult> Login(SignIn input)
    {
        if (string.IsNullOrWhiteSpace(input.TenDangNhap) || input.TenDangNhap.Length > 254 || input.MatKhau is null || input.MatKhau.Length > 128) return Error(400, "Thông tin đăng nhập không hợp lệ.");
        var operation = input.MaThaoTac ?? Guid.NewGuid(); if (operation == Guid.Empty) return Error(400, "Thiếu mã thao tác đăng nhập.");
        var name = input.TenDangNhap.Trim().ToLowerInvariant(); var digest = Digest(new { operation, name, input.MatKhau });
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable); await Lock(db, operation);
        var old = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == operation);
        if (old is not null && (old.HanhDong != "CUSTOMER_LOGIN_SUCCEEDED" && old.HanhDong != "CUSTOMER_LOGIN_FAILED" || old.MaBamYeuCau is null || !CryptographicOperations.FixedTimeEquals(old.MaBamYeuCau, digest))) return Error(409, "Mã thao tác đã dùng với nội dung khác.");
        var user = await db.TaiKhoanKhachHang.SingleOrDefaultAsync(x => x.TenDangNhapChuanHoa == name || x.EmailChuanHoa == name || x.SoDienThoaiChuanHoa == name);
        var denied = user is null || user.TrangThai != "ACTIVE" || user.KhoaDenLuc > DateTime.UtcNow || !Passwords.Verify(input.MatKhau, user.MatKhauBam);
        // Token được suy ra bằng HMAC bí mật và mã lệnh. Audit chỉ chứa digest, không chứa token rõ.
        var token = Convert.ToHexString(Digest(new { Purpose = "CUSTOMER_SESSION", operation }));
        if (old is not null)
        {
            if (denied || old.HanhDong != "CUSTOMER_LOGIN_SUCCEEDED") return Denied();
            var hash = Passwords.TokenHash(token);
            var session = await db.PhienDangNhap.AsNoTracking().SingleOrDefaultAsync(x => x.MaLamMoiDaBam == hash && x.ThuHoiLuc == null && x.HetHanLuc > DateTime.UtcNow);
            if (session is null) return Denied();
            return Results.Ok(new SessionResult(token, "CUSTOMER", user!.HoTen, user.MaKhachHang, MustChangePassword: session.PhamViPhien == "PASSWORD_CHANGE"));
        }
        if (denied)
        {
            if (user is not null && user.TrangThai == "ACTIVE" && !(user.KhoaDenLuc > DateTime.UtcNow)) { user.SoLanDangNhapSai++; if (user.SoLanDangNhapSai >= 5) user.KhoaDenLuc = DateTime.UtcNow.AddMinutes(15); }
            var audit = AuditFactory.Create(user is null ? "SYSTEM" : "CUSTOMER", user?.MaKhachHang ?? 0, "CUSTOMER_LOGIN_FAILED", "TaiKhoanKhachHang", user?.MaKhachHang ?? 0);
            audit.KetQua = "DENIED"; audit.MaThaoTac = operation; audit.MaBamYeuCau = digest; db.NhatKyHeThong.Add(audit);
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Denied();
        }
        user!.SoLanDangNhapSai = 0; user.KhoaDenLuc = null; user.DangNhapLanCuoiLuc = DateTime.UtcNow;
        db.PhienDangNhap.Add(new() { MaKhachHang = user.MaKhachHang, MaLamMoiDaBam = Passwords.TokenHash(token), PhamViPhien = user.BatBuocDoiMatKhau ? "PASSWORD_CHANGE" : "FULL", LoaiUngDung = "WEB", HetHanLuc = DateTime.UtcNow.AddMinutes(user.BatBuocDoiMatKhau ? 15 : 480) });
        var success = AuditFactory.Create("CUSTOMER", user.MaKhachHang, "CUSTOMER_LOGIN_SUCCEEDED", "TaiKhoanKhachHang", user.MaKhachHang); success.TenNguoiThucHienDaChot = user.HoTen; success.MaThaoTac = operation; success.MaBamYeuCau = digest; db.NhatKyHeThong.Add(success);
        await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(new SessionResult(token, "CUSTOMER", user.HoTen, user.MaKhachHang, MustChangePassword: user.BatBuocDoiMatKhau));
    }
    private static IResult Denied() => Error(401, "Tên đăng nhập hoặc mật khẩu không đúng, hoặc tài khoản tạm khóa.");
    public async Task<IResult> Logout(HttpContext context)
    {
        // Cho phép gọi lại logout bằng token đã thu hồi: mất phản hồi không làm người dùng bị kẹt.
        var header = context.Request.Headers.Authorization.ToString(); if (!header.StartsWith("Bearer ", StringComparison.Ordinal)) return Results.Unauthorized();
        var hash = Passwords.TokenHash(header[7..]);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var session = await db.PhienDangNhap.SingleOrDefaultAsync(x => x.MaLamMoiDaBam == hash && x.MaKhachHang != null);
        if (session is null) return Results.Unauthorized();
        if (session.ThuHoiLuc is null)
        {
            session.ThuHoiLuc = DateTime.UtcNow; session.LyDoThuHoi = "LOGOUT";
            db.NhatKyHeThong.Add(AuditFactory.Create("CUSTOMER", session.MaKhachHang!.Value, "CUSTOMER_LOGOUT", "PhienDangNhap", session.MaPhienDangNhap));
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync(); return Results.Ok(new { message = "Đã đăng xuất" });
    }
}
