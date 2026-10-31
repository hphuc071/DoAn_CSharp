using System.ComponentModel.DataAnnotations;
using FourChang.Presentation.Shared.Integration;
using FourChang.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace FourChang.Web.Pages.Account;
public sealed class RegisterModel(CafeApiClient api):PageModel
{
    [BindProperty,Required,StringLength(100,MinimumLength=3)]public string TenDangNhap {get;set;}="";
    [BindProperty,Required,StringLength(120)]public string HoTen {get;set;}="";
    [BindProperty,Required,EmailAddress,StringLength(254)]public string Email {get;set;}="";
    [BindProperty,Required,StringLength(128,MinimumLength=10)]public string MatKhau {get;set;}="";
    [BindProperty,Compare(nameof(MatKhau))]public string XacNhanMatKhau {get;set;}="";
    public async Task<IActionResult> OnPostAsync()
    {
        if(!ModelState.IsValid)return Page();
        try{var session=await api.RegisterAsync(new(TenDangNhap,HoTen,Email,null,MatKhau));CustomerCookie.Set(Response,Request,session);return RedirectToPage("/Index");}
        catch(CafeApiException ex){ModelState.AddModelError("",ex.Message);MatKhau=XacNhanMatKhau="";return Page();}
    }
}
