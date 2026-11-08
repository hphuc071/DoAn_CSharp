using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace FourChang.Web.Pages.Menu;
public sealed class IndexModel(CafeApiClient api) : PageModel
{
    public List<MenuProduct> Products {get;private set;}=[];
    public string? Error {get;private set;}
    public async Task OnGetAsync(string? sort){try{Products=sort is "newest" or "bestseller" ? await api.GetSortedMenuAsync(sort,HttpContext.RequestAborted) : await api.GetMenuAsync(HttpContext.RequestAborted);}catch(CafeApiException ex){Error=ex.Message;}}
}
