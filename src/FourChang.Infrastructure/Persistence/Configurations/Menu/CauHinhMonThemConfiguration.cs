using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class CauHinhMonThemConfiguration : IEntityTypeConfiguration<CauHinhMonThem>
{
    public void Configure(EntityTypeBuilder<CauHinhMonThem> b)
    {
        b.ToTable("CauHinhMonThem", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_CauHinhMonThem_1", "GioiHanMoiLy > 0");
        });
        b.HasKey("MaCauHinhMon", "MaMonThem");
        b.Property(x => x.MaCauHinhMon).ValueGeneratedNever();
        b.Property(x => x.MaMonThem).ValueGeneratedNever();
        b.Property(x => x.MaCauHinhMon).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaMonThem).HasColumnType("bigint").IsRequired();
        b.Property(x => x.GioiHanMoiLy).HasColumnType("int").IsRequired();
        b.Property(x => x.DangSuDung).HasColumnType("bit").IsRequired();
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasOne<CauHinhMon>().WithMany().HasForeignKey("MaCauHinhMon").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<MonThem>().WithMany().HasForeignKey("MaMonThem").OnDelete(DeleteBehavior.NoAction);
    }
}
