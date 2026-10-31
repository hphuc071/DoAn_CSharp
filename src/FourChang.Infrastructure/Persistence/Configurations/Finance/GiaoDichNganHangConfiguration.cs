using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class GiaoDichNganHangConfiguration : IEntityTypeConfiguration<GiaoDichNganHang>
{
    public void Configure(EntityTypeBuilder<GiaoDichNganHang> b)
    {
        b.ToTable("GiaoDichNganHang", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_GiaoDichNganHang_1", "SoTien > 0");
            table.HasCheckConstraint("CK_GiaoDichNganHang_2", "ChieuGiaoDich IN ('IN','OUT')");
            table.HasCheckConstraint("CK_GiaoDichNganHang_3", "MaThanhToan IS NULL OR MaLanChiHoan IS NULL");
            table.HasCheckConstraint("CK_GiaoDichNganHang_4", "MaThanhToan IS NULL OR ChieuGiaoDich = 'IN'");
            table.HasCheckConstraint("CK_GiaoDichNganHang_5", "MaLanChiHoan IS NULL OR ChieuGiaoDich = 'OUT'");
        });
        b.HasKey("MaGiaoDichNganHang");
        b.Property(x => x.MaGiaoDichNganHang).UseIdentityColumn();
        b.Property(x => x.MaGiaoDichNganHang).HasColumnType("bigint").IsRequired();
        b.Property(x => x.TenNhaCungCap).HasColumnType("varchar(50)").IsRequired();
        b.Property(x => x.MaTaiKhoanNganHangQuan).HasColumnType("varchar(100)").IsRequired();
        b.Property(x => x.MaGiaoDichBenNgoai).HasColumnType("varchar(150)").IsRequired();
        b.Property(x => x.ChieuGiaoDich).HasColumnType("varchar(5)").IsRequired();
        b.Property(x => x.SoTien).HasColumnType("decimal(18,0)").IsRequired();
        b.Property(x => x.NoiDungChuyenKhoan).HasColumnType("nvarchar(500)");
        b.Property(x => x.ThoiDiemNganHang).HasColumnType("datetime2(3)").IsRequired();
        b.Property(x => x.TrangThaiXacMinh).HasColumnType("varchar(20)").IsRequired();
        b.Property(x => x.XacMinhLuc).HasColumnType("datetime2(3)");
        b.Property(x => x.MaThanhToan).HasColumnType("bigint");
        b.Property(x => x.MaLanChiHoan).HasColumnType("bigint");
        b.Property(x => x.MaBamNoiDung).HasColumnType("binary(32)");
        b.Property(x => x.ThamChieuChungCu).HasColumnType("nvarchar(500)");
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasIndex(new[] { "TenNhaCungCap", "MaTaiKhoanNganHangQuan", "MaGiaoDichBenNgoai" }).IsUnique().HasDatabaseName("UQ_GiaoDichNganHang_1");
        b.HasOne<ThanhToan>().WithMany().HasForeignKey("MaThanhToan").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<LanChiHoanTien>().WithMany().HasForeignKey("MaLanChiHoan").OnDelete(DeleteBehavior.NoAction);
    }
}
