using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class MonConfiguration : IEntityTypeConfiguration<Mon>
{
    public void Configure(EntityTypeBuilder<Mon> b)
    {
        b.ToTable("Mon", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
        });
        b.HasKey("MaMon");
        b.Property(x => x.MaMon).UseIdentityColumn();
        b.Property(x => x.MaMon).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaDanhMuc).HasColumnType("bigint").IsRequired();
        b.Property(x => x.TenMon).HasColumnType("nvarchar(150)").IsRequired();
        b.Property(x => x.MoTa).HasColumnType("nvarchar(1000)");
        b.Property(x => x.DuongDanAnh).HasColumnType("nvarchar(500)");
        b.Property(x => x.ThuTuHienThi).HasColumnType("int").IsRequired();
        b.Property(x => x.DaCongBo).HasColumnType("bit").IsRequired();
        b.Property(x => x.DangBan).HasColumnType("bit").IsRequired();
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasIndex(new[] { "MaDanhMuc", "TenMon" }).IsUnique().HasDatabaseName("UQ_Mon_1");
        b.HasOne<DanhMucMon>().WithMany().HasForeignKey("MaDanhMuc").OnDelete(DeleteBehavior.NoAction);
    }
}
