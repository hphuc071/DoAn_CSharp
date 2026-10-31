using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class MonThemChiTietDonHangConfiguration : IEntityTypeConfiguration<MonThemChiTietDonHang>
{
    public void Configure(EntityTypeBuilder<MonThemChiTietDonHang> b)
    {
        b.ToTable("MonThemChiTietDonHang", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_MonThemChiTietDonHang_1", "SoPhanMoiLy > 0");
            table.HasCheckConstraint("CK_MonThemChiTietDonHang_2", "GiaMoiPhanDaChot >= 0");
        });
        b.HasKey("MaChiTietDonHang", "MaMonThem");
        b.Property(x => x.MaChiTietDonHang).ValueGeneratedNever();
        b.Property(x => x.MaMonThem).ValueGeneratedNever();
        b.Property(x => x.MaChiTietDonHang).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaMonThem).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaCongThuc).HasColumnType("bigint").IsRequired();
        b.Property(x => x.TenMonThemDaChot).HasColumnType("nvarchar(100)").IsRequired();
        b.Property(x => x.SoPhanMoiLy).HasColumnType("int").IsRequired();
        b.Property(x => x.GiaMoiPhanDaChot).HasColumnType("decimal(18,0)").IsRequired();
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasOne<ChiTietDonHang>().WithMany().HasForeignKey("MaChiTietDonHang").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<MonThem>().WithMany().HasForeignKey("MaMonThem").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<CongThuc>().WithMany().HasForeignKey("MaCongThuc").OnDelete(DeleteBehavior.NoAction);
    }
}
