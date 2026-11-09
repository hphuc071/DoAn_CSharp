using Microsoft.Playwright;

namespace FourChang.Tests;

public sealed partial class CheckoutBrowserTests
{
    [Fact]
    public async Task Unavailable_fulfillment_tabs_do_not_create_or_persist_a_new_operation()
    {
        var page = await Open(true);
        await Confirm(page);
        foreach (var mode in new[] { "dinein", "delivery" })
        {
            await page.Locator($"[data-tab='{mode}']").ClickAsync();
            await Assertions.Expect(page.Locator("#checkout-proceed")).ToBeDisabledAsync();
            Assert.Contains("đang khóa", await page.Locator("#" + mode).InnerTextAsync());
            Assert.Null(await page.EvaluateAsync<string?>("localStorage.getItem('" + IntentKey + "')"));
        }
        Assert.Empty(bodies);
        await Assertions.Expect(page.Locator("#delivery input").First).ToBeDisabledAsync();
        await page.Locator("[data-tab='takeaway']").ClickAsync();
        await Confirm(page); // Switching back must preserve usable TAKEAWAY inputs and pricing.
    }

    [Fact]
    public async Task Switching_to_unavailable_fulfillment_preserves_uncertain_original_command()
    {
        create = route => route.AbortAsync("failed");
        var page = await Open(true); await Confirm(page);
        await page.Locator("#checkout-proceed").ClickAsync(); await RetryReady(page);
        var pending = await Pending(page);
        var cart = await page.EvaluateAsync<string>("localStorage.getItem('" + CartKey + "')");
        foreach (var mode in new[] { "dinein", "delivery" })
        {
            await page.Locator($"[data-tab='{mode}']").ClickAsync();
            Assert.Equal(pending, await Pending(page));
            Assert.Equal(cart, await page.EvaluateAsync<string>("localStorage.getItem('" + CartKey + "')"));
        }
        Assert.Single(bodies); // No new GUID, no implicit retry and no cart cleanup.
    }
}
