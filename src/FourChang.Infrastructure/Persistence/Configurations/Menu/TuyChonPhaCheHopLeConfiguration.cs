using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class TuyChonPhaCheHopLeConfiguration : IEntityTypeConfiguration<TuyChonPhaCheHopLe>
{
    public void Configure(EntityTypeBuilder<TuyChonPhaCheHopLe> b)
    {
        b.ToTable("TuyChonPhaCheHopLe", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_TuyChonPhaCheHopLe_1", "PhanTramDuong IN (0,25,50,75,100)");
            table.HasCheckConstraint("CK_TuyChonPhaCheHopLe_2", "PhanTramDa IN (0,25,50,75,100)");
        });
        b.HasKey("MaCauHinhMon", "PhanTramDuong", "PhanTramDa");
        b.Property(x => x.MaCauHinhMon).ValueGeneratedNever();
        b.Property(x => x.PhanTramDuong).ValueGeneratedNever();
        b.Property(x => x.PhanTramDa).ValueGeneratedNever();
        b.Property(x => x.MaCauHinhMon).HasColumnType("bigint").IsRequired();
        b.Property(x => x.PhanTramDuong).HasColumnType("int").IsRequired();
        b.Property(x => x.PhanTramDa).HasColumnType("int").IsRequired();
        b.Property(x => x.DangSuDung).HasColumnType("bit").IsRequired();
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasOne<CauHinhMon>().WithMany().HasForeignKey("MaCauHinhMon").OnDelete(DeleteBehavior.NoAction);
    }
}
