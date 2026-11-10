using System.Net;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Customer;
using FourChang.Web.Security;
using FourChang.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FourChang.Web.Pages.Orders;

public abstract class CustomerOrderPageModel(CafeApiClient api) : PageModel
{
    protected CafeApiClient Api => api;
    public string? Error { get; protected set; }
    public CustomerOrderDetail? Detail { get; protected set; }
    public bool ExpiryReadEnabled => bool.TryParse(HttpContext.Features
        .Get<Microsoft.AspNetCore.Http.Features.IServiceProvidersFeature>()?.RequestServices?
        .GetService<IConfiguration>()?["Orders:ExpiryStatusReadEnabled"], out var enabled) && enabled;
    [BindProperty(SupportsGet = true)] public int ReturnPage { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public string? ReturnStatus { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? ReturnFromUtc { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? ReturnToUtc { get; set; }

    protected async Task<IActionResult> ReadAsync(Func<Task> read)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Vary = "Cookie";
        var token = Request.Cookies[CustomerCookie.Name];
        if (string.IsNullOrWhiteSpace(token)) return RedirectToPage("/Account/Login");
        api.Token = token; // Server only; Backend authenticates Customer and enforces ownership.
        try { await read(); }
        catch (CafeApiException ex)
        {
            if (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return RedirectToPage("/Account/Login");
            Response.StatusCode = (int)(ex.StatusCode ?? HttpStatusCode.ServiceUnavailable);
            Error = ex.StatusCode == HttpStatusCode.NotFound ? "Không tìm thấy đơn hàng." : ex.Message;
            Detail = null;
        }
        return Page();
    }

    protected async Task LoadDetailAsync(long orderId, CancellationToken ct)
    {
        var snapshot = await api.GetCustomerOrderAsync(orderId, ct);
        if (!CustomerOrderPresentation.Valid(snapshot, orderId, ExpiryReadEnabled))
            throw new CafeApiException("Snapshot đơn không hợp lệ; không xác nhận thanh toán.", HttpStatusCode.BadGateway);
        Detail = snapshot;
    }

    // Deliberately locked until cancellation version/idempotency/stock/refund integration is proven.
    // No runtime flag can unlock it. Razor Pages antiforgery remains enabled.
    protected Task<IActionResult> RejectCancellationAsync(long orderId, string? reason)
    {
        Response.Headers.CacheControl = "no-store";
        if (string.IsNullOrWhiteSpace(Request.Cookies[CustomerCookie.Name]))
            return Task.FromResult<IActionResult>(Unauthorized());
        if (orderId <= 0 || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 3 or > 400)
            return Task.FromResult<IActionResult>(BadRequest(new { message = "Lý do phải từ 3 đến 400 ký tự." }));
        return Task.FromResult<IActionResult>(StatusCode(503, new
        {
            code = "CANCELLATION_NOT_VERIFIED",
            message = "Yêu cầu hủy trực tuyến đang khóa: chưa xác minh đầy đủ version, idempotency, giữ kho và hoàn tiền. Liên hệ hỗ trợ."
        }));
    }
    public bool CustomerCancellationWebEnabled =>
    bool.TryParse(
        HttpContext.Features
            .Get<Microsoft.AspNetCore.Http.Features.IServiceProvidersFeature>()?
            .RequestServices?
            .GetService<IConfiguration>()?[
                "Orders:CustomerCancellationWebEnabled"],
        out var enabled) && enabled;

protected async Task<IActionResult> SubmitCancellationAsync(
    long orderId,
    string? reason,
    string? expectedVersion,
    Guid operationId,
    CancellationToken ct)
{
    Response.Headers.CacheControl = "no-store";
    Response.Headers.Vary = "Cookie";

    // Khóa mặc định. Không thay thế bằng kiểm tra môi trường Development.
    if (!CustomerCancellationWebEnabled)
        return await RejectCancellationAsync(orderId, reason);

    var token = Request.Cookies[CustomerCookie.Name];

    if (string.IsNullOrWhiteSpace(token))
        return Unauthorized();

    if (orderId <= 0 ||
        string.IsNullOrWhiteSpace(reason) ||
        reason.Trim().Length is < 3 or > 400 ||
        reason.Any(char.IsControl) ||
        string.IsNullOrWhiteSpace(expectedVersion) ||
        operationId == Guid.Empty)
    {
        return BadRequest(new
        {
            message = "Thông tin yêu cầu hủy không hợp lệ."
        });
    }

    try
    {
        Api.Token = token;

        var receipt = await Api.CancelCustomerOrderAsync(
            orderId,
            reason,
            expectedVersion,
            operationId,
            ct);

        TempData["CancelMessage"] = receipt.ThongBao;

        return RedirectToPage("/Orders/Details", new { orderId });
    }
    catch (CafeApiException ex)
    {
        if (ex.StatusCode is HttpStatusCode.Unauthorized
            or HttpStatusCode.Forbidden)
            return Unauthorized();

        Response.StatusCode = (int)(
            ex.StatusCode ?? HttpStatusCode.ServiceUnavailable);

        Error = ex.Message;

        try
        {
            await LoadDetailAsync(orderId, ct);
        }
        catch (CafeApiException)
        {
            Detail = null;
        }

        return Page();
    }
}
}
