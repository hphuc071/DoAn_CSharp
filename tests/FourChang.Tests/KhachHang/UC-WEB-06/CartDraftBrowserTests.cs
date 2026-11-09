using System.Text.Json;
using FourChang.Tests.Shared;
using Microsoft.Playwright;

namespace FourChang.Tests;

// Real Razor markup and browser scripts; all catalog GETs intercepted, no server/SQL.
[Trait("Safety", "NoDatabase")]
public sealed class CartDraftBrowserTests : IAsyncLifetime
{
    private IPlaywright playwright = null!;
    private IBrowser browser = null!;
    private IBrowserContext context = null!;
    private IPage page = null!;
    private int price = 45000, limit = 2;
    private bool available = true, sourceError, toppingAvailable = true, accept = true, extraVariant;
    private long topPrice = 5000;
    private const string Key = "fourchang.cart-draft.v1";
    private readonly string root = Path.Combine(TestApi.FindRoot(), "src", "Presentation", "FourChang.Web");

    public async Task InitializeAsync()
    {
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = true });
        context = await browser.NewContextAsync(); page = await context.NewPageAsync();
        page.Dialog += async (_, d) => { if (accept) await d.AcceptAsync(); else await d.DismissAsync(); };
        await context.RouteAsync("**/*", async route =>
        {
            var uri = new Uri(route.Request.Url); Assert.Equal("GET", route.Request.Method);
            if (uri.Query.Contains("handler=Catalog"))
                await route.FulfillAsync(new() { ContentType = "application/json", Body = """[{"productId":1,"name":"Latte","available":true}]""" });
            else if (uri.Query.Contains("handler=Variants"))
            {
                if (sourceError) { await route.FulfillAsync(new() { Status = 503, ContentType = "text/html", Body = "unavailable" }); return; }
                var variants = new List<object> {
                    new { maCauHinhMon = 1, kichCo = "M", nhietDo = "ICED", giaBan = price, available,
                        tuyChonPhaChe = new[] { new { phanTramDuong = 50, phanTramDa = 50 }, new { phanTramDuong = 100, phanTramDa = 0 } },
                        toppings = new[] {
                            new { maMonThem = 2, tenMonThem = "Kem cheese", giaMoiPhan = topPrice, gioiHanMoiLy = limit, available = toppingAvailable },
                            new { maMonThem = 3, tenMonThem = "Pearls", giaMoiPhan = 3000L, gioiHanMoiLy = 3, available = true } } }
                };
                if (extraVariant) variants.Add(new { maCauHinhMon = 4, kichCo = "L", nhietDo = "HOT", giaBan = price + 1000, available = true,
                    tuyChonPhaChe = new[] { new { phanTramDuong = 100, phanTramDa = 0 } }, toppings = Array.Empty<object>() });
                await route.FulfillAsync(new() { ContentType = "application/json", Body = JsonSerializer.Serialize(variants) });
            }
            else if (uri.AbsolutePath == "/Cart")
            {
                var html = await File.ReadAllTextAsync(Path.Combine(root, "Pages", "Cart", "Index.cshtml"));
                html = html[html.IndexOf("<div class=\"breadcrumb\"", StringComparison.Ordinal)..];
                html = html[..html.IndexOf("@section Scripts", StringComparison.Ordinal)];
                await route.FulfillAsync(new() { ContentType = "text/html; charset=utf-8", Body = html });
            }
            else if (uri.AbsolutePath == "/")
            {
                var partial = await File.ReadAllTextAsync(Path.Combine(root, "Pages", "Shared", "_ProductCustomizer.cshtml"));
                partial = partial[..partial.IndexOf("<script", StringComparison.Ordinal)];
                await route.FulfillAsync(new() { ContentType = "text/html; charset=utf-8", Body =
                    "<div class='product-card'><button data-product='Latte' data-product-id='1'>Choose</button></div>" + partial });
            }
            else await route.AbortAsync();
        });
        await OpenAsync();
    }

    private async Task OpenAsync(string path = "/")
    {
        await page.GotoAsync("http://localhost:18793" + path);
        foreach (var file in new[] { "cart-draft.js", "cart-editor.js", path == "/" ? "cart.js" : "cart-page.js" })
            await page.AddScriptTagAsync(new() { Content = await File.ReadAllTextAsync(Path.Combine(root, "wwwroot", "js", file)) });
    }
    private async Task SelectAsync()
    {
        await page.Locator("[data-product]").ClickAsync();
        await Assertions.Expect(page.Locator("[data-editor-variant] option")).ToHaveCountAsync(extraVariant ? 3 : 2);
        if (available) {
            await page.Locator("[data-editor-variant]").SelectOptionAsync("1");
            await page.Locator("[data-editor-brew]").SelectOptionAsync("[50,50]");
        }
    }
    private async Task AddAsync() { await SelectAsync(); await page.Locator("[data-editor-save]").ClickAsync(); await Assertions.Expect(page.GetByRole(AriaRole.Status)).ToContainTextAsync("Đã lưu"); }
    private async Task EditAsync()
    {
        await OpenAsync("/Cart"); await page.GetByRole(AriaRole.Button, new() { Name = "Sửa món", Exact = true }).First.ClickAsync();
        await Assertions.Expect(page.Locator("[data-editor-variant] option")).ToHaveCountAsync(2);
    }
    private Task<string> RawAsync() => page.EvaluateAsync<string>("localStorage.getItem('" + Key + "') || '[]'");
    private async Task<JsonElement[]> CartAsync() { using var j = JsonDocument.Parse(await RawAsync()); return j.RootElement.EnumerateArray().Select(x => x.Clone()).ToArray(); }
    private Task SetRawAsync(string text) => page.EvaluateAsync("text => localStorage.setItem('" + Key + "', text)", text);

    [Fact]
    public async Task Same_configuration_merges_only_with_the_same_price_context()
    {
        await AddAsync(); price = 46000; await AddAsync();
        await page.Locator("[data-editor-save]").ClickAsync();
        await page.WaitForFunctionAsync("JSON.parse(localStorage.getItem('fourchang.cart-draft.v1'))[1].quantity === 2");
        var cart = await CartAsync(); Assert.Equal(2, cart.Length);
        Assert.Equal(45000, cart[0].GetProperty("listedPrice").GetInt32());
        Assert.Equal(1, cart[0].GetProperty("quantity").GetInt32());
        Assert.Equal(46000, cart[1].GetProperty("listedPrice").GetInt32());
    }
    [Fact]
    public async Task Unavailable_variant_cannot_be_added_to_the_draft()
    {
        available = false; await SelectAsync();
        Assert.True(await page.Locator("[data-editor-variant] option[value='1']").EvaluateAsync<bool>("o => o.disabled"));
        await Assertions.Expect(page.Locator("[data-editor-save]")).ToBeDisabledAsync(); Assert.Empty(await CartAsync());
    }
    [Fact]
    public async Task Unsupported_brew_pair_does_not_enter_the_cart()
    {
        await SelectAsync();
        await page.Locator("[data-editor-brew]").EvaluateAsync("s => { s.add(new Option('forged','[25,25]')); s.value='[25,25]'; s.dispatchEvent(new Event('change',{bubbles:true})); }");
        await Assertions.Expect(page.Locator("[data-editor-save]")).ToBeDisabledAsync(); Assert.Empty(await CartAsync());
    }
    [Fact]
    public async Task Topping_limit_comes_from_the_variant_not_the_static_input_maximum()
    {
        limit = 1; await SelectAsync();
        await page.Locator("[data-topping-id='2']").FillAsync("2");
        await Assertions.Expect(page.Locator("[data-editor-save]")).ToBeDisabledAsync();
        await page.Locator("[data-topping-id='2']").FillAsync("1");
        await page.Locator("[data-editor-save]").ClickAsync();
        await page.WaitForFunctionAsync("!!localStorage.getItem('fourchang.cart-draft.v1')"); Assert.Single(await CartAsync());
    }
    [Fact]
    public async Task Edit_preserves_other_data_and_totals_multiple_toppings()
    {
        await AddAsync(); await EditAsync();
        await page.GetByLabel("Số ly", new() { Exact = true }).FillAsync("2");
        await page.GetByLabel("Ghi chú", new() { Exact = true }).FillAsync("Keep this note");
        await page.Locator("[data-topping-id='2']").FillAsync("1"); await page.Locator("[data-topping-id='3']").FillAsync("2");
        await page.Locator("[data-editor-brew]").SelectOptionAsync("[100,0]");
        await Assertions.Expect(page.Locator("[data-editor-total]")).ToContainTextAsync("112.000");
        await page.Locator("[data-editor-save]").ClickAsync(); await Assertions.Expect(page.Locator("#cart-editor")).ToBeHiddenAsync();
        var item = Assert.Single(await CartAsync()); Assert.Equal("Keep this note", item.GetProperty("note").GetString());
        Assert.Equal("100%", item.GetProperty("sugar").GetString()); Assert.Equal("0%", item.GetProperty("ice").GetString());
        Assert.Equal(2, item.GetProperty("toppings").GetArrayLength());
        await Assertions.Expect(page.Locator("#cart-base-total")).ToHaveTextAsync("112.000đ");
    }
    [Fact]
    public async Task Cancel_edit_and_decline_changed_price_leave_original_bytes_unchanged()
    {
        await AddAsync(); var before = await RawAsync(); price = 47000; accept = false; await EditAsync();
        await page.Locator("[data-editor-save]").ClickAsync(); Assert.Equal(before, await RawAsync());
        await page.GetByRole(AriaRole.Button, new() { Name = "Đóng / bỏ chỉnh sửa" }).ClickAsync(); Assert.Equal(before, await RawAsync());
    }
    [Fact]
    public async Task Changed_price_is_saved_only_after_explicit_confirmation()
    {
        await AddAsync(); price = 47000; await EditAsync();
        await Assertions.Expect(page.Locator("[data-editor-total]")).ToContainTextAsync("47.000");
        await page.Locator("[data-editor-save]").ClickAsync(); await Assertions.Expect(page.Locator("#cart-editor")).ToBeHiddenAsync();
        Assert.Equal(47000, Assert.Single(await CartAsync()).GetProperty("listedPrice").GetInt32());
    }
    [Fact]
    public async Task Quantity_remove_and_clear_do_not_remove_pending_checkout()
    {
        await AddAsync(); await page.EvaluateAsync("localStorage.setItem('fourchang.checkout-intent.v1','pending-original')"); await OpenAsync("/Cart");
        await page.GetByRole(AriaRole.Button, new() { Name = "+", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator("#cart-base-total")).ToHaveTextAsync("90.000đ");
        await page.GetByRole(AriaRole.Button, new() { Name = "Xóa món", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator("#cart-empty")).ToBeVisibleAsync();
        await OpenAsync(); await AddAsync(); await OpenAsync("/Cart"); await page.Locator("#clear-cart").ClickAsync();
        Assert.Empty(await CartAsync()); Assert.Equal("pending-original", await page.EvaluateAsync<string>("localStorage.getItem('fourchang.checkout-intent.v1')"));
    }
    [Fact]
    public async Task Source_failure_never_overwrites_existing_draft()
    {
        await AddAsync(); var before = await RawAsync(); sourceError = true; await OpenAsync("/Cart");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sửa món", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator("#cart-editor [role='status']")).ToContainTextAsync("HTTP 503");
        await Assertions.Expect(page.Locator("[data-editor-save]")).ToBeDisabledAsync(); Assert.Equal(before, await RawAsync());
    }
    [Fact]
    public async Task Legacy_topping_requires_revalidation_and_preserves_note()
    {
        await SetRawAsync("""[{"productId":1,"variantId":1,"name":"Latte","size":"M","temperature":"Lạnh","sugar":"50%","ice":"50%","quantity":2,"listedPrice":45000,"topping":"Kem cheese","toppingQuantity":1,"note":"old note"}]""");
        await EditAsync(); await Assertions.Expect(page.GetByLabel("Ghi chú", new() { Exact = true })).ToHaveValueAsync("old note");
        await page.Locator("[data-editor-save]").ClickAsync(); await Assertions.Expect(page.Locator("#cart-editor")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#cart-base-total")).ToHaveTextAsync("100.000đ");
        Assert.Equal(2, Assert.Single(await CartAsync()).GetProperty("draftVersion").GetInt32());
    }
    [Fact]
    public async Task Other_tab_change_prevents_stale_editor_save()
    {
        await AddAsync(); await EditAsync();
        var tab = await context.NewPageAsync(); await tab.GotoAsync("http://localhost:18793/");
        await tab.EvaluateAsync("localStorage.setItem('fourchang.cart-draft.v1','[]')");
        await page.Locator("[data-editor-save]").ClickAsync();
        await Assertions.Expect(page.Locator("#cart-editor [role='status']")).ToContainTextAsync("Giỏ đã đổi"); Assert.Empty(await CartAsync());
    }
    [Fact]
    public async Task Same_price_but_changed_configuration_snapshot_never_merges()
    {
        await AddAsync(); limit = 3; await AddAsync(); Assert.Equal(2, (await CartAsync()).Length);
    }
    [Fact]
    public async Task Different_toppings_and_notes_are_separate_and_explicit_edit_merges_only_identical_rows()
    {
        await AddAsync(); await SelectAsync(); await page.Locator("[data-topping-id='2']").FillAsync("1");
        await page.Locator("[data-editor-save]").ClickAsync();
        await page.WaitForFunctionAsync("JSON.parse(localStorage.getItem('fourchang.cart-draft.v1')).length === 2");
        await page.GetByLabel("Ghi chú", new() { Exact = true }).FillAsync("different");
        await page.Locator("[data-editor-save]").ClickAsync();
        await page.WaitForFunctionAsync("JSON.parse(localStorage.getItem('fourchang.cart-draft.v1')).length === 3");
        await EditAsync(); await page.Locator("[data-topping-id='2']").FillAsync("1");
        await page.Locator("[data-editor-save]").ClickAsync(); await Assertions.Expect(page.Locator("#cart-editor")).ToBeHiddenAsync();
        var rows = await CartAsync(); Assert.Equal(2, rows.Length);
        Assert.Equal(2, rows[0].GetProperty("quantity").GetInt32()); Assert.Equal("different", rows[1].GetProperty("note").GetString());
    }
    [Fact]
    public async Task Changing_variant_requires_a_valid_pair_and_preserves_quantity_and_note()
    {
        extraVariant = true; await SelectAsync();
        await page.GetByLabel("Ghi chú", new() { Exact = true }).FillAsync("same note");
        await page.GetByLabel("Số ly", new() { Exact = true }).FillAsync("2");
        await page.Locator("[data-editor-variant]").SelectOptionAsync("4");
        await Assertions.Expect(page.Locator("[data-editor-save]")).ToBeDisabledAsync();
        await page.Locator("[data-editor-brew]").SelectOptionAsync("[100,0]");
        await page.Locator("[data-editor-save]").ClickAsync();
        await page.WaitForFunctionAsync("!!localStorage.getItem('fourchang.cart-draft.v1')");
        var row = Assert.Single(await CartAsync()); Assert.Equal(4, row.GetProperty("variantId").GetInt32());
        Assert.Equal("Nóng", row.GetProperty("temperature").GetString()); Assert.Equal("same note", row.GetProperty("note").GetString());
        Assert.Equal(2, row.GetProperty("quantity").GetInt32());
    }
    [Fact]
    public async Task Overflow_and_corrupt_storage_fail_closed_without_data_loss()
    {
        topPrice = 9007199254740991; await SelectAsync(); await page.Locator("[data-topping-id='2']").FillAsync("2");
        await Assertions.Expect(page.Locator("[data-editor-save]")).ToBeDisabledAsync(); Assert.Empty(await CartAsync());
        await SetRawAsync("{broken"); await OpenAsync("/Cart");
        await Assertions.Expect(page.Locator("#cart-status")).ToContainTextAsync("bị lỗi"); Assert.Equal("{broken", await RawAsync());
    }
    [Fact]
    public async Task Disabled_old_topping_requires_explicit_removal_before_save()
    {
        await SelectAsync(); await page.Locator("[data-topping-id='2']").FillAsync("1"); await page.Locator("[data-editor-save]").ClickAsync();
        await page.WaitForFunctionAsync("!!localStorage.getItem('fourchang.cart-draft.v1')"); toppingAvailable = false; await EditAsync();
        await Assertions.Expect(page.Locator("[data-editor-save]")).ToBeDisabledAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Bỏ topping này" }).ClickAsync();
        await page.Locator("[data-editor-save]").ClickAsync(); await Assertions.Expect(page.Locator("#cart-editor")).ToBeHiddenAsync();
        Assert.Equal(0, Assert.Single(await CartAsync()).GetProperty("toppings").GetArrayLength());
    }
    public async Task DisposeAsync() { if (context is not null) await context.DisposeAsync(); if (browser is not null) await browser.DisposeAsync(); playwright?.Dispose(); }
}
