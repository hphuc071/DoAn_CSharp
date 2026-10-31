using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class ChiTietPhieuKhoConfiguration : IEntityTypeConfiguration<ChiTietPhieuKho>
{
    public void Configure(EntityTypeBuilder<ChiTietPhieuKho> b)
    {
        b.ToTable("ChiTietPhieuKho", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_ChiTietPhieuKho_1", "HeSoQuyDoiDaChot > 0");
            table.HasCheckConstraint("CK_ChiTietPhieuKho_2", "SoLuongTheoDonViNhap >= 0");
            table.HasCheckConstraint("CK_ChiTietPhieuKho_3", "DonGiaNhap IS NULL OR DonGiaNhap >= 0");
            table.HasCheckConstraint("CK_ChiTietPhieuKho_4", "TonSoDaChot IS NULL OR TonSoDaChot >= 0");
            table.HasCheckConstraint("CK_ChiTietPhieuKho_5", "SoLuongThucDem IS NULL OR SoLuongThucDem >= 0");
        });
        b.HasKey("MaDongPhieuKho");
        b.Property(x => x.MaDongPhieuKho).UseIdentityColumn();
        b.Property(x => x.MaDongPhieuKho).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaPhieuKho).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaNguyenLieu).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaDonViNhap).HasColumnType("bigint").IsRequired();
        b.Property(x => x.DonViNhapDaChot).HasColumnType("nvarchar(60)").IsRequired();
        b.Property(x => x.HeSoQuyDoiDaChot).HasColumnType("decimal(18,6)").IsRequired();
        b.Property(x => x.SoLuongTheoDonViNhap).HasColumnType("decimal(18,4)").IsRequired();
        b.Property(x => x.ChenhLechDonViCoSo).HasColumnType("decimal(18,4)").IsRequired();
        b.Property(x => x.DonGiaNhap).HasColumnType("decimal(18,2)");
        b.Property(x => x.TonSoDaChot).HasColumnType("decimal(18,4)");
        b.Property(x => x.SoLuongThucDem).HasColumnType("decimal(18,4)");
        b.Property(x => x.PhienBanTonKhoDaChot).HasColumnType("binary(8)");
        b.Property(x => x.GhiChu).HasColumnType("nvarchar(500)");
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasIndex(new[] { "MaPhieuKho", "MaNguyenLieu" }).IsUnique().HasDatabaseName("UQ_ChiTietPhieuKho_1");
        b.HasOne<PhieuKho>().WithMany().HasForeignKey("MaPhieuKho").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<NguyenLieu>().WithMany().HasForeignKey("MaNguyenLieu").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<DonViTinh>().WithMany().HasForeignKey("MaDonViNhap").OnDelete(DeleteBehavior.NoAction);
    }
}
