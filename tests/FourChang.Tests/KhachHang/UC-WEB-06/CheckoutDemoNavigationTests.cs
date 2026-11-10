using Microsoft.Playwright;

namespace FourChang.Tests;

// Offline browser fixture: all requests are intercepted, no database or bank connection.
public sealed partial class CheckoutBrowserTests
{
    [Fact]
    public async Task Disabled_checkout_demo_link_does_not_enable_order_creation()
    {
        var page = await Open(seedCart: true, creationEnabled: false);
        await Assertions.Expect(page.Locator("#checkout-demo-preview"))
            .ToHaveAttributeAsync("href", "/Checkout/DemoQr");
        await Assertions.Expect(page.Locator("#checkout-proceed")).ToBeDisabledAsync();
        Assert.Empty(bodies);
        Assert.Null(await page.EvaluateAsync<string?>(
            "localStorage.getItem('fourchang.checkout-intent.v1')"));
    }

    [Theory]
    [InlineData("dinein")]
    [InlineData("delivery")]
    public async Task Preview_navigation_never_unlocks_other_fulfillment_modes(string mode)
    {
        var page = await Open(seedCart: true, creationEnabled: false);
        await page.Locator($"[data-tab='{mode}']").ClickAsync();
        await Assertions.Expect(page.Locator("#checkout-proceed")).ToBeDisabledAsync();
        await Assertions.Expect(page.Locator("#checkout-demo-preview"))
            .ToHaveAttributeAsync("href", "/Checkout/DemoQr");
        Assert.Empty(bodies);
    }
}
