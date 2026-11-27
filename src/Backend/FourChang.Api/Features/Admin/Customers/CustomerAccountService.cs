// UC: UC-ADM-01
// Chức năng: Quản lý hồ sơ khách, trạng thái tài khoản và lịch sử liên quan.
// Cần nhớ: Kiểm tra điều kiện trước thao tác; không xóa tùy ý khách đã có dữ liệu liên quan.

using FourChang.Api.Shared.Audit;
using FourChang.Api.Shared.Customers;
using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FourChang.Domain.Entities.Accounts;
using FourChang.Infrastructure.Database.Context;
using FourChang.Infrastructure.Security;
using FourChang.Presentation.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Api.Features.Admin.Customers;

// UC-ADM-01: toàn bộ quy tắc ghi tài khoản khách nằm tại đây; endpoint chỉ định tuyến.
public sealed class CustomerAccountService(CafeDbContext db)
{
    /// <summary>
    /// UC-ADM-01 — Tìm và phân trang danh sách khách.
    ///
    /// Hàm này được dùng để:
    /// 1. Kiểm tra từ khóa và thông tin trang cần xem.
    /// 2. Tìm các khách hàng phù hợp.
    /// 3. Trả danh sách của trang đó cùng tổng số khách tìm thấy.
    ///
    /// Thông tin cần truyền vào:
    /// - search: Từ khóa người dùng muốn tìm; có thể để trống.
    /// - page: Số trang danh sách cần xem.
    /// - pageSize: Số dòng tối đa cần hiển thị trong một trang.
    ///
    /// Sau khi xử lý: Danh sách khách và thông tin phân trang.
    /// </summary>
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

    /// <summary>
    /// UC-ADM-01 — Xem hồ sơ và lịch sử liên quan của một khách.
    ///
    /// Hàm này được dùng để:
    /// 1. Tìm khách hàng theo khóa được yêu cầu.
    /// 2. Đọc hồ sơ và lịch sử đơn, thanh toán, hóa đơn, hoàn tiền liên quan.
    /// 3. Trả thông tin cho cửa sổ chi tiết khách hàng.
    ///
    /// Thông tin cần truyền vào:
    /// - id: Khóa khách hàng cần xem hoặc cập nhật.
    ///
    /// Sau khi xử lý: Thông tin chi tiết phục vụ quản lý.
    /// </summary>
    public async Task<IResult> Details(long id)
    {
        var user = await db.TaiKhoanKhachHang.AsNoTracking().SingleOrDefaultAsync(x => x.MaKhachHang == id);
        if (user is null) return Results.NotFound();
        var orders = db.DonHang.AsNoTracking().Where(x => x.MaKhachHang == id);
        var recent = await orders.OrderByDescending(x => x.MaDonHang).Take(20)
            .Select(x => new CustomerReference("Đơn hàng", x.MaDonHienThi, x.TrangThai)).ToListAsync();
        recent.AddRange(await (from payment in db.ThanhToan.AsNoTracking() join order in orders on payment.MaDonHang equals order.MaDonHang
            orderby payment.MaThanhToan descending select new CustomerReference("Thanh toán", payment.MaThanhToanHienThi, payment.TrangThai)).Take(20).ToListAsync());
        recent.AddRange(await (from invoice in db.HoaDon.AsNoTracking() join order in orders on invoice.MaDonHang equals order.MaDonHang
            orderby invoice.MaHoaDon descending select new CustomerReference("Hóa đơn", invoice.SoHoaDon, invoice.TrangThai)).Take(20).ToListAsync());
        recent.AddRange(await (from refund in db.HoSoHoanTien.AsNoTracking() join order in orders on refund.MaDonHang equals order.MaDonHang
            orderby refund.MaHoanTien descending select new CustomerReference("Hoàn tiền", refund.MaHoanTienHienThi, refund.TrangThai)).Take(20).ToListAsync());
        return Results.Ok(new CustomerDetails(CustomerRules.View(user), recent));
    }

    /// <summary>Tra kết quả đã commit đúng Admin khi mất phản hồi; không coi timeout là chưa lưu.</summary>
    public async Task<IResult> Operation(Guid operation, HttpContext context)
    {
        var actor = Actor(context);
        var audit = await db.NhatKyHeThong.AsNoTracking().SingleOrDefaultAsync(x => x.MaThaoTac == operation &&
            x.MaQuanLy == actor && x.LoaiDoiTuong == "TaiKhoanKhachHang" && x.KetQua == "SUCCEEDED");
        return audit?.KetQuaDaChotJson is { } json ? Results.Ok(JsonSerializer.Deserialize<CustomerMutation>(json)) : Results.NotFound();
    }

