using System.Text.Json;
using Microsoft.Playwright;

namespace FourChang.Tests.Shared;

/// <summary>Thao tác như khách hàng trên các trường riêng; mã cấu hình chỉ dùng để chọn đúng dữ liệu fixture.</summary>
public static class CartEditorActions
{
    /// <summary>Đọc size/nhiệt độ của mã cấu hình fixture rồi chọn hai trường hiển thị bằng Playwright.</summary>
    public static async Task Variant(IPage page, string id)
    {
        var option = page.Locator($"[data-editor-variant] option[value='{id}']");
        await option.WaitForAsync(new() { State = WaitForSelectorState.Attached });
        var size = await option.GetAttributeAsync("data-size");
        var temperature = await option.GetAttributeAsync("data-temperature");
        await page.Locator("[data-editor-size]").SelectOptionAsync(size!);
        await page.Locator("[data-editor-temperature]").SelectOptionAsync(temperature!);
    }

    /// <summary>Chọn đường và đá riêng; với món nóng xác nhận ô đá được ẩn và định mức đá bằng 0.</summary>
    public static async Task Brew(IPage page, string value)
    {
        var pair = JsonSerializer.Deserialize<int[]>(value)!;
        await page.Locator("[data-editor-sugar]").SelectOptionAsync(pair[0].ToString());
        if (await page.Locator("[data-editor-ice]").IsVisibleAsync())
            await page.Locator("[data-editor-ice]").SelectOptionAsync(pair[1].ToString());
        else
            Assert.Equal(0, pair[1]);
    }
}
