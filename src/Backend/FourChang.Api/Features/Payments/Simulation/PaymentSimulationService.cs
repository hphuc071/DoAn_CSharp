using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Security.Claims;
using FourChang.Api.Shared.Operations;
using FourChang.Api.Features.Staff.Sales;
using FourChang.Domain.Entities.Payments;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Staff;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using QRCoder;
namespace FourChang.Api.Features.Payments.Simulation;

/// <summary>Chỉ mô phỏng thanh toán trong Development có cờ bật rõ; không xác minh ngân hàng thật.</summary>
public sealed class PaymentSimulationService(CafeDbContext db, IConfiguration config, IWebHostEnvironment environment)
{
    public sealed record Ticket(long Payment, decimal Amount, long Deadline, string Version);
    /// <summary>Chặn toàn bộ đường mô phỏng trong Production dù cấu hình bị bật nhầm.</summary>
    public bool Enabled => environment.IsDevelopment() && config.GetValue<bool>("Payments:SimulationEnabled");

    /// <summary>Ký ticket giới hạn đúng thanh toán/số tiền/hạn; không dùng mã QR do client tự cung cấp.</summary>
    private string Encode(Ticket ticket)
    {
        var payload = WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(ticket));
        return payload + "." + Convert.ToHexString(Sign(payload));
    }
    /// <summary>Ràng buộc purpose để ticket mô phỏng không dùng thay phiên POS hoặc đăng nhập.</summary>
    private byte[] Sign(string value) => HMACSHA256.HashData(Encoding.UTF8.GetBytes(
        Environment.GetEnvironmentVariable("FOURCHANG_POS_KEY") ?? throw new InvalidOperationException("Thiếu khóa server.")), Encoding.UTF8.GetBytes("PAYMENT-SIMULATION-V1:" + value));

    /// <summary>Kiểm chữ ký trước khi đọc SQL; token giả/sai mục đích không thể xác nhận tiền.</summary>
    private Ticket Decode(string token)
    {
        if (!Enabled || token.Length > 1500) throw new AdminRule(404, "Chế độ mô phỏng không khả dụng.");
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 2 || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(parts[1]), Sign(parts[0]))) throw new FormatException();
            return JsonSerializer.Deserialize<Ticket>(WebEncoders.Base64UrlDecode(parts[0])) ?? throw new FormatException();
        }
        catch (Exception ex) when (ex is FormatException or JsonException) { throw new AdminRule(400, "Mã mô phỏng không hợp lệ."); }
    }

    /// <summary>Chỉ chủ đơn Web hoặc ca POS sở hữu lấy được QR; lấy QR tuyệt đối không đổi trạng thái.</summary>
    public async Task<SimulationQr> Qr(long orderId, HttpContext context, bool staff)
    {
        if (!Enabled) throw new AdminRule(404, "Chưa bật thanh toán mô phỏng.");
        var owner = long.Parse(context.User.FindFirstValue(staff ? "shift" : ClaimTypes.NameIdentifier)!);
        var order = await db.DonHang.AsNoTracking().SingleOrDefaultAsync(x => x.MaDonHang == orderId &&
            (staff ? x.KenhDatHang == "POS" && x.MaCaLamViec == owner : x.KenhDatHang == "WEB" && x.MaKhachHang == owner))
            ?? throw new AdminRule(404, "Không tìm thấy đơn của bạn.");
        var payment = await db.ThanhToan.AsNoTracking().SingleAsync(x => x.MaDonHang == orderId);
        if (order.TrangThai != "PENDING_PAYMENT" || payment.PhuongThuc != "BANK_TRANSFER" || payment.TrangThai != "PENDING" || payment.SoTienDaXacMinh != 0 ||
            payment.SoTienYeuCau <= 0 || payment.HetHanLuc is not { } deadline || deadline <= DateTime.UtcNow)
            throw new AdminRule(409, "Đơn không còn chờ thanh toán mô phỏng.");
        var ticket = Encode(new(payment.MaThanhToan, payment.SoTienYeuCau,
            new DateTimeOffset(DateTime.SpecifyKind(deadline, DateTimeKind.Utc)).ToUnixTimeSeconds(), Convert.ToBase64String(payment.PhienBanDuLieu)));
        var baseUrl = config["Payments:SimulationPublicBaseUrl"] ?? "http://localhost:15881";
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo) || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new AdminRule(503, "URL trình diễn chưa hợp lệ.");
        var url = baseUrl.TrimEnd('/') + "/simulation/pay/" + ticket;
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        using var png = new PngByteQRCode(data);
        return new("data:image/png;base64," + Convert.ToBase64String(png.GetGraphic(5)), url, DateTime.SpecifyKind(deadline, DateTimeKind.Utc), payment.SoTienYeuCau);
    }

    /// <summary>GET khi quét QR chỉ hiển thị nút xác nhận, tránh ứng dụng quét/link preview tự ghi tiền.</summary>
    public IResult Landing(string token)
    {
        var ticket = Decode(token);
        if (ticket.Deadline <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) throw new AdminRule(410, "Mã mô phỏng đã hết hạn.");
        var html = "<!doctype html><html lang='vi'><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>4CHANG · Mô phỏng</title>" +
            "<body style='background:#FBF8F2;color:#302922;font:18px system-ui;padding:24px'><main style='max-width:480px;margin:10vh auto;background:#FFFDF9;border:1px solid #E1D5C5;border-radius:16px;padding:28px'>" +
            "<h1>4CHANG LINH NGU LAM</h1><h2>Thanh toán mô phỏng</h2><p>Không chuyển tiền thật. Chỉ dùng dữ liệu trình diễn.</p><p>Số tiền mô phỏng: <strong>" + ticket.Amount.ToString("N0") +
            " đ</strong></p><form method='post'><button style='background:#805033;color:white;border:0;border-radius:10px;padding:16px;font:inherit'>Xác nhận thanh toán mô phỏng</button></form></main></body></html>";
        return Results.Content(html, "text/html; charset=utf-8");
    }

    /// <summary>POST xác nhận một lần: kiểm hạn/version, ghi giao dịch SIMULATION, thu, đơn, hóa đơn và audit nguyên tử.</summary>
    public async Task<IResult> Confirm(string token)
    {
        var ticket = Decode(token);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await AdminOperation.Lock(db);
        var payment = await db.ThanhToan.SingleOrDefaultAsync(x => x.MaThanhToan == ticket.Payment) ?? throw new AdminRule(404, "Không tìm thấy thanh toán.");
        var reference = "SIM-PAY-" + payment.MaThanhToan;
        if (payment.TrangThai == "VERIFIED" && payment.ThamChieuChungCu == reference) return Success();
        if (ticket.Deadline <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) throw new AdminRule(410, "Mã đã hết hạn; không cập nhật tiền.");
        var order = await db.DonHang.SingleAsync(x => x.MaDonHang == payment.MaDonHang);
        if (order.TrangThai != "PENDING_PAYMENT" || payment.TrangThai != "PENDING" || payment.PhuongThuc != "BANK_TRANSFER" || payment.SoTienDaXacMinh != 0 ||
            payment.SoTienYeuCau != ticket.Amount || payment.HetHanLuc <= DateTime.UtcNow || Convert.ToBase64String(payment.PhienBanDuLieu) != ticket.Version)
            throw new AdminRule(409, "Nguồn thanh toán đã thay đổi; không xác nhận mô phỏng.");
        var now = DateTime.UtcNow;
        payment.SoTienDaXacMinh = ticket.Amount; payment.TrangThai = "VERIFIED"; payment.XacMinhLuc = now;
        payment.ThamChieuChungCu = reference; payment.ThoiDiemCapNhat = now;
        db.GiaoDichNganHang.Add(new() { TenNhaCungCap = "SIMULATION", MaTaiKhoanNganHangQuan = "DEMO-NO-REAL-ACCOUNT", MaGiaoDichBenNgoai = reference,
            MaThanhToan = payment.MaThanhToan, ChieuGiaoDich = "IN", SoTien = ticket.Amount, NoiDungChuyenKhoan = "MO PHONG - KHONG THU TIEN THAT", ThoiDiemNganHang = now,
            TrangThaiXacMinh = "VERIFIED", XacMinhLuc = now, ThamChieuChungCu = reference, ThoiDiemTao = now, ThoiDiemCapNhat = now });
        order.TrangThai = order.KenhDatHang == "POS" ? "ACCEPTED" : "PENDING_CONFIRMATION";
        if (order.KenhDatHang == "POS") order.TiepNhanLuc = now;
        order.ThoiDiemCapNhat = now;
        await db.SaveChangesAsync();
        await CheckoutInvoice.Issue(db, order, payment, now);
        db.NhatKyHeThong.Add(new() { LoaiTacNhan = "SYSTEM", TenNguoiThucHienDaChot = "PAYMENT_SIMULATION_ONLY", LoaiDoiTuong = "ThanhToan",
            MaDoiTuong = payment.MaThanhToan.ToString(), HanhDong = "SIMULATED_PAYMENT_CONFIRMED", KetQua = "SUCCEEDED", LyDo = "MÔ PHỎNG — KHÔNG PHẢI THU NGÂN HÀNG THẬT",
            DuLieuSauJson = JsonSerializer.Serialize(new { payment.MaThanhToan, payment.MaDonHang, Amount = ticket.Amount, Simulation = true }), PhatSinhLuc = now });
        db.SuKienDongBo.Add(new() { MaSuKien = Guid.NewGuid(), MaThaoTac = Guid.NewGuid(), LoaiSuKien = "SIMULATED_PAYMENT_CONFIRMED", NoiDungJson = JsonSerializer.Serialize(new { order.MaDonHang }), PhatSinhLuc = now });
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        return Success();
    }
    /// <summary>Kết quả có nhãn mô phỏng rõ ràng; trang POS/Web tự đọc trạng thái đã lưu.</summary>
    private static IResult Success() => Results.Content("<!doctype html><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><body style='font:20px system-ui;background:#FBF8F2;padding:32px'><h1>Thanh toán mô phỏng thành công</h1><p>Đơn đã cập nhật. Không có tiền thật được chuyển. Bạn có thể đóng trang này.</p>", "text/html; charset=utf-8");
}
