using System.Text.Json;
using Microsoft.Playwright;

namespace FourChang.Tests;

// All HTTP is routed to the existing in-memory fixture. Does not touch SQL or bank APIs.
public sealed partial class CheckoutBrowserTests
{
    private static async Task<string> ReadDemoQrScriptAsync(string name) => await File.ReadAllTextAsync(
        Path.Combine(WebRoot, "wwwroot", "js", name));

    [Fact]
    public async Task Demo_receives_fresh_backend_price_but_never_creates_order()
    {
        var page = await Open(seedCart: true, creationEnabled: false);
        await page.WaitForFunctionAsync("!!window.fourchangCheckoutQuote");
        await page.AddScriptTagAsync(new() { Content = await ReadDemoQrScriptAsync("demo-qr-navigation.js") });
       await page.EvaluateAsync("""
        () => {
        const a = document.querySelector('#checkout-demo-preview');
        a.addEventListener('click', e => e.preventDefault(), { capture: true });
        a.click();
    }
    """);

        var snapshot = await page.EvaluateAsync<string>(
            "sessionStorage.getItem('fourchang.demo-qr-cart.v1')");
        using (var json = JsonDocument.Parse(snapshot))
        {
            Assert.Equal("TAKEAWAY", json.RootElement.GetProperty("payload")
                .GetProperty("hinhThucNhanMon").GetString());
            Assert.False(json.RootElement.GetProperty("payload")
                .TryGetProperty("tenNguoiNhan", out var recipient) &&
                recipient.ValueKind == JsonValueKind.String);
        }
        await page.SetContentAsync("""
          <div id="demo-quote-antiforgery"><input name="__RequestVerificationToken" value="test-csrf" /></div>
          <span id="demo-cart-quoted-total"></span><p id="demo-cart-quote-status"></p>
        """);
        await page.AddScriptTagAsync(new() { Content = await ReadDemoQrScriptAsync("demo-qr-quote.js") });
        await Assertions.Expect(page.Locator("#demo-cart-quoted-total")).ToHaveTextAsync("45.000đ");
        await Assertions.Expect(page.Locator("#demo-cart-quote-status")).ToContainTextAsync("Backend vừa báo");
        Assert.Empty(bodies); // Backend Quote only; no Create or payment call.
    }

    [Fact]
    public async Task Demo_does_not_display_old_quote_if_cart_was_modified()
    {
        var page = await Open(seedCart: true, creationEnabled: false);
        await page.WaitForFunctionAsync("!!window.fourchangCheckoutQuote");
        await page.AddScriptTagAsync(new() { Content = await ReadDemoQrScriptAsync("demo-qr-navigation.js") });
await page.EvaluateAsync("""
    () => {
        const a = document.querySelector('#checkout-demo-preview');
        a.addEventListener('click', e => e.preventDefault(), { capture: true });
        a.click();
        localStorage.setItem('fourchang.cart-draft.v1', '[]');
    }
    """);
        await page.SetContentAsync("""
          <div id="demo-quote-antiforgery"><input name="__RequestVerificationToken" value="test-csrf" /></div>
          <span id="demo-cart-quoted-total"></span><p id="demo-cart-quote-status"></p>
        """);
        await page.AddScriptTagAsync(new() { Content = await ReadDemoQrScriptAsync("demo-qr-quote.js") });
        await Assertions.Expect(page.Locator("#demo-cart-quoted-total")).ToHaveTextAsync("Chưa có báo giá cho giỏ");
        Assert.Empty(bodies);
    }

    [Fact]
    public async Task Demo_clears_shown_price_when_cart_changes_after_quote()
    {
        var page = await Open(seedCart: true, creationEnabled: false);
        await page.WaitForFunctionAsync("!!window.fourchangCheckoutQuote");
        await page.AddScriptTagAsync(new() { Content = await ReadDemoQrScriptAsync("demo-qr-navigation.js") });
        await page.EvaluateAsync("""
            () => {
                const a = document.querySelector('#checkout-demo-preview');
                a.addEventListener('click', e => e.preventDefault(), { capture: true });
                a.click();
            }
            """);
        await page.SetContentAsync("""
            <div id="demo-quote-antiforgery"><input name="__RequestVerificationToken" value="test-csrf" /></div>
            <span id="demo-cart-quoted-total"></span><p id="demo-cart-quote-status"></p>
            """);
        await page.AddScriptTagAsync(new() { Content = await ReadDemoQrScriptAsync("demo-qr-quote.js") });
        await Assertions.Expect(page.Locator("#demo-cart-quoted-total")).ToHaveTextAsync("45.000đ");

        // storage is sent to other tabs, but synthesized here to test its handler.
        await page.EvaluateAsync("""
            () => {
                localStorage.setItem('fourchang.cart-draft.v1', '[]');
                window.dispatchEvent(new StorageEvent('storage', { key: 'fourchang.cart-draft.v1' }));
            }
            """);
        await Assertions.Expect(page.Locator("#demo-cart-quoted-total"))
            .ToHaveTextAsync("Chưa có báo giá cho giỏ");
        Assert.Empty(bodies); // No create, order or payment call.
    }
}
