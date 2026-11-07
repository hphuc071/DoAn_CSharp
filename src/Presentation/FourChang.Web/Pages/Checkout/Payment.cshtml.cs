using FourChang.Presentation.Shared.ApiClients;
using FourChang.Web.Pages.Orders;
using FourChang.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace FourChang.Web.Pages.Checkout;

public sealed class PaymentModel(CafeApiClient api, IPaymentQrSource qrSource) : CustomerOrderPageModel(api)
{
    public ValidatedPaymentQr? Qr { get; private set; }
    public Task<IActionResult> OnGetAsync(long orderId, CancellationToken ct) => ReadAsync(async () =>
    {
        if (!ModelState.IsValid || orderId <= 0) { Response.StatusCode = 404; Error = "Chọn một đơn của bạn từ Đơn hàng của tôi."; return; }
        await LoadDetailAsync(orderId, ct);
        // Page load/reload never creates a payment request.
        try
        {
            var candidate = await qrSource.GetAsync(Detail!, ct);
            if (CanDisplay(candidate, Detail!, DateTime.UtcNow)) Qr = candidate;
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException ||
            ex is OperationCanceledException && !ct.IsCancellationRequested)
        { Error = "Nguồn QR chưa phản hồi; chưa có QR được xác nhận. Không tự tạo yêu cầu khác."; }
    });

    public static bool CanDisplay(ValidatedPaymentQr? qr,
        FourChang.Presentation.Shared.Contracts.Customer.CustomerOrderDetail order, DateTime nowUtc)
    {
        if (qr is null || order.Order.Status != "PENDING_PAYMENT" || order.Payments.Count != 1 ||
            order.Remaining <= 0 || order.ExpiresUtc is not { } expiry || expiry <= nowUtc ||
            string.IsNullOrWhiteSpace(qr.Reference) || qr.ExpiresUtc <= nowUtc || qr.ExpiresUtc > expiry ||
            qr.PngDataUri is null || !qr.PngDataUri.StartsWith("data:image/png;base64,", StringComparison.Ordinal) ||
            qr.PngDataUri.Length > 350000) return false;
        var payment = order.Payments[0];
        if (payment.Id != qr.PaymentId || payment.Method != "BANK_TRANSFER" || payment.Status != "PENDING" ||
            payment.Verified != 0 || payment.Requested != qr.Amount || order.Remaining != qr.Amount ||
            payment.Reference != qr.Reference || payment.ExpiresUtc is not { } deadline || deadline < qr.ExpiresUtc)
            return false;
        try
        {
            var image = Convert.FromBase64String(qr.PngDataUri[22..]);
            return image.Length >= 8 && image.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        }
        catch (FormatException) { return false; }
    }
}
