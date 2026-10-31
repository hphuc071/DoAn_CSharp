using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class HoaDonConfiguration : IEntityTypeConfiguration<HoaDon>
{
    public void Configure(EntityTypeBuilder<HoaDon> b)
    {
        b.ToTable("HoaDon", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_HoaDon_1", "LoaiHoaDon IN ('ORIGINAL','ADJUSTMENT')");
            table.HasCheckConstraint("CK_HoaDon_2", "(LoaiHoaDon = 'ORIGINAL' AND MaHoaDonGoc IS NULL AND SoTien >= 0) OR (LoaiHoaDon = 'ADJUSTMENT' AND MaHoaDonGoc IS NOT NULL AND MaHoaDonGoc <> MaHoaDon)");
            table.HasCheckConstraint("CK_HoaDon_3", "TrangThai IN ('DRAFT','ISSUED')");
            table.HasCheckConstraint("CK_HoaDon_4", "TrangThai <> 'ISSUED' OR PhatHanhLuc IS NOT NULL");
            table.HasCheckConstraint("CK_HoaDon_5", "ISJSON(NoiDungHoaDonDaChotJson) = 1");
        });
        b.HasKey("MaHoaDon");
        b.Property(x => x.MaHoaDon).UseIdentityColumn();
        b.Property(x => x.MaHoaDon).HasColumnType("bigint").IsRequired();
        b.Property(x => x.SoHoaDon).HasColumnType("varchar(40)").IsRequired();
        b.Property(x => x.MaDonHang).HasColumnType("bigint").IsRequired();
        b.Property(x => x.LoaiHoaDon).HasColumnType("varchar(15)").IsRequired();
        b.Property(x => x.MaHoaDonGoc).HasColumnType("bigint");
        b.Property(x => x.MaHuyMon).HasColumnType("bigint");
        b.Property(x => x.TenNguoiMua).HasColumnType("nvarchar(150)");
        b.Property(x => x.MaSoThueNguoiMua).HasColumnType("varchar(30)");
        b.Property(x => x.SoTien).HasColumnType("decimal(18,0)").IsRequired();
        b.Property(x => x.NoiDungHoaDonDaChotJson).HasColumnType("nvarchar(max)").IsRequired();
        b.Property(x => x.TrangThai).HasColumnType("varchar(15)").IsRequired();
        b.Property(x => x.LyDo).HasColumnType("nvarchar(500)");
        b.Property(x => x.PhatHanhLuc).HasColumnType("datetime2(3)");
        b.Property(x => x.MaQuanLyPhatHanh).HasColumnType("bigint");
        b.Property(x => x.MaCaLamViec).HasColumnType("bigint");
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasIndex(new[] { "SoHoaDon" }).IsUnique().HasDatabaseName("UQ_HoaDon_1");
        b.HasIndex(new[] { "MaDonHang" }).IsUnique().HasDatabaseName("UX_HoaDon_Goc").HasFilter("LoaiHoaDon = 'ORIGINAL'");
        b.HasOne<DonHang>().WithMany().HasForeignKey("MaDonHang").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<HoaDon>().WithMany().HasForeignKey("MaHoaDonGoc").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<HuyMon>().WithMany().HasForeignKey("MaHuyMon").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TaiKhoanQuanLy>().WithMany().HasForeignKey("MaQuanLyPhatHanh").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<CaLamViec>().WithMany().HasForeignKey("MaCaLamViec").OnDelete(DeleteBehavior.NoAction);
    }
}
