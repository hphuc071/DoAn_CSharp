using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class DiaChiKhachHangConfiguration : IEntityTypeConfiguration<DiaChiKhachHang>
{
    public void Configure(EntityTypeBuilder<DiaChiKhachHang> b)
    {
        b.ToTable("DiaChiKhachHang", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_DiaChiKhachHang_1", "ViDo IS NULL OR ViDo BETWEEN -90 AND 90");
            table.HasCheckConstraint("CK_DiaChiKhachHang_2", "KinhDo IS NULL OR KinhDo BETWEEN -180 AND 180");
            table.HasCheckConstraint("CK_DiaChiKhachHang_3", "(ViDo IS NULL AND KinhDo IS NULL) OR (ViDo IS NOT NULL AND KinhDo IS NOT NULL)");
        });
        b.HasKey("MaDiaChi");
        b.Property(x => x.MaDiaChi).UseIdentityColumn();
        b.Property(x => x.MaDiaChi).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaKhachHang).HasColumnType("bigint").IsRequired();
        b.Property(x => x.NhanDiaChi).HasColumnType("nvarchar(50)");
        b.Property(x => x.TenNguoiNhan).HasColumnType("nvarchar(120)").IsRequired();
        b.Property(x => x.DienThoaiNguoiNhan).HasColumnType("varchar(20)").IsRequired();
        b.Property(x => x.DiaChiChiTiet).HasColumnType("nvarchar(500)").IsRequired();
        b.Property(x => x.ViDo).HasColumnType("decimal(9,6)");
        b.Property(x => x.KinhDo).HasColumnType("decimal(9,6)");
        b.Property(x => x.GhiChuGiaoHang).HasColumnType("nvarchar(500)");
        b.Property(x => x.LaDiaChiMacDinh).HasColumnType("bit").IsRequired();
        b.Property(x => x.DangSuDung).HasColumnType("bit").IsRequired();
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasIndex(new[] { "MaKhachHang" }).IsUnique().HasDatabaseName("UX_DiaChi_MacDinh").HasFilter("LaDiaChiMacDinh = 1 AND DangSuDung = 1");
        b.HasOne<TaiKhoanKhachHang>().WithMany().HasForeignKey("MaKhachHang").OnDelete(DeleteBehavior.NoAction);
    }
}
