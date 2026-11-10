using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FourChang.Presentation.Shared.ApiClients;
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

// Razor -> HTTP client -> in-memory backend. No SQL, API Program, external HTTP,
// bank callback, checkout Create, inventory mutation or production flag change.
[Trait("Safety", "NoDatabase")]
public sealed class CustomerOrderCancelWebEnabledHttpTests : IAsyncLifetime
{
    private WebApplication app = null!;
    private HttpClient browser = null!;
    private int cancelPosts;
    private string orderStatus = "PENDING_CONFIRMATION";
    private bool rejectAsStale;
    private string? sentReason;
    private string? sentVersion;
    private string? sentBearer;
    private Guid sentOperation;

    private sealed class FakeBackend(CustomerOrderCancelWebEnabledHttpTests test) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri?.AbsolutePath;
            if (request.Method == HttpMethod.Get)
            {
                Assert.Equal("/api/customer/orders/7", path);
                Assert.Equal("customer-a", request.Headers.Authorization?.Parameter);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(OrderReadTests.Snapshot(test.orderStatus) with
                    { Version = Convert.ToBase64String(new byte[8]) })
                };
            }

            // Unexpected checkout/payment writes fail immediately, even in this mock.
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/customer/orders/7/cancel", path);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            test.sentBearer = request.Headers.Authorization?.Parameter;
            Assert.Equal("customer-a", test.sentBearer);
            var key = Assert.Single(request.Headers.GetValues("Idempotency-Key"));
            Assert.True(Guid.TryParse(key, out var operation) && operation != Guid.Empty);
            test.sentOperation = operation;

            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            test.sentReason = body.RootElement.GetProperty("reason").GetString();
            test.sentVersion = body.RootElement.GetProperty("expectedVersion").GetString();
            test.cancelPosts++;

            if (test.rejectAsStale)
                return new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = JsonContent.Create(new
                    {
                        code = "VERSION_CONFLICT",
                        message = "Đơn đã thay đổi; tải lại trạng thái."
                    })
                };

            test.orderStatus = "CANCELLED"; // Fake snapshot only; no SQL effects.
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    maDonHang = 7L,
                    trangThai = "CANCELLED",
                    soTienDeNghiHoan = 0m,
                    thongBao = "Đã ghi nhận hủy đơn thử nghiệm."
                })
            };
        }
    }

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(FourChang.Web.Pages.Orders.DetailsModel).Assembly.GetName().Name,
            ContentRootPath = Path.Combine(TestApi.FindRoot(), "src", "Presentation", "FourChang.Web"),
            EnvironmentName = "OfflineMockTests"
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Checkout:OrderCreationEnabled"] = "false",
            ["Orders:CustomerCancellationWebEnabled"] = "false"
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddRazorPages();
        builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        builder.Services.AddSingleton<IPaymentQrSource, UnavailablePaymentQrSource>();
        builder.Services.AddHttpClient<CafeApiClient>(client => client.BaseAddress = new Uri("http://mock.invalid"))
            .ConfigurePrimaryHttpMessageHandler(() => new FakeBackend(this));
        app = builder.Build();
        app.MapRazorPages();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        browser = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(address)
        };
        browser.DefaultRequestHeaders.Add("Cookie", "fourchang_session=customer-a");
    }

    private static string Hidden(string html, string name)
    {
        var match = Regex.Match(WebUtility.HtmlDecode(html),
            "name=\"" + Regex.Escape(name) + "\"[^>]*?value=\"([^\"]+)\"");
        Assert.True(match.Success, $"Missing hidden input: {name}");
        return match.Groups[1].Value;
    }

    private static Dictionary<string, string> Form(string html) => new()
    {
        ["orderId"] = Hidden(html, "orderId"),
        ["reason"] = "Khách đổi ý",
        ["expectedVersion"] = Hidden(html, "expectedVersion"),
        ["operationId"] = Hidden(html, "operationId"),
        ["__RequestVerificationToken"] = Hidden(html, "__RequestVerificationToken")
    };

    private Task<HttpResponseMessage> PostAsync(Dictionary<string, string> fields) =>
        browser.PostAsync("/Orders/7?handler=Cancel", new FormUrlEncodedContent(fields));

    [Fact]
    public async Task Enabled_web_cancel_sends_exactly_one_authenticated_backend_post_and_redirects()
    {
        app.Configuration["Orders:CustomerCancellationWebEnabled"] = "true"; // Fixture only.
        var html = await browser.GetStringAsync("/Orders/7");
        Assert.Contains("Xác nhận hủy đơn", html);
        var fields = Form(html);
        var expectedOperation = Guid.Parse(fields["operationId"]);

        using var response = await PostAsync(fields);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(1, cancelPosts);
        Assert.Equal("customer-a", sentBearer);
        Assert.Equal(fields["reason"], sentReason);
        Assert.Equal(fields["expectedVersion"], sentVersion);
        Assert.Equal(expectedOperation, sentOperation);
        Assert.Equal("CANCELLED", orderStatus);
        Assert.NotNull(response.Headers.Location);

        var after = WebUtility.HtmlDecode(await browser.GetStringAsync(response.Headers.Location!.ToString()));
        Assert.Contains("Đã ghi nhận hủy đơn thử nghiệm.", after);
        Assert.Contains("CANCELLED", after);
        Assert.DoesNotContain("name=\"reason\"", after);
        Assert.Equal(1, cancelPosts); // Following redirect is GET only.
    }

    [Fact]
    public async Task Enabled_web_cancel_missing_csrf_rejected_before_backend()
    {
        app.Configuration["Orders:CustomerCancellationWebEnabled"] = "true";
        var fields = Form(await browser.GetStringAsync("/Orders/7"));
        fields.Remove("__RequestVerificationToken");

        using var response = await PostAsync(fields);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, cancelPosts);
        Assert.Equal("PENDING_CONFIRMATION", orderStatus);
    }

    [Fact]
    public async Task Enabled_web_cancel_stale_version_returns_409_without_automatic_retry()
    {
        app.Configuration["Orders:CustomerCancellationWebEnabled"] = "true";
        rejectAsStale = true;
        var fields = Form(await browser.GetStringAsync("/Orders/7"));

        using var response = await PostAsync(fields);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Đơn đã thay đổi", html);
        Assert.Equal(1, cancelPosts);
        Assert.Equal("PENDING_CONFIRMATION", orderStatus);
    }

    [Theory]
    [InlineData("expectedVersion")]
    [InlineData("operationId")]
    public async Task Enabled_web_cancel_missing_required_command_part_never_calls_backend(string field)
    {
        app.Configuration["Orders:CustomerCancellationWebEnabled"] = "true";
        var fields = Form(await browser.GetStringAsync("/Orders/7"));
        fields.Remove(field);

        using var response = await PostAsync(fields);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, cancelPosts);
    }

    [Fact]
    public async Task Disabled_web_cancel_never_calls_backend_even_with_valid_csrf()
    {
        var fields = Form(await browser.GetStringAsync("/Orders/7"));
        using var response = await PostAsync(fields);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(0, cancelPosts);
    }

    public async Task DisposeAsync()
    {
        browser?.Dispose();
        if (app is not null)
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
