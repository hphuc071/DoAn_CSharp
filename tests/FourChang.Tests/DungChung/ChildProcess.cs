using System.Diagnostics;

namespace FourChang.Tests.Shared;

// Chỉ quản lý các process do test tạo. ArgumentList tránh lỗi đường dẫn có dấu cách/#.
public sealed class ChildProcess : IAsyncDisposable
{
    private readonly Process process;
    private readonly Task stdout;
    private readonly Task stderr;
    public bool HasExited => process.HasExited;
    public int ExitCode => process.ExitCode;

    /// <summary>
    /// Mở process do test sở hữu với ArgumentList và môi trường riêng; chuyển stdout/stderr tới file bằng chứng.
    /// </summary>
    public ChildProcess(string file, string[] args, string root, Dictionary<string, string?> env, string log)
    {
        var info = new ProcessStartInfo(file)
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        foreach (var (key, value) in env)
        {
            if (value is null)
            {
                info.Environment.Remove(key);
            }
            else
            {
                info.Environment[key] = value;
            }
        }

        process = Process.Start(info) ?? throw new InvalidOperationException("Không mở được process kiểm thử.");
        stdout = Copy(process.StandardOutput, log + ".stdout.log");
        stderr = Copy(process.StandardError, log + ".stderr.log");
    }

    /// <summary>
    /// Đọc liên tục stdout hoặc stderr vào file log để process con không bị treo do bộ đệm đầu ra đầy.
    /// </summary>
    private static async Task Copy(StreamReader reader, string path)
    {
        await using var writer = new StreamWriter(path);
        while (await reader.ReadLineAsync() is { } line)
        {
            await writer.WriteLineAsync(line);
        }
    }

    /// <summary>
    /// Chờ thao tác hoàn tất trong giới hạn thời gian; để lỗi gốc được báo về test thay vì tiếp tục khi dữ liệu chưa sẵn sàng.
    /// </summary>
    public async Task Wait()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await process.WaitForExitAsync(timeout.Token);
        await Task.WhenAll(stdout, stderr);
    }

    /// <summary>
    /// Kết thúc đúng process con do đối tượng này mở, chờ thu đủ log rồi giải phóng handle của process.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }

        await process.WaitForExitAsync();
        await Task.WhenAll(stdout, stderr);
        process.Dispose();
    }
}
