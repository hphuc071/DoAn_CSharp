using FourChang.Presentation.Shared.ApiClients;

namespace FourChang.Web.Services;

/// <summary>Proxy công khai cùng origin cho menu/SSE; không chuyển token hoặc cấu hình SQL ra trình duyệt.</summary>
public static class CatalogUpdateEndpoints
{
    /// <summary>Đăng ký đọc snapshot và streaming sự kiện, giữ nguyên hợp đồng các API đặt hàng đang có.</summary>
    public static void MapCatalogUpdates(this WebApplication app)
    {
        app.MapGet("/catalog/snapshot", async (CafeApiClient api) =>
        {
            try { return Results.Ok(await api.GetCatalogAsync()); }
            catch (CafeApiException ex) { return Results.Problem(statusCode: (int?)ex.StatusCode ?? 503, detail: "Chưa tải được thực đơn mới. Giữ thông tin đang nhập và thử lại."); }
        });
        app.MapGet("/catalog/events", async (HttpContext context) =>
        {
            using var http = new HttpClient { BaseAddress = CafeApiClient.BaseAddress, Timeout = Timeout.InfiniteTimeSpan };
            try
            {
                using var response = await http.GetAsync("/api/catalog/events", HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
                if (!response.IsSuccessStatusCode) { context.Response.StatusCode = 503; return; }
                context.Response.ContentType = "text/event-stream"; context.Response.Headers.CacheControl = "no-cache";
                await context.Response.StartAsync(context.RequestAborted);
                await using var stream = await response.Content.ReadAsStreamAsync(context.RequestAborted);
                await stream.CopyToAsync(context.Response.Body, context.RequestAborted);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
            catch (HttpRequestException) { if (!context.Response.HasStarted) context.Response.StatusCode = 503; }
        });
    }
}
