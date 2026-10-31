using FourChang.Presentation.Shared.Integration;
namespace FourChang.Web.Security;
public static class CustomerCookie
{
    public const string Name="fourchang_session";
    public static void Set(HttpResponse response,HttpRequest request,SessionResult session)=>response.Cookies.Append(Name,session.Token,new CookieOptions{HttpOnly=true,Secure=request.IsHttps,SameSite=SameSiteMode.Strict,Path="/",MaxAge=TimeSpan.FromHours(8),IsEssential=true});
}
