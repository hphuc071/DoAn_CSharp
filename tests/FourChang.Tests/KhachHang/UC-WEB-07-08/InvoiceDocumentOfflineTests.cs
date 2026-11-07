using System.Net;
using System.Security.Claims;
using System.Text;
using FourChang.Api.Features.Customer.Orders;
using FourChang.Presentation.Shared.Contracts.Customer;
using FourChang.Web.Pages.Orders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FourChang.Tests;

[Trait("Safety", "NoDatabase")]
public sealed class InvoiceDocumentOfflineTests
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n% test-only archive envelope, not a legal invoice\n%%EOF\n");
    private sealed class Reader(string status = "ISSUED") : ICustomerOrderReader
    {
        public Task<CustomerOrderDetail?> DetailAsync(long owner, long id, CancellationToken ct) =>
            Task.FromResult(owner == 6 && id == 7 ? OrderReadTests.Snapshot() with
            { Invoices = [new(11, "INV-MOCK", "ORIGINAL", status, 60000, OrderReadTests.Now)] } : null);
        public Task<CustomerOrderPage> ListAsync(long owner, CustomerOrderFilter filter, CancellationToken ct) => throw new InvalidOperationException();
    }
    private sealed class Source : IInvoiceDocumentSource
    {
        public int Calls;
        public ArchivedInvoicePdf? Document = new(7, 11, "INV-MOCK", Pdf);
        public bool Timeout;
        public Task<ArchivedInvoicePdf?> ReadAsync(long order, long invoice, CancellationToken ct)
        { Calls++; if (Timeout) throw new TimeoutException(); return Task.FromResult(Document); }
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
}
