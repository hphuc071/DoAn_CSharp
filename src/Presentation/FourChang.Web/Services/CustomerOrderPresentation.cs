using FourChang.Presentation.Shared.Contracts.Customer;

namespace FourChang.Web.Services;

public static class CustomerOrderPresentation
{
    // An elapsed deadline is a display warning, not evidence that a worker released stock/cancelled an order.
    public static string PaymentMessage(CustomerPaymentSnapshot payment, DateTime nowUtc) => payment.Status switch
    {
        "VERIFIED" when payment.Verified >= payment.Requested && payment.Requested > 0 && payment.VerifiedUtc is not null
            => "Đã xác minh thanh toán. Tiến trình đơn được hiển thị riêng.",
        "VERIFIED" => "Dữ liệu xác minh chưa nhất quán; cần đối soát.",
        "EXPIRED" => "Yêu cầu thanh toán đã hết hạn. Tiền đến muộn cần đối soát; không tự chuyển lại.",
        "RECONCILING" => "Đang đối soát; không tự chuyển khoản lần nữa.",
        "FAILED" => "Thanh toán chưa thành công; liên hệ hỗ trợ trước khi chuyển lại.",
        "PENDING" or "PARTIAL" when payment.ExpiresUtc is { } deadline && nowUtc >= deadline
            => "Đã qua hạn hiển thị; chưa có xác nhận xử lý hết hạn từ hệ thống. Cần kiểm tra/đối soát.",
        "PARTIAL" => "Chưa xác minh đủ tiền; đơn chưa đủ điều kiện tiếp nhận.",
        "PENDING" => "Đang chờ xác minh. Quét QR hoặc bấm tải lại không phải bằng chứng đã thu tiền.",
        _ => "Trạng thái thanh toán chưa được hỗ trợ; cần đối soát."
    };

    public static bool Valid(CustomerOrderDetail? value, long id, bool expiryReadEnabled = false)
    {
        try
        {
            // Nonnegative individual values can still overflow when quantities/toppings, payments
            // or refunds are aggregated. Reject before Razor or the QR boundary reads those totals.
            return ValidSnapshot(value, id, expiryReadEnabled) && value!.OriginalTotal >= 0 &&
                value.CurrentTotal >= 0 && value.Collected >= 0 && value.Refunded >= 0 &&
                value.Remaining >= 0 && value.RefundObligation >= 0;
        }
        catch (OverflowException) { return false; }
    }

    private static bool ValidSnapshot(CustomerOrderDetail? value, long id, bool expiryReadEnabled) => value is not null &&
        value.Order is not null && value.Order.Id == id && !string.IsNullOrWhiteSpace(value.Order.Code) &&
        !string.IsNullOrWhiteSpace(value.Version) &&
        CustomerOrderStates.IsReadable(value.Order.Status, expiryReadEnabled) && value.Lines is not null &&
        value.Payments is not null && value.Refunds is not null && value.Invoices is not null &&
        value.OriginalShippingFee >= value.ShippingFee && value.ShippingFee >= 0 &&
        value.Lines.All(x => x is not null && x.Id > 0 && x.Toppings is not null && x.Quantity > 0 && x.Cancelled >= 0 &&
            x.Cancelled <= x.Quantity && x.BasePrice >= 0 && x.Started >= 0 && x.Prepared >= 0 &&
            x.Toppings.All(t => t is not null && t.Portions > 0 && t.Price >= 0)) &&
        value.Payments.All(x => x is not null && x.Id > 0 && x.Method == "BANK_TRANSFER" && x.Requested >= 0 &&
            x.Verified >= 0 && CustomerOrderStates.Payments.Contains(x.Status)) &&
        value.Refunds.All(x => x is not null && x.Id > 0 && x.Requested >= 0 && x.SuccessfullyDisbursed >= 0) &&
        // SQL CK_HoaDon_2 permits signed ADJUSTMENT amounts; only ORIGINAL must be nonnegative.
        value.Invoices.All(x => x is not null && x.Id > 0 &&
            (x.Kind == "ORIGINAL" ? x.Amount >= 0 : x.Kind == "ADJUSTMENT")) &&
        value.Lines.Select(x => x.Id).Distinct().Count() == value.Lines.Count &&
        value.Payments.Select(x => x.Id).Distinct().Count() == value.Payments.Count &&
        value.Refunds.Select(x => x.Id).Distinct().Count() == value.Refunds.Count &&
        value.Invoices.Select(x => x.Id).Distinct().Count() == value.Invoices.Count;
}

// A future trusted/configured VietQR adapter must validate the encoded amount, beneficiary,
// reference and expiry before returning an image. Stored arbitrary QR text is never rendered as an image URL.
public sealed record ValidatedPaymentQr(long PaymentId, decimal Amount, string Reference,
    DateTime ExpiresUtc, string PngDataUri);
public interface IPaymentQrSource
{
    Task<ValidatedPaymentQr?> GetAsync(CustomerOrderDetail order, CancellationToken ct);
}
public sealed class UnavailablePaymentQrSource : IPaymentQrSource
{
    public Task<ValidatedPaymentQr?> GetAsync(CustomerOrderDetail order, CancellationToken ct) =>
        Task.FromResult<ValidatedPaymentQr?>(null);
}
