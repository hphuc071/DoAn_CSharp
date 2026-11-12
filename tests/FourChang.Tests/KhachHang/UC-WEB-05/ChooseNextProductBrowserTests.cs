using System.Text.Json;
using FourChang.Tests.Shared;
using Microsoft.Playwright;
namespace FourChang.Tests;

/// <summary>Chạy script thật trong Chromium; mock chỉ nguồn GET catalog, không giả kết quả ghi tiền.</summary>
public sealed class ChooseNextProductBrowserTests
{
    /// <summary>Thêm hai món liên tiếp dọn cấu hình cũ, giữ giỏ; lỗi lưu vẫn giữ form để thử lại.</summary>
    [Fact, Trait("UseCase","UC-WEB-05")]
    public async Task ThemMon_XoaFormCu_GiuGio_VaChonMonTiepTheo()
    {
        using var playwright=await Playwright.CreateAsync();
        await using var browser=await playwright.Chromium.LaunchAsync(new(){Channel="msedge",Headless=true});
        var page=await browser.NewPageAsync(new(){ViewportSize=new(){Width=390,Height=844}});
        await page.RouteAsync("**/*",async route=>
        {
            var uri=new Uri(route.Request.Url);
            if(uri.Query.Contains("handler=Catalog")){await route.FulfillAsync(new(){ContentType="application/json",Body="[{\"productId\":1,\"name\":\"Cà phê sữa\",\"available\":true},{\"productId\":2,\"name\":\"Trà đào\",\"available\":true}]"});return;}
            if(uri.Query.Contains("handler=Variants")){var id=uri.Query.Contains("productId=2")?2:1;await route.FulfillAsync(new(){ContentType="application/json",Body=JsonSerializer.Serialize(new[]{new{maCauHinhMon=id,kichCo="M",nhietDo="ICED",giaBan=35000,available=true,tuyChonPhaChe=new[]{new{phanTramDuong=50,phanTramDa=50}},toppings=new[]{new{maMonThem=1,tenMonThem="Trân châu",giaMoiPhan=10000,gioiHanMoiLy=2,available=true}}}})});return;}
            await route.FulfillAsync(new(){ContentType="text/html; charset=utf-8",Body="<h2 id='menu'>Thực đơn</h2><input id='menu-search'><div id='product-grid'><article class='product-card'><button data-product='Cà phê sữa' data-product-id='1'>Cà phê</button></article><article class='product-card'><button data-product='Trà đào' data-product-id='2'>Trà</button></article></div><section class='customizer'><div id='product-editor'></div></section><span id='cart-badge'></span>"});
        });
        await page.GotoAsync("http://localhost:18796/");
        foreach(var file in new[]{"cart-draft.js","cart-editor.js","cart.js"})await page.AddScriptTagAsync(new(){Content=await File.ReadAllTextAsync(Path.Combine(TestApi.FindRoot(),"src/Presentation/FourChang.Web/wwwroot/js",file))});
        for(var id=1;id<=2;id++)
        {
            await page.Locator($"[data-product-id='{id}']").ClickAsync();
            await Assertions.Expect(page.Locator("[data-editor-size]")).ToBeFocusedAsync();
            await CartEditorActions.Variant(page, id.ToString());
            await CartEditorActions.Brew(page, "[50,50]");
            await page.Locator("#product-editor textarea").FillAsync(id==1?"Ghi chú cũ":"Món mới");
            await page.Locator("[data-editor-save]").ClickAsync();
            await Assertions.Expect(page.Locator("[data-product-added]")).ToBeVisibleAsync();
            await Assertions.Expect(page.Locator("#product-editor form")).ToHaveCountAsync(0);
            await Assertions.Expect(page.Locator("#cart-badge")).ToHaveTextAsync(id.ToString());
        }
        Assert.Equal(2,await page.EvaluateAsync<int>("FourChangCart.parse(FourChangCart.raw()).length"));
        await page.Locator("[data-product-id='1']").ClickAsync();
        await Assertions.Expect(page.Locator("#product-editor textarea")).ToHaveValueAsync("");
        await Assertions.Expect(page.Locator("#product-editor input[type=number]").First).ToHaveValueAsync("1");
    }
}
