using System.Security.Cryptography;
using System.Data;
using FourChang.Domain.Entities;
using FourChang.Infrastructure.Persistence;
using FourChang.Infrastructure.Security;
using FourChang.Presentation.Shared.Integration;
using Microsoft.EntityFrameworkCore;
namespace FourChang.Api.Features;

public static class AuthEndpoints
{
    public static void MapAuth(this WebApplication app)
    {
        app.MapPost("/api/auth/customer/register",async(RegisterCustomer input,CafeDbContext db)=>
        {
            if(input.TenDangNhap is null||input.HoTen is null||input.MatKhau is null)return Results.Problem(detail:"Thiếu thông tin đăng ký.",statusCode:400);
            var username=input.TenDangNhap.Trim().ToLowerInvariant();var email=string.IsNullOrWhiteSpace(input.Email)?null:input.Email.Trim().ToLowerInvariant();var phone=string.IsNullOrWhiteSpace(input.SoDienThoai)?null:input.SoDienThoai.Trim();
            if(!System.Text.RegularExpressions.Regex.IsMatch(username,"^[a-z][a-z0-9._-]{2,99}$")||input.HoTen.Trim().Length is <1 or >120||input.MatKhau.Length is <10 or >128||(email is null&&phone is null)||email?.Length>254||phone is not null&&!System.Text.RegularExpressions.Regex.IsMatch(phone,@"^\+?[0-9]{9,15}$")||email is not null&&!System.Net.Mail.MailAddress.TryCreate(email,out _))return Results.Problem(detail:"Tên đăng nhập bắt đầu bằng chữ a-z, chỉ dùng chữ không dấu, số, dấu chấm, gạch dưới hoặc gạch ngang; cần email/điện thoại hợp lệ và mật khẩu từ 10 ký tự.",statusCode:400);
            await using var tx=await db.Database.BeginTransactionAsync();
            var user=new TaiKhoanKhachHang{TenDangNhapChuanHoa=username,HoTen=input.HoTen.Trim(),EmailChuanHoa=email,SoDienThoaiChuanHoa=phone,MatKhauBam=Passwords.Hash(input.MatKhau),TrangThai="ACTIVE"};
            db.TaiKhoanKhachHang.Add(user);await db.SaveChangesAsync();
            var result=await NewSession(db,user.MaKhachHang,null,user.HoTen,"WEB");
            db.NhatKyHeThong.Add(Audit("CUSTOMER",user.MaKhachHang,"REGISTER_CUSTOMER","TaiKhoanKhachHang",user.MaKhachHang));
            await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(result);
        }).RequireRateLimiting("auth");
        app.MapPost("/api/auth/{role}/login",Login).RequireRateLimiting("auth");
        app.MapPost("/api/auth/logout",async(HttpContext context,CafeDbContext db)=>
        {
            if(!long.TryParse(context.User.FindFirst("session")?.Value,out var id))return Results.BadRequest();
            var session=await db.PhienDangNhap.FindAsync(id);if(session is not null){session.ThuHoiLuc=DateTime.UtcNow;session.LyDoThuHoi="LOGOUT";await db.SaveChangesAsync();}
            return Results.Ok(new{message="Đã đăng xuất"});
        }).RequireAuthorization();
        app.MapGet("/api/customer/orders",async(HttpContext context,CafeDbContext db)=>
        {
            var id=long.Parse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            return await db.DonHang.AsNoTracking().Where(x=>x.MaKhachHang==id).OrderByDescending(x=>x.ThoiDiemTao).Take(100).Select(x=>new{x.MaDonHang,x.MaDonHienThi,x.HinhThucNhanMon,x.TrangThai,x.ThoiDiemTao}).ToListAsync();
        }).RequireAuthorization("Customer");
        app.MapGet("/api/customer/profile",async(HttpContext context,CafeDbContext db)=>
        {
            var id=long.Parse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            return await db.TaiKhoanKhachHang.Where(x=>x.MaKhachHang==id).Select(x=>new{x.MaKhachHang,x.HoTen,x.EmailChuanHoa,x.SoDienThoaiChuanHoa}).SingleAsync();
        }).RequireAuthorization("Customer");
    }
    private static async Task<IResult> Login(string role,SignIn input,CafeDbContext db)
    {
        if(role is not ("customer" or "admin")||input.TenDangNhap is null||input.MatKhau is null||input.TenDangNhap.Length>254||input.MatKhau.Length>128)return Results.Problem(detail:"Thông tin đăng nhập không hợp lệ.",statusCode:400);
        var name=input.TenDangNhap.Trim().ToLowerInvariant();
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if(role=="admin")
        {
            var user=await db.TaiKhoanQuanLy.SingleOrDefaultAsync(x=>x.TenDangNhapChuanHoa==name);
            if(user is null||user.TrangThai!="ACTIVE"||user.KhoaDenLuc>DateTime.UtcNow)return Denied();
            if(!Passwords.Verify(input.MatKhau,user.MatKhauBam)){user.SoLanDangNhapSai++;if(user.SoLanDangNhapSai>=5)user.KhoaDenLuc=DateTime.UtcNow.AddMinutes(15);await db.SaveChangesAsync();await tx.CommitAsync();return Denied();}
            user.SoLanDangNhapSai=0;user.KhoaDenLuc=null;user.DangNhapLanCuoiLuc=DateTime.UtcNow;
            var session=await NewSession(db,null,user.MaQuanLy,user.HoTen,"DESKTOP");await tx.CommitAsync();return Results.Ok(session);
        }
        else
        {
            var user=await db.TaiKhoanKhachHang.SingleOrDefaultAsync(x=>x.TenDangNhapChuanHoa==name||x.EmailChuanHoa==name||x.SoDienThoaiChuanHoa==name);
            if(user is null||user.TrangThai!="ACTIVE"||user.KhoaDenLuc>DateTime.UtcNow)return Denied();
            if(!Passwords.Verify(input.MatKhau,user.MatKhauBam)){user.SoLanDangNhapSai++;if(user.SoLanDangNhapSai>=5)user.KhoaDenLuc=DateTime.UtcNow.AddMinutes(15);await db.SaveChangesAsync();await tx.CommitAsync();return Denied();}
            user.SoLanDangNhapSai=0;user.KhoaDenLuc=null;user.DangNhapLanCuoiLuc=DateTime.UtcNow;
            var session=await NewSession(db,user.MaKhachHang,null,user.HoTen,"WEB");await tx.CommitAsync();return Results.Ok(session);
        }
    }
    private static IResult Denied()=>Results.Problem(detail:"Tên đăng nhập hoặc mật khẩu không đúng, hoặc tài khoản tạm khóa.",statusCode:401);
    private static async Task<SessionResult> NewSession(CafeDbContext db,long? customer,long? admin,string name,string app)
    {
        var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.PhienDangNhap.Add(new(){MaKhachHang=customer,MaQuanLy=admin,MaLamMoiDaBam=Passwords.TokenHash(token),PhamViPhien="FULL",LoaiUngDung=app,HetHanLuc=DateTime.UtcNow.AddHours(8)});
        await db.SaveChangesAsync();return new(token,admin is null?"CUSTOMER":"ADMIN",name,(admin??customer)!.Value);
    }
    public static NhatKyHeThong Audit(string role,long actor,string action,string entity,long id)=>new(){MaLienKetXuLy=Guid.NewGuid(),LoaiTacNhan=role,MaKhachHang=role=="CUSTOMER"?actor:null,MaQuanLy=role=="ADMIN"?actor:null,MaCaLamViec=role=="STAFF"?actor:null,TenNguoiThucHienDaChot=role,HanhDong=action,LoaiDoiTuong=entity,MaDoiTuong=id.ToString(System.Globalization.CultureInfo.InvariantCulture),KetQua="SUCCEEDED",PhatSinhLuc=DateTime.UtcNow};
}
