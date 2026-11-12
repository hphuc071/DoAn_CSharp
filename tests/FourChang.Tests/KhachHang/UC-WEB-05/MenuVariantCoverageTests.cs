using FourChang.Tests.Shared;
using Microsoft.Playwright;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests;

/// <summary>Đối chiếu tất cả cấu hình có bán trên API với các ô chọn thực tế, không tự sinh cấu hình/BOM.</summary>
public sealed class MenuVariantCoverageTests
{
    /// <summary>Mỗi size/nhiệt độ còn hàng đều chọn được; món nóng ẩn đá và món lạnh hiển thị đá.</summary>
    [Fact, Trait("Feature", "MenuVariantCoverage"), Trait("Layer", "Browser")]
    public async Task TatCaMon_ChonDuocDungSizeVaNhietDoDaCongBo()
    {
        await using var f = await BrowserFixture.Start("Menu-Variant-Coverage");
        var menu = (await f.Api.Send("GET", "/api/menu")).Expect(200,"Menu").Json.AsArray();
        await f.Page.GotoAsync(f.WebUrl + "/");
        var checkedVariants = 0;
        foreach(var product in menu.Where(p=>p!.Flag("available")))
        {
            var id = product!.Number("maMon");
            var variants = (await f.Api.Send("GET",$"/api/menu/{id}/variants")).Expect(200,"Cấu hình từng món").Json.AsArray();
            await f.Page.Locator($"[data-product-id='{id}']").ClickAsync();
            foreach(var variant in variants.Where(v=>v!.Flag("available")))
            {
                await CartEditorActions.Variant(f.Page,variant!.Number("maCauHinhMon").ToString());
                await Assertions.Expect(f.Page.Locator("[data-editor-size]")).ToHaveValueAsync(variant!.Text("kichCo"));
                await Assertions.Expect(f.Page.Locator("[data-editor-temperature]")).ToHaveValueAsync(variant!.Text("nhietDo"));
                Assert.Equal(variant!.Text("nhietDo")=="ICED",await f.Page.Locator("[data-editor-ice]").IsVisibleAsync());
                var pair = variant!["tuyChonPhaChe"]!.AsArray().First()!;
                await CartEditorActions.Brew(f.Page,$"[{pair.Number("phanTramDuong")},{pair.Number("phanTramDa")}]");
                await Assertions.Expect(f.Page.Locator("[data-editor-save]")).ToBeEnabledAsync();
                checkedVariants++;
            }
            await f.Page.Locator(".config-close").ClickAsync();
        }
        Assert.True(checkedVariants>0,"Phải kiểm tra ít nhất một cấu hình bán thật.");
        Assert.Null(await f.Page.EvaluateAsync<string?>("localStorage.getItem('fourchang.cart-draft.v1')"));
    }
}
