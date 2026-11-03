using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using FourChang.Infrastructure.Database.Context;
using FourChang.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace FourChang.Api.Security;

public static class PosAccess
{
    private static string Key => Environment.GetEnvironmentVariable("FOURCHANG_POS_KEY") ?? throw new InvalidOperationException("Thiếu khóa thiết bị POS.");

    public static async Task<FourChang.Domain.Entities.Accounts.ThietBiPOS?> Device(HttpRequest request,CafeDbContext db)
    {
        var machine=FourChang.Api.Features.Admin.Access.PosDeviceService.Machine(request.Headers["X-POS-Machine"].ToString());
        var key=request.Headers["X-POS-Key"].ToString();
        if(machine.Length is <1 or >50 || key.Length is <32 or >256)return null;
        var device=await db.ThietBiPOS.AsNoTracking().SingleOrDefaultAsync(x=>x.MaMay==machine && x.TrangThai=="ACTIVE");
        if(device is null)return null;
        var supplied=SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return CryptographicOperations.FixedTimeEquals(supplied,device.MaBamKhoa)?device:null;
    }
    public static string Token(long shift,FourChang.Domain.Entities.Accounts.ThietBiPOS device)
    {
        // Gắn thế hệ cấu hình vào token để cấp lại máy không làm sống lại token cũ đã thu hồi.
        var body=$"pos.{shift}.{DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds()}.{device.MaThietBiPOS}.{Convert.ToHexString(device.PhienBanDuLieu)}";
        return body+"."+Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Key),Encoding.UTF8.GetBytes(body)));
    }
    public sealed record Grant(long Shift,long Device,string Version);
    public static Grant? Validate(string token)
    {
        var parts=token.Split('.');
        if(parts.Length!=6||parts[0]!="pos"||!long.TryParse(parts[1],out var shift)||!long.TryParse(parts[2],out var expires)||expires<=DateTimeOffset.UtcNow.ToUnixTimeSeconds()||!long.TryParse(parts[3],out var device))return null;
        try
        {
            var actual=Convert.FromHexString(parts[5]);
            var expected=HMACSHA256.HashData(Encoding.UTF8.GetBytes(Key),Encoding.UTF8.GetBytes(string.Join('.',parts.Take(5))));
            return CryptographicOperations.FixedTimeEquals(actual,expected)?new(shift,device,parts[4]):null;
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
            var grant=PosAccess.Validate(token);if(grant is null)return AuthenticateResult.Fail("Invalid session");
            var shift=await db.CaLamViec.AsNoTracking().FirstOrDefaultAsync(x=>x.MaCaLamViec==grant.Shift&&x.TrangThai=="OPEN");
            if(shift is null)return AuthenticateResult.Fail("Closed shift");
            var device=await db.ThietBiPOS.AsNoTracking().SingleOrDefaultAsync(x=>x.MaThietBiPOS==grant.Device&&x.MaMay==shift.MaMay&&x.TrangThai=="ACTIVE");
            if(device is null||Convert.ToHexString(device.PhienBanDuLieu)!=grant.Version)return AuthenticateResult.Fail("Revoked device");
            var employee=await db.NhanVien.AsNoTracking().FirstOrDefaultAsync(x=>x.MaNhanVien==shift.MaNhanVien&&x.TrangThai=="ACTIVE");
            if(employee is null)return AuthenticateResult.Fail("Inactive employee");
            claims.AddRange([new(ClaimTypes.Role,"STAFF"),new(ClaimTypes.Name,employee.HoTen),new("shift",shift.MaCaLamViec.ToString(CultureInfo.InvariantCulture))]);
        }
        else
        {
            var hash=Passwords.TokenHash(token);
            var session=await db.PhienDangNhap.AsNoTracking().FirstOrDefaultAsync(x=>x.MaLamMoiDaBam==hash&&x.ThuHoiLuc==null&&x.HetHanLuc>DateTime.UtcNow);
            if(session is null)return AuthenticateResult.Fail("Expired session");
            var role=session.MaQuanLy is null?"CUSTOMER":"ADMIN";
            var name="";
            if(session.MaQuanLy is {} admin)
            {
                var user=await db.TaiKhoanQuanLy.AsNoTracking().FirstOrDefaultAsync(x=>x.MaQuanLy==admin&&x.TrangThai=="ACTIVE");
                if(user is null)return AuthenticateResult.Fail("Locked account");
                if(user.BatBuocDoiMatKhau && session.PhamViPhien!="PASSWORD_CHANGE")return AuthenticateResult.Fail("Password change required");
                if(!user.BatBuocDoiMatKhau && session.PhamViPhien!="FULL")return AuthenticateResult.Fail("Restricted session expired");
                role=session.PhamViPhien=="PASSWORD_CHANGE"?"ADMIN_PASSWORD_CHANGE":"ADMIN";name=user.HoTen;
            }
            else
            {
                var user=await db.TaiKhoanKhachHang.AsNoTracking().FirstOrDefaultAsync(x=>x.MaKhachHang==session.MaKhachHang&&x.TrangThai=="ACTIVE");
                if(user is null)return AuthenticateResult.Fail("Locked account");
                // Chặn cả token FULL cũ khi Admin vừa reset; phiên hạn chế không được nâng quyền bởi client.
                if(user.BatBuocDoiMatKhau && session.PhamViPhien!="PASSWORD_CHANGE")return AuthenticateResult.Fail("Password change required");
                if(!user.BatBuocDoiMatKhau && session.PhamViPhien!="FULL")return AuthenticateResult.Fail("Restricted session expired");
                role=session.PhamViPhien=="PASSWORD_CHANGE"?"CUSTOMER_PASSWORD_CHANGE":"CUSTOMER";
                name=user.HoTen;
            }
            claims.AddRange([new(ClaimTypes.Role,role),new(ClaimTypes.Name,name),new(ClaimTypes.NameIdentifier,(session.MaQuanLy??session.MaKhachHang)!.Value.ToString(CultureInfo.InvariantCulture)),new("session",session.MaPhienDangNhap.ToString(CultureInfo.InvariantCulture))]);
        }
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims,Scheme.Name)),Scheme.Name));
    }
}
