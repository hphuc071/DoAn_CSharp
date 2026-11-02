using System.ComponentModel.DataAnnotations;
using FourChang.Presentation.Shared.Integration;
using FourChang.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace FourChang.Web.Pages.Account;

public sealed class ChangeTemporaryPasswordModel(CafeApiClient api) : PageModel
{
    [BindProperty, Required, StringLength(128, MinimumLength = 10)] public string MatKhauMoi { get; set; } = "";
    [BindProperty, Required, Compare(nameof(MatKhauMoi), ErrorMessage = "Xác nhận mật khẩu chưa khớp.")] public string XacNhanMatKhau { get; set; } = "";
    public async Task<IActionResult> OnGetAsync()
    {
        api.Token = Request.Cookies[CustomerCookie.Name];
        if (api.Token is null) return RedirectToPage("Login");
        try { await api.CheckTemporaryPasswordAsync(); return Page(); }
        catch (CafeApiException ex)
        {
            if (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            { Response.Cookies.Delete(CustomerCookie.Name); return RedirectToPage("Login"); }
            ModelState.AddModelError("", ex.Message); return Page();
        }
    }
    public async Task<IActionResult> OnPostAsync()
    {
        api.Token = Request.Cookies[CustomerCookie.Name];
        if (api.Token is null) return RedirectToPage("Login");
        if (!ModelState.IsValid) { ClearPasswords(); return Page(); }
        try
        {
            await api.ChangeTemporaryPasswordAsync(new(MatKhauMoi));
            Response.Cookies.Delete(CustomerCookie.Name); return RedirectToPage("Login");
        }
        catch (CafeApiException ex)
        {
            ModelState.AddModelError("", ex.Message + " Nếu mất phản hồi sau khi lưu, hãy thử đăng nhập bằng mật khẩu mới trước khi gửi lại.");
            ClearPasswords(); return Page();
        }
    }
    private void ClearPasswords()
    {
        // Không gửi ngược mật khẩu đã nhập trong HTML khi biểu mẫu có lỗi.
        foreach (var field in new[] { nameof(MatKhauMoi), nameof(XacNhanMatKhau) })
        {
            var errors = ModelState[field]?.Errors.Select(x => x.ErrorMessage).ToArray() ?? [];
            ModelState.Remove(field);
            foreach (var error in errors) ModelState.AddModelError("", string.IsNullOrEmpty(error) ? "Mật khẩu không hợp lệ." : error);
        }
        MatKhauMoi = ""; XacNhanMatKhau = "";
    }
}
