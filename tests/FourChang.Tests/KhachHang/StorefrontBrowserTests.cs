using FourChang.Tests.Shared;
using Microsoft.Playwright;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests;

/// <summary>Kiểm tra giao diện cửa hàng mới bằng menu thật; không thay giá hoặc ghi đơn thanh toán.</summary>
public sealed class StorefrontBrowserTests
{
    /// <summary>Đảm bảo bộ lọc, chọn cấu hình, lưu giỏ và Escape hoạt động trên bố cục máy tính, tablet, điện thoại.</summary>
    [Fact, Trait("Feature", "CustomerStorefront"), Trait("Layer", "Browser")]
    public async Task ThucDon_CauHinh_GioHang_VaResponsive()
    {
        await using var f = await BrowserFixture.Start("Customer-Storefront");
        await f.Page.GotoAsync(f.WebUrl + "/");
        await f.Page.EvaluateAsync("document.fonts.ready");
        await Assertions.Expect(f.Page.Locator(".product-card").First).ToBeVisibleAsync();
        await Assertions.Expect(f.Page.Locator(".brand-name")).ToContainTextAsync("4CHANG");
        Assert.Equal("rgb(117, 69, 45)", await f.Page.Locator(".site-header").EvaluateAsync<string>("e => getComputedStyle(e).backgroundColor"));

        foreach (var width in new[] { 1440, 768, 390 })
        {
            await f.Page.SetViewportSizeAsync(width, 900);
            Assert.False(await f.Page.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth + 1"), $"Menu tràn ngang tại {width}px");
            await f.Screenshot($"storefront-menu-{width}");
        }

        // Bộ lọc tiếp tục dùng ID và dữ liệu hiện có, không có danh sách sản phẩm/giá riêng.
        await f.Page.Locator("#menu-search").FillAsync("khong-co-mon-xyz");
        await Assertions.Expect(f.Page.Locator("#menu-empty")).ToBeVisibleAsync();
        await f.Page.Locator("#reset-filter").ClickAsync();
        await Assertions.Expect(f.Page.Locator("#menu-empty")).ToBeHiddenAsync();
        await f.Page.EvaluateAsync("localStorage.removeItem('fourchang.cart-draft.v1')");
        await f.Page.ReloadAsync();

        var choose = f.Page.Locator(".product-card [data-product]:not([disabled])").First;
        var dialog = f.Page.Locator("#product-config-dialog");
        await choose.ClickAsync();
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        await Assertions.Expect(dialog.Locator("[data-editor-variant] option")).Not.ToHaveCountAsync(0);
        var variant = await dialog.Locator("[data-editor-variant] option").EvaluateAllAsync<string>("xs => xs.find(x => x.value && !x.disabled).value");
        await CartEditorActions.Variant(f.Page, variant);
        var brew = await dialog.Locator("[data-editor-brew] option").EvaluateAllAsync<string>("xs => xs.find(x => x.value && !x.disabled).value");
        await CartEditorActions.Brew(f.Page, brew);
        Assert.False(await f.Page.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth + 1"));
        await f.Page.ScreenshotAsync(new() { Path = Path.Combine(f.Api.Artifacts, "storefront-config-390.png"), FullPage = false, Animations = ScreenshotAnimations.Disabled });
        await dialog.Locator("[data-editor-save]").ClickAsync();
        await Assertions.Expect(dialog).ToBeHiddenAsync();
        await Assertions.Expect(f.Page.Locator("[data-product-added]")).ToBeVisibleAsync();
        await Assertions.Expect(f.Page.Locator("#cart-badge")).ToHaveTextAsync("1");

        // Mở món kế tiếp không mang theo cấu hình cũ; Escape đóng hộp thoại và không xóa giỏ.
        await choose.ClickAsync();
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        await f.Page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(dialog).ToBeHiddenAsync();
        await Assertions.Expect(f.Page.Locator("#cart-badge")).ToHaveTextAsync("1");
        await f.Page.SetViewportSizeAsync(1440, 900);
        await choose.ClickAsync();
        await Assertions.Expect(dialog.Locator("[data-editor-variant] option")).Not.ToHaveCountAsync(0);
        await f.Page.ScreenshotAsync(new() { Path = Path.Combine(f.Api.Artifacts, "storefront-config-1440.png"), FullPage = false, Animations = ScreenshotAnimations.Disabled });
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Đóng tùy chỉnh món", Exact = true }).ClickAsync();
        await Assertions.Expect(dialog).ToBeHiddenAsync();
    }

    /// <summary>Đường đi Thanh toán lưu món trước, dùng đúng cấu hình nóng/lạnh và không tự tạo hoặc trả tiền đơn.</summary>
    [Fact, Trait("Feature", "SplitProductOptions"), Trait("Layer", "Browser")]
    public async Task Nong_Lanh_TachSizeDuongDa_VaThanhToan()
    {
        await using var f = await BrowserFixture.Start("Split-Product-Options");
        var menu = (await f.Api.Send("GET", "/api/menu")).Expect(200,"Menu").Json.AsArray();
        var product = menu.First(p => p!.Flag("available"))!;
        var variants = (await f.Api.Send("GET", $"/api/menu/{product.Number("maMon")}/variants")).Expect(200,"Cấu hình").Json.AsArray();
        var hot = variants.First(v => v!.Flag("available") && v!.Text("nhietDo") == "HOT")!;
        var cold = variants.First(v => v!.Flag("available") && v!.Text("nhietDo") == "ICED" && v!.Text("kichCo") == hot.Text("kichCo"))!;
        await f.Page.GotoAsync(f.WebUrl + "/");
        await f.Page.Locator($"[data-product-id='{product.Number("maMon")}']").ClickAsync();
        await CartEditorActions.Variant(f.Page,hot.Number("maCauHinhMon").ToString());
        var hotPair = hot["tuyChonPhaChe"]!.AsArray().First(p=>p!.Number("phanTramDa")==0)!;
        await CartEditorActions.Brew(f.Page,$"[{hotPair.Number("phanTramDuong")},0]");
        await Assertions.Expect(f.Page.Locator("[data-editor-size]")).ToBeVisibleAsync();
        await Assertions.Expect(f.Page.Locator("[data-editor-temperature]")).ToBeVisibleAsync();
        await Assertions.Expect(f.Page.Locator("[data-editor-sugar]")).ToBeVisibleAsync();
        await Assertions.Expect(f.Page.Locator("[data-editor-ice]")).ToBeHiddenAsync();
        await Assertions.Expect(f.Page.Locator("[data-editor-checkout]")).ToBeEnabledAsync();
        await f.Page.ScreenshotAsync(new(){Path=Path.Combine(f.Api.Artifacts,"hot-options.png"),FullPage=false,Animations=ScreenshotAnimations.Disabled});

        await CartEditorActions.Variant(f.Page,cold.Number("maCauHinhMon").ToString());
        var coldPair = cold["tuyChonPhaChe"]!.AsArray().First()!;
        await CartEditorActions.Brew(f.Page,$"[{coldPair.Number("phanTramDuong")},{coldPair.Number("phanTramDa")}]");
        await Assertions.Expect(f.Page.Locator("[data-editor-ice]")).ToBeVisibleAsync();
        await f.Page.SetViewportSizeAsync(390,900);
        Assert.False(await f.Page.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth + 1"));
        await f.Page.ScreenshotAsync(new(){Path=Path.Combine(f.Api.Artifacts,"cold-options.png"),FullPage=false,Animations=ScreenshotAnimations.Disabled});
        await f.Page.Locator("[data-editor-checkout]").ClickAsync();
        await f.Page.WaitForURLAsync("**/Checkout");
        await Assertions.Expect(f.Page.Locator("#cart-badge")).ToHaveTextAsync("1");
        await f.Page.GotoAsync(f.WebUrl + "/Cart");
        await Assertions.Expect(f.Page.Locator("[data-cart-index]")).ToHaveCountAsync(1);
        await f.Page.Locator(".commerce-summary a[href='/Checkout']").ClickAsync();
        await f.Page.WaitForURLAsync("**/Checkout");
    }
}
