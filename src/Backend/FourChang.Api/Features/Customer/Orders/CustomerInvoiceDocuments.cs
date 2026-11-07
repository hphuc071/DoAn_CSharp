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
        if (!CustomerOrderReadEndpoints.Owner(context, out var owner)) return Results.Unauthorized();
        if (orderId <= 0 || invoiceId <= 0) return Results.NotFound();
        var detail = await reader.DetailAsync(owner, orderId, ct);
        if (detail is not null && detail.Order.Id != orderId) return Results.StatusCode(502);
        var matches = detail?.Invoices.Where(x => x.Id == invoiceId).Take(2).ToArray();
        if (matches is { Length: > 1 }) return Results.StatusCode(502);
        var invoice = matches?.SingleOrDefault();
        if (invoice is null || invoice.Status != "ISSUED" || invoice.IssuedUtc is null) return Results.NotFound();
        ArchivedInvoicePdf? document;
        try { document = await source.ReadAsync(orderId, invoiceId, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException or IOException ||
            ex is OperationCanceledException && !ct.IsCancellationRequested)
        { return Results.StatusCode(503); }
        if (document is null) return Results.StatusCode(501);
        if (document.OrderId != orderId || document.InvoiceId != invoiceId || document.InvoiceNumber != invoice.Number ||
            !ValidPdf(document.Content)) return Results.StatusCode(502);
        // Numeric-only file name; never use user-controlled invoice text in response headers.
        return Results.File(document.Content, "application/pdf", $"invoice-{invoiceId}.pdf");
    }

    public static bool ValidPdf(byte[]? content) => content is { Length: > 16 and <= 10485760 } &&
        content.AsSpan(0, 5).SequenceEqual("%PDF-"u8) &&
        Encoding.ASCII.GetString(content.AsSpan(Math.Max(0, content.Length - 1024))).TrimEnd().EndsWith("%%EOF", StringComparison.Ordinal);
}
