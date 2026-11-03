using System.ComponentModel.DataAnnotations;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace FourChang.Web.Pages.Account;

// Chỉ hoàn tất mật khẩu tạm. Không nâng phiên hạn chế thành phiên Customer đầy đủ.
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ChangeTemporaryPasswordModel(CafeApiClient api) : PageModel
{
    [BindProperty] public Guid MaThaoTac { get; set; } = Guid.NewGuid();
    [BindProperty] public string PhienBan { get; set; } = "";
    [BindProperty] public bool ChuaRoKetQua { get; set; }
    [BindProperty, Required, StringLength(128, MinimumLength = 10)] public string MatKhauMoi { get; set; } = "";
    [BindProperty, Required, Compare(nameof(MatKhauMoi), ErrorMessage = "Xác nhận mật khẩu chưa khớp.")] public string XacNhanMatKhau { get; set; } = "";
    public async Task<IActionResult> OnGetAsync()
    {
        api.Token = Request.Cookies[CustomerCookie.Name]; if (api.Token is null) return RedirectToPage("Login");
        try { var context = await api.CheckTemporaryPasswordAsync(); PhienBan = context.GetProperty("phienBanDuLieu").GetString()!; return Page(); }
        catch (CafeApiException ex) { return Failure(ex); }
    }
    public async Task<IActionResult> OnPostAsync()
    {
        api.Token = Request.Cookies[CustomerCookie.Name]; if (api.Token is null) return RedirectToPage("Login");
        try
        {
            // Sau mất phản hồi, token nguồn có thể đã bị thu hồi; receipt chỉ xác nhận đúng lệnh của nó.
            if (ChuaRoKetQua && await api.GetSelfOperationAsync(MaThaoTac) is not null) { Response.Cookies.Delete(CustomerCookie.Name); return RedirectToPage("Login"); }
            if (!ModelState.IsValid) { ClearPasswords(); return Page(); }
            await api.ChangeTemporaryPasswordAsync(new(MatKhauMoi, MaThaoTac, Convert.FromBase64String(PhienBan), XacNhanMatKhau));
            Response.Cookies.Delete(CustomerCookie.Name); return RedirectToPage("Login");
        }
        catch (FormatException) { ModelState.AddModelError("", "Phiên bản không hợp lệ. Tải lại và đối chiếu."); ClearPasswords(); return Page(); }
        catch (CafeApiException ex)
        {
            ChuaRoKetQua = ex.StatusCode is null || (int)ex.StatusCode >= 500; ModelState.Remove(nameof(ChuaRoKetQua));
            if (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden) return Failure(ex);
            ModelState.AddModelError("", ex.Message + (ChuaRoKetQua ? " Chưa rõ kết quả; giữ form và chọn lưu lại để kiểm tra lệnh cũ." : "")); ClearPasswords(); return Page();
        }
    }
    private IActionResult Failure(CafeApiException ex)
    {
        if (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden) { Response.Cookies.Delete(CustomerCookie.Name); return RedirectToPage("Login"); }
        ModelState.AddModelError("", ex.Message); return Page();
    }
    private void ClearPasswords()
    {
        foreach (var field in new[] { nameof(MatKhauMoi), nameof(XacNhanMatKhau) })
        { var errors = ModelState[field]?.Errors.Select(x => x.ErrorMessage).ToArray() ?? []; ModelState.Remove(field); foreach (var error in errors) ModelState.AddModelError("", string.IsNullOrEmpty(error) ? "Mật khẩu không hợp lệ." : error); }
        MatKhauMoi = XacNhanMatKhau = "";
    }
}
