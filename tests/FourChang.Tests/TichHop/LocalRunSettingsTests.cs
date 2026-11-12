using System.Text.Json;
using FourChang.Tests.Shared;

namespace FourChang.Tests;

/// <summary>Kiểm tra URL dùng chung của các lệnh chạy bằng PowerShell thật, không cần SQL hoặc sửa .env dự án.</summary>
[Trait("Feature", "LocalRun"), Trait("Layer", "Integration")]
public sealed class LocalRunSettingsTests
{
    /// <summary>Không có cấu hình vẫn chạy đúng cổng mới; .env và biến môi trường có thứ tự ưu tiên rõ ràng.</summary>
    [Theory]
    [InlineData("", null, "http://localhost:15881", "http://localhost:15880")]
    [InlineData("FOURCHANG_API_URL=http://localhost:16881\nFOURCHANG_WEB_URL=http://localhost:16880\n", null, "http://localhost:16881", "http://localhost:16880")]
    [InlineData("FOURCHANG_API_URL=http://localhost:16881\n", "http://localhost:17881", "http://localhost:17881", "http://localhost:15880")]
    public async Task CacLenhChay_DocCungUrl(string content, string? environment, string api, string web)
    {
        var result = await Resolve(content + "\nSQL_SA_PASSWORD=DO_NOT_PRINT_THIS_VALUE\n", environment);
        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("DO_NOT_PRINT_THIS_VALUE", result.Output);
        using var settings = JsonDocument.Parse(result.Output);
        Assert.Equal(api, settings.RootElement.GetProperty("ApiUrl").GetString());
        Assert.Equal(web, settings.RootElement.GetProperty("WebUrl").GetString());
    }

    /// <summary>Script local không dùng URL sai định dạng, nhúng mật khẩu hoặc máy bên ngoài từ cấu hình.</summary>
    [Theory]
    [InlineData("https://example.test:15881")]
    [InlineData("http://user:secret@localhost:15881")]
    [InlineData("http://localhost:15881/path?token=secret")]
    public async Task UrlKhongPhuHop_BiChan(string url)
    {
        var result = await Resolve("FOURCHANG_API_URL=" + url, null);
        Assert.NotEqual(0, result.ExitCode);
        Assert.DoesNotContain("secret", result.Output);
    }

    /// <summary>Tạo cấu hình fixture riêng, gọi helper thực tế và chỉ đọc stdout; không đọc mật khẩu .env của quán.</summary>
    private static async Task<(int ExitCode, string Output)> Resolve(string content, string? environment)
    {
        var root = Path.Combine(TestApi.Root, "artifacts", "tests", "local-run", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, ".env"), content);
        var script = Path.Combine(TestApi.Root, "scripts", "Get-LocalRunSettings.ps1");
        var log = Path.Combine(root, "settings");
        var command = "& " + Quote(script) + " -Root " + Quote(root) + " | ConvertTo-Json -Compress";
        await using var process = new ChildProcess("powershell", ["-NoProfile", "-Command", command], TestApi.Root,
            new() { ["FOURCHANG_API_URL"] = environment, ["FOURCHANG_WEB_URL"] = null }, log);
        await process.Wait();
        return (process.ExitCode, await File.ReadAllTextAsync(log + ".stdout.log"));
    }

    /// <summary>Quote đối số PowerShell để đường dẫn thư mục có khoảng trắng/dấu nháy không thành lệnh khác.</summary>
    private static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
}
