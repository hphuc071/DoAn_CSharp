using FourChang.Presentation.Shared.ApiClients;
using FourChang.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace FourChang.Web.Pages.Account;

// Đăng xuất xóa cookie cục bộ ngay; nếu backend mất mạng, xếp thu hồi lại bằng cookie được bảo vệ.
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LogoutModel(CafeApiClient api, PendingCustomerLogout pending) : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        api.Token = Request.Cookies[CustomerCookie.Name]; Response.Cookies.Delete(CustomerCookie.Name);
        if (api.Token is not null)
        {
            try { await api.LogoutAsync(); }
            catch (CafeApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized) { }
            catch (CafeApiException) { pending.Queue(HttpContext, api.Token); }
        }
        Response.Headers.CacheControl = "no-store"; return RedirectToPage("/Menu/Index");
    }
}
