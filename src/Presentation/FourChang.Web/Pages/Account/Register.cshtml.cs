using System.ComponentModel.DataAnnotations;
using FourChang.Presentation.Shared.ApiClients;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace FourChang.Web.Pages.Account;

// UC-WEB-01: tạo tài khoản rồi chuyển đến đăng nhập. Không tự cấp cookie sau đăng ký.
public sealed class RegisterModel(CafeApiClient api) : PageModel
{
    [BindProperty] public Guid MaThaoTac { get; set; } = Guid.NewGuid();
    [BindProperty, Required, StringLength(100, MinimumLength = 3)] public string TenDangNhap { get; set; } = "";
    [BindProperty, Required, StringLength(120)] public string HoTen { get; set; } = "";
    [BindProperty, EmailAddress, StringLength(254)] public string? Email { get; set; }
    [BindProperty, Required(ErrorMessage = "Vui lòng nhập số điện thoại."), RegularExpression(@"^[0-9]{10}$", ErrorMessage = "Số điện thoại phải gồm đúng 10 chữ số.")] public string? SoDienThoai { get; set; }
    [BindProperty, Required, StringLength(128, MinimumLength = 10)] public string MatKhau { get; set; } = "";
    [BindProperty, Required, Compare(nameof(MatKhau))] public string XacNhanMatKhau { get; set; } = "";
    public async Task<IActionResult> OnPostAsync()
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            // Tra receipt trước khi gửi lại: vẫn phục hồi được khi mật khẩu không còn trong form lỗi.
            if (await api.GetRegistrationAsync(MaThaoTac) is not null) return RedirectToPage("Login");
            if (!ModelState.IsValid) { ClearPasswords(); return Page(); }
            await api.RegisterAsync(new(TenDangNhap, HoTen, Email, SoDienThoai, MatKhau, MaThaoTac, XacNhanMatKhau));
            return RedirectToPage("Login");
        }
        catch (CafeApiException ex) { ModelState.AddModelError("", ex.Message + " Nếu chưa rõ kết quả, giữ nguyên biểu mẫu và thử lại để kiểm tra mã thao tác."); ClearPasswords(); return Page(); }
    }
    private void ClearPasswords() { ModelState.Remove(nameof(MatKhau)); ModelState.Remove(nameof(XacNhanMatKhau)); MatKhau = XacNhanMatKhau = ""; }
}
