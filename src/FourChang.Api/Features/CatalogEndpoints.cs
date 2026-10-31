using FourChang.Infrastructure.Persistence;
using FourChang.Presentation.Shared.Integration;
using Microsoft.EntityFrameworkCore;
namespace FourChang.Api.Features;

public static class CatalogEndpoints
{
    public static void MapCatalog(this WebApplication app)
    {
        app.MapGet("/api/menu",async(CafeDbContext db)=>
        {
            var variants=await (from v in db.CauHinhMon.AsNoTracking() join m in db.Mon on v.MaMon equals m.MaMon join c in db.DanhMucMon on m.MaDanhMuc equals c.MaDanhMuc where m.DaCongBo&&c.DaCongBo&&c.DangSuDung select new{v.MaCauHinhMon,v.MaMon,m.TenMon,c.TenDanhMuc,v.GiaBan,v.KichCo,v.NhietDo,m.DuongDanAnh,m.ThuTuHienThi,Enabled=v.DangBan&&m.DangBan}).ToListAsync();
            var now=DateTime.UtcNow;
            var recipes=await db.CongThuc.AsNoTracking().Where(x=>x.TrangThai=="ACTIVE"&&x.MaCauHinhMon!=null&&x.HieuLucTu<=now&&(x.HieuLucDen==null||x.HieuLucDen>now)).ToListAsync();
            var requirements=await db.ChiTietCongThuc.AsNoTracking().ToListAsync();
            var stocks=await (from n in db.NguyenLieu.AsNoTracking() join t in db.TonKho on n.MaNguyenLieu equals t.MaNguyenLieu select new{n.MaNguyenLieu,n.DangSuDung,Available=t.TonThucTe-t.TongGiuCho-n.TonAnToan}).ToDictionaryAsync(x=>x.MaNguyenLieu);
            var choices=await db.TuyChonPhaCheHopLe.AsNoTracking().Where(x=>x.DangSuDung).ToListAsync();
            bool Available(long variant)
            {
                var recipe=recipes.FirstOrDefault(x=>x.MaCauHinhMon==variant);if(recipe is null)return false;
                var lines=requirements.Where(x=>x.MaCongThuc==recipe.MaCongThuc).ToList();
                return lines.Count>0&&choices.Where(x=>x.MaCauHinhMon==variant).Any(choice=>lines.GroupBy(x=>x.MaNguyenLieu).All(g=>
                {
                    var required=decimal.Ceiling(g.Sum(l=>l.DinhMucCoSo*(l.LoaiDinhMuc=="SUGAR"?choice.PhanTramDuong/100m:l.LoaiDinhMuc=="ICE"?choice.PhanTramDa/100m:1)/(1-l.TyLeHaoHut))*10000)/10000;
                    return required==0||(stocks.TryGetValue(g.Key,out var stock)&&stock.DangSuDung&&stock.Available>=required);
                }));
            }
            return variants.GroupBy(x=>x.MaMon).OrderBy(g=>g.First().ThuTuHienThi).Select(g=>
            {
                var item=g.OrderBy(x=>x.KichCo=="M"?0:1).ThenBy(x=>x.NhietDo=="ICED"?0:1).First();
                var available=item.Enabled&&Available(item.MaCauHinhMon);
                var image=int.TryParse(item.DuongDanAnh?.Replace("sprite:","",StringComparison.Ordinal),out var index)?index:0;
                return new MenuProduct(item.MaCauHinhMon,item.MaMon,item.TenMon,item.TenDanhMuc,item.GiaBan,image,available,available?"":"Tạm hết");
            });
        });
        app.MapGet("/api/menu/{product:long}/variants",async(long product,CafeDbContext db)=>await db.CauHinhMon.AsNoTracking().Where(x=>x.MaMon==product&&x.DangBan).Select(x=>new{x.MaCauHinhMon,x.KichCo,x.NhietDo,x.GiaBan}).ToListAsync());
    }
}
