using System.Security.Cryptography;
using System.Text.Json;
using FourChang.Api.Security;
using FourChang.Api.Shared.Audit;
using FourChang.Api.Shared.WorkSessions;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Staff.WorkSessions;

/// <summary>Điểm tích hợp POS-01 với ADM-13 đã được chấp thuận: gửi số thực đếm và khóa ca để đối chiếu.</summary>
public static class ReconciliationEndpoints
{
    /// <summary>Token ca LOCKED chỉ được đọc kết quả đã gửi; không mở quyền bán hoặc quyền Admin.</summary>
    public static void MapReconciliation(this WebApplication app)
    {
        app.MapGet("/api/pos/shifts/current/reconciliation", async (HttpContext context, CafeDbContext db) =>
        {
            var id = await AllowedShift(context, db);
            if (id is null) return Results.Unauthorized();
            return await new ShiftReconciliation(db).Details(id.Value);
        });
        app.MapGet("/api/pos/shifts/current/reconciliation/operations/{operation:guid}", async (Guid operation, HttpContext context, CafeDbContext db) =>
        {
            var id = await AllowedShift(context, db);
            if (id is null) return Results.Unauthorized();
            var audit = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == operation &&
                x.MaCaLamViec == id && x.HanhDong == "SHIFT_RECONCILIATION_SENT");
            return audit?.KetQuaDaChotJson is { } json ? Results.Content(json, "application/json") : Results.NotFound();
        });
        app.MapPost("/api/pos/shifts/current/reconciliation", Submit);
    }

    /// <summary>Xác thực cả token ký, máy được cấp, thế hệ khóa và hồ sơ; không tin mã ca do client tự gửi.</summary>
    private static async Task<long?> AllowedShift(HttpContext context, CafeDbContext db)
    {
        var token = context.Request.Headers.Authorization.ToString();
        if (!token.StartsWith("Bearer ", StringComparison.Ordinal)) return null;
        var grant = PosAccess.Validate(token[7..]);
        var device = await PosAccess.Device(context.Request, db);
        if (grant is null || device is null || device.MaThietBiPOS != grant.Device || Convert.ToHexString(device.PhienBanDuLieu) != grant.Version) return null;
        return await db.CaLamViec.AnyAsync(x => x.MaCaLamViec == grant.Shift && x.MaMay == device.MaMay &&
            db.NhanVien.Any(e => e.MaNhanVien == x.MaNhanVien && e.TrangThai == "ACTIVE")) ? grant.Shift : null;
    }

    /// <summary>Lưu số đếm/khóa/audit nguyên tử, lặp cùng mã trả kết quả cũ; không chấp nhận sửa số đã gửi trên ca khóa.</summary>
    private static async Task<IResult> Submit(SubmitReconciliation input, HttpContext context, CafeDbContext db)
    {
        var id = await AllowedShift(context, db);
        if (id is null) return Results.Unauthorized();
        if (input.MaThaoTac == Guid.Empty || input.TienMatThucDem < 0 || input.TienMatThucDem > 999999999999999999m ||
            input.TienMatThucDem != decimal.Truncate(input.TienMatThucDem) || input.LyDoChenhLech?.Length > 500)
            return Results.Problem(statusCode: 400, detail: "Mã thao tác, tiền thực đếm hoặc lý do không hợp lệ.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await WorkforceOperation.Lock(db);
        var digest = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { id, input }));
        var prior = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == input.MaThaoTac);
        if (prior is not null)
            return prior.MaCaLamViec == id && prior.HanhDong == "SHIFT_RECONCILIATION_SENT" && prior.MaBamYeuCau is not null &&
                CryptographicOperations.FixedTimeEquals(digest, prior.MaBamYeuCau)
                ? Results.Content(prior.KetQuaDaChotJson!, "application/json")
                : Results.Problem(statusCode: 409, detail: "Mã thao tác đã dùng với nội dung khác.");
        try
        {
            var shift = await db.CaLamViec.SingleAsync(x => x.MaCaLamViec == id);
            WorkforceOperation.Version(input.PhienBanDuLieu, shift.PhienBanDuLieu);
            if (shift.TrangThai != "OPEN") throw new WorkforceRule(409, "Ca không còn mở. Tải lại kết quả đối chiếu.");
            var expected = await ShiftCash.Expected(db, shift);
            if (expected < 0) throw new WorkforceRule(409, "Nguồn tiền mặt không hợp lệ. Cần Quản lý đối soát.");
            if (expected != input.TienMatThucDem) WorkforceOperation.Reason(input.LyDoChenhLech);
            var before = JsonSerializer.Serialize(new { shift.TrangThai, shift.TienMatThucDem });
            shift.TienMatThucDem = input.TienMatThucDem; shift.LyDoChenhLech = input.LyDoChenhLech?.Trim();
            shift.TrangThai = "LOCKED"; shift.ThoiDiemCapNhat = DateTime.UtcNow;
            await db.SaveChangesAsync();
            var json = JsonSerializer.Serialize(new { maCaLamViec = id, trangThai = shift.TrangThai, tienMatThucDem = shift.TienMatThucDem, phienBanDuLieu = shift.PhienBanDuLieu });
            var audit = AuditFactory.Create("STAFF", id.Value, "SHIFT_RECONCILIATION_SENT", "CaLamViec", id.Value);
            audit.TenNguoiThucHienDaChot = await db.NhanVien.Where(x => x.MaNhanVien == shift.MaNhanVien).Select(x => x.HoTen).SingleAsync();
            audit.MaMayDaChot = shift.MaMay; audit.MaThaoTac = input.MaThaoTac; audit.MaBamYeuCau = digest;
            audit.DuLieuTruocJson = before; audit.DuLieuSauJson = json; audit.KetQuaDaChotJson = json; audit.LyDo = input.LyDoChenhLech?.Trim();
            db.NhatKyHeThong.Add(audit); await db.SaveChangesAsync(); await tx.CommitAsync();
            return Results.Content(json, "application/json");
        }
        catch (WorkforceRule error) { return Results.Problem(statusCode: error.Status, detail: error.Message); }
    }
}
