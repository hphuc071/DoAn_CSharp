using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class HuyMonConfiguration : IEntityTypeConfiguration<HuyMon>
{
    public void Configure(EntityTypeBuilder<HuyMon> b)
    {
        b.ToTable("HuyMon", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_HuyMon_1", "SoLuongHuy > 0");
            table.HasCheckConstraint("CK_HuyMon_2", "LEN(LTRIM(RTRIM(LyDoHuy))) > 0");
            table.HasCheckConstraint("CK_HuyMon_3", "(CASE WHEN MaKhachHangYeuCau IS NULL THEN 0 ELSE 1 END + CASE WHEN MaCaLamViec IS NULL THEN 0 ELSE 1 END + CASE WHEN MaQuanLyYeuCau IS NULL THEN 0 ELSE 1 END) = 1");
            table.HasCheckConstraint("CK_HuyMon_4", "TrangThai IN ('REQUESTED','ACCEPTED','REJECTED')");
            table.HasCheckConstraint("CK_HuyMon_5", "TienHangGiam >= 0 AND PhiGiaoHangGiam >= 0");
        });
        b.HasKey("MaHuyMon");
        b.Property(x => x.MaHuyMon).UseIdentityColumn();
        b.Property(x => x.MaHuyMon).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaNhomHuy).HasColumnType("uniqueidentifier").IsRequired();
        b.Property(x => x.MaChiTietDonHang).HasColumnType("bigint").IsRequired();
        b.Property(x => x.SoLuongHuy).HasColumnType("int").IsRequired();
        b.Property(x => x.LyDoHuy).HasColumnType("nvarchar(500)").IsRequired();
        b.Property(x => x.MaKhachHangYeuCau).HasColumnType("bigint");
        b.Property(x => x.MaCaLamViec).HasColumnType("bigint");
        b.Property(x => x.MaQuanLyYeuCau).HasColumnType("bigint");
        b.Property(x => x.MaQuanLyDuyet).HasColumnType("bigint");
        b.Property(x => x.TrangThai).HasColumnType("varchar(20)").IsRequired();
        b.Property(x => x.TienHangGiam).HasColumnType("decimal(18,0)").IsRequired();
        b.Property(x => x.PhiGiaoHangGiam).HasColumnType("decimal(18,0)").IsRequired();
        b.Property(x => x.XuLyLuc).HasColumnType("datetime2(3)");
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasOne<ChiTietDonHang>().WithMany().HasForeignKey("MaChiTietDonHang").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TaiKhoanKhachHang>().WithMany().HasForeignKey("MaKhachHangYeuCau").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<CaLamViec>().WithMany().HasForeignKey("MaCaLamViec").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TaiKhoanQuanLy>().WithMany().HasForeignKey("MaQuanLyYeuCau").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TaiKhoanQuanLy>().WithMany().HasForeignKey("MaQuanLyDuyet").OnDelete(DeleteBehavior.NoAction);
    }
}
