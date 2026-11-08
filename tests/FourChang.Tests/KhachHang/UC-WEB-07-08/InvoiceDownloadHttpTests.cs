using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using FourChang.Api.Features.Customer.Orders;
using FourChang.Api.Security;
using FourChang.Infrastructure.Database.Context;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FourChang.Tests;

[Trait("Safety", "NoDatabase")]
public sealed class InvoiceDownloadHttpTests
{
    // Transport/policy fixture only, NOT a legal invoice or real Customer login proof.
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n% synthetic archive transport fixture\n%%EOF\n");
    private sealed class Reader : ICustomerOrderReader
    {
        public int Calls;
        public Task<CustomerOrderDetail?> DetailAsync(long owner, long order, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(owner == 6 && order == 7 ? OrderReadTests.Snapshot() with {
                Invoices = [new(11, "../../source-number", "ORIGINAL", "ISSUED", 60000, OrderReadTests.Now)]
            } : null);
        }
        public Task<CustomerOrderPage> ListAsync(long owner, CustomerOrderFilter filter, CancellationToken ct) => throw new InvalidOperationException();
    }
    private sealed class Source : IInvoiceDocumentSource
    {
        public int Calls;
        public Task<ArchivedInvoicePdf?> ReadAsync(long order, long invoice, CancellationToken ct)
        {
            Calls++; Assert.Equal(7, order); Assert.Equal(11, invoice);
            return Task.FromResult<ArchivedInvoicePdf?>(new(order, invoice, "../../source-number", Pdf));
        }
    }
    // Explicit synthetic identity for role/ownership/HTTP result tests. Production DI never uses it.
    private sealed class TestIdentity(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Role", out var role))
                return Task.FromResult(AuthenticateResult.NoResult());
            var user = new ClaimsPrincipal(new ClaimsIdentity([
                new(ClaimTypes.Role, role.ToString()), new(ClaimTypes.NameIdentifier, Request.Headers["X-Test-Owner"].ToString())
            ], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(user, Scheme.Name)));
        }
    }
    private static async Task<WebApplication> StartAsync(Reader reader, Source source, bool realAuthentication = false)
    {
        var b = WebApplication.CreateBuilder(); b.Configuration.Sources.Clear(); b.Logging.ClearProviders(); b.WebHost.UseTestServer();
        b.Services.AddSingleton<ICustomerOrderReader>(reader); b.Services.AddSingleton<IInvoiceDocumentSource>(source);
        if (realAuthentication)
        {
            b.Services.AddDbContext<CafeDbContext>(o => o.UseSqlServer("Server=forbidden.invalid;Database=NoDatabase;Integrated Security=true"));
            b.Services.AddAuthentication("Session").AddScheme<AuthenticationSchemeOptions, SessionAuthentication>("Session", _ => { });
        }
        else b.Services.AddAuthentication("Fixture").AddScheme<AuthenticationSchemeOptions, TestIdentity>("Fixture", _ => { });
        b.Services.AddAuthorization(o => o.AddPolicy("Customer", p => p.RequireRole("CUSTOMER")));
        var app = b.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapCustomerInvoiceDocuments();
        await app.StartAsync(); return app;
    }

    [Theory]
    [InlineData("CUSTOMER", "6", 200, 1)] [InlineData("CUSTOMER", "9", 404, 0)]
    [InlineData("ADMIN", "6", 403, 0)] [InlineData("STAFF", "6", 403, 0)]
    [InlineData("CUSTOMER_PASSWORD_CHANGE", "6", 403, 0)]
    public async Task Given_principals_real_route_policy_and_ownership_control_the_download(string role, string owner, int status, int sourceCalls)
    {
        var reader = new Reader(); var source = new Source(); await using var app = await StartAsync(reader, source);
        using var client = app.GetTestClient(); client.DefaultRequestHeaders.Add("X-Test-Role", role); client.DefaultRequestHeaders.Add("X-Test-Owner", owner);
        using var response = await client.GetAsync("/api/customer/orders/7/invoices/11/pdf");
        Assert.Equal(status, (int)response.StatusCode); Assert.Equal(sourceCalls, source.Calls);
        if (status == 200)
        {
            Assert.Equal(Pdf, await response.Content.ReadAsByteArrayAsync());
            Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
            Assert.Equal("invoice-11.pdf", response.Content.Headers.ContentDisposition?.FileNameStar);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            Assert.Contains("sandbox", string.Join(";", response.Headers.GetValues("Content-Security-Policy")));
            Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        }
        else Assert.DoesNotContain("source-number", await response.Content.ReadAsStringAsync());
        if (status == 403) Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task Actual_session_authentication_rejects_anonymous_before_reader_or_archive()
    {
        var reader = new Reader(); var source = new Source(); await using var app = await StartAsync(reader, source, true);
        using var client = app.GetTestClient(); using var response = await client.GetAsync("/api/customer/orders/7/invoices/11/pdf");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); Assert.Equal(0, reader.Calls); Assert.Equal(0, source.Calls);
    }

    [Theory]
    [InlineData("GET", "/api/customer/orders/0/invoices/11/pdf", 404)]
    [InlineData("GET", "/api/customer/orders/7/invoices/0/pdf", 404)]
    [InlineData("GET", "/api/customer/orders/99/invoices/11/pdf", 404)]
    [InlineData("GET", "/api/customer/orders/7/invoices/99/pdf", 404)]
    [InlineData("GET", "/api/customer/orders/7/invoices/not-an-id/pdf", 404)]
    [InlineData("POST", "/api/customer/orders/7/invoices/11/pdf", 405)]
    public async Task Numeric_get_only_contract_does_not_resolve_caller_paths_or_generate_documents(string method, string path, int status)
    {
        var reader = new Reader(); var source = new Source(); await using var app = await StartAsync(reader, source);
        using var client = app.GetTestClient(); client.DefaultRequestHeaders.Add("X-Test-Role", "CUSTOMER"); client.DefaultRequestHeaders.Add("X-Test-Owner", "6");
        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
        Assert.Equal(status, (int)response.StatusCode); Assert.Equal(0, source.Calls);
    }
}
