using FourChang.Presentation.Shared.ApiClients;
using Microsoft.AspNetCore.DataProtection;
namespace FourChang.Web.Security;

// UC-WEB-02 E4: trình duyệt không còn đăng nhập khi API mất mạng.
// Cookie chờ thu hồi được mã hóa, HttpOnly và chỉ dùng để thu hồi, không dùng làm phiên đăng nhập.
public sealed class PendingCustomerLogout(IDataProtectionProvider protection)
{
    private const string Name = "fourchang_pending_logout";
    private readonly IDataProtector protector = protection.CreateProtector("FourChang.Customer.PendingLogout.v1");
    public void Queue(HttpContext c, string token) => c.Response.Cookies.Append(Name, protector.Protect(token), new CookieOptions { HttpOnly = true, Secure = c.Request.IsHttps, SameSite = SameSiteMode.Strict, Path = "/", MaxAge = TimeSpan.FromHours(8), IsEssential = true });
    public async Task Retry(HttpContext c, CafeApiClient api)
    {
        if (c.Request.Cookies[Name] is not { } encrypted) return;
        try
        {
            api.Token = protector.Unprotect(encrypted); await api.LogoutAsync(); c.Response.Cookies.Delete(Name);
        }
        catch (CafeApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized) { c.Response.Cookies.Delete(Name); }
        catch (CafeApiException) { }
        catch (System.Security.Cryptography.CryptographicException) { c.Response.Cookies.Delete(Name); }
        finally { api.Token = null; }
    }
}
