using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FourChang.Api.Security;
using FourChang.Api.Shared.Audit;
using FourChang.Api.Shared.Operations;
using FourChang.Api.Features.Staff.WorkSessions;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Common;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.WorkSessions;

/// <summary>Mã phục hồi một lần do Admin cấp, chỉ nhập/gửi đối chiếu; không tạo tài khoản hay cấp quyền bán hàng.</summary>
public sealed class ShiftRecoveryService(CafeDbContext db)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public sealed record Ticket(Guid Id, long Shift, long Employee, string Hash, DateTime Expires);

    /// <summary>Cấp mã 15 phút và khóa ca; chỉ lưu hash mã. Lặp cùng thao tác trả receipt không chứa mã bí mật.</summary>
    public async Task<IResult> Issue(long id, IssueShiftRecoveryInput input, HttpContext context)
    {
        if (input.MaThaoTac == Guid.Empty) return Results.Problem(statusCode: 400, detail: "Thiếu mã thao tác.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await AdminOperation.Lock(db);
        var actor = long.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var digest = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { id, input }));
        var old = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == input.MaThaoTac);
        if (old is not null)
            return old.MaQuanLy == actor && old.HanhDong == "SHIFT_RECOVERY_ISSUED" && old.MaBamYeuCau is not null && CryptographicOperations.FixedTimeEquals(old.MaBamYeuCau, digest)
                ? Results.Content(old.KetQuaDaChotJson!, "application/json") : Results.Problem(statusCode: 409, detail: "Mã thao tác đã dùng với nội dung khác.");
        try
        {
            AdminOperation.Reason(input.LyDo);
            var shift = await db.CaLamViec.SingleOrDefaultAsync(x => x.MaCaLamViec == id) ?? throw new AdminRule(404, "Không tìm thấy ca.");
            AdminOperation.Version(input.PhienBanDuLieu, shift.PhienBanDuLieu);
            if (shift.KetThucLuc is not null || !await db.NhanVien.AnyAsync(x => x.MaNhanVien == shift.MaNhanVien && x.TrangThai == "ACTIVE"))
                throw new AdminRule(409, "Chỉ phục hồi đối chiếu cho ca chưa kết thúc và nhân viên đang làm.");
            var before = JsonSerializer.Serialize(shift);
            var code = "PH-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            var ticket = new Ticket(input.MaThaoTac, id, shift.MaNhanVien, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code))), DateTime.UtcNow.AddMinutes(15));
            shift.TrangThai = "LOCKED";
            shift.ThoiDiemCapNhat = DateTime.UtcNow;
            var receipt = new ShiftRecoveryReceipt(id, shift.MaNhanVien, ticket.Id, ticket.Expires);
            var audit = AuditFactory.Create("ADMIN", actor, "SHIFT_RECOVERY_ISSUED", "CaLamViec", id);
            audit.MaThaoTac = input.MaThaoTac;
            audit.MaBamYeuCau = digest;
            audit.TenNguoiThucHienDaChot = context.User.Identity?.Name ?? "Quản lý";
            audit.DuLieuTruocJson = before;
            audit.DuLieuSauJson = JsonSerializer.Serialize(ticket, Json);
            audit.KetQuaDaChotJson = JsonSerializer.Serialize(receipt, Json);
            audit.LyDo = input.LyDo.Trim();
            db.NhatKyHeThong.Add(audit);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(receipt with { MaPhucHoi = code });
        }
        catch (AdminRule error) { return Results.Problem(statusCode: error.Status, detail: error.Message); }
    }

    /// <summary>Đổi đúng mã chưa dùng thành phiên chỉ đối chiếu; kiểm tra mã/họ tên lại, không mở ca bán hàng.</summary>
    public async Task<IResult> Redeem(RedeemShiftRecoveryInput input)
    {
        if (input.MaCaLamViec <= 0 || input.MaNhanVien <= 0 || string.IsNullOrWhiteSpace(input.HoTen) || input.HoTen.Length > 120 || input.MaPhucHoi?.Length != 51)
            return Results.Problem(statusCode: 400, detail: "Nhập mã ca, mã/họ tên nhân viên và mã phục hồi hợp lệ.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await AdminOperation.Lock(db);
        var ticket = await Latest(db, input.MaCaLamViec);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input.MaPhucHoi.Trim().ToUpperInvariant()));
        if (ticket is null || ticket.Employee != input.MaNhanVien || ticket.Expires <= DateTime.UtcNow ||
            !CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(ticket.Hash)) || await IsRedeemed(db, input.MaCaLamViec, ticket.Id))
            return Results.Problem(statusCode: 409, detail: "Mã phục hồi không hợp lệ, hết hạn hoặc đã dùng. Liên hệ Quản lý.");
        var shift = await db.CaLamViec.SingleOrDefaultAsync(x => x.MaCaLamViec == input.MaCaLamViec && x.MaNhanVien == input.MaNhanVien && x.TrangThai == "LOCKED" && x.KetThucLuc == null);
        var employee = await db.NhanVien.AsNoTracking().SingleOrDefaultAsync(x => x.MaNhanVien == input.MaNhanVien && x.TrangThai == "ACTIVE");
        if (shift is null || employee is null || !EmployeeIdentity.Matches(employee.HoTen, input.HoTen))
            return Results.Problem(statusCode: 409, detail: "Mã và họ tên không khớp ca cần đối chiếu.");
        var audit = AuditFactory.Create("STAFF", shift.MaCaLamViec, "SHIFT_RECOVERY_REDEEMED", "CaLamViec", shift.MaCaLamViec);
        audit.TenNguoiThucHienDaChot = employee.HoTen;
        audit.MaMayDaChot = shift.MaMay;
        audit.DuLieuSauJson = JsonSerializer.Serialize(new { ticket.Id, PhamVi = "RECONCILIATION_ONLY" }, Json);
        db.NhatKyHeThong.Add(audit);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new SessionResult(ShiftSessionCodec.Create(shift.MaCaLamViec, true, ticket.Id), "STAFF_RECONCILIATION", employee.HoTen, employee.MaNhanVien, shift.MaCaLamViec));
    }

    /// <summary>Đọc phiếu cấp mới nhất: cấp lại sẽ làm mã/phiên đối chiếu cũ mất hiệu lực.</summary>
    private static async Task<Ticket?> Latest(CafeDbContext db, long id)
    {
        var key = id.ToString();
        var json = await db.NhatKyHeThong.AsNoTracking().Where(x => x.LoaiDoiTuong == "CaLamViec" && x.MaDoiTuong == key && x.HanhDong == "SHIFT_RECOVERY_ISSUED")
            .OrderByDescending(x => x.MaNhatKy).Select(x => x.DuLieuSauJson).FirstOrDefaultAsync();
        return json is null ? null : JsonSerializer.Deserialize<Ticket>(json, Json);
    }

    /// <summary>Xác minh phiên chỉ đối chiếu đã đổi từ mã mới nhất; không dựa vào mã máy hoặc họ tên đơn thuần.</summary>
    public static async Task<bool> IsRedeemed(CafeDbContext db, long id, Guid ticketId)
    {
        var ticket = await Latest(db, id);
        if (ticket?.Id != ticketId) return false;
        var rows = await db.NhatKyHeThong.AsNoTracking().Where(x => x.MaCaLamViec == id && x.HanhDong == "SHIFT_RECOVERY_REDEEMED").Select(x => x.DuLieuSauJson).ToListAsync();
        foreach (var json in rows.Where(x => x is not null))
        {
            using var document = JsonDocument.Parse(json!);
            if (document.RootElement.GetProperty("id").GetGuid() == ticketId) return true;
        }
        return false;
    }
}
