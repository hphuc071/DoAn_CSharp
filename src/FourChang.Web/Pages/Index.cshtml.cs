using FourChang.Presentation.Shared.Integration;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace FourChang.Web.Pages;
public sealed class IndexModel(CafeApiClient api) : PageModel
{
    public List<MenuProduct> Products {get;private set;}=[];
    public string? Error {get;private set;}
    public async Task OnGetAsync(){try{Products=await api.GetMenuAsync(HttpContext.RequestAborted);}catch(CafeApiException ex){Error=ex.Message;}}
}
