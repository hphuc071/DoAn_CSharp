using System.Net.Http.Json;
using System.Text.Json;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FourChang.Web.Pages.Checkout;

// UC-WEB-06: read-only checkout quote. Never creates an order or bank payment.
public sealed class IndexModel(CafeApiClient api) : PageModel
{
    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnGetVariantsAsync(long productId)
    {
        if (productId <= 0)
            return BadRequest(new { message = "Mã sản phẩm không hợp lệ." });

        try
        {
            var variants = await api.GetMenuVariantsAsync(productId);
            return new JsonResult(variants);
        }
        catch (CafeApiException ex)
        {
            return StatusCode(ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : 503,
                new { message = "Không lấy được cấu hình món từ Backend." });
        }
    }

    // Razor Pages validates the antiforgery token on unsafe methods by default.
    public async Task<IActionResult> OnPostQuoteAsync(CancellationToken ct)
    {
        CreateCustomerOrderRequest? request;
        try
        {
            request = await Request.ReadFromJsonAsync<CreateCustomerOrderRequest>(
                cancellationToken: ct);
        }
        catch (JsonException)
        {
            return BadRequest(new { message = "Dữ liệu báo giá không hợp lệ." });
        }
        catch (BadHttpRequestException)
        {
            return BadRequest(new { message = "Yêu cầu phải là JSON hợp lệ." });
        }

        if (request?.Items is null || request.Items.Count is < 1 or > 50)
            return BadRequest(new { message = "Giỏ hàng không hợp lệ." });

        // Do not price DELIVERY until a server-verified distance/fee exists;
        // do not price DINE_IN until a real table token is validated.
        if (!string.Equals(request.HinhThucNhanMon, "TAKEAWAY", StringComparison.Ordinal))
            return BadRequest(new { message = "Bản xem trước chỉ báo giá hình thức mang đi." });

        if (request.Items.Any(i => i.MaCauHinhMon <= 0 || i.SoLuong is < 1 or > 99 ||
            i.PhanTramDuong is < 0 or > 100 || i.PhanTramDa is < 0 or > 100 ||
            (i.Toppings?.Count ?? 0) > 10 ||
            (i.Toppings?.Any(t => t.MaMonThem <= 0 || t.SoPhanMoiLy < 1) ?? false)))
            return BadRequest(new { message = "Cấu hình món hoặc số lượng không hợp lệ." });

        try
        {
            var quote = await api.QuoteCustomerOrderAsync(request);
            return new JsonResult(quote);
        }
        catch (CafeApiException ex)
        {
            return StatusCode(ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : 503,
                new { message = "Backend không thể báo giá giỏ hàng. Vui lòng thử lại." });
        }
    }
}