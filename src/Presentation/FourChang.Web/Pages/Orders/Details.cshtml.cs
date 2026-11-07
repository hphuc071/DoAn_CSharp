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
    public Task<IActionResult> OnPostCancelAsync(long orderId, string? reason, CancellationToken ct) =>
        RejectCancellationAsync(orderId, reason);
    public Task<IActionResult> OnGetAsync(long orderId, CancellationToken ct) => ReadAsync(async () =>
    {
        if (!ModelState.IsValid || orderId <= 0) { Response.StatusCode = 404; Error = "Không tìm thấy đơn hàng."; return; }
        await LoadDetailAsync(orderId, ct);
    });
}
