using System.Security.Claims;
using FourChang.Api.Security;
using FourChang.Api.Shared.Operations;
using FourChang.Api.Features.Admin.WorkSessions;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Staff;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Staff.WorkSessions;

/// <summary>Điểm nối POS-01/ADM-12: chữ ký phiên ca bảo vệ thao tác; nhân viên không có tài khoản cá nhân.</summary>
public sealed class ShiftStaffService(CafeDbContext db, WorkSessionService sessions)
{
    /// <summary>Đọc riêng ca thuộc token, kể cả LOCKED; không cấp quyền Admin hoặc đọc ca tùy ý theo ID.</summary>
    public async Task<IResult> Current(HttpContext context)
    {
        var grant = await ShiftSessionCodec.Resolve(context, db, true);
        return grant is null ? Results.Unauthorized() : await sessions.Details(grant.Shift);
    }

    /// <summary>Tra kết quả lệnh thuộc đúng ca sau timeout, không trả token hoặc receipt của ca khác.</summary>
    public async Task<IResult> Receipt(Guid operation, HttpContext context)
    {
        var grant = await ShiftSessionCodec.Resolve(context, db, true);
        if (grant is null) return Results.Unauthorized();
        var audit = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == operation && x.MaCaLamViec == grant.Shift && x.MaQuanLy == null);
        return audit?.KetQuaDaChotJson is { } json ? Results.Content(json, "application/json") : Results.NotFound();
    }

    /// <summary>Lưu tiền thực đếm và LOCKED nguyên tử. Phiên phục hồi chỉ gửi một lần, không thể mở bán.</summary>
    public Task<IResult> Submit(SubmitReconciliation input, HttpContext context) => Execute(context, input.MaThaoTac,
        "SHIFT_RECONCILIATION_SENT", input, async grant =>
    {
        if (input.TienMatThucDem is not { } counted || counted < 0 || counted > 999999999999999999m || counted != decimal.Truncate(counted) || input.LyDoChenhLech?.Length > 500)
            throw new AdminRule(400, "Tiền thực đếm phải là số đồng không âm; lý do tối đa 500 ký tự.");
        var shift = await db.CaLamViec.SingleAsync(x => x.MaCaLamViec == grant.Shift);
        AdminOperation.Version(input.PhienBanDuLieu, shift.PhienBanDuLieu);
        var lifecycle = await sessions.LastLifecycle(grant.Shift);
        if (shift.KetThucLuc is not null || (grant.ReconciliationOnly
                ? shift.TrangThai != "LOCKED" || lifecycle != "SHIFT_RECOVERY_ISSUED"
                : shift.TrangThai != "OPEN"))
            throw new AdminRule(409, "Ca không còn cho phép gửi số đếm mới. Tải lại để xem kết quả hoặc liên hệ Quản lý.");
        var detail = await sessions.Detail(shift);
        if (detail.TienMatDuKienHienTai < 0) throw new AdminRule(409, "Nguồn tiền đang bất thường. Cần đối chiếu thu/hoàn trước.");
        if (counted != detail.TienMatDuKienHienTai) AdminOperation.Reason(input.LyDoChenhLech);
        shift.TienMatThucDem = counted;
        shift.TienMatDuKienCuoiCa = detail.TienMatDuKienHienTai;
        shift.LyDoChenhLech = input.LyDoChenhLech?.Trim();
        shift.TrangThai = "LOCKED";
        shift.ThoiDiemCapNhat = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return new { maCaLamViec = shift.MaCaLamViec, trangThai = shift.TrangThai, phienBanDuLieu = shift.PhienBanDuLieu };
    });

    /// <summary>Tạm khóa không ghi tiền thực đếm và không kết thúc ca.</summary>
    public Task<IResult> Lock(ResumeWorkSessionInput input, HttpContext context) => Execute(context, input.MaThaoTac,
        "SHIFT_TEMP_LOCKED", input, async grant =>
    {
        if (grant.ReconciliationOnly) throw new AdminRule(403, "Phiên phục hồi chỉ được gửi đối chiếu.");
        AdminOperation.Reason(input.LyDo);
        var shift = await db.CaLamViec.SingleAsync(x => x.MaCaLamViec == grant.Shift);
        AdminOperation.Version(input.PhienBanDuLieu, shift.PhienBanDuLieu);
        if (shift.TrangThai != "OPEN" || shift.KetThucLuc is not null) throw new AdminRule(409, "Ca không còn mở.");
        shift.TrangThai = "LOCKED";
        shift.ThoiDiemCapNhat = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return new { maCaLamViec = shift.MaCaLamViec, trangThai = shift.TrangThai };
    });

    /// <summary>Chứng minh còn giữ phiên hợp lệ và đúng hồ sơ trước khi Admin xem xét mở khóa tạm.</summary>
    public Task<IResult> RequestResume(RequestShiftResumeInput input, HttpContext context) => Execute(context, input.MaThaoTac,
        "SHIFT_RESUME_REQUESTED", input, async grant =>
    {
        if (grant.ReconciliationOnly) throw new AdminRule(403, "Mã phục hồi không cấp quyền tiếp tục bán hàng.");
        if (input.MaNhanVien != grant.Employee || !EmployeeIdentity.Matches(grant.Name, input.HoTen))
            throw new AdminRule(400, "Hồ sơ khai báo không khớp phiên ca.");
        var shift = await db.CaLamViec.AsNoTracking().SingleAsync(x => x.MaCaLamViec == grant.Shift);
        if (shift.TrangThai != "LOCKED" || shift.TienMatThucDem is not null || await sessions.LastLifecycle(grant.Shift) != "SHIFT_TEMP_LOCKED")
            throw new AdminRule(409, "Chỉ đề nghị tiếp tục ca khóa tạm chưa gửi đối chiếu.");
        return new { maCaLamViec = shift.MaCaLamViec, message = "Đã gửi yêu cầu. Quản lý kiểm tra tại Ca làm việc." };
    });

    /// <summary>Giữ đường chốt trực tiếp khi đủ điều kiện; không bỏ qua đơn dở, khoản chưa rõ hoặc lý do chênh lệch.</summary>
    public Task<IResult> Close(CloseShift input, HttpContext context) => Execute(context,
        input.MaThaoTac == Guid.Empty ? Guid.NewGuid() : input.MaThaoTac, "CLOSE_SHIFT", input, async grant =>
    {
        if (grant.ReconciliationOnly) throw new AdminRule(403, "Phiên phục hồi không được tự chốt ca.");
        var shift = await db.CaLamViec.SingleAsync(x => x.MaCaLamViec == grant.Shift);
        if (shift.TrangThai != "OPEN" || shift.KetThucLuc is not null) throw new AdminRule(409, "Ca không còn mở.");
        if (input.PhienBanDuLieu is not null) AdminOperation.Version(input.PhienBanDuLieu, shift.PhienBanDuLieu);
        if (input.TienMatThucDem is not { } counted || counted < 0 || counted > 999999999999999999m || counted != decimal.Truncate(counted))
            throw new AdminRule(400, "Tiền thực đếm không hợp lệ.");
        var detail = await sessions.Detail(shift);
        if (detail.DonDangXuLy.Count > 0 || detail.KhoanChuaRo.Count > 0)
            throw new AdminRule(409, "Ca còn đơn dở/khoản chưa rõ. Xử lý hoặc gửi đối chiếu để Quản lý bàn giao.");
        if (detail.TienMatDuKienHienTai < 0) throw new AdminRule(409, "Nguồn tiền chưa hợp lệ.");
        if (counted != detail.TienMatDuKienHienTai) AdminOperation.Reason(input.LyDoChenhLech);
        shift.TienMatThucDem = counted;
        shift.TienMatDuKienCuoiCa = detail.TienMatDuKienHienTai;
        shift.LyDoChenhLech = input.LyDoChenhLech?.Trim();
        shift.KetThucLuc = DateTime.UtcNow;
        shift.TrangThai = "CLOSED";
        shift.ThoiDiemCapNhat = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return new { maCaLamViec = shift.MaCaLamViec, trangThai = shift.TrangThai, message = "Đã kết thúc ca" };
    });

    /// <summary>Kiểm chữ ký trước và trong giao dịch; chỉ delegate nội bộ nhận principal Staff đã kiểm chứng.</summary>
    private async Task<IResult> Execute(HttpContext context, Guid operation, string action, object input, Func<ShiftSessionCodec.Grant, Task<object>> change)
    {
        var grant = await ShiftSessionCodec.Resolve(context, db, true);
        if (grant is null) return Results.Unauthorized();
        var verified = new DefaultHttpContext { RequestServices = context.RequestServices, TraceIdentifier = context.TraceIdentifier,
            User = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.Role, "STAFF"), new(ClaimTypes.Name, grant.Name), new("shift", grant.Shift.ToString())], "VerifiedShift")) };
        return await AdminOperation.Execute(db, operation, action, "CaLamViec", new { grant.Shift, input }, verified, async () =>
        {
            if (await ShiftSessionCodec.Resolve(context, db, true) is null) throw new AdminRule(401, "Phiên ca không còn hợp lệ.");
            var before = System.Text.Json.JsonSerializer.Serialize(await db.CaLamViec.AsNoTracking().SingleAsync(x => x.MaCaLamViec == grant.Shift));
            var result = await change(grant);
            var reason = input switch
            {
                SubmitReconciliation sent => sent.LyDoChenhLech,
                CloseShift closed => closed.LyDoChenhLech,
                ResumeWorkSessionInput locked => locked.LyDo,
                _ => "Nhân viên đề nghị tiếp tục bằng phiên còn hợp lệ"
            };
            return (grant.Shift, result, before, reason);
        }, allowStaff: true);
    }
}

