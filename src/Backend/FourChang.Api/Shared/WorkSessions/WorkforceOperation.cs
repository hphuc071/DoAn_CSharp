using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using FourChang.Api.Shared.Audit;
using FourChang.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Shared.WorkSessions;

/// <summary>Lỗi nghiệp vụ có thể giải thích cho người dùng; không chứa lỗi SQL hoặc bí mật.</summary>
public sealed class WorkforceRule(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

/// <summary>Dùng các cột chống trùng/audit đã có, không thêm bảng. Một khóa ngắn bảo vệ hồ sơ và ca cùng lúc.</summary>
public static class WorkforceOperation
{
    /// <summary>Tuần tự hóa lệnh hồ sơ/ca để ngừng nhân viên không chạy đua với mở hoặc bàn giao ca.</summary>
    public static Task<int> Lock(CafeDbContext db) => db.Database.ExecuteSqlRawAsync(
        "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='FourChang:Workforce', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r<0 THROW 51090, 'Workforce lock timeout', 1;");

    /// <summary>Kiểm tra rowversion trước khi ghi; không tự ghi đè dữ liệu người khác đã thay đổi.</summary>
    public static void Version(byte[]? supplied, byte[] current)
    {
        if (supplied is not { Length: 8 }) throw new WorkforceRule(400, "Thiếu phiên bản dữ liệu.");
        if (!supplied.SequenceEqual(current)) throw new WorkforceRule(409, "Dữ liệu đã thay đổi. Tải lại và đối chiếu trước khi xác nhận.");
    }

    /// <summary>Lý do là thông tin nghiệp vụ bắt buộc cho trạng thái và bàn giao, tối đa theo cột đã chốt.</summary>
    public static void Reason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw new WorkforceRule(400, "Nhập lý do, tối đa 500 ký tự.");
    }

    /// <summary>Tra kết quả đã commit sau timeout; quyền được kiểm tra tại endpoint và theo người thực hiện.</summary>
    public static async Task<IResult> Receipt(CafeDbContext db, Guid id, string entity, HttpContext context)
    {
        var admin = long.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var audit = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == id &&
            x.MaQuanLy == admin && x.LoaiDoiTuong == entity && x.KetQua == "SUCCEEDED");
        return audit?.KetQuaDaChotJson is { } json ? Results.Content(json, "application/json") : Results.NotFound();
    }

    /// <summary>Ghi nghiệp vụ, trước/sau và kết quả chống trùng trong cùng giao dịch; lỗi audit kéo theo rollback toàn bộ.</summary>
    public static async Task<IResult> Execute(CafeDbContext db, Guid operation, string action, string entity,
        object input, HttpContext context, Func<Task<(long Id, object Result, string? Before, string? Reason)>> change)
    {
        if (operation == Guid.Empty) return Results.Problem(statusCode: 400, detail: "Thiếu mã thao tác. Không tự tạo mã mới khi thử lại.");
        var digest = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { action, input }));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await Lock(db);
        var actor = long.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var existing = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == operation);
        if (existing is not null)
        {
            if (existing.MaQuanLy != actor || existing.LoaiDoiTuong != entity || existing.HanhDong != action ||
                existing.MaBamYeuCau is null || !CryptographicOperations.FixedTimeEquals(existing.MaBamYeuCau, digest))
                return Results.Problem(statusCode: 409, detail: "Mã thao tác đã dùng với nội dung khác.");
            return Results.Content(existing.KetQuaDaChotJson!, "application/json");
        }
        try
        {
            var changed = await change();
            var json = JsonSerializer.Serialize(changed.Result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var audit = AuditFactory.Create("ADMIN", actor, action, entity, changed.Id);
            audit.TenNguoiThucHienDaChot = context.User.Identity?.Name ?? "Quản lý";
            audit.MaThaoTac = operation; audit.MaBamYeuCau = digest; audit.DuLieuTruocJson = changed.Before;
            audit.DuLieuSauJson = json; audit.KetQuaDaChotJson = json; audit.LyDo = changed.Reason;
            db.NhatKyHeThong.Add(audit);
            await db.SaveChangesAsync(); await tx.CommitAsync();
            return Results.Content(json, "application/json");
        }
        catch (WorkforceRule error) { return Results.Problem(statusCode: error.Status, detail: error.Message); }
    }
}
