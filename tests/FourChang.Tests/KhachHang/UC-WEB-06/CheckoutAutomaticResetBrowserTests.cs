using System.Text.Json;
using Microsoft.Playwright;

namespace FourChang.Tests;

public sealed partial class CheckoutBrowserTests
{
    /// <summary>Thanh toán được xác minh tự mở lại form cho đơn mới, giữ món vừa chọn, không tự POST thêm đơn.</summary>
    [Fact, Trait("Feature", "AutomaticCheckoutSession")]
    public async Task Verified_payment_automatically_resets_form_and_preserves_next_cart()
    {
        var paid = false;
        await context.RouteAsync("**/Checkout/Payment?handler=State*", route => route.FulfillAsync(new()
        {
            ContentType = "application/json",
            Body = JsonSerializer.Serialize(new { orderId = 12, paid, status = paid ? "PENDING_CONFIRMATION" : "PENDING_PAYMENT" })
        }));
        var page = await Open(true);
        var nextCart = await page.EvaluateAsync<string>("localStorage.getItem('fourchang.cart-draft.v1')");
        await Confirm(page);
        await page.Locator("#checkout-proceed").ClickAsync();
        await Assertions.Expect(page.Locator("#checkout-receipt")).ToBeVisibleAsync();
        await page.EvaluateAsync("raw => localStorage.setItem('fourchang.cart-draft.v1', raw)", nextCart);
        await page.AddScriptTagAsync(new() { Content = await File.ReadAllTextAsync(Path.Combine(WebRoot, "wwwroot/js/checkout-session.js")) });
        await page.EvaluateAsync("FourChangCheckoutSession.check()");
        await Assertions.Expect(page.Locator("#checkout-receipt")).ToBeVisibleAsync();

        paid = true;
        await page.EvaluateAsync("FourChangCheckoutSession.check()");
        await Assertions.Expect(page.Locator("#checkout-receipt")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#takeaway-name")).ToBeEnabledAsync();
        await Assertions.Expect(page.Locator("#takeaway-name")).ToHaveValueAsync("");
        await Assertions.Expect(page.Locator("#checkout-confirmed")).Not.ToBeCheckedAsync();
        Assert.Equal(nextCart, await page.EvaluateAsync<string>("localStorage.getItem('fourchang.cart-draft.v1')"));
        Assert.Null(await page.EvaluateAsync<string?>("localStorage.getItem('fourchang.checkout-intent.v1')"));
        Assert.Single(bodies);
    }
}
