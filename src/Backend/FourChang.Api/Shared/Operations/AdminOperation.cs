// UC: Dùng chung các UC quản lý; UC-POS-02, UC-POS-03, UC-POS-04, UC-POS-05
// Chức năng: Gom kiểm quyền, chống gửi trùng, ghi nhật ký và lưu thay đổi trong cùng giao dịch.
// Cần nhớ: Nếu một bước ghi thất bại thì không giữ lại một phần kết quả.

using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using FourChang.Api.Shared.Audit;
using FourChang.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Shared.Operations;

/// <summary>Lỗi nghiệp vụ có thể giải thích cho người dùng; không chứa lỗi SQL hoặc bí mật.</summary>
public sealed class AdminRule(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

/// <summary>Dùng các cột chống trùng/audit đã có, không thêm bảng. Một khóa ngắn bảo vệ hồ sơ và ca cùng lúc.</summary>
public static class AdminOperation
{
    /// <summary>Khóa giao dịch quản trị để các lệnh menu, nguyên liệu và BOM không kiểm tra dữ liệu chéo tại hai thời điểm khác nhau.</summary>
    public static Task<int> Lock(CafeDbContext db) => db.Database.ExecuteSqlRawAsync(
        "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='FourChang:Administration', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r<0 THROW 51090, 'Workforce lock timeout', 1;");

    /// <summary>
    /// Dùng chung các UC quản lý; UC-POS-02, UC-POS-03, UC-POS-04, UC-POS-05 — Kiểm tra dữ liệu trên màn hình có bị cũ hay không.
    ///
    /// Hàm này được dùng để:
    /// 1. Kiểm tra màn hình có gửi kèm thông tin phiên bản dữ liệu không.
    /// 2. So sánh phiên bản đó với bản mới nhất trong database.
    /// 3. Nếu khác nhau thì yêu cầu tải lại, tránh lưu đè thay đổi của người khác.
    ///
    /// Thông tin cần truyền vào:
    /// - supplied: Dấu phiên bản của dữ liệu mà màn hình đã tải trước đó.
    /// - current: Dấu phiên bản mới nhất hiện có trong database.
    ///
    /// Cần nhớ: Ví dụ: người khác đã sửa giá sau khi bạn mở màn hình thì bạn phải tải lại trước khi lưu.
    /// </summary>
    public static void Version(byte[]? supplied, byte[] current)
    {
        if (supplied is not { Length: 8 }) throw new AdminRule(400, "Thiếu phiên bản dữ liệu.");
        if (!supplied.SequenceEqual(current)) throw new AdminRule(409, "Dữ liệu đã thay đổi. Tải lại và đối chiếu trước khi xác nhận.");
    }

    /// <summary>Lý do là thông tin nghiệp vụ bắt buộc cho trạng thái và bàn giao, tối đa theo cột đã chốt.</summary>
    public static void Reason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw new AdminRule(400, "Nhập lý do, tối đa 500 ký tự.");
    }

    /// <summary>Tra kết quả đã commit sau timeout; quyền được kiểm tra tại endpoint và theo người thực hiện.</summary>
    public static async Task<IResult> Receipt(CafeDbContext db, Guid id, string entity, HttpContext context)
    {
        var admin = long.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var audit = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == id &&
            x.MaQuanLy == admin && x.LoaiDoiTuong == entity && x.KetQua == "SUCCEEDED");
        return audit?.KetQuaDaChotJson is { } json ? Results.Content(json, "application/json") : Results.NotFound();
    }

    /// <summary>
    /// Dùng chung các UC quản lý; UC-POS-02, UC-POS-03, UC-POS-04, UC-POS-05 — Lưu đầy đủ một lần thao tác và tránh xử lý trùng.
    ///
    /// Hàm này được dùng để:
    /// 1. Kiểm tra mã thao tác và quyền của người gửi.
    /// 2. Nếu thao tác đã xử lý thì kiểm tra nội dung và trả kết quả cũ.
    /// 3. Nếu chưa xử lý thì gọi phần công việc cụ thể của chức năng.
    /// 4. Lưu thay đổi, nhật ký và kết quả trong cùng một giao dịch.
    ///
    /// Thông tin cần truyền vào:
    /// - db: Đối tượng dùng để đọc và ghi dữ liệu của hệ thống.
    /// - operation: Mã nhận biết lần thao tác này, giúp kiểm tra yêu cầu bị gửi lại.
    /// - action: Tên hành động đang được yêu cầu.
    /// - entity: Tên loại dữ liệu đang được xử lý, dùng để ghi lịch sử thao tác.
    /// - input: Mã thao tác, nội dung yêu cầu, người thực hiện và hàm xử lý cụ thể.
    /// - context: Thông tin phiên và quyền của người đang thao tác.
    /// - change: Phần công việc cụ thể của chức năng đang gọi vào hàm dùng chung.
    /// - allowStaff: Cho biết thao tác có được phép thực hiện bởi nhân viên hay không.
    /// - auditInput: Nội dung riêng để ghi lịch sử; nếu không truyền thì dùng thông tin yêu cầu gốc.
    ///
    /// Cần nhớ: Các thay đổi phải cùng thành công hoặc cùng hủy. Gửi lại cùng thao tác không được ghi thêm một lần nữa.
    /// </summary>
    public static async Task<IResult> Execute(CafeDbContext db, Guid operation, string action, string entity,
        object input, HttpContext context, Func<Task<(long Id, object Result, string? Before, string? Reason)>> change, bool allowStaff = false, object? auditInput = null)
    {
        if (operation == Guid.Empty) return Results.Problem(statusCode: 400, detail: "Thiếu mã thao tác. Không tự tạo mã mới khi thử lại.");
        var digest = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { action, input }));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await Lock(db);
        // Chỉ lệnh đơn cho phép Staff; các UC quản trị khác vẫn mặc định Admin.
        var staff = allowStaff && context.User.IsInRole("STAFF");
        if (!context.User.IsInRole("ADMIN") && !staff) throw new AdminRule(403, "Không có quyền thực hiện lệnh.");
        var actor = long.Parse(context.User.FindFirstValue(staff ? "shift" : ClaimTypes.NameIdentifier)!);

        // Bước 1: Tìm kết quả đã lưu của mã thao tác để tránh ghi thêm khi người dùng gửi lại.
        var existing = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == operation);
        if (existing is not null)
        {
            if ((staff ? existing.MaCaLamViec != actor || existing.MaQuanLy is not null : existing.MaQuanLy != actor) || existing.LoaiDoiTuong != entity || existing.HanhDong != action ||
                existing.MaBamYeuCau is null || !CryptographicOperations.FixedTimeEquals(existing.MaBamYeuCau, digest))
                return Results.Problem(statusCode: 409, detail: "Mã thao tác đã dùng với nội dung khác.");
            return Results.Content(existing.KetQuaDaChotJson!, "application/json");
        }
        try
        {

            // Bước 2: Thực hiện phần nghiệp vụ cụ thể do chức năng gọi vào.
            var changed = await change();
            var json = JsonSerializer.Serialize(changed.Result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var audit = AuditFactory.Create(staff ? "STAFF" : "ADMIN", actor, action, entity, changed.Id);
            audit.TenNguoiThucHienDaChot = context.User.Identity?.Name ?? "Quản lý";
            if (staff) audit.MaMayDaChot = await db.CaLamViec.Where(x => x.MaCaLamViec == actor).Select(x => x.MaMay).SingleAsync();
            audit.MaThaoTac = operation; audit.MaBamYeuCau = digest; audit.DuLieuTruocJson = changed.Before;
            audit.DuLieuSauJson = JsonSerializer.Serialize(new { KetQua = changed.Result, NoiDung = auditInput ?? input }); audit.KetQuaDaChotJson = json; audit.LyDo = changed.Reason;
            db.NhatKyHeThong.Add(audit);

            // Bước 3: Lưu dữ liệu cùng nhật ký và xác nhận giao dịch; lỗi sẽ hủy các thay đổi chưa xác nhận.
            await db.SaveChangesAsync(); await tx.CommitAsync();
            return Results.Content(json, "application/json");
        }
        catch (AdminRule error) { return Results.Problem(statusCode: error.Status, detail: error.Message); }
    }
}
