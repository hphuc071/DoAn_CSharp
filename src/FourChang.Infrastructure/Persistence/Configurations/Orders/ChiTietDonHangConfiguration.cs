using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class ChiTietDonHangConfiguration : IEntityTypeConfiguration<ChiTietDonHang>
{
    public void Configure(EntityTypeBuilder<ChiTietDonHang> b)
    {
        b.ToTable("ChiTietDonHang", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_ChiTietDonHang_1", "GiaMonDaChot >= 0");
            table.HasCheckConstraint("CK_ChiTietDonHang_2", "SoLuongDat > 0 AND SoLuongBatDauPha >= 0 AND SoLuongPhaXong >= 0 AND SoLuongDaHuy >= 0");
            table.HasCheckConstraint("CK_ChiTietDonHang_3", "SoLuongBatDauPha + SoLuongDaHuy <= SoLuongDat");
            table.HasCheckConstraint("CK_ChiTietDonHang_4", "SoLuongPhaXong <= SoLuongBatDauPha");
            table.HasCheckConstraint("CK_ChiTietDonHang_5", "PhanTramDuong IN (0,25,50,75,100) AND PhanTramDa IN (0,25,50,75,100)");
        });
        b.HasKey("MaChiTietDonHang");
        b.Property(x => x.MaChiTietDonHang).UseIdentityColumn();
        b.Property(x => x.MaChiTietDonHang).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaDonHang).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaCauHinhMon).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaCongThuc).HasColumnType("bigint").IsRequired();
        b.Property(x => x.TenMonDaChot).HasColumnType("nvarchar(150)").IsRequired();
        b.Property(x => x.KichCoDaChot).HasColumnType("varchar(5)").IsRequired();
        b.Property(x => x.NhietDoDaChot).HasColumnType("varchar(10)").IsRequired();
        b.Property(x => x.PhanTramDuong).HasColumnType("int").IsRequired();
        b.Property(x => x.PhanTramDa).HasColumnType("int").IsRequired();
        b.Property(x => x.GiaMonDaChot).HasColumnType("decimal(18,0)").IsRequired();
        b.Property(x => x.SoLuongDat).HasColumnType("int").IsRequired();
        b.Property(x => x.SoLuongBatDauPha).HasColumnType("int").IsRequired();
        b.Property(x => x.SoLuongPhaXong).HasColumnType("int").IsRequired();
        b.Property(x => x.SoLuongDaHuy).HasColumnType("int").IsRequired();
        b.Property(x => x.GhiChu).HasColumnType("nvarchar(500)");
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasOne<DonHang>().WithMany().HasForeignKey("MaDonHang").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<CauHinhMon>().WithMany().HasForeignKey("MaCauHinhMon").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<CongThuc>().WithMany().HasForeignKey("MaCongThuc").OnDelete(DeleteBehavior.NoAction);
    }
}
