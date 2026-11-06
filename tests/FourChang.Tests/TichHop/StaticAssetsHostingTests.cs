using FourChang.Tests.Shared;
using Microsoft.Playwright;

namespace FourChang.Tests.Integration;

/// <summary>Hồi quy cách mở DLL từ thư mục gốc ở Production; kiểm tra tài nguyên thật, không chỉ HTML trả 200.</summary>
public sealed class StaticAssetsHostingTests
{
    /// <summary>CSS, JavaScript, logo và ảnh phải tải được và trình duyệt thực sự áp dụng giao diện.</summary>
    [Fact, Trait("Actor", "TichHop"), Trait("Layer", "WebHosting")]
    public async Task DllChayTuThuMucGoc_VanTaiDuocGiaoDien()
    {
        var output = Path.Combine(TestApi.Root, "artifacts", "tests", "static-assets", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        var url = TestApi.FreeUrl();
        var dll = Path.Combine(TestApi.Root, "src", "Presentation", "FourChang.Web", "bin", "Debug", "net8.0", "FourChang.Web.dll");
        await using var server = new ChildProcess("dotnet", [dll, "--urls", url], TestApi.Root,
            new() { ["ASPNETCORE_ENVIRONMENT"] = "Production", ["DOTNET_ENVIRONMENT"] = "Production", ["ASPNETCORE_CONTENTROOT"] = null, ["ASPNETCORE_WEBROOT"] = null }, Path.Combine(output, "web"));
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var ready = false;
        for (var i = 0; i < 50; i++)
        {
            try { using var response = await http.GetAsync(url + "/Account/Login"); if (response.IsSuccessStatusCode) { ready = true; break; } }
            catch (HttpRequestException) { }
            await Task.Delay(100);
        }
        Assert.True(ready, "Web chưa mở được từ DLL.");
        foreach (var path in new[] { "/css/tokens.css", "/css/site.css", "/css/account.css", "/css/refresh.css", "/js/site.js", "/js/interface.js", "/js/account.js", "/assets/brand-mark.svg", "/assets/coffee-sheet.png", "/assets/coffee-hero-reference.png", "/favicon.svg" })
        {
            using var response = await http.GetAsync(url + path);
            Assert.True(response.IsSuccessStatusCode, $"Tài nguyên {path}: HTTP {(int)response.StatusCode}");
            Assert.True((await response.Content.ReadAsByteArrayAsync()).Length > 0);
            if (path.EndsWith(".css")) Assert.Equal("text/css", response.Content.Headers.ContentType?.MediaType);
        }
        using var obsoleteTheme = await http.GetAsync(url + "/js/appearance.js");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, obsoleteTheme.StatusCode);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, ExecutablePath = Environment.GetEnvironmentVariable("FOURCHANG_TEST_CHROMIUM_PATH") });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1440, Height = 1000 } });
        await page.GotoAsync(url + "/Account/Login");
        Assert.Equal("flex", await page.Locator(".site-header").EvaluateAsync<string>("e => getComputedStyle(e).display"));
        Assert.True(await page.Locator(".brand-mark-image").EvaluateAsync<bool>("e => e.complete && e.naturalWidth > 0"));
        Assert.True(await page.Locator(".profile-link .ui-icon").EvaluateAsync<double>("e => e.getBoundingClientRect().width") <= 24);
        await page.ScreenshotAsync(new() { Path = Path.Combine(output, "production-dll-login.png"), FullPage = true, Animations = ScreenshotAnimations.Disabled });
    }
}
