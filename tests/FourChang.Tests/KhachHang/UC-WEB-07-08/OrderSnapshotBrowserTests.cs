using FourChang.Tests.Shared;
using Microsoft.Playwright;

namespace FourChang.Tests;

// All URLs mocked; tests the real recovery script with a minimal order DOM, not a bank/API server.
[Trait("Safety", "NoDatabase")]
public sealed class OrderSnapshotBrowserTests : IAsyncLifetime
{
    private IPlaywright playwright = null!;
    private IBrowser browser = null!;
    private IBrowserContext context = null!;
    private int reads;
    public async Task InitializeAsync()
    {
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = true });
        context = await browser.NewContextAsync();
        var script = await File.ReadAllTextAsync(Path.Combine(TestApi.FindRoot(), "src", "Presentation", "FourChang.Web", "wwwroot", "js", "order-snapshot.js"));
        await context.RouteAsync("**/*", async route => {
            Assert.Equal("GET", route.Request.Method);
            reads++;
            await route.FulfillAsync(new() { ContentType = "text/html", Body = $"""
                <p id="version">{reads}</p><p id="order-snapshot-connection" hidden></p>
                <div data-payment-qr>Validated QR fixture</div><a data-online-only href="/pdf">PDF</a>
                <div data-order-private>Customer snapshot</div>
                <script>{script}</script>
                """ });
        });
    }
    [Fact]
    public async Task Offline_hides_qr_blocks_download_and_reconnect_gets_full_new_document_without_touching_pending()
    {
        var page = await context.NewPageAsync();
        await page.GotoAsync("http://mock.invalid/Orders/7?ReturnPage=2");
        await page.EvaluateAsync("localStorage.setItem('fourchang.checkout-intent.v1', 'original-pending'); window.dispatchEvent(new Event('offline'))");
        await Assertions.Expect(page.Locator("#order-snapshot-connection")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("[data-payment-qr]")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("[data-order-private]")).ToBeHiddenAsync();
        // Force a real click despite aria-disabled to verify the script also rejects manual activation.
        await page.Locator("a").ClickAsync(new() { Force = true }); Assert.Equal(1, reads);
        await page.EvaluateAsync("window.dispatchEvent(new Event('online')); window.dispatchEvent(new Event('online'))");
        await Assertions.Expect(page.Locator("#version")).ToHaveTextAsync("2");
        Assert.Equal("http://mock.invalid/Orders/7?ReturnPage=2", page.Url);
        Assert.Equal("original-pending", await page.EvaluateAsync<string>("localStorage.getItem('fourchang.checkout-intent.v1')"));
        Assert.Equal(2, reads);
    }
    [Fact]
    public async Task Restored_bfcache_document_is_not_trusted_and_reloads_once()
    {
        var page = await context.NewPageAsync(); await page.GotoAsync("http://mock.invalid/Orders/7");
        await page.EvaluateAsync("window.dispatchEvent(new PageTransitionEvent('pageshow', {persisted:true}))");
        await Assertions.Expect(page.Locator("#version")).ToHaveTextAsync("2"); Assert.Equal(2, reads);
    }
    [Fact]
    public async Task Normal_online_signal_does_not_create_polling_or_write_requests()
    {
        var page = await context.NewPageAsync(); await page.GotoAsync("http://mock.invalid/Orders/7");
        await page.EvaluateAsync("window.dispatchEvent(new Event('online'))");
        Assert.Equal(1, reads); await Assertions.Expect(page.Locator("#version")).ToHaveTextAsync("1");
    }

    [Fact]
    public async Task Returning_to_hidden_tab_reloads_ownership_snapshot_instead_of_showing_old_account_data()
    {
        var page = await context.NewPageAsync(); await page.GotoAsync("http://mock.invalid/Orders/7");
        await page.EvaluateAsync("Object.defineProperty(document, 'hidden', {configurable:true, get:()=>true}); document.dispatchEvent(new Event('visibilitychange'))");
        await page.EvaluateAsync("Object.defineProperty(document, 'hidden', {configurable:true, get:()=>false}); document.dispatchEvent(new Event('visibilitychange'))");
        await Assertions.Expect(page.Locator("#version")).ToHaveTextAsync("2"); Assert.Equal(2, reads);
    }
    public async Task DisposeAsync()
    {
        if (context is not null) await context.DisposeAsync();
        if (browser is not null) await browser.DisposeAsync();
        playwright?.Dispose();
    }
}
