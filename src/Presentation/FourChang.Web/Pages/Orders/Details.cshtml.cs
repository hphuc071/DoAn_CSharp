
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace FourChang.Web.Pages.Orders;

public sealed class DetailsModel(CafeApiClient api)
    : CustomerOrderPageModel(api)
{
    // Xem chi tiết đơn hàng.
    public Task<IActionResult> OnGetAsync(
        long orderId,
        CancellationToken ct) =>
        ReadAsync(async () =>
        {
            if (!ModelState.IsValid || orderId <= 0)
            {
                Response.StatusCode = 404;
                Error = "Không tìm thấy đơn hàng.";
                return;
            }

            await LoadDetailAsync(orderId, ct);
        });

    // Yêu cầu hủy đơn.
    // SubmitCancellationAsync vẫn kiểm tra cờ bảo vệ.
    public Task<IActionResult> OnPostCancelAsync(
        long orderId,
        string? reason,
        string? expectedVersion,
        Guid operationId,
        CancellationToken ct) =>
        SubmitCancellationAsync(
            orderId,
            reason,
            expectedVersion,
            operationId,
            ct);

    // Tải hóa đơn PDF đã lưu, không tự phát hành hóa đơn.
    public async Task<IActionResult> OnGetInvoiceAsync(
        long orderId,
        long invoiceId,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Vary = "Cookie";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy =
            "sandbox; default-src 'none'; frame-ancestors 'none'";
        Response.Headers.XFrameOptions = "DENY";
        Response.Headers["Referrer-Policy"] = "no-referrer";

        var token = Request.Cookies[CustomerCookie.Name];

        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized();

        if (orderId <= 0 || invoiceId <= 0)
            return NotFound();

        Api.Token = token;

        try
        {
            var pdf = await Api.GetCustomerInvoicePdfAsync(
                orderId,
                invoiceId,
                ct);

            return File(
                pdf,
                "application/pdf",
                $"invoice-{invoiceId}.pdf");
        }
        catch (CafeApiException ex)
        {
            return StatusCode(
                (int)(ex.StatusCode ??
                    System.Net.HttpStatusCode.ServiceUnavailable),
                new
                {
                    message =
                        "Chưa có PDF đã phát hành khả dụng " +
                        "hoặc không đủ quyền. Không tạo hóa đơn mới."
                });
        }
    }
}
