// UC: Dùng chung toàn hệ thống
// Chức năng: Khai báo các bảng EF Core và thực hiện lưu dữ liệu.
// Cần nhớ: Trước khi lưu, gắn thời điểm và cấp mã hiển thị cho bản ghi mới; không thay khóa liên kết.

using FourChang.Domain.Entities.Accounts;
using FourChang.Domain.Entities.Audit;
using FourChang.Domain.Entities.Employees;
using FourChang.Domain.Entities.Inventory;
using FourChang.Domain.Entities.Menu;
using FourChang.Domain.Entities.Orders;
using FourChang.Domain.Entities.Payments;
using Microsoft.EntityFrameworkCore;
namespace FourChang.Infrastructure.Database.Context;

public sealed class CafeDbContext(DbContextOptions<CafeDbContext> options) : DbContext(options)
{
    public DbSet<Banner> Banner => Set<Banner>();
    public DbSet<SuKienDongBo> SuKienDongBo => Set<SuKienDongBo>();
    public DbSet<TaiKhoanKhachHang> TaiKhoanKhachHang => Set<TaiKhoanKhachHang>();
    public DbSet<TaiKhoanQuanLy> TaiKhoanQuanLy => Set<TaiKhoanQuanLy>();
    public DbSet<PhienDangNhap> PhienDangNhap => Set<PhienDangNhap>();
    public DbSet<DiaChiKhachHang> DiaChiKhachHang => Set<DiaChiKhachHang>();
    public DbSet<NhanVien> NhanVien => Set<NhanVien>();
    public DbSet<CaLamViec> CaLamViec => Set<CaLamViec>();
    public DbSet<Mon> Mon => Set<Mon>();
    public DbSet<CauHinhMon> CauHinhMon => Set<CauHinhMon>();
    public DbSet<MonThem> MonThem => Set<MonThem>();
    public DbSet<NguyenLieu> NguyenLieu => Set<NguyenLieu>();
    public DbSet<CongThuc> CongThuc => Set<CongThuc>();
    public DbSet<ChiTietCongThuc> ChiTietCongThuc => Set<ChiTietCongThuc>();
    public DbSet<Ban> Ban => Set<Ban>();
    public DbSet<DonHang> DonHang => Set<DonHang>();
    public DbSet<ChiTietDonHang> ChiTietDonHang => Set<ChiTietDonHang>();
    public DbSet<MonThemChiTietDonHang> MonThemChiTietDonHang => Set<MonThemChiTietDonHang>();
    public DbSet<NguyenLieuChiTietDonHang> NguyenLieuChiTietDonHang => Set<NguyenLieuChiTietDonHang>();
    public DbSet<HuyMon> HuyMon => Set<HuyMon>();
    public DbSet<ThanhToan> ThanhToan => Set<ThanhToan>();
    public DbSet<GiaoDichNganHang> GiaoDichNganHang => Set<GiaoDichNganHang>();
    public DbSet<HoSoHoanTien> HoSoHoanTien => Set<HoSoHoanTien>();
    public DbSet<LanChiHoanTien> LanChiHoanTien => Set<LanChiHoanTien>();
    public DbSet<HoaDon> HoaDon => Set<HoaDon>();
    public DbSet<PhieuKho> PhieuKho => Set<PhieuKho>();
    public DbSet<ChiTietPhieuKho> ChiTietPhieuKho => Set<ChiTietPhieuKho>();
    public DbSet<TonKho> TonKho => Set<TonKho>();
    public DbSet<BienDongKho> BienDongKho => Set<BienDongKho>();
    public DbSet<NhatKyHeThong> NhatKyHeThong => Set<NhatKyHeThong>();
    /// <summary>Nạp cấu hình entity theo từng nhóm nghiệp vụ để mapping và ràng buộc không dồn vào DbContext.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CafeDbContext).Assembly);
        foreach (var prefix in DisplayCodeAllocator.Prefixes) modelBuilder.HasSequence<long>("DisplayCode_" + prefix, "dbo").StartsAt(1).IncrementsBy(1);
    }
    /// <summary>
    /// Dùng chung toàn hệ thống — Chuẩn bị thông tin chung rồi ghi thay đổi xuống database.
    ///
    /// Hàm này được dùng để:
    /// 1. Tìm các đối tượng đang được thêm hoặc sửa.
    /// 2. Gắn thời điểm tạo/cập nhật theo thời gian máy chủ.
    /// 3. Gọi phần cấp mã hiển thị nếu cần.
    /// 4. Yêu cầu EF Core ghi những thay đổi xuống database.
    ///
    /// Thông tin cần truyền vào:
    /// - cancellationToken: Tín hiệu yêu cầu dừng tác vụ nếu không cần xử lý tiếp.
    ///
    /// Sau khi xử lý: Số bản ghi bị tác động; lỗi được trả về cho bên gọi xử lý giao dịch.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            if (entry.State == EntityState.Added && entry.Metadata.FindProperty("ThoiDiemTao") is not null)
                entry.Property("ThoiDiemTao").CurrentValue = now;
            if (entry.Metadata.FindProperty("ThoiDiemCapNhat") is not null)
                entry.Property("ThoiDiemCapNhat").CurrentValue = now;
        }
        await DisplayCodeAllocator.AssignAsync(this, cancellationToken);
        return await base.SaveChangesAsync(cancellationToken);
    }
}
