using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class CongThucConfiguration : IEntityTypeConfiguration<CongThuc>
{
    public void Configure(EntityTypeBuilder<CongThuc> b)
    {
        b.ToTable("CongThuc", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_CongThuc_1", "(MaCauHinhMon IS NOT NULL AND MaMonThem IS NULL) OR (MaCauHinhMon IS NULL AND MaMonThem IS NOT NULL)");
            table.HasCheckConstraint("CK_CongThuc_2", "SoPhienBan > 0");
            table.HasCheckConstraint("CK_CongThuc_3", "HieuLucDen IS NULL OR (HieuLucTu IS NOT NULL AND HieuLucDen > HieuLucTu)");
            table.HasCheckConstraint("CK_CongThuc_4", "TrangThai IN ('DRAFT','SCHEDULED','ACTIVE','RETIRED')");
            table.HasCheckConstraint("CK_CongThuc_5", "TrangThai <> 'ACTIVE' OR (HieuLucTu IS NOT NULL AND MaQuanLyDuyet IS NOT NULL)");
        });
        b.HasKey("MaCongThuc");
        b.Property(x => x.MaCongThuc).UseIdentityColumn();
        b.Property(x => x.MaCongThuc).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaCauHinhMon).HasColumnType("bigint");
        b.Property(x => x.MaMonThem).HasColumnType("bigint");
        b.Property(x => x.SoPhienBan).HasColumnType("int").IsRequired();
        b.Property(x => x.TenCongThuc).HasColumnType("nvarchar(150)").IsRequired();
        b.Property(x => x.TrangThai).HasColumnType("varchar(15)").IsRequired();
        b.Property(x => x.HieuLucTu).HasColumnType("datetime2(3)");
        b.Property(x => x.HieuLucDen).HasColumnType("datetime2(3)");
        b.Property(x => x.MaQuanLyTao).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaQuanLyDuyet).HasColumnType("bigint");
        b.Property(x => x.GhiChu).HasColumnType("nvarchar(500)");
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasIndex(new[] { "MaCauHinhMon", "SoPhienBan" }).IsUnique().HasDatabaseName("UX_CongThuc_CauHinhPhienBan").HasFilter("MaCauHinhMon IS NOT NULL");
        b.HasIndex(new[] { "MaMonThem", "SoPhienBan" }).IsUnique().HasDatabaseName("UX_CongThuc_MonThemPhienBan").HasFilter("MaMonThem IS NOT NULL");
        b.HasIndex(new[] { "MaCauHinhMon" }).IsUnique().HasDatabaseName("UX_CongThuc_CauHinhHieuLuc").HasFilter("MaCauHinhMon IS NOT NULL AND TrangThai = 'ACTIVE'");
        b.HasIndex(new[] { "MaMonThem" }).IsUnique().HasDatabaseName("UX_CongThuc_MonThemHieuLuc").HasFilter("MaMonThem IS NOT NULL AND TrangThai = 'ACTIVE'");
        b.HasOne<CauHinhMon>().WithMany().HasForeignKey("MaCauHinhMon").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<MonThem>().WithMany().HasForeignKey("MaMonThem").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TaiKhoanQuanLy>().WithMany().HasForeignKey("MaQuanLyTao").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TaiKhoanQuanLy>().WithMany().HasForeignKey("MaQuanLyDuyet").OnDelete(DeleteBehavior.NoAction);
    }
}
