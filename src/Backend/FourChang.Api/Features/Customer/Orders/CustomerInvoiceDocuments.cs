using System.Text;

namespace FourChang.Api.Features.Customer.Orders;

public sealed record ArchivedInvoicePdf(long OrderId, long InvoiceId, string InvoiceNumber, byte[] Content);
public interface IInvoiceDocumentSource
{
    // Retrieve an already-issued immutable document. Implementations must never generate/issue invoices here.
    Task<ArchivedInvoicePdf?> ReadAsync(long orderId, long invoiceId, CancellationToken ct);
}
public sealed class UnavailableInvoiceDocumentSource : IInvoiceDocumentSource
{
    public Task<ArchivedInvoicePdf?> ReadAsync(long orderId, long invoiceId, CancellationToken ct) =>
        Task.FromResult<ArchivedInvoicePdf?>(null);
}
public static class CustomerInvoiceDocuments
{
    public static void MapCustomerInvoiceDocuments(this WebApplication app) =>
        app.MapGet("/api/customer/orders/{orderId:long}/invoices/{invoiceId:long}/pdf", ReadAsync)
            .RequireAuthorization("Customer");

    public static async Task<IResult> ReadAsync(long orderId, long invoiceId, HttpContext context,
        ICustomerOrderReader reader, IInvoiceDocumentSource source, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Vary = "Cookie, Authorization";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'; frame-ancestors 'none'";
        context.Response.Headers.XFrameOptions = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        if (!CustomerOrderReadEndpoints.Owner(context, out var owner)) return Results.Unauthorized();
        if (orderId <= 0 || invoiceId <= 0) return Results.NotFound();
        // Bound both ownership lookup and archive read. Never retry or issue/generate an
        // invoice on a timeout. WaitAsync also bounds a source that ignores cancellation.
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            ct.ThrowIfCancellationRequested();
            var detail = await reader.DetailAsync(owner, orderId, budget.Token).WaitAsync(budget.Token);
            if (detail is null) return Results.NotFound();
            if (detail.Order is null || detail.Order.Id != orderId || detail.Invoices is null ||
                detail.Invoices.Any(x => x is null)) return Results.StatusCode(502);
            var matches = detail.Invoices.Where(x => x.Id == invoiceId).Take(2).ToArray();
            if (matches.Length > 1) return Results.StatusCode(502);
            var invoice = matches.SingleOrDefault();
            if (invoice is null || invoice.Status != "ISSUED" || invoice.IssuedUtc is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(invoice.Number) || invoice.Number.Length > 40 ||
                invoice.Number.Any(char.IsControl) || invoice.Kind is not ("ORIGINAL" or "ADJUSTMENT"))
                return Results.StatusCode(502);
            var document = await source.ReadAsync(orderId, invoiceId, budget.Token).WaitAsync(budget.Token);
            if (document is null) return Results.StatusCode(501);
            if (document.OrderId != orderId || document.InvoiceId != invoiceId || document.InvoiceNumber != invoice.Number ||
                document.Content is not { Length: > 16 and <= 10485760 }) return Results.StatusCode(502);
            // An adapter may share its cache buffer. Freeze bytes before validating and
            // sending them so a later buffer mutation cannot change the checked response.
            var content = document.Content.ToArray();
            if (!ValidPdf(content)) return Results.StatusCode(502);
            // Numeric-only file name; never use invoice text or a caller-supplied path.
            return Results.File(content, "application/pdf", $"invoice-{invoiceId}.pdf");
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException or IOException ||
            ex is OperationCanceledException && !ct.IsCancellationRequested)
        { return Results.StatusCode(503); }
    }

    public static bool ValidPdf(byte[]? content) => content is { Length: > 16 and <= 10485760 } &&
        content.AsSpan(0, 5).SequenceEqual("%PDF-"u8) &&
        Encoding.ASCII.GetString(content.AsSpan(Math.Max(0, content.Length - 1024))).TrimEnd().EndsWith("%%EOF", StringComparison.Ordinal);
}
