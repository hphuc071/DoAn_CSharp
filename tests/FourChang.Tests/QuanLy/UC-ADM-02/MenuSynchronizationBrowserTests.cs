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
        await browser.Page.GotoAsync(browser.WebUrl);
        await Assertions.Expect(browser.Page.Locator(".product-card").First).ToBeVisibleAsync();
        var menu = await MenuAdministrationTests.Snapshot(api, admin);
        var variant = menu.CauHinh.First(x => x.LaMacDinh);
        var card = browser.Page.Locator($".product-card[data-mamon='{variant.MaMon}']");
        await browser.Page.EvaluateAsync("localStorage.setItem('fourchang.cart-draft.v1', '[]')");
        var draft = await browser.Page.EvaluateAsync<string>("localStorage.getItem('fourchang.cart-draft.v1')");
        var input = MenuAdministrationTests.Variant(variant, 67890);
        (await api.Send("PUT", $"/api/admin/menu/variants/{variant.MaCauHinhMon}", JsonSerializer.SerializeToNode(input), admin)).Expect(200, "Admin đổi giá");
        await Assertions.Expect(card).ToHaveAttributeAsync("data-price", "67890", new() { Timeout = 15000 });
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
