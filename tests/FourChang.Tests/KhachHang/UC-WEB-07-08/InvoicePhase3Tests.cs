using System.Security.Claims;
using FourChang.Api.Features.Customer.Orders;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FourChang.Tests;

[Trait("Safety", "NoDatabase")]
public sealed class InvoicePhase3Tests
{
    private sealed class Reader(string scenario) : ICustomerOrderReader
    {
        public Task<CustomerOrderDetail?> DetailAsync(long owner, long id, CancellationToken ct)
        {
            var detail = OrderReadTests.Snapshot();
            var invoice = new CustomerInvoiceSnapshot(11, "INV", "ORIGINAL", "ISSUED", 60000, OrderReadTests.Now);
            detail = detail with { Invoices = scenario == "duplicate" ? [invoice, invoice] : [invoice] };
            if (scenario == "wrong-order") detail = detail with { Order = detail.Order with { Id = 99 } };
            return Task.FromResult<CustomerOrderDetail?>(detail);
        }
        public Task<CustomerOrderPage> ListAsync(long owner, CustomerOrderFilter filter, CancellationToken ct) => throw new InvalidOperationException();
    }
    private sealed class Source : IInvoiceDocumentSource
    {
        public int Calls;
        public Task<ArchivedInvoicePdf?> ReadAsync(long order, long invoice, CancellationToken ct)
        { Calls++; throw new IOException("Mock archive stream unavailable"); }
    }

    [Theory]
    [InlineData("wrong-order", 502, 0)] [InlineData("duplicate", 502, 0)] [InlineData("io", 503, 1)]
    public async Task Invalid_archive_snapshot_is_rejected_without_disclosing_document(string scenario, int status, int calls)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "6")], "Mock"));
        var source = new Source();
        var result = await CustomerInvoiceDocuments.ReadAsync(7, 11, context, new Reader(scenario), source, default);
        Assert.Equal(status, ((IStatusCodeHttpResult)result).StatusCode); Assert.Equal(calls, source.Calls);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.Equal("Cookie, Authorization", context.Response.Headers.Vary.ToString());
        Assert.Equal("nosniff", context.Response.Headers.XContentTypeOptions.ToString());
    }

    [Fact]
    public async Task Pdf_endpoint_is_get_only_and_requires_customer_policy_without_starting_a_server()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "OfflineMockTests" });
        builder.Services.AddSingleton<ICustomerOrderReader>(new Reader("io"));
        builder.Services.AddSingleton<IInvoiceDocumentSource>(new UnavailableInvoiceDocumentSource());
        await using var app = builder.Build();
        app.MapCustomerInvoiceDocuments();
        var route = Assert.Single(((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints).OfType<RouteEndpoint>());
        Assert.Equal("Customer", Assert.Single(route.Metadata.GetOrderedMetadata<IAuthorizeData>()).Policy);
        Assert.Equal("GET", Assert.Single(route.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods));
    }
}
