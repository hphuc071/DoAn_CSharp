using System.ComponentModel.DataAnnotations;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Customer;
using FourChang.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace FourChang.Web.Pages.Account;
public sealed class LoginModel(CafeApiClient api) : PageModel
{
    // Giữ mã lệnh qua timeout để thử lại không cấp thêm phiên hoặc tăng bộ đếm sai hai lần.
    [BindProperty] public Guid MaThaoTac { get; set; } = Guid.NewGuid();
    [BindProperty, Required, StringLength(254)] public string TenDangNhap { get; set; } = "";
    [BindProperty, Required, StringLength(128)] public string MatKhau { get; set; } = "";
    public async Task<IActionResult> OnPostAsync()
    {
        Response.Headers.CacheControl = "no-store";
        if (!ModelState.IsValid) { ModelState.Remove(nameof(MatKhau)); MatKhau = ""; return Page(); }
        try { var session = await api.LoginCustomerAsync(new(TenDangNhap, MatKhau, MaThaoTac)); CustomerCookie.Set(Response, Request, session); return session.MustChangePassword ? RedirectToPage("ChangeTemporaryPassword") : RedirectToPage("/Menu/Index"); }
        catch (CafeApiException ex) { ModelState.AddModelError("", ex.Message); ModelState.Remove(nameof(MatKhau)); MatKhau = ""; if (ex.StatusCode is not null && (int)ex.StatusCode < 500) { ModelState.Remove(nameof(MaThaoTac)); MaThaoTac = Guid.NewGuid(); } return Page(); }
    }
}
