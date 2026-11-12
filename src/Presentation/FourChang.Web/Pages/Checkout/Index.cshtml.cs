using System.Net.Http.Json;
using System.Text.Json;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Customer;
using FourChang.Web.Security;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.DataProtection;
using System.Globalization;
using System.Security.Cryptography;

namespace FourChang.Web.Pages.Checkout;

// UC-WEB-06: same-origin, antiforgery-protected checkout. Bank verification belongs to UC-WEB-07.
public sealed class IndexModel(CafeApiClient api, IConfiguration configuration, IDataProtectionProvider protection) : PageModel
{
    public bool SimulationEnabled => configuration.GetValue<bool>("Orders:FulfillmentSimulationEnabled") &&
        (configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"]) == "Development";
    /// <summary>Proxy danh sách bàn đang dùng, không cho trình duyệt đọc bí mật QR hoặc SQL.</summary>
    public async Task<IActionResult> OnGetTablesAsync()
    {
        if (!SimulationEnabled) return NotFound();
        try { return new JsonResult(await api.GetCustomerTablesAsync()); }
        catch (Exception ex) when (ex is CafeApiException or HttpRequestException) { return StatusCode(503, new { message = "Chưa tải được danh sách bàn." }); }
    }
    public long? CustomerId { get; private set; }
    public string? OwnerProof { get; private set; }
    private IDataProtector OwnerProtector => protection.CreateProtector("FourChang.Checkout.Owner.v1");
    // Explicit opt-in only. Environment name and API address never enable writes.
    public bool OrderCreationEnabled =>
        bool.TryParse(configuration["Checkout:OrderCreationEnabled"], out var enabled) && enabled;

    public async Task<IActionResult> OnGetAsync()
    {
        Response.Headers.CacheControl = "no-store";
        if (OrderCreationEnabled && Request.Cookies[CustomerCookie.Name] is { Length: > 0 } token)
        {
            api.Token = token;
            try
            {
                var profile = await api.GetSelfProfileAsync();
                if (profile.MaKhachHang > 0)
                {
                    CustomerId = profile.MaKhachHang;
                    // This proof constrains checkout ownership; it is NOT an authentication token.
                    OwnerProof = OwnerProtector.Protect(profile.MaKhachHang.ToString(CultureInfo.InvariantCulture));
                }
            }
            catch (Exception ex) when (ex is CafeApiException or HttpRequestException or OperationCanceledException
                or JsonException or InvalidOperationException or NotSupportedException) { /* No verified owner => no submit. */ }
        }
        return Page();
    }

    public async Task<IActionResult> OnGetVariantsAsync(long productId)
    {
        if (productId <= 0)
            return BadRequest(new { message = "Mã sản phẩm không hợp lệ." });

        try
        {
            var variants = await api.GetMenuVariantsAsync(productId);
            return new JsonResult(variants);
        }
        catch (CafeApiException ex)
        {
            return StatusCode(ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : 503,
                new { message = "Không lấy được cấu hình món từ Backend." });
        }
    }

    // Razor Pages validates the antiforgery token on unsafe methods by default.
    public async Task<IActionResult> OnPostQuoteAsync(CancellationToken ct)
    {
        CreateCustomerOrderRequest? request;
        try
        {
            request = await Request.ReadFromJsonAsync<CreateCustomerOrderRequest>(
                cancellationToken: ct);
        }
        catch (JsonException)
        {
            return BadRequest(new { message = "Dữ liệu báo giá không hợp lệ." });
        }
        catch (BadHttpRequestException)
        {
            return BadRequest(new { message = "Yêu cầu phải là JSON hợp lệ." });
        }

        if (request?.Items is null || request.Items.Count is < 1 or > 50)
            return BadRequest(new { message = "Giỏ hàng không hợp lệ." });

        // Do not price DELIVERY until a server-verified distance/fee exists;
        // do not price DINE_IN until a real table token is validated.
        if (request.HinhThucNhanMon != "TAKEAWAY" && !SimulationEnabled)
            return BadRequest(new { message = "Bản xem trước chỉ báo giá hình thức mang đi." });

        if (request.Items.Any(i => i is null || i.MaCauHinhMon <= 0 || i.SoLuong is < 1 or > 99 ||
            i.PhanTramDuong is < 0 or > 100 || i.PhanTramDa is < 0 or > 100 ||
            (i.Toppings?.Count ?? 0) > 10 ||
            (i.Toppings?.Any(t => t is null || t.MaMonThem <= 0 || t.SoPhanMoiLy < 1) ?? false)))
            return BadRequest(new { message = "Cấu hình món hoặc số lượng không hợp lệ." });

        try
        {
            var quote = await api.QuoteCustomerOrderAsync(request);
            return new JsonResult(quote);
        }
        catch (CafeApiException ex)
        {
            return StatusCode(ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : 503,
                new { message = "Backend không thể báo giá giỏ hàng. Vui lòng thử lại." });
        }
    }

    public sealed record CheckoutCommand(Guid Operation, decimal? AcceptedTotal, CreateCustomerOrderRequest? Order,
        string? OwnerProof = null);

    // Separate read-only preparation; never forwards a Create command or alters pending/cart data.
    // Razor Pages applies antiforgery just as for the existing checkout handlers.
    public async Task<IActionResult> OnPostFulfillmentPreviewAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!bool.TryParse(configuration["Checkout:FulfillmentPreviewEnabled"], out var enabled) || !enabled)
            return StatusCode(503, new { code = "FULFILLMENT_PREVIEW_DISABLED" });
        if (Request.Cookies[CustomerCookie.Name] is not { Length: > 0 } token)
            return StatusCode(401, new { code = "CUSTOMER_REQUIRED" });
        FulfillmentPreviewRequest? input;
        try
        {
            input = await Request.ReadFromJsonAsync<FulfillmentPreviewRequest>(cancellationToken: ct);
        }
        catch (Exception ex) when (ex is JsonException or BadHttpRequestException or NotSupportedException or InvalidOperationException)
        { return BadRequest(new { code = "FULFILLMENT_INPUT_INVALID" }); }
        if (input is null) return BadRequest(new { code = "FULFILLMENT_INPUT_INVALID" });
        try
        {
            api.Token = token;
            var preview = await api.PreviewCustomerFulfillmentAsync(input, ct);
            if (preview.CreationEnabled || preview.Method != input.Method ||
                preview.Method is not ("DINE_IN" or "DELIVERY") ||
                string.IsNullOrWhiteSpace(preview.Proof) || preview.Proof.Length > 8192 ||
                preview.ExpiresAt <= DateTimeOffset.UtcNow ||
                preview.Price is null || preview.Price.TienHang < 0 || preview.Price.PhiGiaoHang < 0 ||
                preview.Price.TongCong != preview.Price.TienHang + preview.Price.PhiGiaoHang ||
                (preview.Method == "DINE_IN" && (string.IsNullOrWhiteSpace(preview.TableCode) || preview.Price.PhiGiaoHang != 0)) ||
                (preview.Method == "DELIVERY" && preview.TableCode is not null))
                return StatusCode(502, new { code = "FULFILLMENT_PREVIEW_INVALID" });
            return new JsonResult(preview);
        }
        catch (CafeApiException ex) { return StatusCode((int?)ex.StatusCode ?? 503, new { code = ex.Code ?? "FULFILLMENT_PREVIEW_UNAVAILABLE" }); }
        catch (Exception ex) when (ex is JsonException or BadHttpRequestException or NotSupportedException or InvalidOperationException)
        { return StatusCode(502, new { code = "FULFILLMENT_PREVIEW_INVALID" }); }
        catch (HttpRequestException) { return StatusCode(503, new { code = "FULFILLMENT_PREVIEW_UNAVAILABLE" }); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return StatusCode(504, new { code = "FULFILLMENT_PREVIEW_TIMEOUT" }); }
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        // Must precede auth, JSON parsing and every call to Backend, including retries.
        if (!OrderCreationEnabled)
            return StatusCode(503, new
            {
                code = "CHECKOUT_DISABLED",
                message = "Chức năng tạo đơn đang tắt. Thao tác chưa rõ kết quả được giữ nguyên; liên hệ người vận hành để kiểm tra."
            });
        var token = Request.Cookies[CustomerCookie.Name];
        if (string.IsNullOrWhiteSpace(token))
            return StatusCode(401, new { message = "Cần đăng nhập Customer. Giữ nguyên thao tác rồi quay lại Checkout để thử lại." });

