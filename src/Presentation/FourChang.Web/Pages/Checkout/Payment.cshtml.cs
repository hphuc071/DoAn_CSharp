using FourChang.Presentation.Shared.ApiClients;
using FourChang.Web.Pages.Orders;
using FourChang.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FourChang.Web.Pages.Checkout;

public sealed class PaymentModel(CafeApiClient api, IPaymentQrSource qrSource) : CustomerOrderPageModel(api)
{
    public ValidatedPaymentQr? Qr { get; private set; }
    public Task<IActionResult> OnGetAsync(long orderId, CancellationToken ct) => ReadAsync(async () =>
    {
        if (!ModelState.IsValid || orderId <= 0) { Response.StatusCode = 404; Error = "Chọn một đơn của bạn từ Đơn hàng của tôi."; return; }
        await LoadDetailAsync(orderId, ct);
        // Page load/reload never creates a payment request.
        if (!CanRequestQr(Detail!, DateTime.UtcNow)) return;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            // Bound the await even if an adapter ignores cancellation. This is a read timeout,
            // not a payment deadline, and never authorizes a retry or another payment request.
            var candidate = await qrSource.GetAsync(Detail!, timeout.Token).WaitAsync(timeout.Token);
            if (CanDisplay(candidate, Detail!, DateTime.UtcNow)) Qr = candidate;
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException ||
            ex is JsonException or NotSupportedException or FormatException or CafeApiException ||
            ex is OperationCanceledException && !ct.IsCancellationRequested)
        { Error = "Nguồn QR không khả dụng hoặc trả dữ liệu không hợp lệ; chưa có QR được xác nhận. Không tự tạo yêu cầu khác."; }
    });

    private static bool CanRequestQr(
        FourChang.Presentation.Shared.Contracts.Customer.CustomerOrderDetail order, DateTime nowUtc)
    {
        if (order.Order is null || !CustomerOrderPresentation.Valid(order, order.Order.Id) || order.Order.Status != "PENDING_PAYMENT" ||
            order.Payments.Count != 1 || order.ExpiresUtc is not { } expiry || expiry <= nowUtc)
            return false;
        var payment = order.Payments[0];
        return payment.Status == "PENDING" && payment.Verified == 0 && payment.Requested > 0 &&
            !string.IsNullOrWhiteSpace(payment.Reference) && payment.ExpiresUtc is { } deadline && deadline > nowUtc &&
            order.Remaining == payment.Requested;
    }

    public static bool CanDisplay(ValidatedPaymentQr? qr,
        FourChang.Presentation.Shared.Contracts.Customer.CustomerOrderDetail order, DateTime nowUtc)
    {
        if (qr is null || !CanRequestQr(order, nowUtc) || qr.ExpiresUtc.Kind != DateTimeKind.Utc ||
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
