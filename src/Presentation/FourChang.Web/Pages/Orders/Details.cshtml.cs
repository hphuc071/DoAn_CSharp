using FourChang.Presentation.Shared.ApiClients;
using Microsoft.AspNetCore.Mvc;
using FourChang.Web.Security;

namespace FourChang.Web.Pages.Orders;

public sealed class DetailsModel(CafeApiClient api) : CustomerOrderPageModel(api)
{
    public async Task<IActionResult> OnGetInvoiceAsync(long orderId, long invoiceId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Vary = "Cookie";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'; frame-ancestors 'none'";
        Response.Headers.XFrameOptions = "DENY";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var token = Request.Cookies[CustomerCookie.Name];
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();
        if (orderId <= 0 || invoiceId <= 0) return NotFound();
        Api.Token = token;
        try { return File(await Api.GetCustomerInvoicePdfAsync(orderId, invoiceId, ct), "application/pdf", $"invoice-{invoiceId}.pdf"); }
        catch (CafeApiException ex)
        {
            return StatusCode((int)(ex.StatusCode ?? System.Net.HttpStatusCode.ServiceUnavailable),
                new { message = "Chưa có PDF đã phát hành khả dụng hoặc không đủ quyền. Không tạo hóa đơn mới." });
        }
    }
    /// <summary>Razor antiforgery bảo vệ POST; giữ GUID/version của form, không tự báo đơn đã hủy.</summary>
    public async Task<IActionResult> OnPostCancelAsync(long orderId, string? reason, CancellationToken ct, string? expectedVersion = null, Guid operation = default)
    {
        var token = Request.Cookies[CustomerCookie.Name];
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();
        if (orderId <= 0 || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 3 or > 400 || string.IsNullOrWhiteSpace(expectedVersion) || operation == Guid.Empty)
            return BadRequest("Cần lý do, mã thao tác và phiên bản đơn hợp lệ.");
        Api.Token = token;
        try { await Api.RequestCancellationAsync(orderId, reason.Trim(), expectedVersion, operation, ct); TempData["CancellationNotice"] = "Đã gửi yêu cầu. Nhân viên sẽ chuyển Quản lý xét duyệt; đơn chưa bị hủy."; }
        catch (CafeApiException ex) { TempData["CancellationNotice"] = ex.Message; }
        catch (HttpRequestException) { TempData["CancellationNotice"] = "Mất kết nối. Kiểm tra yêu cầu đang chờ trên đơn trước khi gửi lại."; }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { TempData["CancellationNotice"] = "Chưa nhận được phản hồi. Kiểm tra trạng thái đơn trước khi gửi lại."; }
        return RedirectToPage("/Orders/Details", new { orderId });
    }
    public Task<IActionResult> OnGetAsync(long orderId, CancellationToken ct) => ReadAsync(async () =>
    {
        if (!ModelState.IsValid || orderId <= 0) { Response.StatusCode = 404; Error = "Không tìm thấy đơn hàng."; return; }
        await LoadDetailAsync(orderId, ct);
    });
}
