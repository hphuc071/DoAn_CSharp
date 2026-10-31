using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class DonViTinhConfiguration : IEntityTypeConfiguration<DonViTinh>
{
    public void Configure(EntityTypeBuilder<DonViTinh> b)
    {
        b.ToTable("DonViTinh", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
        });
        b.HasKey("MaDonVi");
        b.Property(x => x.MaDonVi).UseIdentityColumn();
        b.Property(x => x.MaDonVi).HasColumnType("bigint").IsRequired();
        b.Property(x => x.KyHieu).HasColumnType("varchar(20)").IsRequired();
        b.Property(x => x.TenDonVi).HasColumnType("nvarchar(60)").IsRequired();
        b.Property(x => x.DangSuDung).HasColumnType("bit").IsRequired();
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasIndex(new[] { "KyHieu" }).IsUnique().HasDatabaseName("UQ_DonViTinh_1");
    }
}
