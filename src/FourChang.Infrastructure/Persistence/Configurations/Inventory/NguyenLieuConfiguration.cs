using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class NguyenLieuConfiguration : IEntityTypeConfiguration<NguyenLieu>
{
    public void Configure(EntityTypeBuilder<NguyenLieu> b)
    {
        b.ToTable("NguyenLieu", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_NguyenLieu_1", "TonAnToan >= 0 AND NguongCanhBao >= TonAnToan");
        });
        b.HasKey("MaNguyenLieu");
        b.Property(x => x.MaNguyenLieu).UseIdentityColumn();
        b.Property(x => x.MaNguyenLieu).HasColumnType("bigint").IsRequired();
        b.Property(x => x.TenNguyenLieu).HasColumnType("nvarchar(150)").IsRequired();
        b.Property(x => x.MaDonViCoSo).HasColumnType("bigint").IsRequired();
        b.Property(x => x.TonAnToan).HasColumnType("decimal(18,4)").IsRequired();
        b.Property(x => x.NguongCanhBao).HasColumnType("decimal(18,4)").IsRequired();
        b.Property(x => x.MoTa).HasColumnType("nvarchar(500)");
        b.Property(x => x.DangSuDung).HasColumnType("bit").IsRequired();
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasIndex(new[] { "TenNguyenLieu" }).IsUnique().HasDatabaseName("UQ_NguyenLieu_1");
        b.HasOne<DonViTinh>().WithMany().HasForeignKey("MaDonViCoSo").OnDelete(DeleteBehavior.NoAction);
    }
}
