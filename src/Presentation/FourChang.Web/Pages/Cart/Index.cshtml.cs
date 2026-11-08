
using System.Net.Http.Json;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FourChang.Web.Pages.Cart;

public sealed class IndexModel(CafeApiClient api) : PageModel
{
    private static readonly HttpClient BackendHttp = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public async Task<IActionResult> OnGetCatalogAsync(
        CancellationToken ct)
    {
        try
        {
            var menu = await api.GetMenuAsync(ct);

            return new JsonResult(menu.Select(x => new
            {
                productId = x.MaMon,
                variantId = x.Id,
                name = x.Name,
                price = x.Price,
                available = x.Available
            }).ToArray());
        }
        catch (CafeApiException)
        {
            return new ObjectResult(new
            {
                message = "Không lấy được thực đơn từ Backend."
            })
            {
                StatusCode = 503
            };
        }
    }

    public async Task<IActionResult> OnGetVariantsAsync(
        long productId,
        CancellationToken ct)
    {
        if (productId <= 0)
        {
            return new BadRequestObjectResult(new
            {
                message = "Mã sản phẩm không hợp lệ."
            });
        }

        try
        {
            var url = new Uri(
                CafeApiClient.BaseAddress,
                $"/api/menu/{productId}/variants"
            );

            using var response =
                await BackendHttp.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                return new ObjectResult(new
                {
                    message = "Không lấy được biến thể món."
                })
                {
                    StatusCode = (int)response.StatusCode
                };
            }

            var variants = await response.Content
                .ReadFromJsonAsync<List<MenuVariantItem>>(
                    cancellationToken: ct
                );

            return new JsonResult(
                variants ?? new List<MenuVariantItem>()
            );
        }
        catch (HttpRequestException)
        {
            return new ObjectResult(new
            {
                message = "Không kết nối được Backend."
            })
            {
                StatusCode = 503
            };
        }
        catch (System.Text.Json.JsonException)
        {
            return new ObjectResult(new { message = "Dữ liệu biến thể từ Backend không hợp lệ." }) { StatusCode = 502 };
        }
        catch (OperationCanceledException)
            when (!ct.IsCancellationRequested)
        {
            return new ObjectResult(new
            {
                message = "Backend phản hồi quá lâu."
            })
            {
                StatusCode = 504
            };
        }
    }
}
