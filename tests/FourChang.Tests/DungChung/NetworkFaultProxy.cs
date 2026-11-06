using System.Net;

namespace FourChang.Tests.Shared;

// Proxy chuyển tiếp API thật; chỉ làm mất phản hồi sau commit hoặc mô phỏng mất mạng.
// Không thay kết quả nghiệp vụ và không ghi mật khẩu/token vào log kiểm thử.
public sealed class NetworkFaultProxy : IAsyncDisposable
{
    private readonly CancellationTokenSource lifetime = new();
    private readonly HttpListener listener = new();
    private readonly HttpClient client = new();
    private readonly string upstream;
    private readonly Task loop;
    private readonly List<Task> requests = [];
    public string Url { get; } = TestApi.FreeUrl();

    public volatile bool Offline;
    public volatile bool DropMenu;
    public volatile bool DropProfile;
    public volatile bool DropWorkforce;
    public volatile bool DropTemporary;
    public int ProfileWrites;
    public int TemporaryWrites;

    /// <summary>
    /// Mở proxy ở cổng trống để chuyển request tới API test và mô phỏng lỗi mạng có kiểm soát.
    /// </summary>
    public NetworkFaultProxy(string upstream)
    {
        this.upstream = upstream;
        listener.Prefixes.Add(Url + "/");
        listener.Start();
        loop = Listen();
    }

    /// <summary>
    /// Nhận các request đến proxy và theo dõi tác vụ chuyển tiếp để có thể dọn sạch khi kết thúc test.
    /// </summary>
    private async Task Listen()
    {
        try
        {
            while (listener.IsListening)
            {
                var context = await listener.GetContextAsync();
                requests.Add(Forward(context));
            }
        }
        catch (HttpListenerException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <summary>
    /// Chuyển request tới API thật; khi kịch bản yêu cầu, cắt phản hồi sau commit để kiểm tra cơ chế phục hồi.
    /// </summary>
    private async Task Forward(HttpListenerContext context)
    {
        try
        {
            if (Offline)
            {
                context.Response.StatusCode = 503;
                context.Response.Close();
                return;
            }

            var path = context.Request.Url!.PathAndQuery;
            var profile = context.Request.HttpMethod == "PUT" && path == "/api/customer/profile";
            var temporary = context.Request.HttpMethod == "POST" && path == "/api/auth/customer/temporary-password";
            if (profile)
            {
                Interlocked.Increment(ref ProfileWrites);
            }

            if (temporary)
            {
                Interlocked.Increment(ref TemporaryWrites);
            }

            var workforce = context.Request.HttpMethod == "POST" && path == "/api/admin/employees";
            var menu = path.StartsWith("/api/admin/menu/") && context.Request.HttpMethod is "POST" or "PUT";
            var drop = (DropMenu && menu) || (DropProfile && profile) || (DropTemporary && temporary) || (DropWorkforce && workforce);
            if (drop)
            {
                DropMenu = false;
                DropWorkforce = false;
                DropProfile = false;
                DropTemporary = false;
            }

            using var request = new HttpRequestMessage(new HttpMethod(context.Request.HttpMethod), upstream + path);
            if (context.Request.HasEntityBody)
            {
                using var memory = new MemoryStream();
                await context.Request.InputStream.CopyToAsync(memory);
                request.Content = new ByteArrayContent(memory.ToArray());
                request.Content.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType ?? "application/json");
            }

            if (context.Request.Headers["Authorization"] is { } authorization)
            {
                request.Headers.TryAddWithoutValidation("Authorization", authorization);
            }

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, lifetime.Token);
            // SSE không được buffer toàn bộ: test phải quan sát đúng thông báo realtime từ API thật.
            if (path == "/api/catalog/events")
            {
                context.Response.StatusCode = (int)response.StatusCode;
                context.Response.ContentType = "text/event-stream"; context.Response.SendChunked = true;
                await using var stream = await response.Content.ReadAsStreamAsync(lifetime.Token);
                var buffer = new byte[1024]; int read;
                while ((read = await stream.ReadAsync(buffer, lifetime.Token)) > 0)
                {
                    await context.Response.OutputStream.WriteAsync(buffer.AsMemory(0, read), lifetime.Token);
                    await context.Response.OutputStream.FlushAsync(lifetime.Token);
                }
                context.Response.Close(); return;
            }
            var bytes = await response.Content.ReadAsByteArrayAsync();
            context.Response.StatusCode = (int)response.StatusCode;
            context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
            // Chủ động kết thúc response thiếu byte: ASP.NET Web nhận lỗi vận chuyển sau API đã commit.
            context.Response.ContentLength64 = drop ? bytes.Length + 100 : bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
            if (drop)
            {
                context.Response.Abort();
            }
            else
            {
                context.Response.Close();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or HttpListenerException or IOException or ObjectDisposedException or OperationCanceledException)
        {
            try
            {
                context.Response.Abort();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    /// <summary>
    /// Ngừng nhận request, chờ các tác vụ chuyển tiếp kết thúc rồi đóng HttpClient để không rò rỉ kết nối.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        listener.Stop();
        listener.Close();
        await loop;
        await Task.WhenAll(requests);
        client.Dispose();
        lifetime.Dispose();
    }
}
