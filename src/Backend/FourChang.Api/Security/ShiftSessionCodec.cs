using System.Security.Cryptography;
using System.Text;
using FourChang.Api.Shared.Operations;
using FourChang.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Security;

/// <summary>Phiên ca được máy chủ ký; mã máy chỉ là lịch sử, không phải bằng chứng xác thực nhân viên.</summary>
public static class ShiftSessionCodec
{
    public sealed record Grant(long Shift, long Employee, string Name, bool ReconciliationOnly, Guid Ticket);

    /// <summary>Chuẩn hóa mã máy lịch sử, giữ giới hạn cột hiện có; không đòi đăng ký thiết bị.</summary>
    public static string Machine(string? value)
    {
        var machine = value?.Trim().ToUpperInvariant() ?? "";
        if (machine.Length is < 1 or > 50 || machine.Any(x => !char.IsAsciiLetterOrDigit(x) && x is not ('-' or '_' or '.')))
            throw new AdminRule(400, "Mã máy lịch sử gồm 1–50 chữ/số, dấu chấm, gạch ngang hoặc gạch dưới.");
        return machine;
    }

    /// <summary>Tạo phiên ca hoặc phiên chỉ đối chiếu; khóa ký chỉ đọc từ môi trường máy chủ.</summary>
    public static string Create(long shift, bool reconciliationOnly = false, Guid? ticket = null)
    {
        var body = $"{(reconciliationOnly ? "recon" : "staff")}.{shift}.{DateTimeOffset.UtcNow.AddMinutes(reconciliationOnly ? 30 : 1440).ToUnixTimeSeconds()}.{ticket ?? Guid.NewGuid():N}";
        return body + "." + Convert.ToHexString(Sign(body));
    }

    /// <summary>Lấy ID từ chữ ký/hạn phiên, sau đó kiểm tra hồ sơ, ca và quyền đối chiếu đã cấp trong SQL.</summary>
    public static async Task<Grant?> Resolve(HttpContext context, CafeDbContext db, bool includeEnded = false)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.Ordinal)) return null;
        var token = header[7..];
        long id;
        var restricted = false;
        var ticket = Guid.Empty;
        if (token.StartsWith("pos.", StringComparison.Ordinal))
        {
            // Tương thích client cũ còn dùng thiết bị; luồng mới staff/recon không phụ thuộc thiết bị.
            var old = PosAccess.Validate(token);
            if (old is null) return null;
            var device = await db.ThietBiPOS.AsNoTracking().SingleOrDefaultAsync(x => x.MaThietBiPOS == old.Device && x.TrangThai == "ACTIVE");
            if (device is null || Convert.ToHexString(device.PhienBanDuLieu) != old.Version) return null;
            id = old.Shift;
            if (!await db.CaLamViec.AnyAsync(x => x.MaCaLamViec == id && x.MaMay == device.MaMay)) return null;
        }
        else
        {
            var parts = token.Split('.');
            if (parts.Length != 5 || parts[0] is not ("staff" or "recon") || !long.TryParse(parts[1], out id) || id <= 0 ||
                !long.TryParse(parts[2], out var expires) || expires <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() || !Guid.TryParseExact(parts[3], "N", out ticket)) return null;
            try { if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(parts[4]), Sign(string.Join('.', parts.Take(4))))) return null; }
            catch (FormatException) { return null; }
            restricted = parts[0] == "recon";
        }
        var shift = await db.CaLamViec.AsNoTracking().SingleOrDefaultAsync(x => x.MaCaLamViec == id);
        if (shift is null || !includeEnded && shift.KetThucLuc is not null) return null;
        var employee = await db.NhanVien.AsNoTracking().SingleOrDefaultAsync(x => x.MaNhanVien == shift.MaNhanVien && x.TrangThai == "ACTIVE");
        if (employee is null) return null;
        if (restricted && !await Features.Admin.WorkSessions.ShiftRecoveryService.IsRedeemed(db, id, ticket)) return null;
        return new(id, employee.MaNhanVien, employee.HoTen, restricted, ticket);
    }

    /// <summary>Tính MAC ở máy chủ; không ghi khóa hoặc token vào nhật ký.</summary>
    private static byte[] Sign(string body)
    {
        var key = Environment.GetEnvironmentVariable("FOURCHANG_POS_KEY") ?? throw new InvalidOperationException("Thiếu khóa ký phiên ca máy chủ.");
        return HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(body));
    }
}
