using System.Data;
using System.Security.Cryptography;
using FourChang.Domain.Entities.Accounts;
using FourChang.Domain.Entities.Audit;
using FourChang.Infrastructure.Database.Context;
using FourChang.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Infrastructure.Database.Initialization;

/// <summary>Thêm 10 hồ sơ giả lập tên tự nhiên khi được gọi rõ ràng; không reset ID hoặc xóa dữ liệu cũ.</summary>
public static class CustomerSampleSeed
{
    /// <summary>Seed lặp an toàn theo username/email cố định; mật khẩu ngẫu nhiên không in ra log và phải reset trước dùng.</summary>
    public static async Task<int> RunAsync(CafeDbContext db)
    {
        string[] names = ["Nguyễn Minh Anh", "Trần Hoàng Nam", "Lê Ngọc Hân", "Phạm Tuấn Kiệt", "Hoàng Thảo Vy", "Vũ Đức Huy", "Đặng Phương Linh", "Bùi Gia Bảo", "Đỗ Khánh Ngọc", "Ngô Nhật Minh"];
        string[] users = ["mau.nguyen.minh.anh", "mau.tran.hoang.nam", "mau.le.ngoc.han", "mau.pham.tuan.kiet", "mau.hoang.thao.vy", "mau.vu.duc.huy", "mau.dang.phuong.linh", "mau.bui.gia.bao", "mau.do.khanh.ngoc", "mau.ngo.nhat.minh"];
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='FourChang:CustomerSamples',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51090,'Sample seed lock timeout',1;");
        var count = 0;
        for (var i = 0; i < names.Length; i++)
        {
            var email = $"khachhang{i + 1:00}@example.test";
            var phone = $"09070000{i + 1:00}";
            var existing = await db.TaiKhoanKhachHang.SingleOrDefaultAsync(x => x.TenDangNhapChuanHoa == users[i]);
            if (existing is not null) continue;
            if (await db.TaiKhoanKhachHang.AnyAsync(x => x.EmailChuanHoa == email || x.SoDienThoaiChuanHoa == phone))
                throw new InvalidOperationException("Định danh mẫu đã thuộc hồ sơ khác; dừng, không ghi đè dữ liệu.");
            var customer = new TaiKhoanKhachHang
            {
                TenDangNhapChuanHoa = users[i],
                HoTen = names[i],
                EmailChuanHoa = email,
                SoDienThoaiChuanHoa = phone,
                TrangThai = "ACTIVE",
                BatBuocDoiMatKhau = true,
                MatKhauBam = Passwords.Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)))
            };
            db.TaiKhoanKhachHang.Add(customer); await db.SaveChangesAsync();
            db.NhatKyHeThong.Add(new NhatKyHeThong
            {
                MaLienKetXuLy = Guid.NewGuid(),
                LoaiTacNhan = "SYSTEM",
                TenNguoiThucHienDaChot = "Khởi tạo dữ liệu mẫu",
                HanhDong = "SEED_CUSTOMER_SAMPLE",
                LoaiDoiTuong = "TaiKhoanKhachHang",
                MaDoiTuong = customer.MaKhachHang.ToString(),
                KetQua = "SUCCEEDED",
                PhatSinhLuc = DateTime.UtcNow
            });
            count++;
        }
        await db.SaveChangesAsync(); await tx.CommitAsync();
        return count;
    }
}
