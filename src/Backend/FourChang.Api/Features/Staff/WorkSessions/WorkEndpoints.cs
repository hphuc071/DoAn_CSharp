using System.Data;
using FourChang.Api.Security;
using FourChang.Api.Shared.Audit;
using FourChang.Api.Shared.Operations;
using FourChang.Api.Shared.WorkSessions;
using FourChang.Domain.Entities.Employees;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Common;
using FourChang.Presentation.Shared.Contracts.Staff;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Staff.WorkSessions;

/// <summary>POS mở ca bằng hồ sơ khai báo, không đăng nhập tài khoản cá nhân; mọi quyền ghi sau đó cần phiên ca ký.</summary>
public static class WorkEndpoints
{
    /// <summary>Giữ URL tích hợp hiện có, bổ sung kiểm tra ca dùng chung với Admin.</summary>
    public static void MapWork(this WebApplication app)
    {
        app.MapGet("/api/pos/employees", async (HttpContext context, CafeDbContext db) =>
        {
            if (context.Request.Headers.ContainsKey("X-POS-Key") && await PosAccess.Device(context.Request, db) is null) return Results.Unauthorized();
            return Results.Ok(await db.NhanVien.AsNoTracking().Where(x => x.TrangThai == "ACTIVE").OrderBy(x => x.HoTen)
                .Select(x => new Employee(x.MaNhanVien, x.HoTen, null, x.NgayVaoLam, x.TrangThai, null, null)).ToListAsync());
        });
        app.MapPost("/api/pos/employees/verify", Verify);
        app.MapPost("/api/pos/shifts", Open);
        // Handler tự kiểm tra chữ ký để tra kết quả của ca đã đóng khi mất phản hồi; không cho phiên đối chiếu chốt.
        app.MapPost("/api/pos/shifts/current/close", (CloseShift input, HttpContext context, ShiftStaffService service) => service.Close(input, context));
    }

    /// <summary>Đối chiếu mã và tên ACTIVE; không tạo hồ sơ hoặc mô tả đây là xác thực cá nhân bằng mật khẩu.</summary>
    private static async Task<IResult> Verify(VerifyEmployee input, HttpContext context, CafeDbContext db)
    {
        if (context.Request.Headers.ContainsKey("X-POS-Key") && await PosAccess.Device(context.Request, db) is null) return Results.Unauthorized();
        if (input.MaNhanVien <= 0 || string.IsNullOrWhiteSpace(input.HoTen) || input.HoTen.Length > 120)
            return Results.Problem(statusCode: 400, detail: "Nhập mã nhân viên và đầy đủ họ tên.");
        var employee = await db.NhanVien.AsNoTracking().SingleOrDefaultAsync(x => x.MaNhanVien == input.MaNhanVien && x.TrangThai == "ACTIVE");
        if (employee is null || !EmployeeIdentity.Matches(employee.HoTen, input.HoTen))
            return Results.Problem(statusCode: 404, detail: "Không tìm thấy nhân viên đang làm khớp mã và họ tên.");
        return Results.Ok(new Employee(employee.MaNhanVien, employee.HoTen, null, employee.NgayVaoLam, employee.TrangThai));
    }

    /// <summary>Mở ca nguyên tử với kiểm tra trạng thái/ca trùng/bàn giao. Có ca cũ phải chứng minh còn giữ phiên.</summary>
    private static async Task<IResult> Open(OpenShift input, HttpContext context, CafeDbContext db)
    {
        try
        {
            // Nhánh thiết bị chỉ để tương thích client nhóm cũ; luồng mới không gửi hoặc yêu cầu khóa máy.
            var legacy = context.Request.Headers.ContainsKey("X-POS-Key");
            var device = legacy ? await PosAccess.Device(context.Request, db) : null;
            if (legacy && device is null) return Results.Unauthorized();
            var machine = ShiftSessionCodec.Machine(input.MaMay);
            if (device is not null && device.MaMay != machine) return Results.Problem(statusCode: 403, detail: "Mã máy không khớp client thiết bị cũ.");
            if (input.TenCa is not ("Sáng" or "Chiều" or "Tối") || input.MaNhanVien <= 0 || string.IsNullOrWhiteSpace(input.HoTen) || input.HoTen.Length > 120 ||
                input.TienMatDauCa < 0 || input.TienMatDauCa > 999999999999999999m || decimal.Truncate(input.TienMatDauCa) != input.TienMatDauCa)
                throw new AdminRule(400, "Hồ sơ, tên ca hoặc tiền đầu ca không hợp lệ.");
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await AdminOperation.Lock(db);
            var employee = await db.NhanVien.SingleOrDefaultAsync(x => x.MaNhanVien == input.MaNhanVien && x.TrangThai == "ACTIVE");
            if (employee is null || !EmployeeIdentity.Matches(employee.HoTen, input.HoTen)) throw new AdminRule(400, "Mã/họ tên không khớp nhân viên đang làm.");
            var current = await db.CaLamViec.FirstOrDefaultAsync(x => x.KetThucLuc == null && (x.MaMay == machine || x.MaNhanVien == employee.MaNhanVien));
            if (current is not null)
            {
                var proof = await ShiftSessionCodec.Resolve(context, db);
                if (current.TrangThai != "OPEN" || current.MaNhanVien != employee.MaNhanVien || current.MaMay != machine ||
                    device is null && (proof is null || proof.ReconciliationOnly || proof.Shift != current.MaCaLamViec))
                    throw new AdminRule(409, "Nhân viên/máy đã có ca. Dùng phiên đã lưu để tiếp tục; mất phiên cần Quản lý cấp mã phục hồi đối chiếu.");
                var token = device is null ? context.Request.Headers.Authorization.ToString()[7..] : PosAccess.Token(current.MaCaLamViec, device);
                return Results.Ok(new SessionResult(token, "STAFF", employee.HoTen, employee.MaNhanVien, current.MaCaLamViec));
            }
            var source = await HandoverLink.SourceForOpen(db, employee.MaNhanVien, machine);
            var shift = new CaLamViec
            {
                MaCaHienThi = "CA-" + Guid.NewGuid().ToString("N")[..20], MaNhanVien = employee.MaNhanVien,
                TenCa = input.TenCa, MaMay = machine, BatDauLuc = DateTime.UtcNow, TrangThai = "OPEN",
                TienMatDauCa = input.TienMatDauCa, MaCaBanGiaoTruoc = source
            };
            db.CaLamViec.Add(shift);
            await db.SaveChangesAsync();
            var audit = AuditFactory.Create("STAFF", shift.MaCaLamViec, "OPEN_SHIFT", "CaLamViec", shift.MaCaLamViec);
            audit.TenNguoiThucHienDaChot = employee.HoTen;
            audit.MaMayDaChot = machine;
            audit.DuLieuSauJson = System.Text.Json.JsonSerializer.Serialize(new { shift.MaCaLamViec, shift.MaNhanVien, shift.TenCa, shift.MaMay, shift.MaCaBanGiaoTruoc, shift.TienMatDauCa });
            db.NhatKyHeThong.Add(audit);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            var session = device is null ? ShiftSessionCodec.Create(shift.MaCaLamViec) : PosAccess.Token(shift.MaCaLamViec, device);
            return Results.Ok(new SessionResult(session, "STAFF", employee.HoTen, employee.MaNhanVien, shift.MaCaLamViec));
        }
        catch (AdminRule error) { return Results.Problem(statusCode: error.Status, detail: error.Message); }
    }
}
