using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class PhieuKhoConfiguration : IEntityTypeConfiguration<PhieuKho>
{
    public void Configure(EntityTypeBuilder<PhieuKho> b)
    {
        b.ToTable("PhieuKho", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_PhieuKho_1", "LoaiPhieu IN ('RECEIPT','ISSUE','COUNT','ADJUSTMENT','REVERSAL')");
            table.HasCheckConstraint("CK_PhieuKho_2", "TrangThai IN ('DRAFT','POSTED')");
            table.HasCheckConstraint("CK_PhieuKho_3", "TrangThai <> 'POSTED' OR (GhiSoLuc IS NOT NULL AND MaQuanLyDuyet IS NOT NULL)");
            table.HasCheckConstraint("CK_PhieuKho_4", "MaPhieuBiDao IS NULL OR MaPhieuBiDao <> MaPhieuKho");
        });
        b.HasKey("MaPhieuKho");
        b.Property(x => x.MaPhieuKho).UseIdentityColumn();
        b.Property(x => x.MaPhieuKho).HasColumnType("bigint").IsRequired();
        b.Property(x => x.SoPhieuKho).HasColumnType("varchar(40)").IsRequired();
        b.Property(x => x.LoaiPhieu).HasColumnType("varchar(20)").IsRequired();
        b.Property(x => x.TrangThai).HasColumnType("varchar(15)").IsRequired();
        b.Property(x => x.TenNhaCungCapDaChot).HasColumnType("nvarchar(200)");
        b.Property(x => x.MaMucDich).HasColumnType("varchar(30)");
        b.Property(x => x.LyDo).HasColumnType("nvarchar(500)");
        b.Property(x => x.MocKiemKe).HasColumnType("datetime2(3)");
        b.Property(x => x.MaPhieuBiDao).HasColumnType("bigint");
        b.Property(x => x.MaQuanLyLap).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaQuanLyDuyet).HasColumnType("bigint");
        b.Property(x => x.GhiSoLuc).HasColumnType("datetime2(3)");
        b.Property(x => x.MaThaoTac).HasColumnType("uniqueidentifier").IsRequired();
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasIndex(new[] { "SoPhieuKho" }).IsUnique().HasDatabaseName("UQ_PhieuKho_1");
        b.HasIndex(new[] { "MaThaoTac" }).IsUnique().HasDatabaseName("UQ_PhieuKho_2");
        b.HasOne<PhieuKho>().WithMany().HasForeignKey("MaPhieuBiDao").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TaiKhoanQuanLy>().WithMany().HasForeignKey("MaQuanLyLap").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TaiKhoanQuanLy>().WithMany().HasForeignKey("MaQuanLyDuyet").OnDelete(DeleteBehavior.NoAction);
    }
}
