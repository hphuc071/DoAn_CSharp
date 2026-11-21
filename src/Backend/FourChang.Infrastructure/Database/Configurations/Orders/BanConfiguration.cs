using FourChang.Domain.Entities.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Database.Configurations.Orders;

public sealed class BanConfiguration : IEntityTypeConfiguration<Ban>
{
    public void Configure(EntityTypeBuilder<Ban> b)
    {
        b.ToTable("Ban", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
        });
        b.HasKey("MaBan");
        b.Property(x => x.MaBan).UseIdentityColumn();
        b.Property(x => x.MaBan).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaBanHienThi).HasColumnType("varchar(20)").IsRequired();
        b.Property(x => x.TenBan).HasColumnType("nvarchar(100)").IsRequired();
        b.Property(x => x.DangSuDung).HasColumnType("bit").IsRequired();
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasIndex(new[] { "MaBanHienThi" }).IsUnique().HasDatabaseName("UQ_Ban_1");
    }
}
