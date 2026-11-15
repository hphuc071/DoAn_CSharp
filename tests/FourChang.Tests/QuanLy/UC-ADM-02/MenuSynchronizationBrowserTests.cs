using System.Text.Json;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Tests.Shared;
using Microsoft.Playwright;

namespace FourChang.Tests.Admin;

/// <summary>Kiểm tra luồng Admin → API → Web thật; giỏ và layout cũ được bảo toàn.</summary>
public sealed class MenuSynchronizationBrowserTests
{
    /// <summary>Đổi giá/còn hết tự hiện trên Web, giữ draft; mobile không tràn ngang và điện thoại giới hạn 10 số.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-02"), Trait("Layer", "Browser")]
    public async Task GiaVaTon_DongBoWeb_GiuGio_Responsive_VaSoDienThoai()
    {
        await using var browser = await BrowserFixture.Start("ADM02-BrowserSync");
        var api = browser.Api; var admin = await api.AdminLogin();
        // Gắn ảnh menu thật từ Admin trước khi tải Web để kiểm cả Razor ban đầu và proxy ảnh.
        var image = (await api.Send("POST", "/api/admin/menu/images", JsonSerializer.SerializeToNode(new MenuImageInput(Convert.ToBase64String(MenuAdministrationTests.ImageBytes()))), admin)).Expect(200, "Tải ảnh Web").Json.Text("path");
        var original = (await MenuAdministrationTests.Snapshot(api, admin)).Mon.First();
        var picture = new MenuDishInput(original.MaDanhMuc, original.TenMon, original.MoTa, image, original.ThuTuHienThi, original.DaCongBo, original.DangBan, null, original.PhienBanDuLieu, Guid.NewGuid());
        (await api.Send("PUT", $"/api/admin/menu/dishes/{original.MaMon}", JsonSerializer.SerializeToNode(picture), admin)).Expect(200, "Đổi ảnh món");
        var beforeMenu = await MenuAdministrationTests.Snapshot(api, admin);
        var small = beforeMenu.CauHinh.First(v => v.MaMon == original.MaMon && v.KichCo == "S");
        (await api.Send("PUT", $"/api/admin/menu/variants/{small.MaCauHinhMon}", JsonSerializer.SerializeToNode(MenuAdministrationTests.Variant(small) with { LaMacDinh = true }), admin)).Expect(200, "Đặt S làm mặc định lưu để kiểm thẻ vẫn ưu tiên M");
        var defaultVariant = (await MenuAdministrationTests.Snapshot(api, admin)).CauHinh.First(v => v.MaMon == original.MaMon && v.KichCo == "M");
        var listed = (await api.Send("GET", "/api/menu")).Json.AsArray().Single(m => m!.Number("maMon") == original.MaMon)!;
        Assert.Equal(defaultVariant.MaCauHinhMon, listed.Number("id")); Assert.Equal(defaultVariant.GiaBan, listed.Number("price"));
        var recipes = JsonSerializer.Deserialize<List<RecipeView>>((await api.Send("GET", "/api/admin/recipes", headers: admin)).Content, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var source = recipes.Single(x => x.MaCauHinhMon == defaultVariant.MaCauHinhMon);
        var ingredients = JsonSerializer.Deserialize<IngredientSnapshot>((await api.Send("GET", "/api/admin/ingredients", headers: admin)).Content, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var inputRecipe = new RecipeInput(source.MaCauHinhMon, null, source.TenCongThuc, null,
            source.ThanhPhan.Select(l => new RecipeLineInput(l.MaNguyenLieu, ingredients.NguyenLieu.Single(n => n.MaNguyenLieu == l.MaNguyenLieu).MaDonViCoSo, l.DinhMucCoSo, l.LoaiDinhMuc, 0)).ToList(), source.PhienBanDuLieu, Guid.NewGuid(), 75);
        (await api.Send("PUT", $"/api/admin/recipes/{source.MaCongThuc}", JsonSerializer.SerializeToNode(inputRecipe), admin)).Expect(200, "Đặt đường mặc định 75%");
        await browser.Page.GotoAsync(browser.WebUrl);
        var actualPhoto = browser.Page.Locator($".product-card[data-mamon='{original.MaMon}'] .drink-photo img");
        await Assertions.Expect(actualPhoto).ToBeVisibleAsync();
        await Assertions.Expect(actualPhoto).ToHaveAttributeAsync("src", image);
        Assert.Equal("contain", await actualPhoto.EvaluateAsync<string>("img => getComputedStyle(img).objectFit"));
        Assert.Contains(await actualPhoto.Locator("..").EvaluateAsync<string>("photo => getComputedStyle(photo, '::before').content"), new[] { "none", "normal" });
        await Assertions.Expect(browser.Page.Locator($".product-card[data-mamon='{original.MaMon}']")).ToHaveAttributeAsync("data-price", defaultVariant.GiaBan.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await browser.Page.WaitForFunctionAsync("selector => document.querySelector(selector)?.naturalWidth === 12", $".product-card[data-mamon='{original.MaMon}'] .drink-photo img");
        await Assertions.Expect(browser.Page.Locator(".product-card").First).ToBeVisibleAsync();
        await browser.Page.Locator($"[data-product-id='{original.MaMon}']").ClickAsync();
        await Assertions.Expect(browser.Page.Locator("[data-editor-sugar]")).ToHaveValueAsync("75");
        await browser.Page.Locator("[data-editor-sugar]").SelectOptionAsync("25");
        await Assertions.Expect(browser.Page.Locator("[data-editor-sugar]")).ToHaveValueAsync("25");
        await browser.Page.Locator(".config-close").ClickAsync();
        var menu = await MenuAdministrationTests.Snapshot(api, admin);
        var variant = menu.CauHinh.Single(x => x.MaCauHinhMon == defaultVariant.MaCauHinhMon);
        var card = browser.Page.Locator($".product-card[data-mamon='{variant.MaMon}']");
        await browser.Page.EvaluateAsync("localStorage.setItem('fourchang.cart-draft.v1', '[]')");
        var draft = await browser.Page.EvaluateAsync<string>("localStorage.getItem('fourchang.cart-draft.v1')");
        var input = MenuAdministrationTests.Variant(variant, 67890);
        (await api.Send("PUT", $"/api/admin/menu/variants/{variant.MaCauHinhMon}", JsonSerializer.SerializeToNode(input), admin)).Expect(200, "Admin đổi giá");
        await Assertions.Expect(card).ToHaveAttributeAsync("data-price", "67890", new() { Timeout = 15000 });
        await Assertions.Expect(actualPhoto).ToHaveAttributeAsync("src", image);
        Assert.Equal("contain", await actualPhoto.EvaluateAsync<string>("img => getComputedStyle(img).objectFit"));
        Assert.Contains(await actualPhoto.Locator("..").EvaluateAsync<string>("photo => getComputedStyle(photo, '::before').content"), new[] { "none", "normal" });
        await browser.Page.ScreenshotAsync(new() { Path = Path.Combine(api.Artifacts, "menu-correct-photo-size-m.png"), FullPage = true });
        Assert.Equal(draft, await browser.Page.EvaluateAsync<string>("localStorage.getItem('fourchang.cart-draft.v1')"));
        await api.Sql("UPDATE dbo.TonKho SET TonThucTe=TongGiuCho");
        await Assertions.Expect(card).ToHaveAttributeAsync("data-available", "false", new() { Timeout = 15000 });
        await Assertions.Expect(card.Locator(".round-add")).ToBeDisabledAsync();
        // Tắt bán vẫn hiện tạm hết; chỉ bỏ công bố mới ẩn theo lựa chọn mới của chủ dự án.
        var current = (await MenuAdministrationTests.Snapshot(api, admin)).Mon.Single(x => x.MaMon == variant.MaMon);
        var off = new MenuDishInput(current.MaDanhMuc, current.TenMon, current.MoTa, current.DuongDanAnh, current.ThuTuHienThi, true, false, current.CongBoTuLuc, current.PhienBanDuLieu, Guid.NewGuid());
        (await api.Send("PUT", $"/api/admin/menu/dishes/{current.MaMon}", JsonSerializer.SerializeToNode(off), admin)).Expect(200, "Tắt bán");
        await Assertions.Expect(card).ToBeVisibleAsync();
        current = (await MenuAdministrationTests.Snapshot(api, admin)).Mon.Single(x => x.MaMon == variant.MaMon);
        (await api.Send("PUT", $"/api/admin/menu/dishes/{current.MaMon}", JsonSerializer.SerializeToNode(off with { DaCongBo = false, PhienBanDuLieu = current.PhienBanDuLieu, MaThaoTac = Guid.NewGuid() }), admin)).Expect(200, "Ẩn món");
        await Assertions.Expect(card).ToHaveCountAsync(0, new() { Timeout = 15000 });
        await browser.Page.SetViewportSizeAsync(390, 844); await browser.Page.ReloadAsync();
        Assert.True(await browser.Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth + 1"));
        await browser.Page.ScreenshotAsync(new() { Path = Path.Combine(api.Artifacts, "menu-mobile.png"), FullPage = true });
        await browser.Page.GotoAsync(browser.WebUrl + "/Account/Register");
        var phone = browser.Page.Locator("input[name='SoDienThoai']");
        await Assertions.Expect(phone).ToHaveAttributeAsync("maxlength", "10");
        Assert.True(await phone.EvaluateAsync<bool>("input => input.required"));
        Assert.False(await phone.EvaluateAsync<bool>("input => input.checkValidity()"));
        await phone.FillAsync("123"); Assert.False(await phone.EvaluateAsync<bool>("input => input.checkValidity()"));
        await phone.FillAsync("0901234567"); Assert.True(await phone.EvaluateAsync<bool>("input => input.checkValidity()"));
    }
}
