using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Customer;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace FourChang.Web.Pages.Menu;
/// <summary>Menu Web dùng cùng catalog với POS; không tự khai báo giá/cấu hình/topping.</summary>
public sealed class IndexModel(CafeApiClient api):PageModel
{
    public CatalogSnapshot Catalog {get;private set;}=new([],[]);
    public List<MenuProduct> Products {get;private set;}=[];
    public string? Error {get;private set;}
    /// <summary>Lấy snapshot mới cho HTML ban đầu; không thay dữ liệu thật bằng dữ liệu mẫu khi mất mạng.</summary>
    public async Task OnGetAsync()
    {
        try
        {
            Catalog=await api.GetCatalogAsync(HttpContext.RequestAborted);
            Products=Catalog.Mon.Where(m=>m.CauHinh.Count>0).Select(m=>
            {
                var v=m.CauHinh.FirstOrDefault(v=>v.LaMacDinh)??m.CauHinh[0];var ok=m.CauHinh.Any(c=>c.ConHang);
                return new MenuProduct(v.MaCauHinhMon,m.MaMon,m.TenMon,m.TenDanhMuc,v.GiaBan,int.TryParse(m.DuongDanAnh?.Replace("sprite:",""),out var n)?n:0,ok,ok?"":"Tạm hết");
            }).ToList();
        }
        catch(CafeApiException ex){Error=ex.Message;}
    }
}
