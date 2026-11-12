using FourChang.Tests.Shared;
using Microsoft.Playwright;

namespace FourChang.Tests;

/// <summary>Kiểm tra trang thật bằng trình duyệt/API/SQL fixture; không sửa dữ liệu quán hoặc thay nghiệp vụ Web.</summary>
[Trait("Feature", "UIRefinement"), Trait("Layer", "Browser")]
public sealed class InterfaceRefinementBrowserTests
{
    /// <summary>Bộ lọc có nhãn, không tràn trên điện thoại; lọc/bỏ lọc và thứ tự bàn phím vẫn hoạt động.</summary>
    [Fact]
    public async Task DonCuaToi_BoLocThangHang_Responsive_VaGiuChucNang()
    {
        await using var f = await BrowserFixture.Start("UIRefinement-Orders");
        var customer = await f.Api.NewCustomer("layout");
        var headers = await f.Api.Login(customer);
        await f.Context.AddCookiesAsync([new Cookie { Name="fourchang_session", Value=headers["Authorization"][7..], Url=f.WebUrl, HttpOnly=true, SameSite=SameSiteAttribute.Lax }]);
        foreach (var width in new[] { 1440, 1024, 768, 390, 375 })
        {
            await f.Page.SetViewportSizeAsync(width, 960);
            await f.Page.GotoAsync(f.WebUrl+"/Orders");
            await Assertions.Expect(f.Page.GetByRole(AriaRole.Heading, new() { Name="Chưa có đơn hàng phù hợp" })).ToBeVisibleAsync();
            await AssertNoOverflow(f.Page);
            Assert.True(await f.Page.Locator(".orders-filters").EvaluateAsync<bool>("form => [...form.querySelectorAll('input:not([type=hidden]),select,button')].every(e => {const r=e.getBoundingClientRect();return r.width>=44 && r.height>=44;})"));
            await f.Screenshot("orders-"+width);
        }
        await f.Page.GetByLabel("Trạng thái đơn").SelectOptionAsync("COMPLETED");
        await f.Page.GetByRole(AriaRole.Button, new() { Name="Lọc đơn hàng", Exact=true }).ClickAsync();
        await f.Page.WaitForURLAsync("**/Orders?**Status=COMPLETED**");
        await Assertions.Expect(f.Page.Locator("#Status")).ToHaveValueAsync("COMPLETED");
        await f.Page.Locator(".orders-filter-actions a").ClickAsync();
        await Assertions.Expect(f.Page.Locator("#Status")).ToHaveValueAsync("");
        await f.Page.Locator("#Status").FocusAsync(); await f.Page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(f.Page.Locator("#FromUtc")).ToBeFocusedAsync();
    }

    /// <summary>Menu giữ hình ảnh/giá từ nguồn thật, tùy chỉnh/thêm giỏ hoạt động; mobile và reduced-motion không tràn.</summary>
    [Fact]
    public async Task ThucDon_Responsive_TuyChinhVaThemGio_VanHoatDong()
    {
        await using var f = await BrowserFixture.Start("UIRefinement-Menu");
        foreach (var width in new[] { 1440, 1024, 768, 390, 375 })
        {
            await f.Page.SetViewportSizeAsync(width, 960); await f.Page.GotoAsync(f.WebUrl);
            await Assertions.Expect(f.Page.Locator(".product-card").First).ToBeVisibleAsync();
            await AssertNoOverflow(f.Page); await f.Screenshot("menu-"+width);
        }
        await f.Page.SetViewportSizeAsync(1440, 1000);
        await f.Page.Locator(".product-card .round-add:not([disabled])").First.ClickAsync();
        // Người dùng vẫn phải chủ động chọn cấu hình hợp lệ trước khi được thêm giỏ.
        await f.Page.Locator("[data-editor-variant] option:not([disabled])").Nth(1).WaitForAsync(new() { State=WaitForSelectorState.Attached });
        var variant = await f.Page.Locator("[data-editor-variant] option:not([disabled])").Nth(1).GetAttributeAsync("value");
        await CartEditorActions.Variant(f.Page, variant!);
        var brew = await f.Page.Locator("[data-editor-brew] option").Nth(1).GetAttributeAsync("value");
        await CartEditorActions.Brew(f.Page, brew!);
        await Assertions.Expect(f.Page.Locator("[data-editor-save]")).ToBeEnabledAsync();
        await f.Screenshot("menu-customizer-desktop");
        await f.Page.Locator("[data-editor-save]").ClickAsync();
        await Assertions.Expect(f.Page.Locator("#cart-badge")).ToHaveTextAsync("1");
        await f.Page.SetViewportSizeAsync(375, 900); await AssertNoOverflow(f.Page);
        await f.Screenshot("menu-customizer-mobile");
        await f.Page.EmulateMediaAsync(new() { ReducedMotion=ReducedMotion.Reduce });
        Assert.Equal("0s", await f.Page.Locator(".round-add").First.EvaluateAsync<string>("e=>getComputedStyle(e).transitionDuration"));
    }

    /// <summary>Đo document thật sau layout, không chỉ kiểm tra CSS có media query.</summary>
    private static async Task AssertNoOverflow(IPage page) => Assert.True(
        await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth + 1"),
        "Nội dung Web tràn ngang vùng hiển thị.");
}
