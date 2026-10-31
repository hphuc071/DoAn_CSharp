using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class TaiKhoanQuanLyConfiguration : IEntityTypeConfiguration<TaiKhoanQuanLy>
{
    public void Configure(EntityTypeBuilder<TaiKhoanQuanLy> b)
    {
        b.ToTable("TaiKhoanQuanLy", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_TaiKhoanQuanLy_1", "MaQuanLy = 1");
            table.HasCheckConstraint("CK_TaiKhoanQuanLy_2", "TrangThai IN ('ACTIVE','LOCKED','INACTIVE')");
            table.HasCheckConstraint("CK_TaiKhoanQuanLy_3", "SoLanDangNhapSai >= 0");
        });
        b.HasKey("MaQuanLy");
        b.Property(x => x.MaQuanLy).UseIdentityColumn();
        b.Property(x => x.MaQuanLy).HasColumnType("bigint").IsRequired();
        b.Property(x => x.TenDangNhapChuanHoa).HasColumnType("nvarchar(100)").IsRequired();
        b.Property(x => x.MatKhauBam).HasColumnType("nvarchar(512)").IsRequired();
        b.Property(x => x.TrangThai).HasColumnType("varchar(15)").IsRequired();
        b.Property(x => x.BatBuocDoiMatKhau).HasColumnType("bit").IsRequired();
        b.Property(x => x.SoLanDangNhapSai).HasColumnType("int").IsRequired();
        b.Property(x => x.KhoaDenLuc).HasColumnType("datetime2(3)");
        b.Property(x => x.DangNhapLanCuoiLuc).HasColumnType("datetime2(3)");
        b.Property(x => x.HoTen).HasColumnType("nvarchar(120)").IsRequired();
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasIndex(new[] { "TenDangNhapChuanHoa" }).IsUnique().HasDatabaseName("UQ_TaiKhoanQuanLy_1");
    }
}
