using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class NguyenLieuChiTietDonHangConfiguration : IEntityTypeConfiguration<NguyenLieuChiTietDonHang>
{
    public void Configure(EntityTypeBuilder<NguyenLieuChiTietDonHang> b)
    {
        b.ToTable("NguyenLieuChiTietDonHang", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_NguyenLieuChiTietDonHang_1", "DinhMucMoiLy > 0");
        });
        b.HasKey("MaChiTietDonHang", "MaNguyenLieu");
        b.Property(x => x.MaChiTietDonHang).ValueGeneratedNever();
        b.Property(x => x.MaNguyenLieu).ValueGeneratedNever();
        b.Property(x => x.MaChiTietDonHang).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaNguyenLieu).HasColumnType("bigint").IsRequired();
        b.Property(x => x.TenNguyenLieuDaChot).HasColumnType("nvarchar(150)").IsRequired();
        b.Property(x => x.DonViCoSoDaChot).HasColumnType("nvarchar(60)").IsRequired();
        b.Property(x => x.DinhMucMoiLy).HasColumnType("decimal(18,4)").IsRequired();
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.HasOne<ChiTietDonHang>().WithMany().HasForeignKey("MaChiTietDonHang").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<NguyenLieu>().WithMany().HasForeignKey("MaNguyenLieu").OnDelete(DeleteBehavior.NoAction);
    }
}
