using FourChang.Tests.Shared;
using Microsoft.Playwright;

namespace FourChang.Tests.Customer;

/// <summary>Kiểm thử giao diện Web thật: responsive, bỏ qua theme cũ, không mất nháp, bàn phím và giảm chuyển động.</summary>
public sealed class AppearanceBrowserTests
{
    /// <summary>Duyệt các trang ở hai thiết lập hệ điều hành và năm kích thước; không thay database dự án chính.</summary>
    [Fact, Trait("Actor", "KhachHang"), Trait("Layer", "Web")]
    public async Task GiaoDienCoDinh_ResponsiveVaGiuNhapLieu()
    {
        await using var browser = await BrowserFixture.Start("Appearance-Web");
        var page = browser.Page;
        var errors = new List<string>();
        page.PageError += (_, message) => errors.Add(message);
        // Cấu hình màu cũ và hệ điều hành tối không được ghi đè giao diện thống nhất.
        await browser.Context.AddInitScriptAsync("localStorage.setItem('fourchang.appearance','dark');");
        foreach (var size in new[] { (320, 740), (375, 812), (768, 1024), (812, 375), (1440, 1000) })
        {
            await page.SetViewportSizeAsync(size.Item1, size.Item2);
            foreach (var system in new[] { ColorScheme.Light, ColorScheme.Dark })
            {
                await page.EmulateMediaAsync(new() { ColorScheme = system });
                await page.GotoAsync(browser.WebUrl + "/Account/Login");
                foreach (var route in new[] { "/", "/Account/Login", "/Account/Register", "/Cart", "/Checkout", "/Checkout/Payment", "/Orders" })
                {
                    var response = await page.GotoAsync(browser.WebUrl + route);
                    Assert.True(response?.Ok, $"Trang {route} không tải được.");
                    Assert.Null(await page.Locator("html").GetAttributeAsync("data-theme"));
                    Assert.Equal(0, await page.Locator("#appearance-toggle").CountAsync());
                    Assert.Equal("light", await page.Locator("html").EvaluateAsync<string>("e => getComputedStyle(e).colorScheme"));
                    // Kiểm tra nội dung thực, không che lỗi bằng overflow-x:hidden.
                    var overflow = await page.EvaluateAsync<int>("document.documentElement.scrollWidth - innerWidth");
                    Assert.True(overflow <= 1, $"{route}: tràn ngang {overflow}px tại {size}, OS={system}.");
                    await AssertContrast(page);
                    if (size.Item2 != 375)
                        await browser.Screenshot($"{route.Trim('/').Replace('/', '-') switch { "" => "menu", var name => name }}-OS{system}-{size.Item1}");
                }
            }
        }
        await page.SetViewportSizeAsync(375, 812);
        await page.GotoAsync(browser.WebUrl + "/Account/Login");
        await browser.Field("Tên đăng nhập, email hoặc số điện thoại").FillAsync("nhap-thu");
        await browser.Field("Mật khẩu").FillAsync("ChuaGuiForm@123");
        await page.GetByRole(AriaRole.Button, new() { Name = "Hiện mật khẩu", Exact = true }).ClickAsync();
        Assert.Equal("text", await browser.Field("Mật khẩu").GetAttributeAsync("type"));
        Assert.Equal("ChuaGuiForm@123", await browser.Field("Mật khẩu").InputValueAsync());
        await page.GetByRole(AriaRole.Button, new() { Name = "Ẩn mật khẩu", Exact = true }).ClickAsync();
        Assert.Equal("password", await browser.Field("Mật khẩu").GetAttributeAsync("type"));
        await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark });
        Assert.Equal("nhap-thu", await browser.Field("Tên đăng nhập, email hoặc số điện thoại").InputValueAsync());
        Assert.Equal("ChuaGuiForm@123", await browser.Field("Mật khẩu").InputValueAsync());
        await page.ReloadAsync();
        Assert.Null(await page.Locator("html").GetAttributeAsync("data-theme"));
        Assert.Equal("dark", await page.EvaluateAsync<string>("localStorage.getItem('fourchang.appearance')"));

        await page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce });
        Assert.Equal("none", await page.Locator("main").EvaluateAsync<string>("e => getComputedStyle(e).animationName"));
        await page.GotoAsync(browser.WebUrl);
        await page.Locator("#menu-search").FillAsync("zzzz-khong-co-mon");
        await Assertions.Expect(page.Locator("#menu-empty")).ToBeVisibleAsync();
        await page.Locator("#reset-filter").ClickAsync();
        Assert.True(await page.Locator(".product-card:visible").CountAsync() > 0);

        await page.GotoAsync(browser.WebUrl + "/Checkout");
        await page.GetByRole(AriaRole.Tab, new() { Name = "Giao tận nơi", Exact = true }).FocusAsync();
        await page.Keyboard.PressAsync("ArrowLeft");
        Assert.Equal("true", await page.GetByRole(AriaRole.Tab, new() { Name = "Mang đi", Exact = true }).GetAttributeAsync("aria-selected"));
        Assert.Equal("0đ", await page.Locator("#shipping-fee").TextContentAsync());

        // Hồ sơ đăng nhập dùng chính API test, bảo đảm CSS không làm mất các trường tài khoản/địa chỉ.
        var customer = await browser.Api.NewCustomer();
        await browser.Login(customer.Input.Text("TenDangNhap"), customer.Input.Text("MatKhau"));
        await page.GotoAsync(browser.WebUrl + "/Account/Profile");
        Assert.Contains("Thông tin cá nhân", await page.TitleAsync());
        Assert.True(await page.EvaluateAsync<int>("document.documentElement.scrollWidth - innerWidth") <= 1);
        await browser.Screenshot("profile-mobile");
        await page.GotoAsync(browser.WebUrl + "/Orders");
        await Assertions.Expect(page.Locator(".orders-empty")).ToBeVisibleAsync();
        await browser.Screenshot("orders-authenticated-mobile");
        Assert.Empty(errors);
    }

    /// <summary>Đo cặp chữ/nền semantic ngay từ CSS đã render; không suy ra chất lượng dark mode từ light mode.</summary>
    private static async Task AssertContrast(IPage page)
    {
        var ratios = await page.EvaluateAsync<double[]>("""
            () => {
                const rgb = value => value.match(/[\d.]+/g).slice(0,3).map(Number);
                const luminance = value => rgb(value).map(v => {
                    v /= 255; return v <= .04045 ? v/12.92 : ((v+.055)/1.055)**2.4;
                }).reduce((sum,v,i) => sum + v*[.2126,.7152,.0722][i],0);
                const ratio = (fg,bg) => (Math.max(luminance(fg),luminance(bg))+.05)/(Math.min(luminance(fg),luminance(bg))+.05);
                const root = getComputedStyle(document.documentElement);
                const read = name => { const e=document.createElement('span'); e.style.color=root.getPropertyValue(name); document.body.append(e); const c=getComputedStyle(e).color; e.remove(); return c; };
                return [['--ink','--surface'],['--muted','--surface'],['--ink','--cream'],['--on-primary','--coffee'],['--sage-ink','--sage'],['--amber-ink','--amber']].map(p => ratio(read(p[0]),read(p[1])));
            }
            """);
        Assert.All(ratios, ratio => Assert.True(ratio >= 4.5, $"Độ tương phản {ratio:F2}:1 thấp hơn 4.5:1."));
    }
}
