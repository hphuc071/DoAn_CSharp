using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class NhanVienConfiguration : IEntityTypeConfiguration<NhanVien>
{
    public void Configure(EntityTypeBuilder<NhanVien> b)
    {
        b.ToTable("NhanVien", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_NhanVien_1", "TrangThai IN ('ACTIVE','INACTIVE')");
            table.HasCheckConstraint("CK_NhanVien_2", "LEN(LTRIM(RTRIM(HoTen))) > 0");
        });
        b.HasKey("MaNhanVien");
        b.Property(x => x.MaNhanVien).UseIdentityColumn();
        b.Property(x => x.MaNhanVien).HasColumnType("bigint").IsRequired();
        b.Property(x => x.HoTen).HasColumnType("nvarchar(120)").IsRequired();
        b.Property(x => x.SoDienThoai).HasColumnType("varchar(20)");
        b.Property(x => x.NgayVaoLam).HasColumnType("datetime2(3)").IsRequired();
        b.Property(x => x.TrangThai).HasColumnType("varchar(15)").IsRequired();
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
    }
}
