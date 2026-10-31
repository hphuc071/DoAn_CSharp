using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using FourChang.Infrastructure.Persistence;
using FourChang.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace FourChang.Api.Security;

public static class PosAccess
{
    private static string Key => Environment.GetEnvironmentVariable("FOURCHANG_POS_KEY") ?? throw new InvalidOperationException("Thiếu khóa thiết bị POS.");
    public static bool IsDevice(HttpRequest request)
    {
        var supplied=request.Headers["X-POS-Key"].ToString();
        return supplied.Length>0 && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(supplied)),SHA256.HashData(Encoding.UTF8.GetBytes(Key)));
    }
    public static string Token(long shift)
    {
        var body=$"pos.{shift}.{DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds()}";
        return body+"."+Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Key),Encoding.UTF8.GetBytes(body)));
    }
    public static long? Validate(string token)
    {
        var parts=token.Split('.');
        if(parts.Length!=4||parts[0]!="pos"||!long.TryParse(parts[1],out var shift)||!long.TryParse(parts[2],out var expires)||expires<=DateTimeOffset.UtcNow.ToUnixTimeSeconds())return null;
        try
        {
            var actual=Convert.FromHexString(parts[3]);
            var expected=HMACSHA256.HashData(Encoding.UTF8.GetBytes(Key),Encoding.UTF8.GetBytes(string.Join('.',parts.Take(3))));
            return CryptographicOperations.FixedTimeEquals(actual,expected)?shift:null;
        }
        catch(FormatException){return null;}
    }
}
public sealed class SessionAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder,CafeDbContext db) : AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header=Request.Headers.Authorization.ToString();
        if(!header.StartsWith("Bearer ",StringComparison.Ordinal))return AuthenticateResult.NoResult();
        var token=header[7..];
        List<Claim> claims=[];
        if(token.StartsWith("pos.",StringComparison.Ordinal))
        {
            var id=PosAccess.Validate(token);if(id is null)return AuthenticateResult.Fail("Invalid session");
            var shift=await db.CaLamViec.AsNoTracking().FirstOrDefaultAsync(x=>x.MaCaLamViec==id&&x.TrangThai=="OPEN");
            if(shift is null)return AuthenticateResult.Fail("Closed shift");
            var employee=await db.NhanVien.AsNoTracking().FirstOrDefaultAsync(x=>x.MaNhanVien==shift.MaNhanVien&&x.TrangThai=="ACTIVE");
            if(employee is null)return AuthenticateResult.Fail("Inactive employee");
            claims.AddRange([new(ClaimTypes.Role,"STAFF"),new(ClaimTypes.Name,employee.HoTen),new("shift",shift.MaCaLamViec.ToString(CultureInfo.InvariantCulture))]);
        }
        else
        {
            var hash=Passwords.TokenHash(token);
            var session=await db.PhienDangNhap.AsNoTracking().FirstOrDefaultAsync(x=>x.MaLamMoiDaBam==hash&&x.ThuHoiLuc==null&&x.HetHanLuc>DateTime.UtcNow&&x.PhamViPhien=="FULL");
            if(session is null)return AuthenticateResult.Fail("Expired session");
            var role=session.MaQuanLy is null?"CUSTOMER":"ADMIN";
            var name="";
            if(session.MaQuanLy is {} admin){var user=await db.TaiKhoanQuanLy.AsNoTracking().FirstOrDefaultAsync(x=>x.MaQuanLy==admin&&x.TrangThai=="ACTIVE");if(user is null)return AuthenticateResult.Fail("Locked account");name=user.HoTen;}
            else {var user=await db.TaiKhoanKhachHang.AsNoTracking().FirstOrDefaultAsync(x=>x.MaKhachHang==session.MaKhachHang&&x.TrangThai=="ACTIVE");if(user is null)return AuthenticateResult.Fail("Locked account");name=user.HoTen;}
            claims.AddRange([new(ClaimTypes.Role,role),new(ClaimTypes.Name,name),new(ClaimTypes.NameIdentifier,(session.MaQuanLy??session.MaKhachHang)!.Value.ToString(CultureInfo.InvariantCulture)),new("session",session.MaPhienDangNhap.ToString(CultureInfo.InvariantCulture))]);
        }
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims,Scheme.Name)),Scheme.Name));
    }
}
