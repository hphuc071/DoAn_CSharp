using System.Net.Http.Headers;
using System.Text.Json;
using FourChang.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FourChang.Presentation.Shared.Integration;
namespace FourChang.Web.Pages.Account;
public sealed class ProfileModel(IHttpClientFactory factory):PageModel
{
    public string HoTen{get;private set;}="";public string Email{get;private set;}="";public string DienThoai{get;private set;}="";public string? Error{get;private set;}
    public async Task<IActionResult> OnGetAsync()
    {
        var token=Request.Cookies[CustomerCookie.Name];if(token is null)return RedirectToPage("Login");
        using var http=factory.CreateClient();using var request=new HttpRequestMessage(HttpMethod.Get,new Uri(CafeApiClient.BaseAddress,"/api/customer/profile"));request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        try
        {
            using var response=await http.SendAsync(request);if(response.StatusCode==System.Net.HttpStatusCode.Unauthorized){Response.Cookies.Delete(CustomerCookie.Name);return RedirectToPage("Login");}
            if(!response.IsSuccessStatusCode){Error="Chưa tải được hồ sơ. Vui lòng thử lại.";return Page();}
            var data=await response.Content.ReadFromJsonAsync<JsonElement>();HoTen=data.GetProperty("hoTen").GetString()??"";Email=data.GetProperty("emailChuanHoa").GetString()??"";DienThoai=data.GetProperty("soDienThoaiChuanHoa").GetString()??"";
        }
        catch(HttpRequestException){Error="Không kết nối được backend.";}
        return Page();
    }
}
