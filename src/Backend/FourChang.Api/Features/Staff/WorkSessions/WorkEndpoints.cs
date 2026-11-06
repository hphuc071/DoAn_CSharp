using FourChang.Api.Shared.Audit;
using FourChang.Api.Shared.WorkSessions;
using System.Data;
using System.Security.Claims;
using FourChang.Api.Security;
using FourChang.Domain.Entities.Audit;
using FourChang.Domain.Entities.Employees;
using FourChang.Domain.Entities.Orders;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Common;
using FourChang.Presentation.Shared.Contracts.Staff;
using Microsoft.EntityFrameworkCore;
namespace FourChang.Api.Features.Staff.WorkSessions;

public static class WorkEndpoints
{
    public static void MapWork(this WebApplication app)
    {
        app.MapGet("/api/pos/employees", async (HttpContext context, CafeDbContext db) =>
        {
            if (await PosAccess.Device(context.Request, db) is null) return Results.Unauthorized();
            return Results.Ok(await db.NhanVien.AsNoTracking().Where(x => x.TrangThai == "ACTIVE").OrderBy(x => x.HoTen).Select(x => new Employee(x.MaNhanVien, x.HoTen, null, x.NgayVaoLam, x.TrangThai, null, null)).ToListAsync());
        });
        // POS-01: chỉ máy được cấp quyền mới kiểm tra hồ sơ. Không trả danh sách ứng viên theo tên.
        app.MapPost("/api/pos/employees/verify", async (VerifyEmployee input, HttpContext context, CafeDbContext db) =>
        {
            if (await PosAccess.Device(context.Request, db) is null) return Results.Unauthorized();
            if (input.MaNhanVien <= 0 || string.IsNullOrWhiteSpace(input.HoTen) || input.HoTen.Length > 120) return Results.Problem(detail: "Nhập mã nhân viên hợp lệ và đầy đủ họ tên.", statusCode: 400);
            var employee = await db.NhanVien.AsNoTracking().SingleOrDefaultAsync(x => x.MaNhanVien == input.MaNhanVien && x.TrangThai == "ACTIVE");
            if (employee is null || !EmployeeIdentity.Matches(employee.HoTen, input.HoTen)) return Results.Problem(detail: "Không tìm thấy nhân viên đang hoạt động khớp mã và họ tên.", statusCode: 404);
            return Results.Ok(new Employee(employee.MaNhanVien, employee.HoTen, null, employee.NgayVaoLam, employee.TrangThai));
        });
        app.MapPost("/api/pos/shifts", async (OpenShift input, HttpContext context, CafeDbContext db) =>
        {
            var device = await PosAccess.Device(context.Request, db); if (device is null) return Results.Unauthorized();
            if (FourChang.Api.Features.Admin.Access.PosDeviceService.Machine(input.MaMay) != device.MaMay) return Results.Problem(detail: "Mã máy không khớp cấu hình thiết bị.", statusCode: 403);
            input = input with { MaMay = device.MaMay };
            if (input.TenCa is not ("Sáng" or "Chiều" or "Tối") || string.IsNullOrWhiteSpace(input.MaMay) || input.MaMay.Length > 50 || input.TienMatDauCa < 0 || input.TienMatDauCa != decimal.Truncate(input.TienMatDauCa)) return Results.Problem(detail: "Ca làm, mã máy hoặc tiền đầu ca không hợp lệ.", statusCode: 400);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await WorkforceOperation.Lock(db);
            var employee = await db.NhanVien.SingleOrDefaultAsync(x => x.MaNhanVien == input.MaNhanVien && x.TrangThai == "ACTIVE");
            // Kiểm tra lại ngay trong giao dịch mở ca: kết quả xác minh trước đó không thay thế kiểm tra máy chủ.
            if (employee is null || !EmployeeIdentity.Matches(employee.HoTen, input.HoTen)) return Results.Problem(detail: "Mã và họ tên không khớp nhân viên đang hoạt động. Vui lòng kiểm tra lại.", statusCode: 400);
            var existing = await db.CaLamViec.FirstOrDefaultAsync(x => x.KetThucLuc == null && (x.MaMay == input.MaMay || x.MaNhanVien == input.MaNhanVien));
            if (existing is not null)
            {
                if (existing.MaNhanVien == input.MaNhanVien && existing.MaMay == input.MaMay && existing.TrangThai == "OPEN") return Results.Ok(new SessionResult(PosAccess.Token(existing.MaCaLamViec, device), "STAFF", employee.HoTen, employee.MaNhanVien, existing.MaCaLamViec));
                return Results.Problem(detail: "Nhân viên hoặc máy đang có ca chưa kết thúc.", statusCode: 409);
            }
            var shift = new CaLamViec { MaCaHienThi = "CA-" + Guid.NewGuid().ToString("N")[..20], MaNhanVien = employee.MaNhanVien, TenCa = input.TenCa, MaMay = input.MaMay.Trim(), BatDauLuc = DateTime.UtcNow, TrangThai = "OPEN", TienMatDauCa = input.TienMatDauCa };
            try { shift.MaCaBanGiaoTruoc = await HandoverLink.SourceForOpen(db, employee.MaNhanVien, device.MaMay); }
            catch (WorkforceRule error) { return Results.Problem(statusCode: error.Status, detail: error.Message); }
            db.CaLamViec.Add(shift); await db.SaveChangesAsync();
            var audit = AuditFactory.Create("STAFF", shift.MaCaLamViec, "OPEN_SHIFT", "CaLamViec", shift.MaCaLamViec); audit.TenNguoiThucHienDaChot = employee.HoTen; audit.MaMayDaChot = shift.MaMay; db.NhatKyHeThong.Add(audit);
            await db.SaveChangesAsync(); await tx.CommitAsync();
            return Results.Ok(new SessionResult(PosAccess.Token(shift.MaCaLamViec, device), "STAFF", employee.HoTen, employee.MaNhanVien, shift.MaCaLamViec));
        });
        app.MapPost("/api/pos/shifts/current/close", async (CloseShift input, HttpContext context, CafeDbContext db) =>
        {
            if (input.TienMatThucDem < 0 || input.TienMatThucDem != decimal.Truncate(input.TienMatThucDem)) return Results.Problem(detail: "Tiền thực đếm không hợp lệ.", statusCode: 400);
            var id = long.Parse(context.User.FindFirst("shift")!.Value);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await WorkforceOperation.Lock(db);
            var shift = await db.CaLamViec.SingleAsync(x => x.MaCaLamViec == id);
            if (shift.TrangThai != "OPEN") return Results.Conflict();
            var reconciliation = await new ShiftReconciliation(db).Detail(shift);
            if (reconciliation.DonDangXuLy.Count > 0 || reconciliation.KhoanChuaRo.Count > 0)
                return Results.Problem(statusCode: 409, detail: "Còn đơn hoặc khoản chưa rõ cần xử lý/bàn giao trước khi kết thúc ca.");
            shift.TienMatDuKienCuoiCa = await FourChang.Api.Shared.WorkSessions.ShiftCash.Expected(db, shift);
            if (input.TienMatThucDem != shift.TienMatDuKienCuoiCa && string.IsNullOrWhiteSpace(input.LyDoChenhLech)) return Results.Problem(detail: "Bắt buộc ghi lý do chênh lệch tiền.", statusCode: 400);
            shift.TienMatThucDem = input.TienMatThucDem; shift.LyDoChenhLech = input.LyDoChenhLech; shift.KetThucLuc = DateTime.UtcNow; shift.TrangThai = "CLOSED";
            var audit = AuditFactory.Create("STAFF", id, "CLOSE_SHIFT", "CaLamViec", id); audit.TenNguoiThucHienDaChot = context.User.Identity?.Name ?? "Nhân viên"; audit.MaMayDaChot = shift.MaMay; db.NhatKyHeThong.Add(audit);
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(new { message = "Đã kết thúc ca" });
        }).RequireAuthorization("Staff");
    }
}
