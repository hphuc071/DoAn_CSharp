using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class MonThemConfiguration : IEntityTypeConfiguration<MonThem>
{
    public void Configure(EntityTypeBuilder<MonThem> b)
    {
        b.ToTable("MonThem", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_MonThem_1", "GiaMoiPhan >= 0");
            table.HasCheckConstraint("CK_MonThem_2", "GioiHanMacDinhMoiLy > 0");
        });
        b.HasKey("MaMonThem");
        b.Property(x => x.MaMonThem).UseIdentityColumn();
        b.Property(x => x.MaMonThem).HasColumnType("bigint").IsRequired();
        b.Property(x => x.TenMonThem).HasColumnType("nvarchar(100)").IsRequired();
        b.Property(x => x.MoTa).HasColumnType("nvarchar(500)");
        b.Property(x => x.GiaMoiPhan).HasColumnType("decimal(18,0)").IsRequired();
        b.Property(x => x.GioiHanMacDinhMoiLy).HasColumnType("int").IsRequired();
        b.Property(x => x.DuongDanAnh).HasColumnType("nvarchar(500)");
        b.Property(x => x.DangSuDung).HasColumnType("bit").IsRequired();
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasIndex(new[] { "TenMonThem" }).IsUnique().HasDatabaseName("UQ_MonThem_1");
    }
}
