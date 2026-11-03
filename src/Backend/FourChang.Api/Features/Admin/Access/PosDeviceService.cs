using FourChang.Api.Shared.Audit;
using FourChang.Api.Shared.WorkSessions;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FourChang.Domain.Entities.Accounts;
using FourChang.Domain.Entities.Audit;
using FourChang.Domain.Entities.Employees;
using FourChang.Domain.Entities.Orders;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Staff;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.Access;

// A3 của UC-ADM-02: tập quyền POS cố định. Không tạo vai trò/tài khoản nhân viên hoặc cho POS gọi Admin.
public sealed class PosDeviceService(CafeDbContext db)
{
    private sealed class Rule(int status, string message) : Exception(message) { internal int Status => status; }
    private sealed record Change(ThietBiPOS Device, string? Before, long? ShiftId = null);
    public static string Machine(string? text) => text?.Trim().ToUpperInvariant() ?? "";
    private static void Reason(string? reason) { if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500) throw new Rule(400, "Lý do bắt buộc, tối đa 500 ký tự."); }
    private static void Version(byte[]? supplied, byte[] current) { if (supplied is not { Length: 8 }) throw new Rule(400, "Thiếu phiên bản dữ liệu."); if (!supplied.SequenceEqual(current)) throw new Rule(409, "Dữ liệu đã thay đổi. Tải lại để đối chiếu trước khi xác nhận."); }
    private async Task<PosDeviceInfo> View(ThietBiPOS device) => new(device.MaThietBiPOS, device.MaMay, device.TenMay,
        device.TrangThai, device.CapLuc, device.ThuHoiLuc, device.LyDo, device.PhienBanDuLieu,
        await db.CaLamViec.CountAsync(x => x.MaMay == device.MaMay && x.KetThucLuc == null));

    public async Task<IResult> List()
    {
        var devices = await db.ThietBiPOS.AsNoTracking().OrderBy(x => x.MaMay).ToListAsync();
        var rows = new List<PosDeviceInfo>(); foreach (var device in devices) rows.Add(await View(device));
        return Results.Ok(rows);
    }
    public async Task<IResult> Register(RegisterPosDevice input, HttpContext context) => await Execute(input.MaThaoTac, "POS_DEVICE_GRANTED", input, context, async () =>
    {
        Reason(input.LyDo); var machine = Machine(input.MaMay);
        if (!Regex.IsMatch(machine, "^[A-Z0-9][A-Z0-9._-]{0,49}$") || string.IsNullOrWhiteSpace(input.TenMay) || input.TenMay.Trim().Length > 120)
            throw new Rule(400, "Mã máy dùng chữ không dấu, số, . _ - và tối đa 50 ký tự; tên máy bắt buộc, tối đa 120 ký tự.");
        if (input.KhoaThietBi is not { Length: >= 32 and <= 256 }) throw new Rule(400, "Khóa thiết bị phải có 32–256 ký tự.");
        var device = await db.ThietBiPOS.SingleOrDefaultAsync(x => x.MaMay == machine);
        var before = device is null ? null : JsonSerializer.Serialize(await View(device));
        if (device is not null)
        {
            Version(input.PhienBanDuLieu, device.PhienBanDuLieu);
            if (await db.CaLamViec.AnyAsync(x => x.MaMay == machine && x.KetThucLuc == null))
                throw new Rule(409, "Cần chốt/bàn giao ca liên quan trước khi cấp lại cấu hình máy.");
        }
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input.KhoaThietBi));
        if (await db.ThietBiPOS.AnyAsync(x => x.MaMay != machine && x.MaBamKhoa == hash)) throw new Rule(409, "Khóa thiết bị đã được sử dụng. Hãy sinh khóa mới riêng cho máy này.");
        if (device is null) { device = new ThietBiPOS { MaMay = machine }; db.ThietBiPOS.Add(device); }
        device.TenMay = input.TenMay.Trim(); device.MaBamKhoa = hash; device.TrangThai = "ACTIVE";
        device.MaQuanLyCap = 1; device.CapLuc = DateTime.UtcNow; device.ThuHoiLuc = null; device.LyDo = input.LyDo.Trim();
        return new Change(device, before);
    });

    public async Task<IResult> Revoke(long id, RevokePosDevice input, HttpContext context) => await Execute(input.MaThaoTac, "POS_DEVICE_REVOKED", new { id, input }, context, async () =>
    {
        Reason(input.LyDo); var device = await db.ThietBiPOS.SingleOrDefaultAsync(x => x.MaThietBiPOS == id) ?? throw new Rule(404, "Không tìm thấy thiết bị.");
        Version(input.PhienBanDuLieu, device.PhienBanDuLieu);
        if (device.TrangThai != "ACTIVE") throw new Rule(409, "Thiết bị đã bị thu hồi. Tải lại trạng thái hiện tại.");
        var before = JsonSerializer.Serialize(await View(device));
        // Không tự đóng ca hoặc đổi chủ ca. Middleware sẽ từ chối cả token ca đã cấp trên máy thu hồi.
        device.TrangThai = "REVOKED"; device.ThuHoiLuc = DateTime.UtcNow; device.LyDo = input.LyDo.Trim();
        return new Change(device, before);
    });

    public async Task<IResult> Shifts(long id)
    {
        var device = await db.ThietBiPOS.AsNoTracking().SingleOrDefaultAsync(x => x.MaThietBiPOS == id); if (device is null) return Results.NotFound();
        var shifts = await (from shift in db.CaLamViec.AsNoTracking() join employee in db.NhanVien on shift.MaNhanVien equals employee.MaNhanVien
            where shift.MaMay == device.MaMay && shift.KetThucLuc == null
            select new { Shift = shift, Employee = employee.HoTen }).ToListAsync();
        var rows = new List<DeviceShift>();
        foreach (var row in shifts) rows.Add(new(row.Shift.MaCaLamViec, row.Shift.MaCaHienThi, row.Employee, row.Shift.TenCa,
            row.Shift.TrangThai, row.Shift.BatDauLuc, row.Shift.TienMatDauCa, await ShiftCash.Expected(db, row.Shift), row.Shift.PhienBanDuLieu,
            await db.DonHang.AnyAsync(x => x.MaCaLamViec == row.Shift.MaCaLamViec && x.TrangThai != "COMPLETED" && x.TrangThai != "CANCELLED" && x.TrangThai != "REJECTED")));
        return Results.Ok(rows);
    }
    public async Task<IResult> CloseRelated(long deviceId, long shiftId, CloseDeviceShift input, HttpContext context) => await Execute(input.MaThaoTac, "POS_DEVICE_SHIFT_CLOSED", new { deviceId, shiftId, input }, context, async () =>
    {
        Reason(input.LyDo);
        if (input.TienMatThucDem < 0 || decimal.Truncate(input.TienMatThucDem) != input.TienMatThucDem) throw new Rule(400, "Tiền thực đếm không hợp lệ.");
        var device = await db.ThietBiPOS.SingleOrDefaultAsync(x => x.MaThietBiPOS == deviceId) ?? throw new Rule(404, "Không tìm thấy thiết bị.");
        if (device.TrangThai != "REVOKED") throw new Rule(409, "Tại chức năng này chỉ xử lý ca của thiết bị đã thu hồi.");
        var shift = await db.CaLamViec.SingleOrDefaultAsync(x => x.MaCaLamViec == shiftId && x.MaMay == device.MaMay) ?? throw new Rule(404, "Ca không thuộc thiết bị này.");
        Version(input.PhienBanDuLieu, shift.PhienBanDuLieu);
        if (shift.KetThucLuc is not null) throw new Rule(409, "Ca đã kết thúc.");
        if (await db.DonHang.AnyAsync(x => x.MaCaLamViec == shiftId && x.TrangThai != "COMPLETED" && x.TrangThai != "CANCELLED" && x.TrangThai != "REJECTED"))
            throw new Rule(409, "Ca còn đơn cần xử lý/bàn giao. Không thể chốt bằng cách bỏ qua đơn đang xử lý.");
        var before = JsonSerializer.Serialize(new { shift.MaCaLamViec, shift.TrangThai, shift.TienMatThucDem });
        shift.TienMatDuKienCuoiCa = await ShiftCash.Expected(db, shift); shift.TienMatThucDem = input.TienMatThucDem;
        shift.LyDoChenhLech = input.LyDo.Trim(); shift.GhiChu = "Chốt ca máy đã thu hồi: " + input.LyDo.Trim();
        shift.KetThucLuc = DateTime.UtcNow; shift.TrangThai = "CLOSED"; shift.MaQuanLyDuyet = 1;
        return new Change(device, before, shiftId);
    });

    public async Task<IResult> Operation(Guid operation) =>
        await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == operation && x.MaQuanLy == 1 && x.LoaiDoiTuong == "ThietBiPOS") is { KetQuaDaChotJson: { } json }
            ? Results.Ok(JsonSerializer.Deserialize<PosDeviceOperation>(json)) : Results.NotFound();

    private async Task<IResult> Execute(Guid operation, string action, object input, HttpContext context, Func<Task<Change>> mutate)
    {
        if (operation == Guid.Empty) return Results.Problem(detail: "Thiếu mã thao tác.", statusCode: 400);
        var key = Environment.GetEnvironmentVariable("FOURCHANG_POS_KEY") ?? throw new InvalidOperationException("Thiếu khóa máy chủ.");
        // HMAC bảo vệ kiểm tra chống trùng; không ghi khóa thiết bị hoặc bản băm khóa vào audit/DTO.
        var digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), JsonSerializer.SerializeToUtf8Bytes(new { action, input }));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var resource = "UC-ADM-02:DEVICE:" + operation.ToString("N");
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r<0 THROW 51090, 'Operation lock timeout', 1;");
        var existing = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == operation);
        if (existing is not null)
        {
            if (existing.LoaiDoiTuong != "ThietBiPOS" || existing.HanhDong != action || existing.MaBamYeuCau is null || !CryptographicOperations.FixedTimeEquals(existing.MaBamYeuCau, digest))
                return Results.Problem(detail: "Mã thao tác đã dùng với nội dung khác.", statusCode: 409);
            return Results.Ok(JsonSerializer.Deserialize<PosDeviceOperation>(existing.KetQuaDaChotJson!));
        }
        try
        {
            var change = await mutate(); await db.SaveChangesAsync();
            var result = new PosDeviceOperation(operation, await View(change.Device), change.ShiftId);
            var audit = AuditFactory.Create("ADMIN", 1, action, "ThietBiPOS", change.Device.MaThietBiPOS);
            audit.TenNguoiThucHienDaChot = context.User.Identity?.Name ?? "Quản lý"; audit.MaThaoTac = operation;
            audit.MaBamYeuCau = digest; audit.DuLieuTruocJson = change.Before; audit.DuLieuSauJson = JsonSerializer.Serialize(result);
            audit.LyDo = change.ShiftId is null ? change.Device.LyDo : "Xử lý ca liên quan đến thu hồi máy";
            audit.KetQuaDaChotJson = JsonSerializer.Serialize(result); db.NhatKyHeThong.Add(audit);
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(result);
        }
        catch (Rule rule) { return Results.Problem(detail: rule.Message, statusCode: rule.Status); }
    }
}

// Một công thức tiền mặt dùng cho POS và xử lý ca máy bị thu hồi, tránh tính chênh nhau ở hai màn hình.
