using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class TonKhoConfiguration : IEntityTypeConfiguration<TonKho>
{
    public void Configure(EntityTypeBuilder<TonKho> b)
    {
        b.ToTable("TonKho", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_TonKho_1", "TonThucTe >= 0 AND TongGiuCho >= 0 AND TongGiuCho <= TonThucTe");
        });
        b.HasKey("MaNguyenLieu");
        b.Property(x => x.MaNguyenLieu).ValueGeneratedNever();
        b.Property(x => x.MaNguyenLieu).HasColumnType("bigint").IsRequired();
        b.Property(x => x.TonThucTe).HasColumnType("decimal(18,4)").IsRequired();
        b.Property(x => x.TongGiuCho).HasColumnType("decimal(18,4)").IsRequired();
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasOne<NguyenLieu>().WithMany().HasForeignKey("MaNguyenLieu").OnDelete(DeleteBehavior.NoAction);
    }
}
