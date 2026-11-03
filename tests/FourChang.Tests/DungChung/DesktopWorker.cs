using System.Reflection;

namespace FourChang.Tests.Shared;

/// <summary>
/// Chạy giao diện trong process con của chính assembly test.
/// Mỗi process có LiveSession và message loop riêng, không sửa mã ứng dụng để phục vụ test.
/// </summary>
public static class DesktopWorker
{

    /// <summary>
    /// Mở worker từ chính assembly test, truyền cấu hình qua môi trường process con và báo lỗi WinForms về xUnit.
    /// </summary>
    public static async Task Run(TestApi api, string useCase, Dictionary<string, string?> values)
    {
        var environment = api.ChildEnvironment();
        foreach (var (key, value) in values)
        {
            environment[key] = value;
        }
        var log = Path.Combine(api.Artifacts, "winforms-worker");
        await using var worker = new ChildProcess("dotnet", [Assembly.GetExecutingAssembly().Location, "--desktop-scenario", useCase, api.Artifacts], TestApi.Root, environment, log);
        await worker.Wait();
        // Chỉ in lỗi assertion sau thất bại; file log không chứa request body hoặc bí mật.
        var detail = worker.ExitCode == 0 ? "" : await File.ReadAllTextAsync(log + ".stderr.log");
        Assert.True(worker.ExitCode == 0, $"WinForms {useCase} không đạt. {detail}");
    }
}
