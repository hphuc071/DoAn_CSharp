using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Customer;
using FourChang.Tests.Shared;
using FourChang.Web.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FourChang.Tests;

// Isolated Razor host + in-memory Backend HTTP handler. No API Program, LocalEnvironment,
// DB fixture, SQL connection, external HTTP or business POST can run in this fixture.
[Trait("Safety", "NoDatabase")]
public sealed class OrderRazorOfflineTests : IAsyncLifetime
{
    private WebApplication app = null!;
    private HttpClient http = null!;
    private int backendReads;
    private string state = "PENDING_CONFIRMATION";

    private sealed class Backend(OrderRazorOfflineTests fixture) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal(HttpMethod.Get, request.Method); // Any outbound write fails the test immediately.
            fixture.backendReads++;
            var token = request.Headers.Authorization?.Parameter;
            if (token == "customer-b") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            Assert.Equal("customer-a", token);
            if (request.RequestUri!.AbsolutePath.EndsWith("/pdf", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotImplemented));
            var detail = OrderReadTests.Snapshot(fixture.state) with
            { Invoices = [new(11, "INV-MOCK", "ORIGINAL", "ISSUED", 60000, OrderReadTests.Now)] };
            object value = request.RequestUri!.AbsolutePath == "/api/customer/orders"
                ? new CustomerOrderPage([detail.Order], 1, 10, 1, OrderReadTests.Now) : detail;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(value) });
        }
    }

    public async Task InitializeAsync()
    {
        app = Build();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(address) };
        http.DefaultRequestHeaders.Add("Cookie", "fourchang_session=customer-a");
    }

    private WebApplication Build()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(FourChang.Web.Pages.Orders.IndexModel).Assembly.GetName().Name,
            ContentRootPath = Path.Combine(TestApi.FindRoot(), "src", "Presentation", "FourChang.Web"),
            EnvironmentName = "OfflineMockTests"
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Checkout:OrderCreationEnabled"] = "false" });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddRazorPages();
        builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        builder.Services.AddSingleton<IPaymentQrSource, UnavailablePaymentQrSource>();
        builder.Services.AddHttpClient<CafeApiClient>(client => client.BaseAddress = new Uri("http://mock.invalid"))
            .ConfigurePrimaryHttpMessageHandler(() => new Backend(this));
        var host = builder.Build(); host.MapRazorPages(); return host;
    }

    [Fact]
    public async Task Razor_routes_render_owned_snapshots_links_and_disabled_cancellation_with_csrf()
    {
        var list = await http.GetStringAsync("/Orders");
        Assert.Contains("WEB-FAKE-7", list); Assert.Contains("/Orders/7", list);
        var detail = await http.GetStringAsync("/Orders/7");
        Assert.Contains("__RequestVerificationToken", detail);
        Assert.Contains("disabled", detail); Assert.Contains("name=\"reason\"", detail);
        Assert.Contains("/Checkout/Payment?orderId=7", detail);
        Assert.Contains("name=\"orderId\" value=\"7\"", detail);
        var payment = WebUtility.HtmlDecode(await http.GetStringAsync("/Checkout/Payment?orderId=7"));
        Assert.Contains("REF-FAKE-7", payment); Assert.Contains("Chưa hỗ trợ VietQR", payment);
        Assert.DoesNotContain("data-payment-state", payment); Assert.DoesNotContain("QR MINH HỌA", payment);
        Assert.DoesNotContain("customer-a", payment); Assert.DoesNotContain("localStorage", payment);
    }

    [Fact]
    public async Task Cancel_form_is_absent_after_staff_acceptance_and_reloads_snapshot()
    {
        var before = await http.GetStringAsync("/Orders/7"); Assert.Contains("name=\"reason\"", before);
        state = "ACCEPTED";
        var after = await http.GetStringAsync("/Orders/7"); Assert.DoesNotContain("name=\"reason\"", after);
        Assert.Contains("ACCEPTED", after); Assert.Equal(2, backendReads);
    }

    [Fact]
    public async Task Detail_and_payment_links_keep_history_filters_without_browser_storage()
    {
        const string query = "ReturnPage=2&ReturnStatus=READY&ReturnFromUtc=2026-11-01T00%3A00%3A00Z";
        foreach (var path in new[] { "/Orders/7?" + query, "/Checkout/Payment?orderId=7&" + query })
        {
            var html = WebUtility.HtmlDecode(await http.GetStringAsync(path));
            Assert.Contains("PageNumber=2", html); Assert.Contains("Status=READY", html);
            Assert.Contains("ReturnPage=2", html); Assert.Contains("ReturnStatus=READY", html);
            Assert.Contains("2026-11-01", html); Assert.DoesNotContain("localStorage", html);
        }
    }

    [Fact]
    public async Task Issued_invoice_link_is_read_only_and_missing_archive_returns_501_without_generation()
    {
        var html = WebUtility.HtmlDecode(await http.GetStringAsync("/Orders/7"));
        Assert.Contains("handler=Invoice", html); Assert.Contains("invoiceId=11", html);
        using var missing = await http.GetAsync("/Orders/7?handler=Invoice&invoiceId=11");
        Assert.Equal(HttpStatusCode.NotImplemented, missing.StatusCode); Assert.Equal(2, backendReads);
        http.DefaultRequestHeaders.Remove("Cookie"); http.DefaultRequestHeaders.Add("Cookie", "fourchang_session=customer-b");
        using var other = await http.GetAsync("/Orders/7?handler=Invoice&invoiceId=11");
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    }

    [Fact]
    public async Task Expired_status_requires_read_opt_in_and_never_displays_a_new_payment_demand()
    {
        state = "EXPIRED";
        using var blocked = await http.GetAsync("/Orders/7");
        Assert.Equal(HttpStatusCode.BadGateway, blocked.StatusCode);
        app.Configuration["Orders:ExpiryStatusReadEnabled"] = "true"; // This fixture process only.
        var detail = WebUtility.HtmlDecode(await http.GetStringAsync("/Orders/7"));
        Assert.Contains("EXPIRED", detail); Assert.DoesNotContain("name=\"reason\"", detail);
        var payment = WebUtility.HtmlDecode(await http.GetStringAsync("/Checkout/Payment?orderId=7"));
        Assert.Contains("Không chuyển khoản", payment); Assert.DoesNotContain("Còn thu theo số dư", payment);
        var list = await http.GetStringAsync("/Orders?Status=EXPIRED");
        Assert.Contains("WEB-FAKE-7", list);
    }

    [Fact]
    public async Task Customer_b_receives_no_customer_a_details_or_payment_reference()
    {
        http.DefaultRequestHeaders.Remove("Cookie"); http.DefaultRequestHeaders.Add("Cookie", "fourchang_session=customer-b");
        foreach (var path in new[] { "/Orders/7", "/Checkout/Payment?orderId=7" })
        {
            using var response = await http.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("WEB-FAKE-7", html); Assert.DoesNotContain("REF-FAKE-7", html);
        }
    }

    [Fact]
    public async Task Razor_rejects_missing_csrf_and_manual_cancel_with_valid_csrf_never_reaches_backend()
    {
        using var missing = await http.PostAsync("/Orders/7?handler=Cancel", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["orderId"] = "7", ["reason"] = "Đổi ý" }));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode); Assert.Equal(0, backendReads);
        var html = await http.GetStringAsync("/Orders/7");
        var csrf = WebUtility.HtmlDecode(Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Assert.NotEmpty(csrf);
        using var blocked = await http.PostAsync("/Orders/7?handler=Cancel", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["orderId"] = "7", ["reason"] = "Đổi ý", ["__RequestVerificationToken"] = csrf }));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, blocked.StatusCode);
        Assert.Contains("CANCELLATION_NOT_VERIFIED", await blocked.Content.ReadAsStringAsync());
        Assert.Equal(1, backendReads); // Only the GET snapshot; no outbound cancel/create.
    }

    [Fact]
    public async Task Checkout_regression_stays_locked_after_order_and_payment_reads()
    {
        await http.GetStringAsync("/Orders/7"); await http.GetStringAsync("/Checkout/Payment?orderId=7");
        var html = await http.GetStringAsync("/Checkout");
        Assert.Contains("data-order-creation-enabled=\"false\"", html);
        var csrf = WebUtility.HtmlDecode(Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Assert.NotEmpty(csrf);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Checkout?handler=Create")
        { Content = JsonContent.Create(new { operation = Guid.NewGuid() }) };
        request.Headers.Add("RequestVerificationToken", csrf);
        using var response = await http.SendAsync(request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(2, backendReads);
    }

    public async Task DisposeAsync()
    {
        http?.Dispose();
        if (app is not null) { await app.StopAsync(); await app.DisposeAsync(); }
    }
}
