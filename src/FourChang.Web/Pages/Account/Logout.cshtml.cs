using FourChang.Presentation.Shared.Integration;
using FourChang.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace FourChang.Web.Pages.Account;
public sealed class LogoutModel(CafeApiClient api):PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        api.Token=Request.Cookies[CustomerCookie.Name];
        if(api.Token is not null){try{await api.LogoutAsync();}catch(CafeApiException ex){ModelState.AddModelError("",ex.Message);return Page();}}
        Response.Cookies.Delete(CustomerCookie.Name);return RedirectToPage("/Index");
    }
}
