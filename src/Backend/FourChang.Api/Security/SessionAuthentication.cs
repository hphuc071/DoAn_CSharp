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

public sealed class SessionAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder,CafeDbContext db) : AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
    /// <summary>Kiểm tra phiên Customer/Admin hoặc chữ ký/trạng thái ca; phiên khóa/đối chiếu không nhận quyền Staff bán hàng.</summary>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header=Request.Headers.Authorization.ToString();
        if(!header.StartsWith("Bearer ",StringComparison.Ordinal))return AuthenticateResult.NoResult();
        var token=header[7..];
        List<Claim> claims=[];
        if (token.StartsWith("staff.", StringComparison.Ordinal) || token.StartsWith("recon.", StringComparison.Ordinal))
        {
            var grant = await ShiftSessionCodec.Resolve(Context, db);
            if (grant is null) return AuthenticateResult.Fail("Invalid shift session");
            var open = await db.CaLamViec.AnyAsync(x => x.MaCaLamViec == grant.Shift && x.TrangThai == "OPEN");
            var role = grant.ReconciliationOnly ? "STAFF_RECONCILIATION" : open ? "STAFF" : "STAFF_LOCKED";
            claims.AddRange([new(ClaimTypes.Role, role), new(ClaimTypes.Name, grant.Name), new("shift", grant.Shift.ToString(CultureInfo.InvariantCulture))]);
        }
        else if(token.StartsWith("pos.",StringComparison.Ordinal)) return AuthenticateResult.Fail("Phiên thiết bị cũ đã hết hỗ trợ; mở ca bằng hồ sơ nhân viên.");
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
