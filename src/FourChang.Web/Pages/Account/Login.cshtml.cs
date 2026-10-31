using System.ComponentModel.DataAnnotations;
using FourChang.Presentation.Shared.Integration;
using FourChang.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace FourChang.Web.Pages.Account;
public sealed class LoginModel(CafeApiClient api):PageModel
{
    [BindProperty,Required,StringLength(254)]public string TenDangNhap {get;set;}="";
    [BindProperty,Required,StringLength(128)]public string MatKhau {get;set;}="";
    public async Task<IActionResult> OnPostAsync()
    {
        if(!ModelState.IsValid)return Page();
        try{var session=await api.LoginCustomerAsync(new(TenDangNhap,MatKhau));CustomerCookie.Set(Response,Request,session);return RedirectToPage("/Index");}
        catch(CafeApiException ex){ModelState.AddModelError("",ex.Message);MatKhau="";return Page();}
    }
}
