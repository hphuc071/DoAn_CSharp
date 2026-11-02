using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FourChang.Domain.Entities;
using FourChang.Infrastructure.Persistence;
using FourChang.Infrastructure.Security;
using FourChang.Presentation.Shared.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FourChang.Api.Features.Customers;

// UC-ADM-01: toàn bộ quy tắc ghi tài khoản khách nằm tại đây; endpoint chỉ định tuyến.
public sealed class CustomerAccountService(CafeDbContext db)
{
    // Luồng chính 1: lọc ở SQL và chỉ lấy một trang, tránh tải toàn bộ dữ liệu cá nhân.
    public async Task<IResult> Search(string? search, int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100 || search?.Length > 254 || (long)(page - 1) * pageSize > int.MaxValue)
            return Error(400, "Bộ lọc hoặc phân trang không hợp lệ.");
        var term = search?.Trim() ?? "";
        var query = db.TaiKhoanKhachHang.AsNoTracking();
        if (term.Length > 0)
        {
            long.TryParse(term, out var id);
            query = query.Where(x => x.MaKhachHang == id || x.HoTen.Contains(term) ||
                x.TenDangNhapChuanHoa.Contains(term) || x.EmailChuanHoa != null && x.EmailChuanHoa.Contains(term) ||
                x.SoDienThoaiChuanHoa != null && x.SoDienThoaiChuanHoa.Contains(term));
        }
        var total = await query.CountAsync();
        // Sắp xếp ổn định trước Skip/Take để không đổi thứ tự tùy ý giữa hai trang.
        var users = await query.OrderBy(x => x.MaKhachHang).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Results.Ok(new CustomerPage(users.Select(CustomerRules.View).ToList(), page, pageSize, total));
    }

    // Luồng chính 2 / A2: hiển thị hồ sơ và nguồn tham chiếu để Admin biết vì sao được/không được xóa.
    public async Task<IResult> Details(long id)
    {
        var user = await db.TaiKhoanKhachHang.AsNoTracking().SingleOrDefaultAsync(x => x.MaKhachHang == id);
        if (user is null) return Results.NotFound();
        var references = await References(id);
        var orders = db.DonHang.AsNoTracking().Where(x => x.MaKhachHang == id);
        var recent = await orders.OrderByDescending(x => x.MaDonHang).Take(20)
            .Select(x => new CustomerReference("Đơn hàng", x.MaDonHienThi, x.TrangThai)).ToListAsync();
        recent.AddRange(await (from payment in db.ThanhToan.AsNoTracking() join order in orders on payment.MaDonHang equals order.MaDonHang
            orderby payment.MaThanhToan descending select new CustomerReference("Thanh toán", payment.MaThanhToanHienThi, payment.TrangThai)).Take(20).ToListAsync());
        recent.AddRange(await (from invoice in db.HoaDon.AsNoTracking() join order in orders on invoice.MaDonHang equals order.MaDonHang
            orderby invoice.MaHoaDon descending select new CustomerReference("Hóa đơn", invoice.SoHoaDon, invoice.TrangThai)).Take(20).ToListAsync());
        recent.AddRange(await (from refund in db.HoSoHoanTien.AsNoTracking() join order in orders on refund.MaDonHang equals order.MaDonHang
            orderby refund.MaHoanTien descending select new CustomerReference("Hoàn tiền", refund.MaHoanTienHienThi, refund.TrangThai)).Take(20).ToListAsync());
        return Results.Ok(new CustomerDetails(CustomerRules.View(user), references.All(x => x.SoLuong == 0), references, recent));
    }

    // E4: tra kết quả đã commit khi đường truyền bị ngắt; không suy đoán timeout nghĩa là chưa lưu.
    public async Task<IResult> Operation(Guid operation, HttpContext context)
    {
        var actor = Actor(context);
        var audit = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == operation &&
            x.MaQuanLy == actor && x.LoaiDoiTuong == "TaiKhoanKhachHang" && x.KetQua == "SUCCEEDED");
        return audit?.KetQuaDaChotJson is { } json ? Results.Ok(JsonSerializer.Deserialize<CustomerMutation>(json)) : Results.NotFound();
    }

    // Luồng chính 3–5: tạo hoặc sửa các trường hồ sơ được phép, không gộp với thao tác đổi trạng thái/reset.
    public async Task<IResult> Save(long? id, CustomerSaveInput input, HttpContext context)
    {
        var problem = CustomerRules.Validate(input, id is null);
        if (problem is not null) return Error(400, problem);
        return await Execute(id, id is null ? "CREATE" : "UPDATE", input.MaThaoTac, input, input.PhienBanDuLieu, null, context,
            async user =>
            {
                var username = CustomerRules.Normalize(input.TenDangNhap, true)!;
                var email = CustomerRules.Normalize(input.Email, true);
                var phone = CustomerRules.Normalize(input.SoDienThoai);
                if (id is not null && username != user.TenDangNhapChuanHoa) return "Không thay đổi tên đăng nhập của hồ sơ hiện có.";
                if (await db.TaiKhoanKhachHang.AnyAsync(x => (id == null || x.MaKhachHang != id) &&
                    (x.TenDangNhapChuanHoa == username || email != null && x.EmailChuanHoa == email || phone != null && x.SoDienThoaiChuanHoa == phone)))
                    return "Tên đăng nhập, email hoặc số điện thoại đã được sử dụng.";
                user.TenDangNhapChuanHoa = username; user.HoTen = input.HoTen.Trim();
                user.EmailChuanHoa = email; user.SoDienThoaiChuanHoa = phone;
                if (id is null)
                {
                    user.MatKhauBam = Passwords.Hash(input.MatKhauTam!);
                    user.BatBuocDoiMatKhau = true; user.TrangThai = "ACTIVE";
                }
                return null;
            });
    }

    // A1/A2: thay trạng thái, reset và xóa dùng cùng cơ chế version/audit nhưng điều kiện nghiệp vụ riêng.
    public async Task<IResult> Act(long id, string action, CustomerActionInput input, HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(input.LyDo) || input.LyDo.Trim().Length > 500) return Error(400, "Lý do bắt buộc, tối đa 500 ký tự.");
        if (action == "STATUS" && input.TrangThai is not ("ACTIVE" or "LOCKED" or "INACTIVE")) return Error(400, "Trạng thái không hợp lệ.");
        if (action == "RESET_PASSWORD" && !CustomerRules.ValidPassword(input.MatKhauTam)) return Error(400, "Mật khẩu tạm phải có 10–128 ký tự.");
        return await Execute(id, action, input.MaThaoTac, input, input.PhienBanDuLieu, input.LyDo.Trim(), context,
            async user =>
            {
                if (action == "DELETE")
                {
                    // BR-28: xét mọi FK, không chỉ đếm đơn. Không xóa phiên, địa chỉ hoặc log để lách kiểm tra.
                    if ((await References(id)).Any(x => x.SoLuong > 0)) return "Khách hàng đã có dữ liệu tham chiếu. Không thể xóa; hãy ngừng hoặc khóa với lý do.";
                    db.TaiKhoanKhachHang.Remove(user);
                }
                else if (action == "STATUS") user.TrangThai = input.TrangThai!;
                else
                {
                    if (Passwords.Verify(input.MatKhauTam!, user.MatKhauBam)) return "Mật khẩu tạm mới không được trùng mật khẩu hiện tại.";
                    user.MatKhauBam = Passwords.Hash(input.MatKhauTam!); user.BatBuocDoiMatKhau = true;
                    user.SoLanDangNhapSai = 0; user.KhoaDenLuc = null;
                }
                if (action == "RESET_PASSWORD" || action == "STATUS" && user.TrangThai != "ACTIVE")
                {
                    // BR-07/09: khóa/reset mất hiệu lực phiên nhưng không đụng đơn, hóa đơn hoặc nghĩa vụ hoàn.
                    var sessions = await db.PhienDangNhap.Where(x => x.MaKhachHang == id && x.ThuHoiLuc == null).ToListAsync();
                    foreach (var session in sessions) { session.ThuHoiLuc = DateTime.UtcNow; session.LyDoThuHoi = "ADMIN_" + action; }
                }
                return null;
            });
    }

    // BR-14/29: một điểm ghi chung bảo đảm chống trùng và commit nguyên tử hồ sơ + phiên + nhật ký.
    private async Task<IResult> Execute(long? id, string action, Guid operation, object input, byte[]? version,
        string? reason, HttpContext context, Func<TaiKhoanKhachHang, Task<string?>> mutate)
    {
        if (operation == Guid.Empty) return Error(400, "Thiếu mã thao tác để kiểm tra kết quả khi thử lại.");
        if (id is not null && version is not { Length: 8 }) return Error(400, "Thiếu phiên bản hồ sơ hợp lệ.");
        var actor = Actor(context);
        // Chỉ lưu HMAC của yêu cầu: không lưu mật khẩu tạm, cũng không tạo hash dễ dò từ mật khẩu ngắn.
        var secret = Environment.GetEnvironmentVariable("FOURCHANG_POS_KEY") ?? throw new InvalidOperationException("Thiếu khóa máy chủ.");
        var digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), JsonSerializer.SerializeToUtf8Bytes(new { id, action, input }));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        // Khóa theo mã thao tác trong transaction: hai lần gửi đồng thời cùng lệnh phải cùng một kết quả.
        var resource = "UC-ADM-01:" + operation.ToString("N");
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r<0 THROW 51090, 'Operation lock timeout', 1;");
        var previous = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == operation);
        if (previous is not null)
        {
            if (previous.MaQuanLy != actor || previous.LoaiDoiTuong != "TaiKhoanKhachHang" || previous.MaBamYeuCau is null ||
                !CryptographicOperations.FixedTimeEquals(previous.MaBamYeuCau, digest)) return Error(409, "Mã thao tác đã được dùng với nội dung khác.");
            return Results.Ok(JsonSerializer.Deserialize<CustomerMutation>(previous.KetQuaDaChotJson!));
        }
        var user = id is null ? new TaiKhoanKhachHang() : await db.TaiKhoanKhachHang.SingleOrDefaultAsync(x => x.MaKhachHang == id);
        if (user is null) return Results.NotFound();
        if (id is not null && !version!.SequenceEqual(user.PhienBanDuLieu))
            return Results.Conflict(new { detail = "Hồ sơ đã thay đổi. Tải lại trước khi xác nhận.", current = CustomerRules.View(user) });
        var before = id is null ? null : JsonSerializer.Serialize(CustomerRules.View(user));
        var problem = await mutate(user);
        if (problem is not null) return Error(409, problem);
        if (id is null) db.TaiKhoanKhachHang.Add(user);
        try
        {
            await db.SaveChangesAsync();
            var result = new CustomerMutation(operation, action == "DELETE" ? null : CustomerRules.View(user), action == "DELETE");
            var audit = AuthEndpoints.Audit("ADMIN", actor, "CUSTOMER_" + action, "TaiKhoanKhachHang", user.MaKhachHang);
            audit.TenNguoiThucHienDaChot = context.User.Identity?.Name ?? "Quản lý";
            audit.MaThaoTac = operation; audit.MaBamYeuCau = digest; audit.LyDo = reason;
            audit.DuLieuTruocJson = before;
            audit.DuLieuSauJson = action == "DELETE" ? null : JsonSerializer.Serialize(result.Account);
            audit.KetQuaDaChotJson = JsonSerializer.Serialize(result);
            // BR-29/E6: audit thất bại thì toàn bộ thay đổi hồ sơ/phiên cũng phải rollback.
            db.NhatKyHeThong.Add(audit); await db.SaveChangesAsync(); await tx.CommitAsync();
            return Results.Ok(result);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(); db.ChangeTracker.Clear();
            var latest = await db.TaiKhoanKhachHang.AsNoTracking().SingleOrDefaultAsync(x => x.MaKhachHang == id);
            return Results.Conflict(new { detail = "Dữ liệu đã được thay đổi đồng thời; tải lại để đối chiếu.", current = latest is null ? null : CustomerRules.View(latest) });
        }
    }

    private async Task<List<CustomerReferenceCount>> References(long id)
    {
        var counts = new List<CustomerReferenceCount>();
        if (db.Database.GetDbConnection().State != ConnectionState.Open) await db.Database.OpenConnectionAsync();
        // Tên bảng/cột chỉ lấy từ mô hình EF tin cậy, còn id luôn là tham số SQL.
        // Nhờ đó kiểm tra không bỏ sót FK khi bảo trì mà không cần thêm bảng phụ.
        foreach (var entity in db.Model.GetEntityTypes())
        foreach (var fk in entity.GetForeignKeys().Where(x => x.PrincipalEntityType.ClrType == typeof(TaiKhoanKhachHang)))
        {
            var table = entity.GetTableName()!; var column = fk.Properties.Single().Name;
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"SELECT COUNT_BIG(*) FROM [dbo].[{table.Replace("]", "]]")}] WHERE [{column.Replace("]", "]]")}]=@customer";
            var parameter = command.CreateParameter(); parameter.ParameterName = "@customer"; parameter.Value = id; command.Parameters.Add(parameter);
            var count = Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            counts.Add(new(table + "." + column, count));
        }
        return counts;
    }

    private static long Actor(HttpContext context) => long.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)!.Value, CultureInfo.InvariantCulture);
    private static IResult Error(int status, string message) => Results.Problem(detail: message, statusCode: status);
}
