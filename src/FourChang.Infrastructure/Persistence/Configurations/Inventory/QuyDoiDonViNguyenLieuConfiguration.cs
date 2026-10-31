using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class QuyDoiDonViNguyenLieuConfiguration : IEntityTypeConfiguration<QuyDoiDonViNguyenLieu>
{
    public void Configure(EntityTypeBuilder<QuyDoiDonViNguyenLieu> b)
    {
        b.ToTable("QuyDoiDonViNguyenLieu", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_QuyDoiDonViNguyenLieu_1", "HeSoVeDonViCoSo > 0");
        });
        b.HasKey("MaNguyenLieu", "MaDonVi");
        b.Property(x => x.MaNguyenLieu).ValueGeneratedNever();
        b.Property(x => x.MaDonVi).ValueGeneratedNever();
        b.Property(x => x.MaNguyenLieu).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaDonVi).HasColumnType("bigint").IsRequired();
        b.Property(x => x.HeSoVeDonViCoSo).HasColumnType("decimal(18,6)").IsRequired();
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasOne<NguyenLieu>().WithMany().HasForeignKey("MaNguyenLieu").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<DonViTinh>().WithMany().HasForeignKey("MaDonVi").OnDelete(DeleteBehavior.NoAction);
    }
}
