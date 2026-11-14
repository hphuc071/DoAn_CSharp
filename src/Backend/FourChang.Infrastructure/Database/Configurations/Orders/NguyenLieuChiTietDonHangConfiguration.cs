using FourChang.Domain.Entities.Inventory;
using FourChang.Domain.Entities.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Database.Configurations.Orders;

public sealed class NguyenLieuChiTietDonHangConfiguration : IEntityTypeConfiguration<NguyenLieuChiTietDonHang>
{
    public void Configure(EntityTypeBuilder<NguyenLieuChiTietDonHang> b)
    {
        b.ToTable("NguyenLieuChiTietDonHang", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_NguyenLieuDon_SoLuong", "SoLuongGiuCho >= 0 AND SoLuongDaTieuHao >= 0 AND SoLuongDaGiaiPhong >= 0 AND SoLuongDaTieuHao + SoLuongDaGiaiPhong <= SoLuongGiuCho");
            table.HasCheckConstraint("CK_NguyenLieuDon_TrangThai", "TrangThai IN ('RESERVED','PARTIAL','CONSUMED','RELEASED','EXPIRED')");
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
        b.Property(x=>x.SoLuongGiuCho).HasColumnType("decimal(18,4)");
        b.Property(x=>x.SoLuongDaTieuHao).HasColumnType("decimal(18,4)");
        b.Property(x=>x.SoLuongDaGiaiPhong).HasColumnType("decimal(18,4)");
        b.Property(x=>x.HetHanLuc).HasColumnType("datetime2(3)");
        b.Property(x=>x.TrangThai).HasColumnType("varchar(20)").IsRequired();
        b.Property(x=>x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x=>x.PhienBanDuLieu).IsRowVersion();
        b.HasOne<ChiTietDonHang>().WithMany().HasForeignKey("MaChiTietDonHang").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<NguyenLieu>().WithMany().HasForeignKey("MaNguyenLieu").OnDelete(DeleteBehavior.NoAction);
    }
}
