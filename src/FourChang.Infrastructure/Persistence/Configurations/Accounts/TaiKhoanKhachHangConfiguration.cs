using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class TaiKhoanKhachHangConfiguration : IEntityTypeConfiguration<TaiKhoanKhachHang>
{
    public void Configure(EntityTypeBuilder<TaiKhoanKhachHang> b)
    {
        b.ToTable("TaiKhoanKhachHang", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_TaiKhoanKhachHang_1", "TrangThai IN ('ACTIVE','LOCKED','INACTIVE')");
            table.HasCheckConstraint("CK_TaiKhoanKhachHang_2", "SoLanDangNhapSai >= 0");
            table.HasCheckConstraint("CK_TaiKhoanKhachHang_3", "EmailChuanHoa IS NOT NULL OR SoDienThoaiChuanHoa IS NOT NULL");
            table.HasCheckConstraint("CK_TaiKhoanKhachHang_4", "LEN(LTRIM(RTRIM(HoTen))) > 0");
        });
        b.HasKey("MaKhachHang");
        b.Property(x => x.MaKhachHang).UseIdentityColumn();
        b.Property(x => x.MaKhachHang).HasColumnType("bigint").IsRequired();
        b.Property(x => x.TenDangNhapChuanHoa).HasColumnType("nvarchar(100)").IsRequired();
        b.Property(x => x.MatKhauBam).HasColumnType("nvarchar(512)").IsRequired();
        b.Property(x => x.TrangThai).HasColumnType("varchar(15)").IsRequired();
        b.Property(x => x.BatBuocDoiMatKhau).HasColumnType("bit").IsRequired();
        b.Property(x => x.SoLanDangNhapSai).HasColumnType("int").IsRequired();
        b.Property(x => x.KhoaDenLuc).HasColumnType("datetime2(3)");
        b.Property(x => x.DangNhapLanCuoiLuc).HasColumnType("datetime2(3)");
        b.Property(x => x.HoTen).HasColumnType("nvarchar(120)").IsRequired();
        b.Property(x => x.EmailChuanHoa).HasColumnType("nvarchar(254)");
        b.Property(x => x.SoDienThoaiChuanHoa).HasColumnType("varchar(20)");
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasIndex(new[] { "TenDangNhapChuanHoa" }).IsUnique().HasDatabaseName("UQ_TaiKhoanKhachHang_1");
        b.HasIndex(new[] { "EmailChuanHoa" }).IsUnique().HasDatabaseName("UX_KhachHang_Email").HasFilter("EmailChuanHoa IS NOT NULL");
        b.HasIndex(new[] { "SoDienThoaiChuanHoa" }).IsUnique().HasDatabaseName("UX_KhachHang_DienThoai").HasFilter("SoDienThoaiChuanHoa IS NOT NULL");
    }
}
