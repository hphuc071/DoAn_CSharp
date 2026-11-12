using FourChang.Tests.Shared;
using Microsoft.Playwright;

namespace FourChang.Tests;

/// <summary>Kiểm tra kết thúc phiên cục bộ: chỉ thu đủ đã xác minh mới được dọn; không sửa giỏ hoặc gửi tiền.</summary>
public sealed class CheckoutSessionBrowserTests
{
    /// <summary>Đã trả tiền tự bỏ phiên; chưa rõ kết quả, mất mạng, sai đơn hoặc không đủ quyền đều giữ nguyên.</summary>
    [Theory, Trait("Feature", "AutomaticCheckoutSession"), Trait("Safety", "NoDatabase")]
    [InlineData(true, 12, 200, true)]
    [InlineData(false, 12, 200, false)]
    [InlineData(true, 13, 200, false)]
    [InlineData(true, 12, 401, false)]
    [InlineData(true, 12, 503, false)]
    public async Task ChiDonDaThanhToanMoiTuKetThuc(bool paid, int returnedOrder, int status, bool removed)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = true });
        var page = await browser.NewPageAsync();
        await page.RouteAsync("**/*", async route =>
        {
            Assert.Equal("GET", route.Request.Method);
            if (route.Request.Url.Contains("handler=State"))
                await route.FulfillAsync(new() { Status = status, ContentType = "application/json", Body = $"{{\"orderId\":{returnedOrder},\"paid\":{paid.ToString().ToLowerInvariant()},\"status\":\"PENDING_CONFIRMATION\"}}" });
            else await route.FulfillAsync(new() { ContentType = "text/html", Body = "<h1>Checkout</h1>" });
        });
        await page.GotoAsync("http://localhost:18797/Checkout");
        await page.EvaluateAsync("""
        () => {
            localStorage.setItem('fourchang.checkout-intent.v1', JSON.stringify({version:1, command:{operation:'old-operation'}, receipt:{maDonHang:12}}));
            localStorage.setItem('fourchang.cart-draft.v1', 'new-cart-must-stay');
            window.resetCount = 0; window.addEventListener('checkout:session-reset', () => window.resetCount++);
        }
        """);
        await page.AddScriptTagAsync(new() { Content = await File.ReadAllTextAsync(Path.Combine(TestApi.FindRoot(), "src/Presentation/FourChang.Web/wwwroot/js/checkout-session.js")) });
        await page.EvaluateAsync("FourChangCheckoutSession.check()");
        Assert.Equal(removed, await page.EvaluateAsync<bool>("localStorage.getItem('fourchang.checkout-intent.v1') === null"));
        Assert.Equal("new-cart-must-stay", await page.EvaluateAsync<string>("localStorage.getItem('fourchang.cart-draft.v1')"));
        Assert.Equal(removed ? 1 : 0, await page.EvaluateAsync<int>("window.resetCount"));
    }
}
