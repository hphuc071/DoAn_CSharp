using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class LanChiHoanTienConfiguration : IEntityTypeConfiguration<LanChiHoanTien>
{
    public void Configure(EntityTypeBuilder<LanChiHoanTien> b)
    {
        b.ToTable("LanChiHoanTien", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_LanChiHoanTien_1", "SoThuTuLanChi > 0 AND SoTien > 0");
            table.HasCheckConstraint("CK_LanChiHoanTien_2", "TrangThai IN ('PENDING','SUCCEEDED','FAILED','RECONCILING')");
            table.HasCheckConstraint("CK_LanChiHoanTien_3", "PhuongThuc IN ('CASH','BANK_TRANSFER')");
            table.HasCheckConstraint("CK_LanChiHoanTien_4", "CoKetQuaLuc IS NULL OR CoKetQuaLuc >= BatDauLuc");
        });
        b.HasKey("MaLanChiHoan");
        b.Property(x => x.MaLanChiHoan).UseIdentityColumn();
        b.Property(x => x.MaLanChiHoan).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaHoanTien).HasColumnType("bigint").IsRequired();
        b.Property(x => x.SoThuTuLanChi).HasColumnType("int").IsRequired();
        b.Property(x => x.MaChongChiLap).HasColumnType("uniqueidentifier").IsRequired();
        b.Property(x => x.SoTien).HasColumnType("decimal(18,0)").IsRequired();
        b.Property(x => x.PhuongThuc).HasColumnType("varchar(15)").IsRequired();
        b.Property(x => x.NhaCungCap).HasColumnType("varchar(50)");
        b.Property(x => x.MaThamChieuBenNgoai).HasColumnType("varchar(150)");
        b.Property(x => x.TrangThai).HasColumnType("varchar(20)").IsRequired();
        b.Property(x => x.BatDauLuc).HasColumnType("datetime2(3)").IsRequired();
        b.Property(x => x.CoKetQuaLuc).HasColumnType("datetime2(3)");
        b.Property(x => x.MaCaLamViec).HasColumnType("bigint");
        b.Property(x => x.MaQuanLyXacMinh).HasColumnType("bigint");
        b.Property(x => x.ThamChieuChungCu).HasColumnType("nvarchar(500)");
        b.Property(x => x.LyDoThatBai).HasColumnType("nvarchar(500)");
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasIndex(new[] { "MaHoanTien", "SoThuTuLanChi" }).IsUnique().HasDatabaseName("UQ_LanChiHoanTien_1");
        b.HasIndex(new[] { "MaChongChiLap" }).IsUnique().HasDatabaseName("UQ_LanChiHoanTien_2");
        b.HasOne<HoSoHoanTien>().WithMany().HasForeignKey("MaHoanTien").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<CaLamViec>().WithMany().HasForeignKey("MaCaLamViec").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TaiKhoanQuanLy>().WithMany().HasForeignKey("MaQuanLyXacMinh").OnDelete(DeleteBehavior.NoAction);
    }
}
