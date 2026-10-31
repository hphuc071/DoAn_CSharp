using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class BienDongKhoConfiguration : IEntityTypeConfiguration<BienDongKho>
{
    public void Configure(EntityTypeBuilder<BienDongKho> b)
    {
        b.ToTable("BienDongKho", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_BienDongKho_1", "SoLuongThayDoi <> 0");
            table.HasCheckConstraint("CK_BienDongKho_2", "(MaDongPhieuKho IS NOT NULL AND MaChiTietDonHang IS NULL) OR (MaDongPhieuKho IS NULL AND MaChiTietDonHang IS NOT NULL)");
            table.HasCheckConstraint("CK_BienDongKho_3", "MaBienDongBiDao IS NULL OR MaBienDongBiDao <> MaBienDongKho");
        });
        b.HasKey("MaBienDongKho");
        b.Property(x => x.MaBienDongKho).UseIdentityColumn();
        b.Property(x => x.MaBienDongKho).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaNguyenLieu).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaDongPhieuKho).HasColumnType("bigint");
        b.Property(x => x.MaChiTietDonHang).HasColumnType("bigint");
        b.Property(x => x.LoaiBienDong).HasColumnType("varchar(25)").IsRequired();
        b.Property(x => x.SoLuongThayDoi).HasColumnType("decimal(18,4)").IsRequired();
        b.Property(x => x.MaThaoTac).HasColumnType("uniqueidentifier").IsRequired();
        b.Property(x => x.MaBienDongBiDao).HasColumnType("bigint");
        b.Property(x => x.PhatSinhLuc).HasColumnType("datetime2(3)").IsRequired();
        b.Property(x => x.GhiChu).HasColumnType("nvarchar(500)");
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.HasIndex(new[] { "MaThaoTac", "MaNguyenLieu" }).IsUnique().HasDatabaseName("UQ_BienDongKho_1");
        b.HasOne<NguyenLieu>().WithMany().HasForeignKey("MaNguyenLieu").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<ChiTietPhieuKho>().WithMany().HasForeignKey("MaDongPhieuKho").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<ChiTietDonHang>().WithMany().HasForeignKey("MaChiTietDonHang").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<BienDongKho>().WithMany().HasForeignKey("MaBienDongBiDao").OnDelete(DeleteBehavior.NoAction);
    }
}
