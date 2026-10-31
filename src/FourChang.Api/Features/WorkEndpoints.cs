using System.Data;
using System.Security.Claims;
using FourChang.Api.Security;
using FourChang.Domain.Entities;
using FourChang.Infrastructure.Persistence;
using FourChang.Presentation.Shared.Integration;
using Microsoft.EntityFrameworkCore;
namespace FourChang.Api.Features;

public static class WorkEndpoints
{
    public static void MapWork(this WebApplication app)
    {
        app.MapGet("/api/pos/employees",async(HttpContext context,CafeDbContext db)=>
        {
            if(!PosAccess.IsDevice(context.Request))return Results.Unauthorized();
            return Results.Ok(await db.NhanVien.AsNoTracking().Where(x=>x.TrangThai=="ACTIVE").OrderBy(x=>x.HoTen).Select(x=>new Employee(x.MaNhanVien,x.HoTen,null,x.NgayVaoLam,x.TrangThai,null)).ToListAsync());
        });
        app.MapPost("/api/pos/shifts",async(OpenShift input,HttpContext context,CafeDbContext db)=>
        {
            if(!PosAccess.IsDevice(context.Request))return Results.Unauthorized();
            if(input.TenCa is not ("Sáng" or "Chiều" or "Tối")||string.IsNullOrWhiteSpace(input.MaMay)||input.MaMay.Length>50||input.TienMatDauCa<0||input.TienMatDauCa!=decimal.Truncate(input.TienMatDauCa))return Results.Problem(detail:"Ca làm, mã máy hoặc tiền đầu ca không hợp lệ.",statusCode:400);
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var employee=await db.NhanVien.SingleOrDefaultAsync(x=>x.MaNhanVien==input.MaNhanVien&&x.TrangThai=="ACTIVE");
            if(employee is null)return Results.Problem(detail:"Nhân viên không còn hoạt động.",statusCode:400);
            var existing=await db.CaLamViec.FirstOrDefaultAsync(x=>x.KetThucLuc==null&&(x.MaMay==input.MaMay||x.MaNhanVien==input.MaNhanVien));
            if(existing is not null)
            {
                if(existing.MaNhanVien==input.MaNhanVien&&existing.MaMay==input.MaMay&&existing.TrangThai=="OPEN")return Results.Ok(new SessionResult(PosAccess.Token(existing.MaCaLamViec),"STAFF",employee.HoTen,employee.MaNhanVien,existing.MaCaLamViec));
                return Results.Problem(detail:"Nhân viên hoặc máy đang có ca chưa kết thúc.",statusCode:409);
            }
            var shift=new CaLamViec{MaCaHienThi="CA-"+Guid.NewGuid().ToString("N")[..20],MaNhanVien=employee.MaNhanVien,TenCa=input.TenCa,MaMay=input.MaMay.Trim(),BatDauLuc=DateTime.UtcNow,TrangThai="OPEN",TienMatDauCa=input.TienMatDauCa};
            db.CaLamViec.Add(shift);await db.SaveChangesAsync();
            var audit=AuthEndpoints.Audit("STAFF",shift.MaCaLamViec,"OPEN_SHIFT","CaLamViec",shift.MaCaLamViec);audit.TenNguoiThucHienDaChot=employee.HoTen;audit.MaMayDaChot=shift.MaMay;db.NhatKyHeThong.Add(audit);
            await db.SaveChangesAsync();await tx.CommitAsync();
            return Results.Ok(new SessionResult(PosAccess.Token(shift.MaCaLamViec),"STAFF",employee.HoTen,employee.MaNhanVien,shift.MaCaLamViec));
        });
        app.MapPost("/api/pos/shifts/current/close",async(CloseShift input,HttpContext context,CafeDbContext db)=>
        {
            if(input.TienMatThucDem<0||input.TienMatThucDem!=decimal.Truncate(input.TienMatThucDem))return Results.Problem(detail:"Tiền thực đếm không hợp lệ.",statusCode:400);
            var id=long.Parse(context.User.FindFirst("shift")!.Value);
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var shift=await db.CaLamViec.SingleAsync(x=>x.MaCaLamViec==id);
            if(shift.TrangThai!="OPEN")return Results.Conflict();
            if(await db.DonHang.AnyAsync(x=>x.MaCaLamViec==id&&x.TrangThai!="COMPLETED"&&x.TrangThai!="CANCELLED"&&x.TrangThai!="REJECTED"))return Results.Problem(detail:"Cần xử lý hoặc bàn giao các đơn còn mở trước khi chốt ca.",statusCode:409);
            var collected=await db.ThanhToan.Where(x=>x.MaCaLamViec==id&&x.PhuongThuc=="CASH"&&x.TrangThai=="VERIFIED").SumAsync(x=>(decimal?)x.SoTienDaXacMinh)??0;
            var refunded=await db.LanChiHoanTien.Where(x=>x.MaCaLamViec==id&&x.PhuongThuc=="CASH"&&x.TrangThai=="SUCCEEDED").SumAsync(x=>(decimal?)x.SoTien)??0;
            shift.TienMatDuKienCuoiCa=shift.TienMatDauCa+collected-refunded;
            if(input.TienMatThucDem!=shift.TienMatDuKienCuoiCa&&string.IsNullOrWhiteSpace(input.LyDoChenhLech))return Results.Problem(detail:"Bắt buộc ghi lý do chênh lệch tiền.",statusCode:400);
            shift.TienMatThucDem=input.TienMatThucDem;shift.LyDoChenhLech=input.LyDoChenhLech;shift.KetThucLuc=DateTime.UtcNow;shift.TrangThai="CLOSED";
            var audit=AuthEndpoints.Audit("STAFF",id,"CLOSE_SHIFT","CaLamViec",id);audit.TenNguoiThucHienDaChot=context.User.Identity?.Name??"Nhân viên";audit.MaMayDaChot=shift.MaMay;db.NhatKyHeThong.Add(audit);
            await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(new{message="Đã kết thúc ca"});
        }).RequireAuthorization("Staff");
        app.MapGet("/api/admin/employees",async(CafeDbContext db)=>await db.NhanVien.AsNoTracking().OrderBy(x=>x.HoTen).Select(x=>new Employee(x.MaNhanVien,x.HoTen,x.SoDienThoai,x.NgayVaoLam,x.TrangThai,x.PhienBanDuLieu)).ToListAsync()).RequireAuthorization("Admin");
        app.MapPost("/api/admin/employees",async(EmployeeInput input,HttpContext context,CafeDbContext db)=>await SaveEmployee(null,input,context,db)).RequireAuthorization("Admin");
        app.MapPut("/api/admin/employees/{id:long}",async(long id,EmployeeInput input,HttpContext context,CafeDbContext db)=>await SaveEmployee(id,input,context,db)).RequireAuthorization("Admin");
    }
    private static async Task<IResult> SaveEmployee(long? id,EmployeeInput input,HttpContext context,CafeDbContext db)
    {
        if(string.IsNullOrWhiteSpace(input.HoTen)||input.HoTen.Length>120||input.SoDienThoai?.Length>20||input.TrangThai is not ("ACTIVE" or "INACTIVE"))return Results.Problem(detail:"Thông tin nhân viên không hợp lệ.",statusCode:400);
        await using var tx=await db.Database.BeginTransactionAsync();
        var employee=id is null?new NhanVien():await db.NhanVien.SingleOrDefaultAsync(x=>x.MaNhanVien==id);
        if(employee is null)return Results.NotFound();
        if(id is not null&&(input.PhienBanDuLieu is null||!input.PhienBanDuLieu.SequenceEqual(employee.PhienBanDuLieu)))return Results.Problem(detail:"Hồ sơ đã thay đổi. Tải lại trước khi lưu.",statusCode:409);
        if(id is not null&&input.TrangThai=="INACTIVE"&&await db.CaLamViec.AnyAsync(x=>x.MaNhanVien==id&&x.KetThucLuc==null))return Results.Problem(detail:"Cần kết thúc ca trước khi ngừng sử dụng hồ sơ nhân viên.",statusCode:409);
        employee.HoTen=input.HoTen.Trim();employee.SoDienThoai=string.IsNullOrWhiteSpace(input.SoDienThoai)?null:input.SoDienThoai.Trim();employee.NgayVaoLam=input.NgayVaoLam.Date;employee.TrangThai=input.TrangThai;
        if(id is null)db.NhanVien.Add(employee);await db.SaveChangesAsync();
        db.NhatKyHeThong.Add(AuthEndpoints.Audit("ADMIN",long.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)!.Value),id is null?"CREATE_EMPLOYEE":"UPDATE_EMPLOYEE","NhanVien",employee.MaNhanVien));
        await db.SaveChangesAsync();await tx.CommitAsync();
        return Results.Ok(new Employee(employee.MaNhanVien,employee.HoTen,employee.SoDienThoai,employee.NgayVaoLam,employee.TrangThai,employee.PhienBanDuLieu));
    }
}
