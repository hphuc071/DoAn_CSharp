using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class BanConfiguration : IEntityTypeConfiguration<Ban>
{
    public void Configure(EntityTypeBuilder<Ban> b)
    {
        b.ToTable("Ban", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_Ban_1", "SoChoNgoi IS NULL OR SoChoNgoi > 0");
            table.HasCheckConstraint("CK_Ban_2", "PhienBanQR > 0");
        });
        b.HasKey("MaBan");
        b.Property(x => x.MaBan).UseIdentityColumn();
        b.Property(x => x.MaBan).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaBanHienThi).HasColumnType("varchar(20)").IsRequired();
        b.Property(x => x.TenBan).HasColumnType("nvarchar(100)").IsRequired();
        b.Property(x => x.TenKhuVuc).HasColumnType("nvarchar(100)");
        b.Property(x => x.SoChoNgoi).HasColumnType("int");
        b.Property(x => x.MaBamQR).HasColumnType("binary(32)").IsRequired();
        b.Property(x => x.PhienBanQR).HasColumnType("int").IsRequired();
        b.Property(x => x.QRHetHanLuc).HasColumnType("datetime2(3)");
        b.Property(x => x.DangSuDung).HasColumnType("bit").IsRequired();
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasIndex(new[] { "MaBanHienThi" }).IsUnique().HasDatabaseName("UQ_Ban_1");
        b.HasIndex(new[] { "MaBamQR" }).IsUnique().HasDatabaseName("UQ_Ban_2");
    }
}
