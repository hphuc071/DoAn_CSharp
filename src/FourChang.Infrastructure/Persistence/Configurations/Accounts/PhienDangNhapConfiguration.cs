using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class PhienDangNhapConfiguration : IEntityTypeConfiguration<PhienDangNhap>
{
    public void Configure(EntityTypeBuilder<PhienDangNhap> b)
    {
        b.ToTable("PhienDangNhap", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_PhienDangNhap_1", "(MaKhachHang IS NOT NULL AND MaQuanLy IS NULL) OR (MaKhachHang IS NULL AND MaQuanLy IS NOT NULL)");
            table.HasCheckConstraint("CK_PhienDangNhap_2", "HetHanLuc > ThoiDiemTao");
            table.HasCheckConstraint("CK_PhienDangNhap_3", "PhamViPhien IN ('FULL','PASSWORD_CHANGE')");
        });
        b.HasKey("MaPhienDangNhap");
        b.Property(x => x.MaPhienDangNhap).UseIdentityColumn();
        b.Property(x => x.MaPhienDangNhap).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaKhachHang).HasColumnType("bigint");
        b.Property(x => x.MaQuanLy).HasColumnType("bigint");
        b.Property(x => x.MaLamMoiDaBam).HasColumnType("binary(32)").IsRequired();
        b.Property(x => x.PhamViPhien).HasColumnType("varchar(20)").IsRequired();
        b.Property(x => x.LoaiUngDung).HasColumnType("varchar(15)").IsRequired();
        b.Property(x => x.ThongTinThietBi).HasColumnType("nvarchar(300)");
        b.Property(x => x.HetHanLuc).HasColumnType("datetime2(3)").IsRequired();
        b.Property(x => x.HoatDongLanCuoiLuc).HasColumnType("datetime2(3)");
        b.Property(x => x.ThuHoiLuc).HasColumnType("datetime2(3)");
        b.Property(x => x.LyDoThuHoi).HasColumnType("nvarchar(250)");
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasIndex(new[] { "MaLamMoiDaBam" }).IsUnique().HasDatabaseName("UQ_PhienDangNhap_1");
        b.HasOne<TaiKhoanKhachHang>().WithMany().HasForeignKey("MaKhachHang").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TaiKhoanQuanLy>().WithMany().HasForeignKey("MaQuanLy").OnDelete(DeleteBehavior.NoAction);
    }
}