    /// <summary>
    /// UC-ADM-01 — Lưu thông tin hồ sơ khách do quản lý chỉnh sửa.
    ///
    /// Hàm này được dùng để:
    /// 1. Kiểm tra hồ sơ khách và thông tin quản lý muốn sửa.
    /// 2. Kiểm tra dữ liệu trên màn hình có bị cũ không.
    /// 3. Lưu thay đổi hợp lệ và ghi nhận người đã thực hiện.
    ///
    /// Thông tin cần truyền vào:
    /// - id: Khóa khách hàng cần xem hoặc cập nhật.
    /// - input: Mã khách, nội dung mới, phiên bản và phiên quản lý.
    /// - context: Thông tin phiên và quyền của người đang thao tác.
    ///
    /// Sau khi xử lý: Kết quả cập nhật hoặc lỗi, không tự tạo đơn cho khách.
    /// </summary>
    public async Task<IResult> Save(long id, CustomerSaveInput input, HttpContext context)
    {
        var problem = CustomerRules.Validate(input, false);
        if (problem is not null) return Error(400, problem);
        return await Execute(id, "UPDATE", input.MaThaoTac, input, input.PhienBanDuLieu, null, context,
            async user =>
            {
                var username = CustomerRules.Normalize(input.TenDangNhap, true)!;
                var email = CustomerRules.Normalize(input.Email, true);
                var phone = CustomerRules.Normalize(input.SoDienThoai);
                if (username != user.TenDangNhapChuanHoa) return "Không thay đổi tên đăng nhập của hồ sơ hiện có.";
                if (await db.TaiKhoanKhachHang.AnyAsync(x => x.MaKhachHang != id &&
                    (email != null && x.EmailChuanHoa == email || x.SoDienThoaiChuanHoa == phone)))
                    return "Email hoặc số điện thoại đã được sử dụng.";
                user.HoTen = input.HoTen.Trim();
                user.EmailChuanHoa = email;
                user.SoDienThoaiChuanHoa = phone;
                user.ThoiDiemCapNhat = DateTime.UtcNow;
                return null;
            });
    }

    /// <summary>
    /// UC-ADM-01 — Thực hiện thao tác quản lý trạng thái tài khoản khách.
    ///
    /// Hàm này được dùng để:
    /// 1. Xác định thao tác quản lý muốn thực hiện với khách.
    /// 2. Kiểm tra quyền và điều kiện riêng của thao tác đó.
    /// 3. Thực hiện thay đổi được phép và ghi lại kết quả.
    ///
    /// Thông tin cần truyền vào:
    /// - id: Khóa khách hàng cần xem hoặc cập nhật.
    /// - action: Tên hành động đang được yêu cầu.
    /// - input: Khách, hành động, mã thao tác và phiên bản hồ sơ.
    /// - context: Thông tin phiên và quyền của người đang thao tác.
    ///
    /// Sau khi xử lý: Kết quả thao tác có ghi nhận nhật ký.
    /// </summary>
    public async Task<IResult> Act(long id, string action, CustomerActionInput input, HttpContext context)
    {
        if (action is not ("STATUS" or "RESET_PASSWORD")) return Error(400, "Thao tác khách hàng không còn được hỗ trợ.");
        if (input.LyDo?.Length > 500) return Error(400, "Ghi chú tối đa 500 ký tự.");
        if (action == "STATUS" && input.TrangThai is not ("ACTIVE" or "LOCKED")) return Error(400, "Trạng thái không hợp lệ.");
        if (action == "RESET_PASSWORD" && !CustomerRules.ValidPassword(input.MatKhauTam)) return Error(400, "Mật khẩu tạm phải có 5–128 ký tự.");
        if (action == "RESET_PASSWORD" && input.MatKhauTam != input.XacNhanMatKhau)
            return Error(400, "Xác nhận mật khẩu chưa khớp.");
        var reason = string.IsNullOrWhiteSpace(input.LyDo)
            ? action == "RESET_PASSWORD" ? "Cấp mật khẩu tạm theo yêu cầu khách hàng" : input.TrangThai == "LOCKED" ? "Quản lý khóa tài khoản" : "Quản lý mở khóa tài khoản"
            : input.LyDo.Trim();
        return await Execute(id, action, input.MaThaoTac, input, input.PhienBanDuLieu, reason, context,
            async user =>
            {
                if (action == "STATUS") user.TrangThai = input.TrangThai!;
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

    /// <summary>Ghi hồ sơ, thu hồi phiên và audit nguyên tử; chống lặp và từ chối rowversion cũ.</summary>
    private async Task<IResult> Execute(long id, string action, Guid operation, object input, byte[]? version,
        string? reason, HttpContext context, Func<TaiKhoanKhachHang, Task<string?>> mutate)
    {
        if (operation == Guid.Empty) return Error(400, "Thiếu mã thao tác để kiểm tra kết quả khi thử lại.");
        if (version is not { Length: 8 }) return Error(400, "Thiếu phiên bản hồ sơ hợp lệ.");
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
        var user = await db.TaiKhoanKhachHang.SingleOrDefaultAsync(x => x.MaKhachHang == id);
        if (user is null) return Results.NotFound();
        if (!version!.SequenceEqual(user.PhienBanDuLieu))
            return Results.Conflict(new { detail = "Hồ sơ đã thay đổi. Tải lại trước khi xác nhận.", current = CustomerRules.View(user) });
        var before = JsonSerializer.Serialize(CustomerRules.View(user));
        var problem = await mutate(user);
        if (problem is not null) return Error(409, problem);
        try
        {
            await db.SaveChangesAsync();
            var result = new CustomerMutation(operation, CustomerRules.View(user), false);
            var audit = AuditFactory.Create("ADMIN", actor, "CUSTOMER_" + action, "TaiKhoanKhachHang", user.MaKhachHang);
            audit.TenNguoiThucHienDaChot = context.User.Identity?.Name ?? "Quản lý";
            audit.MaThaoTac = operation; audit.MaBamYeuCau = digest; audit.LyDo = reason;
            audit.DuLieuTruocJson = before;
            audit.DuLieuSauJson = JsonSerializer.Serialize(result.Account);
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

    /// <summary>Lấy mã Admin từ phiên đã được middleware xác thực.</summary>
    private static long Actor(HttpContext context) => long.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)!.Value, CultureInfo.InvariantCulture);
    /// <summary>Trả lỗi nghiệp vụ chuẩn, không đưa dữ liệu nhạy cảm vào phản hồi.</summary>
    private static IResult Error(int status, string message) => Results.Problem(detail: message, statusCode: status);
}
