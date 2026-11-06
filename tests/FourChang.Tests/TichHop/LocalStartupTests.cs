using System.Net;
using System.Net.Sockets;
using FourChang.Tests.Shared;

namespace FourChang.Tests.Integration;

/// <summary>Kiểm tra launcher local: nhận lại API sẵn sàng và phân biệt cổng bị chiếm với lỗi database.</summary>
public sealed class LocalStartupTests
{
    /// <summary>Cổng đang được một socket khác giữ phải báo đúng lỗi, không khởi động API hoặc thay database.</summary>
    [Fact, Trait("Actor", "TichHop"), Trait("Layer", "Startup")]
    public async Task CongBiChiem_BaoLoiCongThayViBatKhoiTaoDatabase()
    {
        var output = Path.Combine(TestApi.Root, "artifacts", "tests", "startup", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var log = Path.Combine(output, "busy-port");
            await using var child = new ChildProcess("powershell", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(TestApi.Root, "scripts", "Start-LocalBackend.ps1"), "-ApiUrl", $"http://127.0.0.1:{port}"], TestApi.Root, new(), log);
            await child.Wait();
            Assert.NotEqual(0, child.ExitCode);
            Assert.Contains("Khong mo duoc cong API", await File.ReadAllTextAsync(log + ".stderr.log"));
        }
        finally { listener.Stop(); }
    }

    /// <summary>API của fixture đang ready phải được dùng lại ngay, không mở process trùng cổng.</summary>
    [Fact, Trait("Actor", "TichHop"), Trait("Layer", "Startup")]
    public async Task BackendDaSanSang_DuocTaiSuDung()
    {
        await using var api = await TestApi.Start("Startup-Ready");
        var log = Path.Combine(api.Artifacts, "already-ready");
        await using var child = new ChildProcess("powershell", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(TestApi.Root, "scripts", "Start-LocalBackend.ps1"), "-ApiUrl", api.Url], TestApi.Root, api.ChildEnvironment(), log);
        await child.Wait();
        Assert.True(child.ExitCode == 0, await File.ReadAllTextAsync(log + ".stderr.log"));
        Assert.Contains("Backend san sang", await File.ReadAllTextAsync(log + ".stdout.log"));
        Assert.Equal(200, (await api.Send("GET", "/health/ready")).Status);
    }
}
