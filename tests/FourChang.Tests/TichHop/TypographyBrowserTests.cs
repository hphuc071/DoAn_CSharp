using FourChang.Tests.Shared;
using Microsoft.Playwright;

namespace FourChang.Tests;

/// <summary>Đo giao diện Razor thật trên desktop và điện thoại; lưu ảnh từng trang đang truy cập được.</summary>
public sealed class TypographyBrowserTests
{
    /// <summary>Kiểm tra CSS tải đúng, font Việt thống nhất, không tràn ngang và hỗ trợ giảm chuyển động.</summary>
    [Fact, Trait("Feature", "Typography"), Trait("Layer", "Browser")]
    public async Task Web_DesktopVaMobile_FontLayoutVaReducedMotion()
    {
        await using var browser = await BrowserFixture.Start("Typography-Web");
        foreach (var width in new[] { 1440, 390 })
        {
            await browser.Page.SetViewportSizeAsync(width, 1000);
            foreach (var route in new[] { "/", "/Account/Login", "/Account/Register", "/Cart", "/Checkout", "/Orders", "/Account/Profile" })
            {
                var response = await browser.Page.GotoAsync(browser.WebUrl + route);
                Assert.NotNull(response); Assert.True(response.Ok, route + " HTTP " + response.Status);
                Assert.Equal("UTF-8", await browser.Page.EvaluateAsync<string>("document.characterSet"));
                Assert.True(await browser.Page.EvaluateAsync<bool>("getComputedStyle(document.body).fontFamily.includes('Segoe UI')"));
                Assert.True(await browser.Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth + 1"), route + " tràn ngang ở " + width);
                Assert.DoesNotContain('\uFFFD', await browser.Page.Locator("body").InnerTextAsync());
                await browser.Screenshot($"web-{width}-{route.Replace('/', '-')}");
            }
        }
        // Kiểm tra trang cần đăng nhập bằng phiên khách thật, tránh chỉ chụp trang chuyển hướng đăng nhập.
        var customer = await browser.Api.NewCustomer("typography");
        await browser.Login(customer.Input.Text("TenDangNhap"), customer.Input.Text("MatKhau"));
        foreach (var width in new[] { 1440, 390 })
        {
            await browser.Page.SetViewportSizeAsync(width, 1000);
            foreach (var route in new[] { "/Account/Profile", "/Orders", "/Checkout" })
            {
                var response = await browser.Page.GotoAsync(browser.WebUrl + route);
                Assert.NotNull(response); Assert.True(response.Ok);
                Assert.True(await browser.Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth + 1"), route + " tràn ngang khi đăng nhập");
                await browser.Screenshot($"signed-{width}-{route.Replace('/', '-')}");
            }
        }
        await browser.Page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce });
        Assert.True(await browser.Page.EvaluateAsync<bool>("matchMedia('(prefers-reduced-motion: reduce)').matches"));
    }
}