        CheckoutCommand? command;
        try { command = await Request.ReadFromJsonAsync<CheckoutCommand>(cancellationToken: ct); }
        catch (Exception ex) when (ex is JsonException or BadHttpRequestException or InvalidOperationException)
        { return BadRequest(new { message = "Yêu cầu phải là JSON hợp lệ." }); }

        var order = command?.Order;
        if (command is null || command.Operation == Guid.Empty || command.AcceptedTotal is null or < 0m ||
            order is null || order.HinhThucNhanMon is not ("TAKEAWAY" or "DINE_IN" or "DELIVERY") ||
            (order.HinhThucNhanMon != "TAKEAWAY" && !SimulationEnabled) || order.KhoangCachKm is not null ||
            string.IsNullOrWhiteSpace(order.TenNguoiNhan) || order.TenNguoiNhan.Length > 120 ||
            order.TenNguoiNhan.Any(char.IsControl) ||
            !Regex.IsMatch(order.DienThoaiNguoiNhan ?? "", @"^\+?[0-9]{9,15}\z") ||
            (order.GhiChu?.Length ?? 0) > 500 || order.Items is null || order.Items.Count is < 1 or > 50 ||
            order.Items.Any(i => i is null || i.MaCauHinhMon <= 0 || i.SoLuong is < 1 or > 99 ||
                i.PhanTramDuong is < 0 or > 100 || i.PhanTramDa is < 0 or > 100 ||
                (i.GhiChu?.Length ?? 0) > 500 || (i.Toppings?.Count ?? 0) > 10 ||
                (i.Toppings?.Any(t => t is null || t.MaMonThem <= 0 || t.SoPhanMoiLy < 1) ?? false)))
            return BadRequest(new { message = "Thông tin nhận hoặc giỏ hàng không hợp lệ. Chỉ hỗ trợ mang đi, nhận sớm nhất." });

