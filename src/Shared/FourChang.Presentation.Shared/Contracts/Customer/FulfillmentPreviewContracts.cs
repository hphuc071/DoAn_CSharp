namespace FourChang.Presentation.Shared.Contracts.Customer;

// Read-only preparation. This is NOT a Create command or permission to reserve stock.
// Never persist QrToken, recipient or address in browser storage for this preview.
public sealed record FulfillmentPreviewRequest(
    string Method, string? QrToken, string? Recipient, string? Phone, string? Address,
    decimal? Latitude, decimal? Longitude, List<OrderItemInput> Items, string? PreviousProof = null);

public sealed record FulfillmentPreview(
    string Method, string? TableCode, OrderPriceSummary Price, DateTimeOffset ExpiresAt,
    string Proof, bool CreationEnabled = false);
