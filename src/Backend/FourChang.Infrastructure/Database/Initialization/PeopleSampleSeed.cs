using System.Security.Cryptography;
using System.Text.Json;
using FourChang.Domain.Entities.Accounts;
using FourChang.Domain.Entities.Audit;
using FourChang.Domain.Entities.Employees;
using FourChang.Infrastructure.Database.Context;
using FourChang.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace FourChang.Infrastructure.Database.Initialization;

/// <summary>Bộ dữ liệu minh họa được gọi chủ động; không chạy tự động khi đăng nhập hoặc khởi động API.</summary>
public static class PeopleSampleSeed
{
    public sealed record SamplePerson(long MaNoiBo, string HoTen, string DienThoai, string? TenDangNhap);
    public sealed record SamplePeopleResult(int NhanVienDaThem, int KhachHangDaThem,
        IReadOnlyList<SamplePerson> NhanVien, IReadOnlyList<SamplePerson> KhachHang);

    internal static readonly string[] EmployeeNames = ["Nguyễn Hoàng Minh", "Trần Ngọc Anh", "Lê Đức Huy", "Phạm Khánh Linh", "Hoàng Gia Bảo",
        "Vũ Thanh Hà", "Đặng Quang Phúc", "Bùi Thảo Vy", "Đỗ Minh Khang", "Ngô Diệu Nhi"];
    internal static readonly string[] CustomerNames = ["Nguyễn Tuấn Kiệt", "Trần Bảo Ngọc", "Lê Minh Quân", "Phạm Phương Thảo", "Hoàng Nhật Nam",
        "Vũ Quỳnh Chi", "Đặng Hải Đăng", "Bùi Ngọc Hân", "Đỗ Quốc Bảo", "Ngô Thanh Trúc"];

    /// <summary>Thêm đúng bộ 10+10 trong một giao dịch, giữ nguyên dữ liệu cũ và chống chạy trùng bằng khóa SQL.</summary>
    public static async Task<SamplePeopleResult> RunAsync(CafeDbContext db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='FourChang:SamplePeople', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r<0 THROW 51090, 'Seed lock timeout', 1;");
        var employees = new List<NhanVien>(); var customers = new List<TaiKhoanKhachHang>();
        var employeeAdded = 0; var customerAdded = 0;
        for (var i = 0; i < 10; i++)
        {
            var phone = $"099810{i + 1:0000}";
            var employee = await db.NhanVien.SingleOrDefaultAsync(x => x.SoDienThoai == phone);
            if (employee is null)
            {
                employee = new NhanVien { HoTen = EmployeeNames[i], SoDienThoai = phone, NgayVaoLam = new DateTime(2026, 10, i + 1), TrangThai = "ACTIVE" };
                db.NhanVien.Add(employee); employeeAdded++;
            }
            else if (employee.HoTen != EmployeeNames[i])
                throw new InvalidOperationException("Số liên hệ dành cho mẫu đã được hồ sơ khác sử dụng. Dừng, không ghi đè hồ sơ.");
            employees.Add(employee);
            var username = $"mau.kh{i + 1:00}";
            var customer = await db.TaiKhoanKhachHang.SingleOrDefaultAsync(x => x.TenDangNhapChuanHoa == username);
            if (customer is null)
            {
                // Mật khẩu ngẫu nhiên không xuất ra file/log/GitHub; thử đăng nhập bằng cách Admin cấp lại mật khẩu tạm.
                customer = new TaiKhoanKhachHang
                {
                    HoTen = CustomerNames[i],
                    TenDangNhapChuanHoa = username,
                    EmailChuanHoa = $"khachhang.mau{i + 1:00}@example.test",
                    SoDienThoaiChuanHoa = $"099820{i + 1:0000}",
                    MatKhauBam = Passwords.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
                    TrangThai = "ACTIVE",
                    BatBuocDoiMatKhau = true
                };
                db.TaiKhoanKhachHang.Add(customer); customerAdded++;
            }
            else if (customer.HoTen != CustomerNames[i] || customer.EmailChuanHoa != $"khachhang.mau{i + 1:00}@example.test")
                throw new InvalidOperationException("Tài khoản dành cho mẫu đã được hồ sơ khác sử dụng. Dừng, không ghi đè tài khoản.");
            customers.Add(customer);
        }
        await db.SaveChangesAsync();
        var result = new SamplePeopleResult(employeeAdded, customerAdded,
            employees.Select(x => new SamplePerson(x.MaNhanVien, x.HoTen, x.SoDienThoai!, null)).ToList(),
            customers.Select(x => new SamplePerson(x.MaKhachHang, x.HoTen, x.SoDienThoaiChuanHoa!, x.TenDangNhapChuanHoa)).ToList());
        if (employeeAdded + customerAdded > 0)
        {
            db.NhatKyHeThong.Add(new NhatKyHeThong
            {
                MaLienKetXuLy = Guid.NewGuid(),
                LoaiTacNhan = "SYSTEM",
                TenNguoiThucHienDaChot = "Nạp dữ liệu người dùng mẫu",
                HanhDong = "SEED_SAMPLE_PEOPLE",
                LoaiDoiTuong = "Database",
                DuLieuSauJson = JsonSerializer.Serialize(result),
                KetQua = "SUCCEEDED",
                PhatSinhLuc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        await transaction.CommitAsync(); return result;
    }
}
