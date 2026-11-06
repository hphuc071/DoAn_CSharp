using FourChang.Presentation.Shared.ApiClients;
namespace FourChang.Web.Services;
/// <summary>Proxy cùng origin để browser không cần URL backend/CORS; không chuyển token/cookie khách vào feed công khai.</summary>
public static class CatalogProxy
{
    /// <summary>Snapshot và SSE chỉ đọc; ngắt request sẽ đóng stream backend để không giữ kết nối thừa.</summary>
    public static void MapCatalogProxy(this WebApplication app)
    {
        app.MapGet("/catalog/data",async(CafeApiClient api,HttpContext c)=>
        {
            try{c.Response.Headers.CacheControl="no-store";return Results.Ok(await api.GetCatalogAsync(c.RequestAborted));}
            catch(CafeApiException ex){return Results.Problem(statusCode:503,detail:ex.Message);}
        });
        app.MapGet("/catalog/events",async(HttpContext c,IHttpClientFactory factory)=>
        {
            using var http=factory.CreateClient();http.Timeout=Timeout.InfiniteTimeSpan;
            try
            {
                using var request=new HttpRequestMessage(HttpMethod.Get,new Uri(CafeApiClient.BaseAddress,"/api/catalog/events"));
                using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,c.RequestAborted);response.EnsureSuccessStatusCode();
                c.Response.ContentType="text/event-stream";c.Response.Headers.CacheControl="no-cache";c.Response.Headers["X-Accel-Buffering"]="no";
                await using var source=await response.Content.ReadAsStreamAsync(c.RequestAborted);
                var buffer=new byte[1024];int read;
                while((read=await source.ReadAsync(buffer,c.RequestAborted))>0){await c.Response.Body.WriteAsync(buffer.AsMemory(0,read),c.RequestAborted);await c.Response.Body.FlushAsync(c.RequestAborted);}
            }
            catch(OperationCanceledException)when(c.RequestAborted.IsCancellationRequested){}
            catch(HttpRequestException){if(!c.Response.HasStarted)c.Response.StatusCode=503;}
        });
    }
}
