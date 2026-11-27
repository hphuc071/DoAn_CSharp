// UC: UC-POS-01
// Chức năng: Kiểm tra hồ sơ nhân viên và mở ca làm việc.
// Cần nhớ: Một nhân viên không được chọn lại cùng tên ca trong một ngày; không bắt buộc giờ thực tế.

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
            if (context.Request.Headers.ContainsKey("X-POS-Key")) return Results.Unauthorized();
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
        if (context.Request.Headers.ContainsKey("X-POS-Key")) return Results.Unauthorized();
        if (input.MaNhanVien <= 0 || string.IsNullOrWhiteSpace(input.HoTen) || input.HoTen.Length > 120)
            return Results.Problem(statusCode: 400, detail: "Nhập mã nhân viên và đầy đủ họ tên.");
        var employee = await db.NhanVien.AsNoTracking().SingleOrDefaultAsync(x => x.MaNhanVien == input.MaNhanVien && x.TrangThai == "ACTIVE");
        if (employee is null || !EmployeeIdentity.Matches(employee.HoTen, input.HoTen))
            return Results.Problem(statusCode: 404, detail: "Không tìm thấy nhân viên đang làm khớp mã và họ tên.");
        return Results.Ok(new Employee(employee.MaNhanVien, employee.HoTen, null, employee.NgayVaoLam, employee.TrangThai,
            CaDaLamTrongNgay: await UsedShifts(db, employee.MaNhanVien, DateTime.UtcNow)));
    }

    /// <summary>
    /// UC-POS-01 — Mở một ca theo hồ sơ nhân viên.
    ///
    /// Hàm này được dùng để:
    /// 1. Kiểm tra thông tin nhân viên, tên ca và tiền đầu ca.
    /// 2. Đối chiếu nhân viên đang làm việc và kiểm tra yêu cầu gửi lại.
    /// 3. Kiểm tra nhân viên đã làm ca cùng tên trong ngày chưa.
    /// 4. Tạo ca mới, ghi nhận thao tác và trả phiên ca cho ứng dụng nhân viên.
    ///
    /// Thông tin cần truyền vào:
    /// - input: Nhân viên, tên ca, máy sử dụng, tiền đầu ca và mã thao tác.
    /// - context: Thông tin phiên và quyền của người đang thao tác.
    /// - db: Đối tượng dùng để đọc và ghi dữ liệu của hệ thống.
    ///
    /// Sau khi xử lý: Phiên ca làm việc hợp lệ hoặc lỗi; không mở lặp cùng tên ca trong ngày.
    /// </summary>
    private static async Task<IResult> Open(OpenShift input, HttpContext context, CafeDbContext db)
    {
        try
        {
            // Client thiết bị đời cũ phải mở lại theo hồ sơ; không chấp nhận khóa máy như phiên nhân viên.
            if (context.Request.Headers.ContainsKey("X-POS-Key")) return Results.Unauthorized();

            // Bước 1: Kiểm tra dữ liệu mở ca và mã máy của phiên làm việc.
            var machine = ShiftSessionCodec.Machine(input.MaMay);
            if (input.TenCa is not ("Sáng" or "Chiều" or "Tối") || input.MaNhanVien <= 0 || string.IsNullOrWhiteSpace(input.HoTen) || input.HoTen.Length > 120 ||
                input.TienMatDauCa < 0 || input.TienMatDauCa > 999999999999999999m || decimal.Truncate(input.TienMatDauCa) != input.TienMatDauCa)
                throw new AdminRule(400, "Hồ sơ, tên ca hoặc tiền đầu ca không hợp lệ.");
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await AdminOperation.Lock(db);

            // Bước 2: Đối chiếu mã và họ tên với hồ sơ nhân viên đang hoạt động.
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
                    return Results.Ok(new SessionResult(ShiftSessionCodec.Create(known.MaCaLamViec),
                        "STAFF", employee.HoTen, employee.MaNhanVien, known.MaCaLamViec));
                }
            }
            else
            {
                // Tương thích client cũ: có phiên ca rõ ràng thì tiếp tục đúng ID đó, không chọn ngẫu nhiên một ca của máy.
                var proof = await ShiftSessionCodec.Resolve(context, db);
                var old = proof is not null && !proof.ReconciliationOnly
                    ? await db.CaLamViec.SingleOrDefaultAsync(x => x.MaCaLamViec == proof.Shift && x.MaNhanVien == employee.MaNhanVien && x.MaMay == machine && x.TenCa == input.TenCa && x.TrangThai == "OPEN")
                    : null;
                if (old is not null)
                    return Results.Ok(new SessionResult(context.Request.Headers.Authorization.ToString()[7..],
                        "STAFF", employee.HoTen, employee.MaNhanVien, old.MaCaLamViec));
            }
            var now = DateTime.UtcNow;
            // Kiểm sau receipt: gửi lại cùng mã vẫn trả ca cũ; mã mới không được lặp tên ca trong ngày.

            // Bước 3: Chặn nhân viên chọn lại cùng ca Sáng/Chiều/Tối trong ngày.
            if ((await UsedShifts(db, employee.MaNhanVien, now)).Contains(input.TenCa))
                throw new AdminRule(409, $"Nhân viên đã làm ca {input.TenCa} hôm nay. Vui lòng chọn ca khác; ngày mai có thể chọn lại.");
            var source = await HandoverLink.SourceForOpen(db, employee.MaNhanVien, machine);

            // Bước 4: Tạo ca, lưu nhật ký và trả phiên làm việc cho ứng dụng nhân viên.
            var shift = new CaLamViec
            {
                MaNhanVien = employee.MaNhanVien,
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
            var session = ShiftSessionCodec.Create(shift.MaCaLamViec);
            return Results.Ok(new SessionResult(session, "STAFF", employee.HoTen, employee.MaNhanVien, shift.MaCaLamViec));
        }
        catch (AdminRule error) { return Results.Problem(statusCode: error.Status, detail: error.Message); }
    }
    /// <summary>
    /// UC-POS-01 — Lấy các tên ca nhân viên đã làm trong ngày nghiệp vụ.
    ///
    /// Hàm này được dùng để:
    /// 1. Xác định đầu ngày và cuối ngày cần kiểm tra theo giờ nghiệp vụ.
    /// 2. Tìm các ca nhân viên đã mở trong khoảng ngày đó.
    /// 3. Trả các tên ca để phần mở ca chặn chọn trùng.
    ///
    /// Thông tin cần truyền vào:
    /// - db: Đối tượng dùng để đọc và ghi dữ liệu của hệ thống.
    /// - employeeId: Khóa của nhân viên cần kiểm tra ca.
    /// - now: Thời điểm đang thực hiện việc xử lý.
    ///
    /// Sau khi xử lý: Danh sách tên ca để chặn chọn trùng.
    /// </summary>
    private static Task<List<string>> UsedShifts(CafeDbContext db, long employeeId, DateTime now)
    {
        var start = DateTime.SpecifyKind(now.AddHours(7).Date.AddHours(-7), DateTimeKind.Utc);
        var end = start.AddDays(1);
        return db.CaLamViec.AsNoTracking()
            .Where(x => x.MaNhanVien == employeeId && x.BatDauLuc >= start && x.BatDauLuc < end)
            .Select(x => x.TenCa).Distinct().ToListAsync();
    }}
