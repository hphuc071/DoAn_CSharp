using FourChang.Infrastructure.Database.Context;
using FourChang.Domain.Entities.Accounts;
using FourChang.Domain.Entities.Audit;
using FourChang.Domain.Entities.Employees;
using FourChang.Domain.Entities.Inventory;
using FourChang.Domain.Entities.Menu;
using FourChang.Domain.Entities.Orders;
using FourChang.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
namespace FourChang.Infrastructure.Database.Initialization;

public static class DemoSeed
{
    /// <summary>Thực thi khởi tạo/kiểm tra dữ liệu theo lệnh tường minh, không thay đổi dữ liệu từ giao diện.</summary>
    public static async Task RunAsync(CafeDbContext db)
    {
        if (await db.Mon.AnyAsync()) return;
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (!await db.TaiKhoanQuanLy.AnyAsync())
        {
            var password = Environment.GetEnvironmentVariable("ADMIN_INITIAL_PASSWORD");
            if (string.IsNullOrWhiteSpace(password) || password.Length < 5) throw new InvalidOperationException("ADMIN_INITIAL_PASSWORD phải có ít nhất 5 ký tự.");
            db.TaiKhoanQuanLy.Add(new() { TenDangNhapChuanHoa = "quanly", HoTen = "Quản lý", MatKhauBam = Passwords.Hash(password), TrangThai = "ACTIVE" });
            await db.SaveChangesAsync();
        }
        db.NhanVien.AddRange(new NhanVien { HoTen = "Nguyễn Văn An", NgayVaoLam = DateTime.UtcNow.Date, TrangThai = "ACTIVE" }, new NhanVien { HoTen = "Trần Thị Lan", NgayVaoLam = DateTime.UtcNow.Date, TrangThai = "ACTIVE" });
        var coffee = (MaDanhMuc: 1L, Name: "Cà phê");
        var tea = (MaDanhMuc: 2L, Name: "Trà");
        var gram = (MaDonVi: 1L, Unit: "g");
        var ml = (MaDonVi: 2L, Unit: "ml");
        await db.SaveChangesAsync();
        var ingredientNames = new[] { "Hạt cà phê", "Sữa tươi", "Đường", "Đá", "Trà đào nền", "Bột matcha", "Cold brew nền", "Kem cheese", "Trân châu" };
        var ingredients = ingredientNames.Select((name, i) => new NguyenLieu { TenNguyenLieu = name, MaDonViCoSo = (i is 1 or 4 or 6) ? ml.MaDonVi : gram.MaDonVi, TonAnToan = 0, NguongCanhBao = 100, DangSuDung = true }).ToArray();
        db.NguyenLieu.AddRange(ingredients); await db.SaveChangesAsync();
        var document = new PhieuKho { SoPhieuKho = "PN-KHOI-TAO", LoaiPhieu = "RECEIPT", TrangThai = "DRAFT", MaQuanLyLap = 1, MaThaoTac = Guid.NewGuid(), LyDo = "Nhập dữ liệu mẫu phục vụ chạy thử" };
        db.PhieuKho.Add(document); await db.SaveChangesAsync();
        for (var i = 0; i < ingredients.Length; i++)
        {
            var quantity = i == 6 ? 0 : 10000;
            db.TonKho.Add(new() { MaNguyenLieu = ingredients[i].MaNguyenLieu, TonThucTe = quantity, TongGiuCho = 0 });
            if (quantity == 0) continue;
            var line = new ChiTietPhieuKho { MaPhieuKho = document.MaPhieuKho, MaNguyenLieu = ingredients[i].MaNguyenLieu, DonViNhapDaChot = ingredients[i].MaDonViCoSo == ml.MaDonVi ? "ml" : "g", SoLuongTheoDonViNhap = quantity, ChenhLechDonViCoSo = quantity, DonGiaNhap = 0 };
            db.ChiTietPhieuKho.Add(line); await db.SaveChangesAsync();
            db.BienDongKho.Add(new() { MaNguyenLieu = ingredients[i].MaNguyenLieu, MaDongPhieuKho = line.MaDongPhieuKho, LoaiBienDong = "RECEIPT", SoLuongThayDoi = quantity, MaThaoTac = document.MaThaoTac, PhatSinhLuc = DateTime.UtcNow });
        }
        await db.SaveChangesAsync();
        document.TrangThai = "POSTED"; document.MaQuanLyDuyet = 1; document.GhiSoLuc = DateTime.UtcNow; await db.SaveChangesAsync();
        var toppingNames = new[] { "Kem cheese", "Thạch cà phê", "Trân châu" };
        var toppings = toppingNames.Select(n => new MonThem { TenMonThem = n, GiaMoiPhan = 10000, GioiHanMacDinhMoiLy = 3, DangSuDung = true }).ToArray();
        db.MonThem.AddRange(toppings); await db.SaveChangesAsync();
        for (var i = 0; i < toppings.Length; i++)
        {
            var recipe = new CongThuc { MaMonThem = toppings[i].MaMonThem, SoPhienBan = 1, TenCongThuc = toppings[i].TenMonThem, TrangThai = "DRAFT", MaQuanLyTao = 1 };
            db.CongThuc.Add(recipe); await db.SaveChangesAsync();
            db.ChiTietCongThuc.Add(new() { MaCongThuc = recipe.MaCongThuc, MaNguyenLieu = ingredients[i == 0 ? 7 : i == 1 ? 6 : 8].MaNguyenLieu, LoaiDinhMuc = "FIXED", DinhMucCoSo = 20 });
            await db.SaveChangesAsync(); recipe.TrangThai = "ACTIVE"; recipe.MaQuanLyDuyet = 1; recipe.HieuLucTu = DateTime.UtcNow; await db.SaveChangesAsync();
        }
        var names = new[] { "Cà phê sữa", "Bạc xỉu", "Latte", "Trà đào", "Matcha latte", "Cold brew" };
        var prices = new decimal[] { 35000, 39000, 45000, 42000, 49000, 45000 };
        for (var i = 0; i < names.Length; i++)
        {
            var product = new Mon { TenMon = names[i], MaDanhMuc = (i is 3 or 4) ? tea.MaDanhMuc : coffee.MaDanhMuc, DuongDanAnh = "sprite:" + i, ThuTuHienThi = i, DaCongBo = true, DangBan = true };
            db.Mon.Add(product); await db.SaveChangesAsync();
            foreach (var size in new[] { "S", "M", "L" }) foreach (var temperature in (i is 3 or 5) ? new[] { "ICED" } : new[] { "HOT", "ICED" })
                {
                    var variant = new CauHinhMon { MaMon = product.MaMon, KichCo = size, NhietDo = temperature, GiaBan = prices[i] + (size == "S" ? -5000 : size == "L" ? 5000 : 0), DangBan = true, LaMacDinh = size == "M" && temperature == "ICED" };
                    db.CauHinhMon.Add(variant); await db.SaveChangesAsync();
                    var recipe = new CongThuc { MaCauHinhMon = variant.MaCauHinhMon, SoPhienBan = 1, TenCongThuc = $"{product.TenMon} {size} {temperature}", TrangThai = "DRAFT", MaQuanLyTao = 1 };
                    db.CongThuc.Add(recipe); await db.SaveChangesAsync();
                    var factor = size == "S" ? .8m : size == "L" ? 1.2m : 1;
                    var primary = i == 3 ? 4 : i == 4 ? 5 : i == 5 ? 6 : 0;
                    db.ChiTietCongThuc.Add(new() { MaCongThuc = recipe.MaCongThuc, MaNguyenLieu = ingredients[primary].MaNguyenLieu, LoaiDinhMuc = "FIXED", DinhMucCoSo = (i == 3 || i == 5 ? 100 : 18) * factor });
                    if (i is 0 or 1 or 2 or 4) db.ChiTietCongThuc.Add(new() { MaCongThuc = recipe.MaCongThuc, MaNguyenLieu = ingredients[1].MaNguyenLieu, LoaiDinhMuc = "FIXED", DinhMucCoSo = 100 * factor });
                    db.ChiTietCongThuc.Add(new() { MaCongThuc = recipe.MaCongThuc, MaNguyenLieu = ingredients[2].MaNguyenLieu, LoaiDinhMuc = "SUGAR", DinhMucCoSo = 20 * factor });
                    if (temperature == "ICED") db.ChiTietCongThuc.Add(new() { MaCongThuc = recipe.MaCongThuc, MaNguyenLieu = ingredients[3].MaNguyenLieu, LoaiDinhMuc = "ICE", DinhMucCoSo = 100 * factor });
                    await db.SaveChangesAsync(); recipe.TrangThai = "ACTIVE"; recipe.MaQuanLyDuyet = 1; recipe.HieuLucTu = DateTime.UtcNow; await db.SaveChangesAsync();
                }
        }
        for (var i = 1; i <= 10; i++) db.Ban.Add(new() { MaBanHienThi = $"B{i:00}", TenBan = $"Bàn {i:00}", TenKhuVuc = "Tầng trệt", SoChoNgoi = 4, DangSuDung = true });
        db.NhatKyHeThong.Add(new() { MaLienKetXuLy = Guid.NewGuid(), LoaiTacNhan = "SYSTEM", TenNguoiThucHienDaChot = "Khởi tạo database", HanhDong = "SEED_DATABASE", LoaiDoiTuong = "Database", KetQua = "SUCCEEDED", PhatSinhLuc = DateTime.UtcNow });
        await db.SaveChangesAsync(); await transaction.CommitAsync();
    }
}
