using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class ThanhToanConfiguration : IEntityTypeConfiguration<ThanhToan>
{
    public void Configure(EntityTypeBuilder<ThanhToan> b)
    {
        b.ToTable("ThanhToan", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_ThanhToan_1", "PhuongThuc IN ('CASH','BANK_TRANSFER')");
            table.HasCheckConstraint("CK_ThanhToan_2", "SoTienYeuCau >= 0 AND SoTienDaXacMinh >= 0");
            table.HasCheckConstraint("CK_ThanhToan_3", "PhuongThuc <> 'CASH' OR MaCaLamViec IS NOT NULL");
            table.HasCheckConstraint("CK_ThanhToan_4", "TienKhachDua IS NULL OR TienKhachDua >= SoTienDaXacMinh");
            table.HasCheckConstraint("CK_ThanhToan_5", "TrangThai IN ('PENDING','PARTIAL','VERIFIED','EXPIRED','FAILED','RECONCILING')");
        });
        b.HasKey("MaThanhToan");
        b.Property(x => x.MaThanhToan).UseIdentityColumn();
        b.Property(x => x.MaThanhToan).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaThanhToanHienThi).HasColumnType("varchar(40)").IsRequired();
        b.Property(x => x.MaDonHang).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaCaLamViec).HasColumnType("bigint");
        b.Property(x => x.PhuongThuc).HasColumnType("varchar(15)").IsRequired();
        b.Property(x => x.SoTienYeuCau).HasColumnType("decimal(18,0)").IsRequired();
        b.Property(x => x.SoTienDaXacMinh).HasColumnType("decimal(18,0)").IsRequired();
        b.Property(x => x.TienKhachDua).HasColumnType("decimal(18,0)");
        b.Property(x => x.MaThamChieuChuyenKhoan).HasColumnType("varchar(100)");
        b.Property(x => x.NoiDungQR).HasColumnType("nvarchar(max)");
        b.Property(x => x.TrangThai).HasColumnType("varchar(20)").IsRequired();
        b.Property(x => x.HetHanLuc).HasColumnType("datetime2(3)");
        b.Property(x => x.XacMinhLuc).HasColumnType("datetime2(3)");
        b.Property(x => x.MaQuanLyXacMinh).HasColumnType("bigint");
        b.Property(x => x.ThamChieuChungCu).HasColumnType("nvarchar(500)");
        b.Property(x => x.MaThaoTac).HasColumnType("uniqueidentifier").IsRequired();
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasIndex(new[] { "MaThanhToanHienThi" }).IsUnique().HasDatabaseName("UQ_ThanhToan_1");
        b.HasIndex(new[] { "MaThaoTac" }).IsUnique().HasDatabaseName("UQ_ThanhToan_2");
        b.HasIndex(new[] { "MaThamChieuChuyenKhoan" }).IsUnique().HasDatabaseName("UX_ThanhToan_ThamChieu").HasFilter("MaThamChieuChuyenKhoan IS NOT NULL");
        b.HasOne<DonHang>().WithMany().HasForeignKey("MaDonHang").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<CaLamViec>().WithMany().HasForeignKey("MaCaLamViec").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TaiKhoanQuanLy>().WithMany().HasForeignKey("MaQuanLyXacMinh").OnDelete(DeleteBehavior.NoAction);
    }
}
