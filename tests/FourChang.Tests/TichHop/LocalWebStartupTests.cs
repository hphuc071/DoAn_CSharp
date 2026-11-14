using System.Net;
using System.Net.Sockets;
using FourChang.Tests.Shared;

namespace FourChang.Tests.Integration;

/// <summary>Chạy script thật trên cổng test riêng; không tắt hoặc đổi cổng Web người dùng đang chạy.</summary>
public sealed class LocalWebStartupTests
{
    /// <summary>Hai lần chạy kể cả lúc chưa bind chỉ tạo một Web; lần sau báo sẵn sàng và thoát thành công.</summary>
    [Fact, Trait("Feature", "LocalWebStartup")]
    public async Task ChayHaiLan_DungChungWeb_KhongBaoTrungCong()
    {
        var root = TestApi.FindRoot(); var url = TestApi.FreeUrl();
        var output = Path.Combine(root, "artifacts", "tests", "local-web-startup", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(output);
        var environment = EnvironmentFor(url);
        await using var first = Start(root, environment, Path.Combine(output, "first"));
        await using var second = Start(root, environment, Path.Combine(output, "second"));
        // Windows có thể cho lần bấm thứ hai lấy mutex trước; chỉ cần đúng một process giữ Web.
        var completed = await Task.WhenAny(first.Wait(), second.Wait());
        await completed;
        Assert.True(first.HasExited ^ second.HasExited);
        var reused = first.HasExited ? "first" : "second";
        Assert.Equal(0, first.HasExited ? first.ExitCode : second.ExitCode);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
        Assert.Contains("4CHANG", await http.GetStringAsync(url + "/Account/Login"));
        Assert.Contains("khong khoi dong them", await File.ReadAllTextAsync(Path.Combine(output, reused + ".stdout.log")));
        Assert.DoesNotContain("address already in use", await File.ReadAllTextAsync(Path.Combine(output, reused + ".stderr.log")));
    }

    /// <summary>Cổng của chương trình khác phải được báo đúng, không mở thêm Web và không tự kill chương trình đó.</summary>
    [Fact, Trait("Feature", "LocalWebStartup")]
    public async Task CongCuaUngDungKhac_BaoLoiRoRang_KhongDungUngDung()
    {
        var root = TestApi.FindRoot();
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var output = Path.Combine(root, "artifacts", "tests", "local-web-startup", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(output);
            await using var process = Start(root, EnvironmentFor("http://127.0.0.1:" + port), Path.Combine(output, "occupied"));
            await process.Wait();
            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains("ung dung khac", await File.ReadAllTextAsync(Path.Combine(output, "occupied.stderr.log")));
            using var probe = new TcpClient(); await probe.ConnectAsync(IPAddress.Loopback, port);
            Assert.True(probe.Connected);
        }
        finally { listener.Stop(); }
    }

    /// <summary>Chỉ ghi đè URL/cấu hình Web của process test, không truyền mật khẩu SQL.</summary>
    private static Dictionary<string, string?> EnvironmentFor(string url) => new()
    {
        ["FOURCHANG_WEB_URL"] = url, ["FOURCHANG_API_URL"] = "http://127.0.0.1:9", ["ASPNETCORE_ENVIRONMENT"] = "Development"
    };

    /// <summary>ArgumentList của ChildProcess giữ nguyên đường dẫn chứa dấu cách/#; process test được dọn theo đúng cây sở hữu.</summary>
    private static ChildProcess Start(string root, Dictionary<string, string?> environment, string output) =>
        new("powershell", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(root, "scripts", "Start-LocalWeb.ps1")], root, environment, output);
}
