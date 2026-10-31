using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class DanhMucMonConfiguration : IEntityTypeConfiguration<DanhMucMon>
{
    public void Configure(EntityTypeBuilder<DanhMucMon> b)
    {
        b.ToTable("DanhMucMon", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
        });
        b.HasKey("MaDanhMuc");
        b.Property(x => x.MaDanhMuc).UseIdentityColumn();
        b.Property(x => x.MaDanhMuc).HasColumnType("bigint").IsRequired();
        b.Property(x => x.TenDanhMuc).HasColumnType("nvarchar(100)").IsRequired();
        b.Property(x => x.MoTa).HasColumnType("nvarchar(500)");
        b.Property(x => x.ThuTuHienThi).HasColumnType("int").IsRequired();
        b.Property(x => x.DaCongBo).HasColumnType("bit").IsRequired();
        b.Property(x => x.DangSuDung).HasColumnType("bit").IsRequired();
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasIndex(new[] { "TenDanhMuc" }).IsUnique().HasDatabaseName("UQ_DanhMucMon_1");
    }
}
