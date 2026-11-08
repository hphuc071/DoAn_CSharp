using System.Net;
using System.Security.Claims;
using System.Text;
using FourChang.Api.Features.Customer.Orders;
using FourChang.Presentation.Shared.Contracts.Customer;
using FourChang.Web.Pages.Orders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FourChang.Tests;

[Trait("Safety", "NoDatabase")]
public sealed class InvoiceDocumentOfflineTests
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n% test-only archive envelope, not a legal invoice\n%%EOF\n");
    private sealed class Reader(string status = "ISSUED") : ICustomerOrderReader
    {
        public Func<CustomerOrderDetail, CustomerOrderDetail>? Transform;
        public Exception? Error;
        public Task<CustomerOrderDetail?> DetailAsync(long owner, long id, CancellationToken ct) =>
            Error is not null ? Task.FromException<CustomerOrderDetail?>(Error) :
            Task.FromResult(owner == 6 && id == 7 ? Apply(OrderReadTests.Snapshot() with
            { Invoices = [new(11, "INV-MOCK", "ORIGINAL", status, 60000, OrderReadTests.Now)] }) : null);
        private CustomerOrderDetail Apply(CustomerOrderDetail value) => Transform?.Invoke(value) ?? value;
        public Task<CustomerOrderPage> ListAsync(long owner, CustomerOrderFilter filter, CancellationToken ct) => throw new InvalidOperationException();
    }
    private sealed class Source : IInvoiceDocumentSource
    {
        public int Calls;
        public ArchivedInvoicePdf? Document = new(7, 11, "INV-MOCK", Pdf);
        public bool Timeout;
        public bool NeverComplete;
        public Task<ArchivedInvoicePdf?> ReadAsync(long order, long invoice, CancellationToken ct)
        { Calls++; if (Timeout) throw new TimeoutException(); return NeverComplete ? new TaskCompletionSource<ArchivedInvoicePdf?>().Task : Task.FromResult(Document); }
    }
    private static DefaultHttpContext Actor(long owner)
    {
        var context = new DefaultHttpContext();
        if (owner > 0) context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, owner.ToString())], "Mock"));
        return context;
    }

    [Theory]
    [InlineData(6, "ISSUED", 200, 1)] [InlineData(9, "ISSUED", 404, 0)]
    [InlineData(0, "ISSUED", 401, 0)] [InlineData(6, "DRAFT", 404, 0)]
    public async Task Only_owner_can_read_an_already_issued_pdf(long owner, string status, int expected, int calls)
    {
        var source = new Source(); var context = Actor(owner);
        var result = await CustomerInvoiceDocuments.ReadAsync(7, 11, context, new Reader(status), source, default);
        Assert.Equal(expected, result is IStatusCodeHttpResult statusResult ? statusResult.StatusCode : 200);
        Assert.Equal(calls, source.Calls); Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        if (expected == 200) { var file = Assert.IsAssignableFrom<IFileHttpResult>(result); Assert.Equal("application/pdf", file.ContentType); Assert.Equal("invoice-11.pdf", file.FileDownloadName); }
    }

    [Theory]
    [InlineData("missing", 501)] [InlineData("timeout", 503)] [InlineData("wrong-owner-document", 502)]
    [InlineData("html", 502)] [InlineData("wrong-number", 502)]
    public async Task Invalid_or_unavailable_archive_never_generates_an_invoice(string scenario, int status)
    {
        var source = new Source();
        switch (scenario)
        {
            case "missing": source.Document = null; break;
            case "timeout": source.Timeout = true; break;
            case "wrong-owner-document": source.Document = source.Document! with { OrderId = 8 }; break;
            case "wrong-number": source.Document = source.Document! with { InvoiceNumber = "OTHER" }; break;
            case "html": source.Document = source.Document! with { Content = Encoding.ASCII.GetBytes("<html>Not PDF</html>") }; break;
        }
        var result = await CustomerInvoiceDocuments.ReadAsync(7, 11, Actor(6), new Reader(), source, default);
        Assert.Equal(status, ((IStatusCodeHttpResult)result).StatusCode); Assert.Equal(1, source.Calls);
    }

    [Theory]
    [InlineData(200, "application/pdf", 200)] [InlineData(200, "text/html", 502)]
    [InlineData(404, "text/html", 404)] [InlineData(501, "text/html", 501)] [InlineData(503, "text/html", 503)]
    public async Task Web_pdf_proxy_only_uses_authenticated_get_and_returns_controlled_errors(int status, string media, int expected)
    {
        var stub = new OrderReadTests.Stub(request => {
            Assert.Equal(HttpMethod.Get, request.Method); Assert.Equal("customer-a", request.Headers.Authorization?.Parameter);
            Assert.Equal("/api/customer/orders/7/invoices/11/pdf", request.RequestUri!.AbsolutePath);
            var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new ByteArrayContent(Pdf) };
            response.Content.Headers.ContentType = new(media); return response;
        });
        var page = OrderReadTests.Page(new DetailsModel(OrderReadTests.Client(stub)));
        var result = await page.OnGetInvoiceAsync(7, 11, default);
        Assert.Equal("Cookie", page.Response.Headers.Vary.ToString());
        if (expected == 200) { Assert.Equal(Pdf, Assert.IsType<FileContentResult>(result).FileContents); }
        else Assert.Equal(expected, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Equal(1, stub.Calls);
    }

    [Fact]
    public async Task Runtime_archive_source_is_unavailable_and_pdf_proxy_missing_cookie_sends_nothing()
    {
        Assert.Null(await new UnavailableInvoiceDocumentSource().ReadAsync(7, 11, default));
        var stub = new OrderReadTests.Stub(_ => throw new InvalidOperationException());
        var page = OrderReadTests.Page(new DetailsModel(OrderReadTests.Client(stub)), null);
        Assert.IsType<UnauthorizedResult>(await page.OnGetInvoiceAsync(7, 11, default)); Assert.Equal(0, stub.Calls);
    }

    [Theory]
    [InlineData(true, 504)] [InlineData(false, 503)]
    public async Task Pdf_network_timeout_and_io_failure_are_controlled_without_retry(bool timeout, int expected)
    {
        var stub = new OrderReadTests.Stub(_ => throw (timeout ? new TaskCanceledException() : new IOException()));
        var page = OrderReadTests.Page(new DetailsModel(OrderReadTests.Client(stub)));
        Assert.Equal(expected, Assert.IsType<ObjectResult>(await page.OnGetInvoiceAsync(7, 11, default)).StatusCode);
        Assert.Equal(1, stub.Calls);
    }

    [Theory]
    [InlineData("number")] [InlineData("kind")] [InlineData("null-list")]
    public async Task Invalid_issued_metadata_is_refused_before_reading_any_archive(string field)
    {
        var source = new Source(); var reader = new Reader { Transform = d => field == "null-list"
            ? d with { Invoices = null! } : d with { Invoices = [d.Invoices[0] with {
                Number = field == "number" ? "\r\n" : "INV-MOCK", Kind = field == "kind" ? "OTHER" : "ORIGINAL" }] } };
        var result = await CustomerInvoiceDocuments.ReadAsync(7, 11, Actor(6), reader, source, default);
        Assert.Equal(502, ((IStatusCodeHttpResult)result).StatusCode); Assert.Equal(0, source.Calls);
    }

    [Fact]
    public async Task Failure_of_ownership_snapshot_is_controlled_and_never_reads_document()
    {
        var source = new Source();
        var result = await CustomerInvoiceDocuments.ReadAsync(7, 11, Actor(6), new Reader { Error = new IOException("private-source-detail") }, source, default);
        Assert.Equal(503, ((IStatusCodeHttpResult)result).StatusCode); Assert.Equal(0, source.Calls);
    }

    [Fact]
    public async Task Response_owns_a_frozen_byte_copy_not_the_sources_mutable_cache()
    {
        var source = new Source { Document = new(7, 11, "INV-MOCK", Pdf.ToArray()) };
        var result = Assert.IsType<FileContentHttpResult>(await CustomerInvoiceDocuments.ReadAsync(7, 11, Actor(6), new Reader(), source, default));
        source.Document!.Content[0] = (byte)'X';
        Assert.Equal(Pdf, result.FileContents.ToArray()); Assert.Equal(1, source.Calls);
    }

    [Fact]
    public async Task Reader_and_archive_share_a_bounded_budget_even_if_source_ignores_cancellation()
    {
        var source = new Source { NeverComplete = true };
        var result = await CustomerInvoiceDocuments.ReadAsync(7, 11, Actor(6), new Reader(), source, default)
            .WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(503, ((IStatusCodeHttpResult)result).StatusCode); Assert.Equal(1, source.Calls);
    }

    [Fact]
    public async Task Caller_cancellation_is_propagated_without_retrying_or_issuing_documents()
    {
        var source = new Source { NeverComplete = true }; using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CustomerInvoiceDocuments.ReadAsync(7, 11, Actor(6), new Reader(), source, cancel.Token));
        Assert.InRange(source.Calls, 0, 1);
    }

    [Fact]
    public async Task Truncated_content_with_a_false_length_is_not_returned_by_the_web_proxy()
    {
        var stub = new OrderReadTests.Stub(_ => {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Pdf) };
            response.Content.Headers.ContentType = new("application/pdf"); response.Content.Headers.ContentLength = Pdf.Length + 10;
            return response;
        });
        var page = OrderReadTests.Page(new DetailsModel(OrderReadTests.Client(stub)));
        Assert.Equal(502, Assert.IsType<ObjectResult>(await page.OnGetInvoiceAsync(7, 11, default)).StatusCode);
        Assert.Equal("DENY", page.Response.Headers.XFrameOptions.ToString());
        Assert.Contains("sandbox", page.Response.Headers.ContentSecurityPolicy.ToString()); Assert.Equal(1, stub.Calls);
    }

    [Theory]
    [InlineData("empty")] [InlineData("null")] [InlineData("oversize")] [InlineData("wrong-id")]
    public async Task Invalid_source_documents_never_reach_the_customer(string change)
    {
        var source = new Source();
        source.Document = change switch {
            "empty" => source.Document! with { Content = [] }, "null" => source.Document! with { Content = null! },
            "oversize" => source.Document! with { Content = new byte[10485761] },
            _ => source.Document! with { InvoiceId = 99 }
        };
        Assert.Equal(502, ((IStatusCodeHttpResult)await CustomerInvoiceDocuments.ReadAsync(7, 11, Actor(6), new Reader(), source, default)).StatusCode);
        Assert.Equal(1, source.Calls);
    }
}
