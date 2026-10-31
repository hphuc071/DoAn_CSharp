using System.Globalization;
using FourChang.Infrastructure.Persistence;
using FourChang.Presentation.Shared.Integration;
using Microsoft.EntityFrameworkCore;
namespace FourChang.Api.Features;

public static class DataEndpoints
{
    public static void MapData(this WebApplication app)
    {
        app.MapGet("/api/admin/data/TaiKhoanKhachHang", async(CafeDbContext db) => await Read(db.TaiKhoanKhachHang)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/TaiKhoanQuanLy", async(CafeDbContext db) => await Read(db.TaiKhoanQuanLy)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/PhienDangNhap", async(CafeDbContext db) => await Read(db.PhienDangNhap)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/DiaChiKhachHang", async(CafeDbContext db) => await Read(db.DiaChiKhachHang)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/NhanVien", async(CafeDbContext db) => await Read(db.NhanVien)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/CaLamViec", async(CafeDbContext db) => await Read(db.CaLamViec)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/DanhMucMon", async(CafeDbContext db) => await Read(db.DanhMucMon)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/Mon", async(CafeDbContext db) => await Read(db.Mon)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/CauHinhMon", async(CafeDbContext db) => await Read(db.CauHinhMon)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/MonThem", async(CafeDbContext db) => await Read(db.MonThem)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/CauHinhMonThem", async(CafeDbContext db) => await Read(db.CauHinhMonThem)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/TuyChonPhaCheHopLe", async(CafeDbContext db) => await Read(db.TuyChonPhaCheHopLe)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/NguyenLieu", async(CafeDbContext db) => await Read(db.NguyenLieu)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/DonViTinh", async(CafeDbContext db) => await Read(db.DonViTinh)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/QuyDoiDonViNguyenLieu", async(CafeDbContext db) => await Read(db.QuyDoiDonViNguyenLieu)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/CongThuc", async(CafeDbContext db) => await Read(db.CongThuc)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/ChiTietCongThuc", async(CafeDbContext db) => await Read(db.ChiTietCongThuc)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/Ban", async(CafeDbContext db) => await Read(db.Ban)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/DonHang", async(CafeDbContext db) => await Read(db.DonHang)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/ChiTietDonHang", async(CafeDbContext db) => await Read(db.ChiTietDonHang)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/MonThemChiTietDonHang", async(CafeDbContext db) => await Read(db.MonThemChiTietDonHang)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/NguyenLieuChiTietDonHang", async(CafeDbContext db) => await Read(db.NguyenLieuChiTietDonHang)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/HuyMon", async(CafeDbContext db) => await Read(db.HuyMon)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/ThanhToan", async(CafeDbContext db) => await Read(db.ThanhToan)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/GiaoDichNganHang", async(CafeDbContext db) => await Read(db.GiaoDichNganHang)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/HoSoHoanTien", async(CafeDbContext db) => await Read(db.HoSoHoanTien)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/LanChiHoanTien", async(CafeDbContext db) => await Read(db.LanChiHoanTien)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/HoaDon", async(CafeDbContext db) => await Read(db.HoaDon)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/PhieuKho", async(CafeDbContext db) => await Read(db.PhieuKho)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/ChiTietPhieuKho", async(CafeDbContext db) => await Read(db.ChiTietPhieuKho)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/TonKho", async(CafeDbContext db) => await Read(db.TonKho)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/GiuChoNguyenLieu", async(CafeDbContext db) => await Read(db.GiuChoNguyenLieu)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/BienDongKho", async(CafeDbContext db) => await Read(db.BienDongKho)).RequireAuthorization("Admin");
        app.MapGet("/api/admin/data/NhatKyHeThong", async(CafeDbContext db) => await Read(db.NhatKyHeThong)).RequireAuthorization("Admin");
        app.MapGet("/api/pos/data/DonHang",async(CafeDbContext db)=>await Read(db.DonHang)).RequireAuthorization("Staff");
        app.MapGet("/api/pos/data/HoaDon",async(CafeDbContext db)=>await Read(db.HoaDon)).RequireAuthorization("Staff");
        app.MapGet("/api/admin/dashboard",async(CafeDbContext db)=>
        {
            var collected=await db.ThanhToan.Where(x=>x.TrangThai=="VERIFIED").SumAsync(x=>(decimal?)x.SoTienDaXacMinh)??0;
            var refunds=await db.LanChiHoanTien.Where(x=>x.TrangThai=="SUCCEEDED").SumAsync(x=>(decimal?)x.SoTien)??0;
            return new DashboardData(collected-refunds,await db.DonHang.CountAsync(x=>x.TrangThai=="COMPLETED"),await db.HoSoHoanTien.CountAsync(x=>x.TrangThai!="REFUNDED"&&x.TrangThai!="REJECTED"),await(from n in db.NguyenLieu join t in db.TonKho on n.MaNguyenLieu equals t.MaNguyenLieu where t.TonThucTe-t.TongGiuCho<=n.NguongCanhBao select n.MaNguyenLieu).CountAsync());
        }).RequireAuthorization("Admin");
    }
    private static async Task<TableData> Read<T>(IQueryable<T> source) where T:class
    {
        var forbidden=new HashSet<string>(StringComparer.Ordinal){"MatKhauBam","MaLamMoiDaBam","MaBamQR","TaiKhoanNhanMaHoa","NoiDungQR","MaBamYeuCau","DuLieuTruocJson","DuLieuSauJson","KetQuaDaChotJson"};
        var columns=typeof(T).GetProperties().Where(p=>!forbidden.Contains(p.Name)&&p.PropertyType!=typeof(byte[])).ToArray();
        var records=await source.AsNoTracking().Take(100).ToListAsync();
        var rows=records.Select(row=>columns.Select(p=>p.GetValue(row) switch{null=>"",DateTime time=>time.ToLocalTime().ToString("dd/MM/yyyy HH:mm",CultureInfo.GetCultureInfo("vi-VN")),decimal number=>number.ToString("0.####",CultureInfo.InvariantCulture),bool boolean=>boolean?"Có":"Không",var value=>Convert.ToString(value,CultureInfo.InvariantCulture)??""}).ToArray()).ToArray();
        return new(columns.Select(p=>p.Name).ToArray(),rows);
    }
}
