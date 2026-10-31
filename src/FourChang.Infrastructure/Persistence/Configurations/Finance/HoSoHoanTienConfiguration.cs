using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class HoSoHoanTienConfiguration : IEntityTypeConfiguration<HoSoHoanTien>
{
    public void Configure(EntityTypeBuilder<HoSoHoanTien> b)
    {
        b.ToTable("HoSoHoanTien", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_HoSoHoanTien_1", "SoTienYeuCau > 0");
            table.HasCheckConstraint("CK_HoSoHoanTien_2", "SoTienDuocDuyet IS NULL OR (SoTienDuocDuyet > 0 AND SoTienDuocDuyet <= SoTienYeuCau)");
            table.HasCheckConstraint("CK_HoSoHoanTien_3", "TrangThai IN ('REQUESTED','APPROVED','REJECTED','PROCESSING','PARTIALLY_REFUNDED','REFUNDED','RECONCILING')");
            table.HasCheckConstraint("CK_HoSoHoanTien_4", "(CASE WHEN MaKhachHangYeuCau IS NULL THEN 0 ELSE 1 END + CASE WHEN MaCaYeuCau IS NULL THEN 0 ELSE 1 END + CASE WHEN MaQuanLyYeuCau IS NULL THEN 0 ELSE 1 END) = 1");
            table.HasCheckConstraint("CK_HoSoHoanTien_5", "PhuongThucHoan IN ('CASH','BANK_TRANSFER')");
        });
        b.HasKey("MaHoanTien");
        b.Property(x => x.MaHoanTien).UseIdentityColumn();
        b.Property(x => x.MaHoanTien).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaHoanTienHienThi).HasColumnType("varchar(40)").IsRequired();
        b.Property(x => x.MaDonHang).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaThanhToan).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaHuyMon).HasColumnType("bigint");
        b.Property(x => x.LyDoHoan).HasColumnType("nvarchar(500)").IsRequired();
        b.Property(x => x.SoTienYeuCau).HasColumnType("decimal(18,0)").IsRequired();
        b.Property(x => x.SoTienDuocDuyet).HasColumnType("decimal(18,0)");
        b.Property(x => x.PhuongThucHoan).HasColumnType("varchar(15)").IsRequired();
        b.Property(x => x.TenNguoiNhan).HasColumnType("nvarchar(120)");
        b.Property(x => x.NganHangNguoiNhan).HasColumnType("nvarchar(100)");
        b.Property(x => x.TaiKhoanNhanMaHoa).HasColumnType("varbinary(max)");
        b.Property(x => x.TaiKhoanNhanDaChe).HasColumnType("nvarchar(50)");
        b.Property(x => x.TrangThai).HasColumnType("varchar(25)").IsRequired();
        b.Property(x => x.MaKhachHangYeuCau).HasColumnType("bigint");
        b.Property(x => x.MaCaYeuCau).HasColumnType("bigint");
        b.Property(x => x.MaQuanLyYeuCau).HasColumnType("bigint");
        b.Property(x => x.MaQuanLyDuyet).HasColumnType("bigint");
        b.Property(x => x.DuyetLuc).HasColumnType("datetime2(3)");
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasIndex(new[] { "MaHoanTienHienThi" }).IsUnique().HasDatabaseName("UQ_HoSoHoanTien_1");
        b.HasOne<DonHang>().WithMany().HasForeignKey("MaDonHang").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<ThanhToan>().WithMany().HasForeignKey("MaThanhToan").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<HuyMon>().WithMany().HasForeignKey("MaHuyMon").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TaiKhoanKhachHang>().WithMany().HasForeignKey("MaKhachHangYeuCau").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<CaLamViec>().WithMany().HasForeignKey("MaCaYeuCau").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TaiKhoanQuanLy>().WithMany().HasForeignKey("MaQuanLyYeuCau").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TaiKhoanQuanLy>().WithMany().HasForeignKey("MaQuanLyDuyet").OnDelete(DeleteBehavior.NoAction);
    }
}
