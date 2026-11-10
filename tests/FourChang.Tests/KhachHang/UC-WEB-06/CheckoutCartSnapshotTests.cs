using System.Text.Json;
using Microsoft.Playwright;

namespace FourChang.Tests;

public sealed partial class CheckoutBrowserTests
{
    [Fact]
    public async Task V2_multi_topping_draft_uses_server_ids_and_preserves_takeaway_consent_contract()
    {
        await context.RouteAsync("**/*handler=Variants*", r => r.FulfillAsync(new() { ContentType = "application/json", Body = """[{"maCauHinhMon":1,"available":true,"tuyChonPhaChe":[{"phanTramDuong":100,"phanTramDa":100}],"toppings":[{"maMonThem":2,"available":true,"gioiHanMoiLy":2},{"maMonThem":3,"available":true,"gioiHanMoiLy":2}]}]""" }));
        var page = await Open();
        await page.EvaluateAsync("""() => { localStorage.setItem('fourchang.cart-draft.v1',JSON.stringify([{draftVersion:2,productId:1,variantId:1,name:'Latte',size:'M',quantity:1,listedPrice:37000,sugar:'100%',ice:'100%',note:'keep',toppings:[{id:2,name:'Cheese',price:5000,portions:1},{id:3,name:'Pearls',price:3000,portions:1}]}])); window.dispatchEvent(new Event('checkout:refresh')); }""");
        await Confirm(page); await Assertions.Expect(page.Locator("#checkout-base-subtotal")).ToContainTextAsync("45.000");
        await page.Locator("#checkout-proceed").ClickAsync(); await page.WaitForFunctionAsync("document.querySelector('#checkout-receipt').hidden === false");
        using var body = JsonDocument.Parse(Assert.Single(bodies));
        Assert.Equal(45000, body.RootElement.GetProperty("acceptedTotal").GetInt32());
        var order = body.RootElement.GetProperty("order"); Assert.Equal("TAKEAWAY", order.GetProperty("hinhThucNhanMon").GetString());
        var tops = order.GetProperty("items")[0].GetProperty("toppings"); Assert.Equal(2, tops.GetArrayLength());
        Assert.Equal(2, tops[0].GetProperty("maMonThem").GetInt32()); Assert.Equal(3, tops[1].GetProperty("maMonThem").GetInt32());
        Assert.False(tops[0].TryGetProperty("price", out _));
    }
    [Fact]
    public async Task V2_invalid_or_stopped_topping_cannot_get_a_checkout_quote_or_create()
    {
        var page = await Open();
        await page.EvaluateAsync("""() => { localStorage.setItem('fourchang.cart-draft.v1',JSON.stringify([{draftVersion:2,productId:1,variantId:1,quantity:1,listedPrice:45000,sugar:'100%',ice:'100%',toppings:[{id:999,name:'stopped',price:0,portions:1}]}])); window.dispatchEvent(new Event('checkout:refresh')); }""");
        await Assertions.Expect(page.Locator("#checkout-price-warning")).ToContainTextAsync("không còn hiệu lực");
        Assert.Null(await page.EvaluateAsync<object?>("window.fourchangCheckoutQuote")); Assert.Empty(bodies);
    }

    [Fact]
    public async Task Same_tab_cart_change_invalidates_checkout_quote_and_consent()
    {
        var page = await Open(seedCart: true);
        await Confirm(page);
        await page.EvaluateAsync("""
            () => {
                localStorage.setItem('fourchang.cart-draft.v1', '[]');
                window.dispatchEvent(new Event('cart:changed'));
            }
            """);
        await Assertions.Expect(page.Locator("#checkout-proceed")).ToBeDisabledAsync();
        await Assertions.Expect(page.Locator("#checkout-confirmed")).Not.ToBeCheckedAsync();
        await Assertions.Expect(page.Locator("#checkout-cart-count")).ToContainTextAsync("trống");
        await Assertions.Expect(page.Locator("#checkout-total")).ToHaveTextAsync("Chưa thể báo giá");
        Assert.Null(await page.EvaluateAsync<object?>("window.fourchangCheckoutQuote"));
        Assert.Empty(bodies);
    }
}
