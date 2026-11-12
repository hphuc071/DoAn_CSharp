namespace FourChang.Presentation.Shared.Contracts.Customer;

// UC06 DINE_IN v2 ONLY. The existing TAKEAWAY CreateCustomerOrderRequest and
// its serialized fingerprint must remain unchanged for historical replay.
// QR secret/proof are only sent in an authenticated HTTPS POST body, never URL,
// query string, browser storage, receipt, or audit payload.
public sealed record DineInOrderCreateRequest(
    string? QrToken,
    string? PreviewProof,
    List<OrderItemInput> Items,
    string? GhiChu = null);
