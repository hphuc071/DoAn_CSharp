using FourChang.Domain.Entities.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Database.Configurations.Accounts;

public sealed class DiaChiKhachHangConfiguration : IEntityTypeConfiguration<DiaChiKhachHang>
{
    /// <summary>Ánh xạ thuộc tính, khóa và ràng buộc SQL của DiaChiKhachHang; giữ quan hệ dữ liệu nhất quán.</summary>
    public void Configure(EntityTypeBuilder<DiaChiKhachHang> b)
    {
        b.ToTable("DiaChiKhachHang", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            // Cùng CHECK với script nâng cấp SQL: nhập trực tiếp SQL cũng không được lưu số sai.
            table.HasCheckConstraint("CK_DiaChiKhachHang_DienThoai10", "DATALENGTH(DienThoaiNguoiNhan)=10 AND DienThoaiNguoiNhan COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9]%'");
        });
        b.HasKey("MaDiaChi");
        b.Property(x => x.MaDiaChi).UseIdentityColumn();
        b.Property(x => x.MaDiaChi).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaKhachHang).HasColumnType("bigint").IsRequired();
        b.Property(x => x.NhanDiaChi).HasColumnType("nvarchar(50)");
        b.Property(x => x.TenNguoiNhan).HasColumnType("nvarchar(120)").IsRequired();
        b.Property(x => x.DienThoaiNguoiNhan).HasColumnType("varchar(20)").IsRequired();
        b.Property(x => x.DiaChiChiTiet).HasColumnType("nvarchar(500)").IsRequired();
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
