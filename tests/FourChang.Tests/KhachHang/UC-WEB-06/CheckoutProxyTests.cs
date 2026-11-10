using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Checkout = FourChang.Web.Pages.Checkout.IndexModel;

namespace FourChang.Tests;

// Pure HTTP stub: never starts TestApi, SQL Server, bootstrap or a real backend.
[Trait("Safety", "NoDatabase")]
public sealed class CheckoutProxyTests
{
    private static readonly IDataProtectionProvider Protection = new EphemeralDataProtectionProvider();
    private static string Proof(long owner) => Protection.CreateProtector("FourChang.Checkout.Owner.v1").Protect(owner.ToString(System.Globalization.CultureInfo.InvariantCulture));
    private static readonly CreateCustomerOrderRequest Order = new("TAKEAWAY", null,
        "Khách thử", "0901234567", null, null, "", [new(1, 1, 100, 100, "", [])]);
    private static readonly Guid Operation = Guid.Parse("8059a874-243b-4ea2-a69b-1d09490602f2");
    private static readonly CustomerOrderReceipt Receipt = new(12, "ORD-TEST", "PENDING_PAYMENT", 45000, 0, 45000, null);

    private sealed class Stub(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public long CurrentCustomer { get; set; } = 41;
        public List<string?> ProfileTokens { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath == "/api/customer/profile")
            {
                ProfileTokens.Add(request.Headers.Authorization?.Parameter);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { maKhachHang = CurrentCustomer }) });
            }
            Calls++; return send(request);
        }
    }

    private static Checkout Page(Stub stub, object command, bool authenticated = true,
        string? flag = "true", IConfiguration? configuration = null)
    {
        var context = new DefaultHttpContext();
        if (command is Checkout.CheckoutCommand checkout && checkout.OwnerProof is null)
            command = checkout with { OwnerProof = Proof(41) };
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(command));
        if (authenticated) context.Request.Headers.Cookie = "fourchang_session=server-only-token";
        var client = new CafeApiClient(new HttpClient(stub) { BaseAddress = new Uri("http://backend.invalid") });
        // Existing success/error tests opt in explicitly; production has no enabled default.
        configuration ??= new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Checkout:OrderCreationEnabled"] = flag, ["ASPNETCORE_ENVIRONMENT"] = "Development" }).Build();
        return new Checkout(client, configuration, Protection) { PageContext = new PageContext { HttpContext = context } };
    }

    [Theory]
    [InlineData("text/plain", "not json")]
    [InlineData("application/json", "{")]
    public async Task QuoteRejectsInvalidMediaOrJsonWithoutCallingBackend(string contentType, string body)
    {
        var stub = new Stub(_ => throw new InvalidOperationException("Must not call Backend"));
        var page = Page(stub, Order);
        page.Request.ContentType = contentType;
        page.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        Assert.IsType<BadRequestObjectResult>(await page.OnPostQuoteAsync(default));
        Assert.Equal(0, stub.Calls);
    }

    [Theory]
    [InlineData("text/plain", "not json")]
    [InlineData("application/json", "{")]
    public async Task CreateRejectsInvalidMediaOrJsonWithoutCallingBackend(string contentType, string body)
    {
        var stub = new Stub(_ => throw new InvalidOperationException("Must not call Backend"));
        var command = new Checkout.CheckoutCommand(Operation, 45000, Order);
        var page = Page(stub, command);
        page.Request.ContentType = contentType;
        page.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        Assert.IsType<BadRequestObjectResult>(await page.OnPostCreateAsync(default));
        Assert.Equal(0, stub.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("Development")]
    [InlineData("1")]
    public async Task DisabledOrInvalidFlagRejectsManualPostWithoutBackendRequest(string? flag)
    {
        var stub = new Stub(_ => throw new InvalidOperationException("Must not send"));
        var page = Page(stub, new Checkout.CheckoutCommand(Operation, 45000, Order), flag: flag);
        Assert.False(page.OrderCreationEnabled);
        var result = Assert.IsType<ObjectResult>(await page.OnPostCreateAsync(default));
        Assert.Equal(503, result.StatusCode);
        Assert.Contains("CHECKOUT_DISABLED", JsonSerializer.Serialize(result.Value));
        Assert.Equal(0, stub.Calls);
    }

    [Fact]
    public async Task FlagIsRecheckedAtPostAfterPageWasRenderedEnabled()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Checkout:OrderCreationEnabled"] = "true" }).Build();
        var stub = new Stub(_ => throw new InvalidOperationException("Must not send"));
        var page = Page(stub, new Checkout.CheckoutCommand(Operation, 45000, Order), configuration: config);
        Assert.True(page.OrderCreationEnabled);
        await page.OnGetAsync();
        config["Checkout:OrderCreationEnabled"] = "false";
        var result = Assert.IsType<ObjectResult>(await page.OnPostCreateAsync(default));
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(0, stub.Calls);
    }

    [Theory]
    [InlineData(201)]
    [InlineData(200)]
    public async Task CreateAndReplayForwardSameKeyPayloadAndServerOnlyToken(int status)
    {
        var bodies = new List<string>();
        var stub = new Stub(async request =>
        {
            Assert.Equal("/api/customer/orders", request.RequestUri!.AbsolutePath);
            Assert.Equal(Operation.ToString("D"), Assert.Single(request.Headers.GetValues("Idempotency-Key")));
            Assert.Equal("45000", Assert.Single(request.Headers.GetValues("X-Checkout-Accepted-Total")));
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("server-only-token", request.Headers.Authorization.Parameter);
            bodies.Add(await request.Content!.ReadAsStringAsync());
            return new HttpResponseMessage((HttpStatusCode)status) { Content = JsonContent.Create(Receipt) };
        });
        var command = new Checkout.CheckoutCommand(Operation, 45000, Order);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var result = Assert.IsType<ObjectResult>(await Page(stub, command).OnPostCreateAsync(default));
            Assert.Equal(status, result.StatusCode);
            Assert.Equal(Receipt, Assert.IsType<CustomerOrderReceipt>(result.Value));
            Assert.DoesNotContain("server-only-token", JsonSerializer.Serialize(result.Value));
        }
        Assert.Equal(bodies[0], bodies[1]);
    }

    [Fact]
    public async Task MissingCookieDoesNotCallBackend()
    {
        var stub = new Stub(_ => throw new InvalidOperationException("Must not send"));
        var result = Assert.IsType<ObjectResult>(await Page(stub, new Checkout.CheckoutCommand(Operation, 45000, Order), false).OnPostCreateAsync(default));
        Assert.Equal(401, result.StatusCode);
        Assert.Equal(0, stub.Calls);
    }

    [Theory]
    [InlineData("DELIVERY", "Khách", "0901234567")]
    [InlineData("TAKEAWAY", "", "0901234567")]
    [InlineData("TAKEAWAY", "Khách", "abc")]
    [InlineData("TAKEAWAY", "Khách", "0901234567\n")]
    public async Task InvalidFulfillmentOrContactDoesNotCallBackend(string mode, string name, string phone)
    {
        var stub = new Stub(_ => throw new InvalidOperationException("Must not send"));
        var command = new Checkout.CheckoutCommand(Operation, 45000, Order with { HinhThucNhanMon = mode, TenNguoiNhan = name, DienThoaiNguoiNhan = phone });
        Assert.IsType<BadRequestObjectResult>(await Page(stub, command).OnPostCreateAsync(default));
        Assert.Equal(0, stub.Calls);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(409)]
    public async Task BackendErrorsPreserveStatusAndMessage(int status)
    {
        var stub = new Stub(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
        { Content = JsonContent.Create(new { message = "Backend từ chối", code = "CHECKOUT_PRICE_CHANGED" }) }));
        var result = Assert.IsType<ObjectResult>(await Page(stub, new Checkout.CheckoutCommand(Operation, 45000, Order)).OnPostCreateAsync(default));
        Assert.Equal(status, result.StatusCode);
        var body = JsonSerializer.Serialize(result.Value);
        Assert.Contains("CHECKOUT_PRICE_CHANGED", body);
    }

    [Theory]
    [InlineData("network", 503)]
    [InlineData("timeout", 504)]
    [InlineData("invalid-json", 502)]
    [InlineData("invalid-receipt", 502)]
    public async Task UnknownResultsAreNotReportedAsSuccess(string failure, int expectedStatus)
    {
        var stub = new Stub(_ => failure switch
        {
            "network" => throw new HttpRequestException("offline"),
            "timeout" => throw new TaskCanceledException("timeout"),
            "invalid-json" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("not json", Encoding.UTF8, "application/json") }),
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(Receipt with { TongCong = 99000 }) })
        });
        var result = Assert.IsType<ObjectResult>(await Page(stub, new Checkout.CheckoutCommand(Operation, 45000, Order)).OnPostCreateAsync(default));
        Assert.Equal(expectedStatus, result.StatusCode);
    }

    [Fact]
    public async Task CustomerBCannotSubmitCustomerAProofEvenFromStaleTab()
    {
        var stub = new Stub(_ => throw new InvalidOperationException("Must not create")) { CurrentCustomer = 42 };
        var command = new Checkout.CheckoutCommand(Operation, 45000, Order, Proof(41));
        var page = Page(stub, command);
        page.Request.Headers.Cookie = "fourchang_session=customer-B-token";
        var result = Assert.IsType<ObjectResult>(await page.OnPostCreateAsync(default));
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("CHECKOUT_OWNER_MISMATCH", JsonSerializer.Serialize(result.Value));
        Assert.Equal(0, stub.Calls);
        Assert.Equal("customer-B-token", Assert.Single(stub.ProfileTokens));
    }

    [Fact]
    public async Task MockCommitThenLostResponseReplaysSameBackendKeyBodyTotalAndReceipt()
    {
        string? committedBody = null;
        var creations = 0;
        var calls = 0;
        var receipt = Receipt with { HetHanLuc = new DateTime(2026, 11, 7, 7, 13, 19, DateTimeKind.Utc) };
        var stub = new Stub(async request =>
        {
            Assert.Equal(Operation.ToString("D"), Assert.Single(request.Headers.GetValues("Idempotency-Key")));
            Assert.Equal("45000", Assert.Single(request.Headers.GetValues("X-Checkout-Accepted-Total")));
            Assert.Equal("server-only-token", request.Headers.Authorization!.Parameter);
            var body = await request.Content!.ReadAsStringAsync();
            if (++calls == 1)
            {
                committedBody = body;
                creations++;
                throw new TaskCanceledException("Mock commit completed; response lost.");
            }
            Assert.Equal(committedBody, body);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(receipt) };
        });
        var command = new Checkout.CheckoutCommand(Operation, 45000, Order, Proof(41));
        Assert.Equal(504, Assert.IsType<ObjectResult>(await Page(stub, command).OnPostCreateAsync(default)).StatusCode);
        var replay = Assert.IsType<ObjectResult>(await Page(stub, command).OnPostCreateAsync(default));
        Assert.Equal(200, replay.StatusCode);
        Assert.Equal(receipt, Assert.IsType<CustomerOrderReceipt>(replay.Value));
        Assert.Equal(1, creations);
        Assert.Equal(2, stub.Calls);
    }

    [Theory]
    [InlineData(502, "text/html", "<html>gateway failed</html>")]
    [InlineData(503, "text/plain", "")]
    [InlineData(503, "application/json", "")]
    [InlineData(502, "application/octet-stream", "unavailable")]
    public async Task NonJsonBackendErrorsKeepHttpStatusAndRemainControlled(int status, string media, string body)
    {
        var stub = new Stub(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
        { Content = new StringContent(body, Encoding.UTF8, media) }));
        var result = Assert.IsType<ObjectResult>(await Page(stub, new Checkout.CheckoutCommand(Operation, 45000, Order)).OnPostCreateAsync(default));
        Assert.Equal(status, result.StatusCode);
        Assert.DoesNotContain("<html>", JsonSerializer.Serialize(result.Value));
    }

    [Theory]
    [InlineData(null, 503)]
    [InlineData("false", 503)]
    [InlineData("true", 401)]
    public async Task RealRazorPipelineEnforcesCsrfAndServerFlagInDevelopment(string? flag, int expectedStatus)
    {
        var root = FourChang.Tests.Shared.TestApi.FindRoot();
        var webRoot = Path.Combine(root, "src", "Presentation", "FourChang.Web");
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var start = new System.Diagnostics.ProcessStartInfo("dotnet")
        { WorkingDirectory = webRoot, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(Path.Combine(webRoot, "bin", "Debug", "net8.0", "FourChang.Web.dll"));
        start.ArgumentList.Add("--urls");
        start.ArgumentList.Add($"http://127.0.0.1:{port}");
        // Even a mistakenly added outbound call can only reach a closed local port.
        start.Environment["FOURCHANG_API_URL"] = "http://127.0.0.1:1";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        // Override inherited configuration; missing case deliberately removes the variable.
        start.Environment.Remove("Checkout__OrderCreationEnabled");
        start.Environment.Remove("Checkout:OrderCreationEnabled");
        if (flag is not null) start.Environment["Checkout__OrderCreationEnabled"] = flag;
        using var process = System.Diagnostics.Process.Start(start)!;
        try
        {
            using var http = new HttpClient(new HttpClientHandler { UseCookies = true })
            { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(5) };
            string? html = null;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                Assert.False(process.HasExited, "Isolated Web process exited before ready.");
                try { html = await http.GetStringAsync("/Checkout"); break; }
                catch (HttpRequestException) { await Task.Delay(100); }
            }
            Assert.NotNull(html);
            Assert.Contains($"data-order-creation-enabled=\"{(flag == "true" ? "true" : "false")}\"", html);
            var banner = System.Text.RegularExpressions.Regex.Match(html!, "<p id=\"checkout-disabled\"[^>]*>").Value;
            Assert.NotEmpty(banner);
            Assert.Equal(flag == "true", banner.Contains("hidden", StringComparison.Ordinal));
            var button = System.Text.RegularExpressions.Regex.Match(html!, "<button[^>]*id=\"checkout-proceed\"[^>]*>").Value;
            Assert.Contains("disabled", button);
            // The auto-merged legacy Web proxy must never bypass Razor ownership/CSRF/consent.
            using var legacy = await http.PostAsJsonAsync("/api/customer/orders", Order);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, legacy.StatusCode);
            Assert.Contains("CHECKOUT_HANDLER_REQUIRED", await legacy.Content.ReadAsStringAsync());
            var command = new Checkout.CheckoutCommand(Operation, 45000, Order);
            using var missing = await http.PostAsJsonAsync("/Checkout?handler=Create", command);
            Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
            var token = System.Text.RegularExpressions.Regex.Match(html!, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/Checkout?handler=Create") { Content = JsonContent.Create(command) };
            request.Headers.Add("RequestVerificationToken", token);
            using var anonymous = await http.SendAsync(request);
            Assert.Equal(expectedStatus, (int)anonymous.StatusCode);
            if (expectedStatus == 503)
                Assert.Contains("CHECKOUT_DISABLED", await anonymous.Content.ReadAsStringAsync());
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
        }
    }
}
