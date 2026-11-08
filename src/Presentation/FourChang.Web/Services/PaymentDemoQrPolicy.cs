using FourChang.Presentation.Shared.Contracts.Customer;

namespace FourChang.Web.Services;

// Display-only demo: never supplies a bank account, payment reference, transaction or proof of funds.
// The real IPaymentQrSource and financial processing flags are intentionally unchanged.
public static class PaymentDemoQrPolicy
{
    public static bool CanShow(CustomerOrderDetail? order, string? flag, string? environment, DateTime nowUtc)
    {
        if (!string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase) ||
            order?.Order is null || !CustomerOrderPresentation.Valid(order, order.Order.Id) ||
            order.Order.Status != "PENDING_PAYMENT" || order.Payments.Count != 1 ||
            order.ExpiresUtc is not { } orderDeadline || orderDeadline <= nowUtc)
            return false;

        var payment = order.Payments[0];
        return payment.Method == "BANK_TRANSFER" && payment.Status == "PENDING" &&
            payment.Requested > 0 && payment.Verified == 0 && order.Collected == 0 &&
            order.Remaining == payment.Requested &&
            payment.ExpiresUtc is { } paymentDeadline && paymentDeadline > nowUtc;
    }

    // Backend owns UTC deadlines. SQL datetime2 has no timezone metadata after materialization.
    // The JavaScript QR-visibility guard requires an explicit UTC suffix and hides at expiry.
    public static DateTime ExpiryUtc(CustomerOrderDetail order)
    {
        var orderDeadline = order.ExpiresUtc!.Value;
        var paymentDeadline = order.Payments[0].ExpiresUtc!.Value;
        var earliest = orderDeadline <= paymentDeadline ? orderDeadline : paymentDeadline;
        return DateTime.SpecifyKind(earliest, DateTimeKind.Utc);
    }
}
