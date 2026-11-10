using Microsoft.Playwright;

namespace FourChang.Tests;

public sealed partial class CheckoutBrowserTests
{
    [Fact]
    public async Task Disabled_checkout_explains_operator_flag_instead_of_demanding_login()
    {
        var page = await Open(true, creationEnabled: false);
        await Assertions.Expect(page.Locator("#checkout-disabled")).ToContainTextAsync("theo cấu hình");
        await Assertions.Expect(page.Locator("#checkout-order-status")).ToContainTextAsync("Tạo đơn đang tắt theo cấu hình");
        await Assertions.Expect(page.Locator("#checkout-login")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#checkout-proceed")).ToBeDisabledAsync();
        Assert.Empty(bodies);
    }

    [Theory]
    [InlineData("dinein")]
    [InlineData("delivery")]
    public async Task Unavailable_fulfillment_tab_hides_takeaway_consent(string mode)
    {
        var page = await Open(true);
        await Assertions.Expect(page.Locator("#checkout-confirmed")).ToBeVisibleAsync();
        await page.Locator($"[data-tab='{mode}']").ClickAsync();
        await Assertions.Expect(page.Locator("#checkout-confirmed")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#checkout-proceed")).ToBeDisabledAsync();
        await page.Locator("[data-tab='takeaway']").ClickAsync();
        await Assertions.Expect(page.Locator("#checkout-confirmed")).ToBeVisibleAsync();
    }
}
