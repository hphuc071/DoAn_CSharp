using System.Net.Http.Headers;
using System.Text.Json;
using FourChang.Web.Security;
using FourChang.Presentation.Shared.Integration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace FourChang.Web.Pages.Orders;
public sealed class IndexModel(IHttpClientFactory factory):PageModel
{
    public List<JsonElement> Orders{get;private set;}=[];public string? Error{get;private set;}
    public async Task<IActionResult> OnGetAsync()
    {
        var token=Request.Cookies[CustomerCookie.Name];if(token is null)return RedirectToPage("/Account/Login");
        using var http=factory.CreateClient();using var request=new HttpRequestMessage(HttpMethod.Get,new Uri(CafeApiClient.BaseAddress,"/api/customer/orders"));request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        try{using var response=await http.SendAsync(request);if(response.StatusCode==System.Net.HttpStatusCode.Unauthorized)return RedirectToPage("/Account/Login");if(response.IsSuccessStatusCode)Orders=await response.Content.ReadFromJsonAsync<List<JsonElement>>()??[];else Error="Chưa tải được đơn hàng.";}catch(HttpRequestException){Error="Không kết nối được backend.";}
        return Page();
    }
}
