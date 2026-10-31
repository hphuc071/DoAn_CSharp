using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class GiuChoNguyenLieuConfiguration : IEntityTypeConfiguration<GiuChoNguyenLieu>
{
    public void Configure(EntityTypeBuilder<GiuChoNguyenLieu> b)
    {
        b.ToTable("GiuChoNguyenLieu", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_GiuChoNguyenLieu_1", "SoLuongGiuCho >= 0 AND SoLuongDaTieuHao >= 0 AND SoLuongDaGiaiPhong >= 0");
            table.HasCheckConstraint("CK_GiuChoNguyenLieu_2", "SoLuongDaTieuHao + SoLuongDaGiaiPhong <= SoLuongGiuCho");
            table.HasCheckConstraint("CK_GiuChoNguyenLieu_3", "TrangThai IN ('RESERVED','PARTIAL','CONSUMED','RELEASED','EXPIRED')");
        });
        b.HasKey("MaChiTietDonHang", "MaNguyenLieu");
        b.Property(x => x.MaChiTietDonHang).ValueGeneratedNever();
        b.Property(x => x.MaNguyenLieu).ValueGeneratedNever();
        b.Property(x => x.MaChiTietDonHang).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaNguyenLieu).HasColumnType("bigint").IsRequired();
        b.Property(x => x.SoLuongGiuCho).HasColumnType("decimal(18,4)").IsRequired();
        b.Property(x => x.SoLuongDaTieuHao).HasColumnType("decimal(18,4)").IsRequired();
        b.Property(x => x.SoLuongDaGiaiPhong).HasColumnType("decimal(18,4)").IsRequired();
        b.Property(x => x.HetHanLuc).HasColumnType("datetime2(3)");
        b.Property(x => x.TrangThai).HasColumnType("varchar(20)").IsRequired();
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasOne<NguyenLieuChiTietDonHang>().WithMany().HasForeignKey("MaChiTietDonHang", "MaNguyenLieu").OnDelete(DeleteBehavior.NoAction);
    }
}
