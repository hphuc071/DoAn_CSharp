using FourChang.Api.Features.Admin.Menu;
using FourChang.Presentation.Shared.Contracts.Customer;
namespace FourChang.Api.Features.Customer.Menu;
/// <summary>API menu công khai dùng chung cho Web/POS; giá từ cấu hình, không sinh bảng giá theo kênh.</summary>
public static class CatalogEndpoints
{
    /// <summary>Giữ hợp đồng /api/menu cũ; thêm snapshot đầy đủ để các client dùng chung tùy chọn/topping.</summary>
    public static void MapCatalog(this WebApplication app)
    {
        app.MapGet("/api/catalog",(CatalogReadService s)=>s.Read());
        app.MapGet("/api/menu",async(CatalogReadService s)=>
        {
            var snapshot=await s.Read();
            return snapshot.Mon.Where(x=>x.CauHinh.Count>0).Select(m=>
            {
                var v=m.CauHinh.FirstOrDefault(x=>x.LaMacDinh)??m.CauHinh[0];
                var image=int.TryParse(m.DuongDanAnh?.Replace("sprite:","",StringComparison.Ordinal),out var n)?n:0;
                return new MenuProduct(v.MaCauHinhMon,m.MaMon,m.TenMon,m.TenDanhMuc,v.GiaBan,image,m.CauHinh.Any(x=>x.ConHang),m.CauHinh.Any(x=>x.ConHang)?"":"Tạm hết");
            });
        });
        app.MapGet("/api/menu/{product:long}/variants",async(long product,CatalogReadService s)=>
        {
            var dish=(await s.Read()).Mon.SingleOrDefault(x=>x.MaMon==product);
            return dish is null?Results.NotFound():Results.Ok(dish.CauHinh);
        });
    }
}
