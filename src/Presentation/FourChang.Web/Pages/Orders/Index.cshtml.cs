
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FourChang.Web.Security;
using FourChang.Presentation.Shared.ApiClients;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FourChang.Web.Pages.Orders;

public sealed class IndexModel(
    IHttpClientFactory factory
) : PageModel
{
    public List<JsonElement> Orders { get; private set; } = [];

    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var token = Request.Cookies[CustomerCookie.Name];

        if (string.IsNullOrWhiteSpace(token))
            return RedirectToPage("/Account/Login");

        using var http = factory.CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(CafeApiClient.BaseAddress, "/api/customer/orders")
        );

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var response = await http.SendAsync(request);

            if (response.StatusCode ==
                System.Net.HttpStatusCode.Unauthorized)
                return RedirectToPage("/Account/Login");

            if (response.IsSuccessStatusCode)
            {
                Orders = await response.Content
                    .ReadFromJsonAsync<List<JsonElement>>() ?? [];
            }
            else
            {
                Error = "Chưa tải được đơn hàng.";
            }
        }
        catch (HttpRequestException)
        {
            Error = "Không kết nối được Backend.";
        }

        return Page();
    }

    // Trả JSON cho chức năng cập nhật trạng thái 15 giây.
    public async Task<IActionResult> OnGetStatusAsync(
        CancellationToken ct)
    {
        var token = Request.Cookies[CustomerCookie.Name];

        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized();

        using var http = factory.CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(CafeApiClient.BaseAddress, "/api/customer/orders")
        );

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var response = await http.SendAsync(request, ct);

            if (response.StatusCode ==
                System.Net.HttpStatusCode.Unauthorized ||
                response.StatusCode ==
                System.Net.HttpStatusCode.Forbidden)
                return Unauthorized();

            if (!response.IsSuccessStatusCode)
                return StatusCode(503, new
                {
                    message = "Không tải được trạng thái đơn hàng."
                });

            var orders = await response.Content
                .ReadFromJsonAsync<List<JsonElement>>(
                    cancellationToken: ct
                ) ?? [];

            return new JsonResult(orders);
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new
            {
                message = "Không kết nối được Backend."
            });
        }
        catch (OperationCanceledException)
            when (!ct.IsCancellationRequested)
        {
            return StatusCode(504, new
            {
                message = "Backend phản hồi quá lâu."
            });
        }
    }

    // POST /Orders?handler=Cancel - proxy có chống CSRF của Razor Pages.
    public async Task<IActionResult> OnPostCancelAsync(
        long orderId, string reason, CancellationToken ct)
    {
        var token = Request.Cookies[CustomerCookie.Name];
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized();
        if (orderId <= 0 || string.IsNullOrWhiteSpace(reason) ||
            reason.Trim().Length < 3 || reason.Trim().Length > 400)
            return BadRequest(new { message = "Lý do hủy không hợp lệ." });

        using var http = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(CafeApiClient.BaseAddress,
                $"/api/customer/orders/{orderId}/cancel"));
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { reason = reason.Trim() });

        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
              return new UnauthorizedObjectResult(new
{
    message = "Phiên đăng nhập không hợp lệ."
});

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: ct);
            if (!response.IsSuccessStatusCode)
            {
                string message = "Không hủy được đơn; vui lòng tải lại trạng thái.";
                if (result.ValueKind == JsonValueKind.Object &&
                    result.TryGetProperty("message", out var property) &&
                    property.ValueKind == JsonValueKind.String)
                    message = property.GetString() ?? message;
                return StatusCode((int)response.StatusCode, new { message });
            }
            return new JsonResult(result);
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { message = "Không kết nối được Backend." });
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return StatusCode(504, new { message = "Backend phản hồi quá lâu." });
        }
        catch (System.Text.Json.JsonException)
        {
            return StatusCode(502, new { message = "Backend trả dữ liệu không hợp lệ." });
        }
    }

}
