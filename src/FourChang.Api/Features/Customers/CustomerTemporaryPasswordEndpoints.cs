using System.Data;
using System.Security.Claims;
using FourChang.Infrastructure.Persistence;
using FourChang.Infrastructure.Security;
using FourChang.Presentation.Shared.Integration;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Customers;

public static class CustomerTemporaryPasswordEndpoints
{
    public static void MapCustomerTemporaryPassword(this WebApplication app)
    {
        // Đây là phần thực thi điều kiện mật khẩu tạm của UC-ADM-01, không phải UC mới.
        var group = app.MapGroup("/api/auth/customer/temporary-password").RequireAuthorization("CustomerPasswordChange");
        group.MapGet("", () => Results.Ok(new { required = true }));
        group.MapPost("", async (ChangeTemporaryPassword input, HttpContext context, CafeDbContext db) =>
        {
            if (!CustomerRules.ValidPassword(input.MatKhauMoi)) return Results.Problem(detail: "Mật khẩu mới phải có 10–128 ký tự.", statusCode: 400);
            var id = long.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var user = await db.TaiKhoanKhachHang.SingleAsync(x => x.MaKhachHang == id);
            if (!user.BatBuocDoiMatKhau || user.TrangThai != "ACTIVE") return Results.Conflict();
            if (Passwords.Verify(input.MatKhauMoi, user.MatKhauBam)) return Results.Problem(detail: "Mật khẩu mới phải khác mật khẩu tạm.", statusCode: 400);
            user.MatKhauBam = Passwords.Hash(input.MatKhauMoi); user.BatBuocDoiMatKhau = false;
            user.SoLanDangNhapSai = 0; user.KhoaDenLuc = null;
            var sessions = await db.PhienDangNhap.Where(x => x.MaKhachHang == id && x.ThuHoiLuc == null).ToListAsync();
            foreach (var session in sessions) { session.ThuHoiLuc = DateTime.UtcNow; session.LyDoThuHoi = "TEMPORARY_PASSWORD_CHANGED"; }
            db.NhatKyHeThong.Add(AuthEndpoints.Audit("CUSTOMER", id, "CHANGE_TEMPORARY_PASSWORD", "TaiKhoanKhachHang", id));
            await db.SaveChangesAsync(); await tx.CommitAsync();
            // Không chuyển phiên PASSWORD_CHANGE thành FULL; khách đăng nhập lại bằng mật khẩu mới.
            return Results.Ok(new { message = "Đã đổi mật khẩu. Vui lòng đăng nhập lại." });
        });
    }
}