        api.Token = token; // HttpOnly cookie -> server -> Backend; never expose it to browser scripts.
        try
        {
            long owner;
            try
            {
                if (string.IsNullOrWhiteSpace(command.OwnerProof) ||
                    !long.TryParse(OwnerProtector.Unprotect(command.OwnerProof), NumberStyles.None,
                        CultureInfo.InvariantCulture, out owner) || owner <= 0)
                    return StatusCode(409, new { code = "CHECKOUT_OWNER_MISMATCH", message = "Không xác minh được chủ thao tác. Giữ nguyên dữ liệu để đối soát; không gửi dưới tài khoản khác." });
            }
            catch (CryptographicException)
            { return StatusCode(409, new { code = "CHECKOUT_OWNER_MISMATCH", message = "Chứng thực chủ thao tác không hợp lệ. Giữ nguyên thao tác để đối soát." }); }
            // Check the CURRENT authenticated Customer on every submission, even from a stale tab.
            var profile = await api.GetSelfProfileAsync();
            if (profile.MaKhachHang != owner)
                return StatusCode(409, new { code = "CHECKOUT_OWNER_MISMATCH", message = "Thao tác thuộc tài khoản khác. Không gửi lại; đăng nhập đúng tài khoản hoặc liên hệ đối soát." });
            var (receipt, status) = await api.CreateCustomerOrderAsync(order, command.Operation, command.AcceptedTotal.Value, ct);
            if (receipt.MaDonHang <= 0 || string.IsNullOrWhiteSpace(receipt.MaDonHienThi) ||
                receipt.TrangThai != "PENDING_PAYMENT" || receipt.TienHang < 0 || receipt.PhiGiaoHang < 0 ||
                (order.HinhThucNhanMon != "DELIVERY" && receipt.PhiGiaoHang != 0) ||
                receipt.TongCong != receipt.TienHang + receipt.PhiGiaoHang || receipt.TongCong != command.AcceptedTotal.Value)
                return StatusCode(502, new { message = "Biên nhận chưa hợp lệ. Chưa xác định kết quả; thử lại với cùng mã thao tác." });
            return StatusCode(status, receipt);
        }
        catch (CafeApiException ex)
        {
            return StatusCode(ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : 503,
                new { message = ex.Message, code = ex.Code });
        }
        catch (HttpRequestException)
        { return StatusCode(503, new { message = "Mất kết nối Backend; chưa xác định kết quả. Thử lại cùng thao tác." }); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return StatusCode(504, new { message = "Backend phản hồi quá lâu; chưa xác định kết quả. Thử lại cùng thao tác." }); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException)
        { return StatusCode(502, new { message = "Backend trả biên nhận không hợp lệ. Thử lại cùng thao tác." }); }
    }
}
