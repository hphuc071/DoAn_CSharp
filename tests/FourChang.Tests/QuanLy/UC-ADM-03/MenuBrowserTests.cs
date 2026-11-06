using System.Text.Json;
using FourChang.Tests.Shared;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
namespace FourChang.Tests.Admin;

/// <summary>Browser thật: giá Admin thay đổi phải tới Web bằng SSE mà không reload/mất nháp.</summary>
public sealed class MenuBrowserTests
{
    /// <summary>Kiểm tra menu responsive, đồng bộ giá, banner, giữ ô tìm/ghi chú và an toàn nội dung.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-03"), Trait("Layer", "Browser")]
    public async Task AdminDoiGia_WebCapNhatRealtime_GiuNhapLieu()
    {
        await using var f = await BrowserFixture.Start("ADM03-Browser");
        var api = f.Api;
        var admin = await api.AdminLogin();
        var snap = await MenuAdministrationTests.Snapshot(api, admin);
        await f.Page.GotoAsync(f.WebUrl, new()
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });
        await Expect(f.Page.Locator(".product-card")).ToHaveCountAsync(6);
        var v = snap.CauHinh.First(x => x.LaMacDinh);
        var dish = snap.Mon.Single(x => x.MaMon == v.MaMon);
        await f.Page.Locator($"button[data-menu-id='{dish.MaMon}']").ClickAsync();
        await f.Page.Locator(".customizer textarea").FillAsync("Giữ nguyên ghi chú");
        await f.Page.Locator("#menu-search").FillAsync(dish.TenMon);
        var input = MenuAdministrationTests.Variant(v, 61000);
        (await api.Send("PUT", $"/api/admin/menu/variants/{v.MaCauHinhMon}", JsonSerializer.SerializeToNode(input), admin)).Expect(200, "Admin đổi giá");
        await Expect(f.Page.Locator("#selected-price")).ToContainTextAsync("61.000", new()
        {
            Timeout = 15000
        });
        await Expect(f.Page.Locator("#menu-search")).ToHaveValueAsync(dish.TenMon);
        await Expect(f.Page.Locator(".customizer textarea")).ToHaveValueAsync("Giữ nguyên ghi chú");
        await f.Page.Locator("#menu-search").FillAsync("");
        await Expect(f.Page.Locator(".product-card")).ToHaveCountAsync(6);
        foreach (var width in new[] { 375, 768, 1440 })
        {
            await f.Page.SetViewportSizeAsync(width, 960);
            await f.Page.WaitForTimeoutAsync(200);
            Assert.True(await f.Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth + 1"), $"Không tràn ngang ở {width}px");
            await f.Page.ScreenshotAsync(new()
            {
                Path = Path.Combine(api.Artifacts, $"menu-{width}.png"),
                FullPage = true
            });
        }
        var banner = new FourChang.Presentation.Shared.Contracts.Admin.MenuBannerInput("Nội dung mới <script>", "Thử nội dung", "sprite:2", "/#menu", "HOME_HERO", 0, DateTime.UtcNow.AddMinutes(-1), null, true, null, Guid.NewGuid());
        (await api.Send("POST", "/api/admin/menu/banners", JsonSerializer.SerializeToNode(banner), admin)).Expect(200, "Công bố banner");
        await Expect(f.Page.Locator("#catalog-banner h1")).ToHaveTextAsync(banner.TenBanner, new()
        {
            Timeout = 15000
        });
        Assert.Equal(0, await f.Page.Locator("#catalog-banner script").CountAsync());
        await f.Page.ScreenshotAsync(new()
        {
            Path = Path.Combine(api.Artifacts, "menu-banner.png"),
            FullPage = true
        });
    }
}
