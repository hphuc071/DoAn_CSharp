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
                .Select(x => new Employee(x.MaNhanVien, x.HoTen, null, x.NgayVaoLam, x.TrangThai, null, null, null)).ToListAsync());
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
        return Results.Ok(new Employee(employee.MaNhanVien, employee.HoTen, null, employee.NgayVaoLam, employee.TrangThai,
            CaDaLamTrongNgay: await UsedShifts(db, employee.MaNhanVien, DateTime.UtcNow)));
    }

    /// <summary>Mở ca độc lập, xác minh hồ sơ và mã chống trùng thao tác; mỗi tên ca chỉ được mở một lần/ngày/nhân viên, không phụ thuộc giờ hoặc máy.</summary>
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
            // Mỗi lần bấm Mở ca là một thao tác riêng. Gửi lặp sau timeout chỉ trả lại ca đã tạo bởi đúng mã này.
            var digest = System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(input));
            if (input.MaThaoTac != Guid.Empty)
            {
                var saved = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == input.MaThaoTac);
                if (saved is not null)
                {
                    if (saved.HanhDong != "OPEN_SHIFT" || saved.MaBamYeuCau is null ||
                        !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(saved.MaBamYeuCau, digest) || saved.MaCaLamViec is null)
                        throw new AdminRule(409, "Mã mở ca đã dùng với nội dung khác.");
                    var known = await db.CaLamViec.SingleAsync(x => x.MaCaLamViec == saved.MaCaLamViec);
                    return Results.Ok(new SessionResult(device is null ? ShiftSessionCodec.Create(known.MaCaLamViec) : PosAccess.Token(known.MaCaLamViec, device),
                        "STAFF", employee.HoTen, employee.MaNhanVien, known.MaCaLamViec));
                }
            }
            else
            {
                // Tương thích client cũ: có phiên ca rõ ràng thì tiếp tục đúng ID đó, không chọn ngẫu nhiên một ca của máy.
                var proof = await ShiftSessionCodec.Resolve(context, db);
                var old = proof is not null && !proof.ReconciliationOnly
                    ? await db.CaLamViec.SingleOrDefaultAsync(x => x.MaCaLamViec == proof.Shift && x.MaNhanVien == employee.MaNhanVien && x.MaMay == machine && x.TenCa == input.TenCa && x.TrangThai == "OPEN")
                    : device is not null ? await db.CaLamViec.Where(x => x.MaNhanVien == employee.MaNhanVien && x.MaMay == machine && x.TenCa == input.TenCa && x.TrangThai == "OPEN").OrderByDescending(x => x.MaCaLamViec).FirstOrDefaultAsync() : null;
                if (old is not null)
                    return Results.Ok(new SessionResult(device is null ? context.Request.Headers.Authorization.ToString()[7..] : PosAccess.Token(old.MaCaLamViec, device),
                        "STAFF", employee.HoTen, employee.MaNhanVien, old.MaCaLamViec));
            }
            var now = DateTime.UtcNow;
            // Kiểm sau receipt: gửi lại cùng mã vẫn trả ca cũ; mã mới không được lặp tên ca trong ngày.
            if ((await UsedShifts(db, employee.MaNhanVien, now)).Contains(input.TenCa))
                throw new AdminRule(409, $"Nhân viên đã làm ca {input.TenCa} hôm nay. Vui lòng chọn ca khác; ngày mai có thể chọn lại.");
            var source = await HandoverLink.SourceForOpen(db, employee.MaNhanVien, machine);
            var shift = new CaLamViec
            {
                MaCaHienThi = "CA-" + Guid.NewGuid().ToString("N")[..20], MaNhanVien = employee.MaNhanVien,
                TenCa = input.TenCa, MaMay = machine, BatDauLuc = now, TrangThai = "OPEN",
                TienMatDauCa = input.TienMatDauCa, MaCaBanGiaoTruoc = source
            };
            db.CaLamViec.Add(shift);
            await db.SaveChangesAsync();
            var audit = AuditFactory.Create("STAFF", shift.MaCaLamViec, "OPEN_SHIFT", "CaLamViec", shift.MaCaLamViec);
            if (input.MaThaoTac != Guid.Empty) { audit.MaThaoTac = input.MaThaoTac; audit.MaBamYeuCau = digest; }
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
    /// <summary>Ngày nghiệp vụ UTC+07, lấy cả ca đã đóng/khóa; không ràng buộc giờ Sáng/Chiều/Tối.</summary>
    private static Task<List<string>> UsedShifts(CafeDbContext db, long employeeId, DateTime now)
    {
        var start = DateTime.SpecifyKind(now.AddHours(7).Date.AddHours(-7), DateTimeKind.Utc);
        var end = start.AddDays(1);
        return db.CaLamViec.AsNoTracking()
            .Where(x => x.MaNhanVien == employeeId && x.BatDauLuc >= start && x.BatDauLuc < end)
            .Select(x => x.TenCa).Distinct().ToListAsync();
    }}
