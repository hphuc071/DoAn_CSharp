namespace FourChang.Presentation.Shared.Contracts.Customer;

// Read snapshots only. No authentication tokens, bank credentials or write commands.
public sealed record CustomerOrderFilter(int Page = 1, int PageSize = 10, string? Status = null,
    DateTime? FromUtc = null, DateTime? ToUtc = null)
{
    public string? Validate(bool expiryReadEnabled = false) => Page < 1 || Page > 100000 || PageSize < 1 || PageSize > 50
        ? "Trang phải từ 1 đến 100000; mỗi trang từ 1 đến 50 đơn."
        : Status is not null && !CustomerOrderStates.IsReadable(Status, expiryReadEnabled)
            ? "Trạng thái đơn không hợp lệ."
            : FromUtc > ToUtc ? "Khoảng thời gian không hợp lệ." : null;
}

public static class CustomerOrderStates
{
    // Read compatibility only, not approval to write the proposed SQL status.
    public static bool IsReadable(string status, bool expiryReadEnabled = false) =>
        Orders.Contains(status) || expiryReadEnabled && status == "EXPIRED";
    public static readonly IReadOnlySet<string> Orders = new HashSet<string>(StringComparer.Ordinal)
    { "DRAFT", "PENDING_PAYMENT", "PENDING_CONFIRMATION", "ACCEPTED", "PREPARING", "READY",
      "DELIVERING", "COMPLETED", "REJECTED", "CANCELLED", "DELIVERY_FAILED" };
    public static readonly IReadOnlySet<string> Payments = new HashSet<string>(StringComparer.Ordinal)
    { "PENDING", "PARTIAL", "VERIFIED", "EXPIRED", "FAILED", "RECONCILING" };
}

public sealed record CustomerOrderSummary(long Id, string Code, string Status, string Fulfilment,
    DateTime CreatedUtc, DateTime UpdatedUtc);
public sealed record CustomerOrderPage(IReadOnlyList<CustomerOrderSummary> Items, int Page, int PageSize,
    int TotalCount, DateTime SnapshotUtc);
public sealed record CustomerOrderTopping(string Name, int Portions, decimal Price);
public sealed record CustomerOrderLine(long Id, string Name, string Size, string Temperature,
    int Sugar, int Ice, int Quantity, int Cancelled, int Started, int Prepared, decimal BasePrice,
    string? Note, IReadOnlyList<CustomerOrderTopping> Toppings)
{
    public decimal UnitPrice => BasePrice + Toppings.Sum(x => x.Price * x.Portions);
    public decimal OriginalTotal => UnitPrice * Quantity;
    public decimal CurrentTotal => UnitPrice * (Quantity - Cancelled);
}
public sealed record CustomerPaymentSnapshot(long Id, string Code, string Method, string Status,
    decimal Requested, decimal Verified, string? Reference, DateTime? ExpiresUtc, DateTime? VerifiedUtc);
public sealed record CustomerRefundSnapshot(long Id, string Code, string Status, decimal Requested,
    decimal? Approved, decimal SuccessfullyDisbursed);
public sealed record CustomerInvoiceSnapshot(long Id, string Number, string Kind, string Status,
    decimal Amount, DateTime? IssuedUtc);
public sealed record CustomerOrderDetail(CustomerOrderSummary Order, string Version,
    DateTime? ExpiresUtc, DateTime? AcceptedUtc, DateTime? CompletedUtc,
    decimal OriginalShippingFee, decimal ShippingFee, IReadOnlyList<CustomerOrderLine> Lines,
    IReadOnlyList<CustomerPaymentSnapshot> Payments, IReadOnlyList<CustomerRefundSnapshot> Refunds,
    IReadOnlyList<CustomerInvoiceSnapshot> Invoices, DateTime SnapshotUtc)
{
    public decimal OriginalTotal => Lines.Sum(x => x.OriginalTotal) + OriginalShippingFee;
    public decimal CurrentTotal => Lines.Sum(x => x.CurrentTotal) + ShippingFee;
    public decimal Collected => Payments.Sum(x => x.Verified);
    public decimal Refunded => Refunds.Sum(x => x.SuccessfullyDisbursed);
    public decimal Remaining => Math.Max(0, CurrentTotal - Collected + Refunded);
    public decimal RefundObligation => Math.Max(0, Collected - Refunded - CurrentTotal);
    public bool CancellationEligible => Order.Status == "PENDING_CONFIRMATION";
}
