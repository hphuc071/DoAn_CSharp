using System.Text.Json;
using FourChang.Tests.Shared;
using Microsoft.Playwright;

namespace FourChang.Tests;

// Every URL is intercepted. No application server or database is started.
[Trait("Safety", "NoDatabase")]
public sealed partial class CheckoutBrowserTests : IAsyncLifetime
{
    private IPlaywright playwright = null!;
    private IBrowser browser = null!;
    private IBrowserContext context = null!;
    private readonly List<string> bodies = [];
    private readonly List<string> csrf = [];
    private Func<IRoute, Task> create = null!;
    private string html = null!;
    private static readonly string WebRoot = Path.Combine(TestApi.FindRoot(), "src", "Presentation", "FourChang.Web");
    private const string CartKey = "fourchang.cart-draft.v1";
    private const string IntentKey = "fourchang.checkout-intent.v1";
    private const string Receipt = """{"maDonHang":12,"maDonHienThi":"ORD-TEST","trangThai":"PENDING_PAYMENT","tienHang":45000,"phiGiaoHang":0,"tongCong":45000,"hetHanLuc":null}""";

    public async Task InitializeAsync()
    {
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = true });
        context = await browser.NewContextAsync();
        create = route => route.FulfillAsync(new() { Status = 201, ContentType = "application/json", Body = Receipt });
        html = await File.ReadAllTextAsync(Path.Combine(WebRoot, "Pages", "Checkout", "Index.cshtml"));
        html = html[html.IndexOf("<div class=\"breadcrumb\"", StringComparison.Ordinal)..];
        html = html[..html.IndexOf("@section Scripts", StringComparison.Ordinal)]
            .Replace("@Html.AntiForgeryToken()", "<input name=\"__RequestVerificationToken\" value=\"test-csrf\" />")
            .Replace("@(Model.OrderCreationEnabled ? \"true\" : \"false\")", "true")
            .Replace("@Model.CustomerId", "41").Replace("@Model.OwnerProof", "test-owner-proof-A");
        await Wire(context);
    }

    private async Task Wire(IBrowserContext target)
    {
        await target.RouteAsync("**/*", async route =>
        {
            var uri = new Uri(route.Request.Url);
            if (uri.AbsolutePath == "/Checkout" && uri.Query.Contains("handler=Create"))
            {
                bodies.Add(route.Request.PostData!);
                csrf.Add(route.Request.Headers.GetValueOrDefault("requestverificationtoken", ""));
                await create(route);
            }
            else if (uri.Query.Contains("handler=Variants"))
                await route.FulfillAsync(new() { ContentType = "application/json", Body = """[{"maCauHinhMon":1,"available":true,"tuyChonPhaChe":[{"phanTramDuong":100,"phanTramDa":100}],"toppings":[]}]""" });
            else if (uri.Query.Contains("handler=Quote"))
                await route.FulfillAsync(new() { ContentType = "application/json", Body = """{"tienHang":45000,"phiGiaoHang":0,"tongCong":45000}""" });
            else if (uri.AbsolutePath == "/Checkout" && route.Request.Method == "GET")
                await route.FulfillAsync(new() { ContentType = "text/html; charset=utf-8", Body = html });
            else await route.AbortAsync();
        });
    }

    private async Task<IPage> Open(bool seedCart = false, bool creationEnabled = true, string customerId = "41")
    {
        var page = await context.NewPageAsync();
        await page.GotoAsync("http://localhost:18791/Checkout");
        await page.EvaluateAsync("id => { const b=document.getElementById('checkout-proceed'); b.dataset.customerId=id; }", customerId);
        if (!creationEnabled)
            await page.EvaluateAsync("document.getElementById('checkout-proceed').dataset.orderCreationEnabled = 'false'");
        if (seedCart)
            await page.EvaluateAsync("""() => localStorage.setItem('fourchang.cart-draft.v1', JSON.stringify([{productId:1,variantId:1,quantity:1,name:'Trà',size:'M',listedPrice:45000,sugar:'100%',ice:'100%',topping:'Không'}]))""");
        await Mount(page);
        return page;
    }

    private static async Task Mount(IPage page)
    {
        foreach (var file in new[] { "site.js", "checkout-cart.js", "checkout-quote.js", "checkout-order.js" })
            await page.AddScriptTagAsync(new() { Content = await File.ReadAllTextAsync(Path.Combine(WebRoot, "wwwroot", "js", file)) });
        await page.EvaluateAsync("window.dispatchEvent(new Event('pageshow'))");
    }

    private static async Task Confirm(IPage page)
    {
        await page.WaitForFunctionAsync("!!window.fourchangCheckoutQuote");
        await page.Locator("#takeaway-name").FillAsync("Khách thử");
        await page.Locator("#takeaway-phone").FillAsync("0901234567");
        await page.Locator("#checkout-confirmed").CheckAsync();
        await Assertions.Expect(page.Locator("#checkout-proceed")).ToBeEnabledAsync();
    }

    [Fact]
    public async Task DisabledUiDoesNotSendOrCreateIntentEvenWhenButtonIsTamperedWith()
    {
        var page = await Open(true, creationEnabled: false);
        await page.WaitForFunctionAsync("!!window.fourchangCheckoutQuote");
        await Assertions.Expect(page.Locator("#checkout-disabled")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#checkout-proceed")).ToBeDisabledAsync();
        await page.EvaluateAsync("""
        () => {
            const button = document.getElementById('checkout-proceed');
            button.disabled = false;
            button.click();
        }
        """);
        Assert.Empty(bodies);
        Assert.Null(await page.EvaluateAsync<string?>("localStorage.getItem('" + IntentKey + "')"));
    }

    [Fact]
    public async Task DisabledFlagKeepsPendingGuidAndPayloadWithoutRetrying()
    {
        create = route => route.AbortAsync("failed");
        var page = await Open(true);
        await Confirm(page);
        await page.Locator("#checkout-proceed").ClickAsync();
        await Assertions.Expect(page.Locator("#checkout-proceed")).ToHaveTextAsync("Thử lại thao tác đang chờ kết quả");
        var pending = await page.EvaluateAsync<string>("localStorage.getItem('" + IntentKey + "')");
        var disabledPage = await Open(creationEnabled: false);
        await Assertions.Expect(disabledPage.Locator("#checkout-proceed")).ToBeDisabledAsync();
        await Assertions.Expect(disabledPage.Locator("#checkout-disabled")).ToBeVisibleAsync();
        Assert.Equal(pending, await disabledPage.EvaluateAsync<string>("localStorage.getItem('" + IntentKey + "')"));
        Assert.Single(bodies);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(409)]
    [InlineData(504)]
    [InlineData(0)]
    public async Task UncertainOrRejectedResponseReloadAndRetryKeepExactCommand(int firstStatus)
    {
        create = route => firstStatus == 0 ? route.AbortAsync("failed") : route.FulfillAsync(new()
        { Status = firstStatus, ContentType = "application/json", Body = """{"message":"Chưa xác nhận"}""" });
        var page = await Open(true);
        await Confirm(page);
        await page.Locator("#checkout-proceed").ClickAsync();
        await Assertions.Expect(page.Locator("#checkout-proceed")).ToHaveTextAsync("Thử lại thao tác đang chờ kết quả");
        Assert.NotNull(await page.EvaluateAsync<string?>("localStorage.getItem('" + CartKey + "')"));
        var pending = await page.EvaluateAsync<string>("localStorage.getItem('" + IntentKey + "')");
        await page.ReloadAsync();
        await Mount(page);
        await Assertions.Expect(page.Locator("#takeaway-name")).ToBeDisabledAsync();
        await Assertions.Expect(page.Locator("#takeaway-name")).ToHaveValueAsync("Khách thử");
        await Assertions.Expect(page.Locator("#checkout-pending-details")).ToContainTextAsync("45.000đ");
        create = route => route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = Receipt });
        await page.Locator("#checkout-proceed").ClickAsync();
        await Assertions.Expect(page.Locator("#checkout-receipt")).ToBeVisibleAsync();
        Assert.Equal(2, bodies.Count);
        Assert.Equal(bodies[0], bodies[1]);
        Assert.All(csrf, value => Assert.Equal("test-csrf", value));
        Assert.Equal(JsonDocument.Parse(pending).RootElement.GetProperty("command").GetProperty("operation").GetString(),
            JsonDocument.Parse(bodies[1]).RootElement.GetProperty("operation").GetString());
        Assert.Null(await page.EvaluateAsync<string?>("localStorage.getItem('" + CartKey + "')"));
        await Assertions.Expect(page.Locator("#receipt-code")).ToHaveTextAsync("ORD-TEST");
    }

    [Fact]
    public async Task TwoTabsCannotSubmitDifferentOperationsWhileFirstIsInFlight()
    {
        var release = new TaskCompletionSource();
        create = async route => { await release.Task; await route.FulfillAsync(new() { Status = 201, ContentType = "application/json", Body = Receipt }); };
        var first = await Open(true);
        var second = await Open();
        await Confirm(first);
        await Confirm(second);
        await first.Locator("#checkout-proceed").ClickAsync();
        await second.WaitForFunctionAsync("!!localStorage.getItem('fourchang.checkout-intent.v1')");
        await Assertions.Expect(second.Locator("#checkout-pending-details")).ToBeVisibleAsync();
        await second.Locator("#checkout-proceed").ClickAsync();
        await Assertions.Expect(second.Locator("#checkout-order-status")).ToContainTextAsync("Một tab khác");
        Assert.Single(bodies);
        // A changed cart must survive success for the original cart.
        await second.EvaluateAsync("localStorage.setItem('fourchang.cart-draft.v1', '[]')");
        release.SetResult();
        await Assertions.Expect(first.Locator("#checkout-receipt")).ToBeVisibleAsync();
        await Assertions.Expect(second.Locator("#checkout-receipt")).ToBeVisibleAsync();
        await Assertions.Expect(second.Locator("#checkout-proceed")).ToBeDisabledAsync();
        Assert.Single(bodies);
        Assert.Equal("[]", await first.EvaluateAsync<string>("localStorage.getItem('" + CartKey + "')"));
    }

    [Fact]
    public async Task PriceChangedRequiresNewQuoteAndExplicitConfirmation()
    {
        create = route => route.FulfillAsync(new() { Status = 409, ContentType = "application/json",
            Body = """{"code":"CHECKOUT_PRICE_CHANGED","message":"Giá đã thay đổi"}""" });
        var page = await Open(true);
        await Confirm(page);
        await page.Locator("#checkout-proceed").ClickAsync();
        await Assertions.Expect(page.Locator("#checkout-order-status")).ToContainTextAsync("Giá đã thay đổi");
        Assert.Null(await page.EvaluateAsync<string?>("localStorage.getItem('" + IntentKey + "')"));
        await Assertions.Expect(page.Locator("#checkout-confirmed")).Not.ToBeCheckedAsync();
        await Assertions.Expect(page.Locator("#checkout-proceed")).ToBeDisabledAsync();
        Assert.NotNull(await page.EvaluateAsync<string?>("localStorage.getItem('" + CartKey + "')"));
    }

    [Fact]
    public async Task InvalidReceiptDoesNotClearCartOrPermitNewOperation()
    {
        create = route => route.FulfillAsync(new() { Status = 201, ContentType = "application/json", Body = Receipt.Replace("PENDING_PAYMENT", "PAID") });
        var page = await Open(true);
        await Confirm(page);
        await page.Locator("#checkout-proceed").ClickAsync();
        await Assertions.Expect(page.Locator("#checkout-order-status")).ToContainTextAsync("Biên nhận không hợp lệ");
        Assert.NotNull(await page.EvaluateAsync<string?>("localStorage.getItem('" + CartKey + "')"));
        await Assertions.Expect(page.Locator("#checkout-receipt")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#takeaway-name")).ToBeDisabledAsync();
    }

    [Fact]
    public async Task FailedDurableWritePreventsNetworkRequest()
    {
        var page = await Open(true);
        await Confirm(page);
        await page.EvaluateAsync("() => { Storage.prototype.setItem = () => { throw new Error('Storage unavailable'); }; }");
        await page.Locator("#checkout-proceed").ClickAsync();
        await Assertions.Expect(page.Locator("#checkout-order-status")).ToContainTextAsync("Storage unavailable");
        Assert.Empty(bodies);
        Assert.NotNull(await page.EvaluateAsync<string?>("localStorage.getItem('" + CartKey + "')"));
    }

    [Fact]
    public async Task ClientTimeoutPreservesPendingOperation()
    {
        create = _ => Task.CompletedTask; // The fake endpoint never replies.
        var page = await Open(true);
        await Confirm(page);
        await page.EvaluateAsync("""
        () => {
            const timeout = AbortSignal.timeout.bind(AbortSignal);
            AbortSignal.timeout = () => timeout(100);
        }
        """);
        await page.Locator("#checkout-proceed").ClickAsync();
        await Assertions.Expect(page.Locator("#checkout-proceed")).ToHaveTextAsync("Thử lại thao tác đang chờ kết quả");
        Assert.NotNull(await page.EvaluateAsync<string?>("localStorage.getItem('" + IntentKey + "')"));
        Assert.NotNull(await page.EvaluateAsync<string?>("localStorage.getItem('" + CartKey + "')"));
        await Assertions.Expect(page.Locator("#checkout-new-order")).ToBeHiddenAsync();
    }

    [Fact]
    public async Task CustomerSwitchKeepsPendingAndPreventsReplayByB()
    {
        create = route => route.AbortAsync("failed");
        var a = await Open(true);
        await Confirm(a);
        await a.Locator("#checkout-proceed").ClickAsync();
        await Assertions.Expect(a.Locator("#checkout-proceed")).ToHaveTextAsync("Thử lại thao tác đang chờ kết quả");
        var pending = await a.EvaluateAsync<string>("localStorage.getItem('" + IntentKey + "')");
        var b = await Open(customerId: "42");
        await Assertions.Expect(b.Locator("#checkout-proceed")).ToBeDisabledAsync();
        await b.EvaluateAsync("() => { const b=document.getElementById('checkout-proceed'); b.disabled=false; b.click(); }");
        Assert.Single(bodies);
        Assert.Equal(pending, await b.EvaluateAsync<string>("localStorage.getItem('" + IntentKey + "')"));
        await Assertions.Expect(b.Locator("#takeaway-name")).ToHaveValueAsync("");
        await Assertions.Expect(b.Locator("#checkout-pending-details")).ToBeHiddenAsync();
    }

    [Theory]
    [InlineData(502, "text/html", "<html>bad gateway</html>")]
    [InlineData(503, "text/plain", "")]
    [InlineData(503, "application/json", "")]
    [InlineData(502, "application/octet-stream", "unavailable")]
    public async Task NonJsonGatewayResponseDoesNotReplacePendingOperation(int status, string media, string body)
    {
        create = route => route.FulfillAsync(new() { Status = status, ContentType = media, Body = body });
        var page = await Open(true);
        await Confirm(page);
        await page.Locator("#checkout-proceed").ClickAsync();
        await Assertions.Expect(page.Locator("#checkout-order-status")).ToContainTextAsync("HTTP " + status);
        var pending = await page.EvaluateAsync<string>("localStorage.getItem('" + IntentKey + "')");
        await page.Locator("#checkout-proceed").ClickAsync();
        await Assertions.Expect(page.Locator("#checkout-proceed")).ToHaveTextAsync("Thử lại thao tác đang chờ kết quả");
        Assert.Equal(2, bodies.Count);
        Assert.Equal(bodies[0], bodies[1]);
        Assert.Equal(pending, await page.EvaluateAsync<string>("localStorage.getItem('" + IntentKey + "')"));
        Assert.NotNull(await page.EvaluateAsync<string?>("localStorage.getItem('" + CartKey + "')"));
        await Assertions.Expect(page.Locator("#checkout-receipt")).ToBeHiddenAsync();
        create = route => route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = Receipt });
        await page.Locator("#checkout-proceed").ClickAsync();
        await Assertions.Expect(page.Locator("#checkout-receipt")).ToBeVisibleAsync();
        Assert.Equal(3, bodies.Count);
        Assert.Equal(bodies[0], bodies[2]);
        Assert.Null(await page.EvaluateAsync<string?>("localStorage.getItem('" + CartKey + "')"));
    }

    public async Task DisposeAsync()
    {
        if (context is not null) await context.DisposeAsync();
        if (browser is not null) await browser.DisposeAsync();
        playwright?.Dispose();
    }
}