/// <summary>Đăng ký luồng POS bổ trợ ADM-12; token chỉ đối chiếu không được dùng làm Staff toàn quyền.</summary>
public static class ReconciliationEndpoints
{
    /// <summary>Không nhận ID ca từ client cho các thao tác hiện tại; ca được suy ra từ token ký.</summary>
    public static void MapReconciliation(this WebApplication app)
    {
        app.MapGet("/api/pos/shifts/current/session", async (HttpContext c, CafeDbContext db, WorkSessionService service) =>
        {
            var grant = await ShiftSessionCodec.Resolve(c, db, true);
            if (grant is null) return Results.Unauthorized();
            var shift = await db.CaLamViec.AsNoTracking().SingleAsync(x => x.MaCaLamViec == grant.Shift);
            var token = c.Request.Headers.Authorization.ToString()[7..];
            var session = new FourChang.Presentation.Shared.Contracts.Common.SessionResult(token,
                grant.ReconciliationOnly ? "STAFF_RECONCILIATION" : "STAFF", grant.Name, grant.Employee, grant.Shift);
            return Results.Ok(new ShiftSessionContext(session, shift.TrangThai, grant.ReconciliationOnly,
                shift.TrangThai == "LOCKED" && await service.LastLifecycle(grant.Shift) == "SHIFT_RECONCILIATION_SENT"));
        });
        app.MapGet("/api/pos/shifts/current/reconciliation", (HttpContext c, ShiftStaffService s) => s.Current(c));
        app.MapGet("/api/pos/shifts/current/reconciliation/operations/{id:guid}", (Guid id, HttpContext c, ShiftStaffService s) => s.Receipt(id, c));
        app.MapPost("/api/pos/shifts/current/reconciliation", (SubmitReconciliation i, HttpContext c, ShiftStaffService s) => s.Submit(i, c));
        app.MapPost("/api/pos/shifts/current/lock", (ResumeWorkSessionInput i, HttpContext c, ShiftStaffService s) => s.Lock(i, c));
        app.MapPost("/api/pos/shifts/current/resume-request", (RequestShiftResumeInput i, HttpContext c, ShiftStaffService s) => s.RequestResume(i, c));
        app.MapPost("/api/pos/shifts/recovery", (RedeemShiftRecoveryInput i, ShiftRecoveryService s) => s.Redeem(i));
    }
}
