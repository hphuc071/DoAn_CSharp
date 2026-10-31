using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class NhatKyHeThongConfiguration : IEntityTypeConfiguration<NhatKyHeThong>
{
    public void Configure(EntityTypeBuilder<NhatKyHeThong> b)
    {
        b.ToTable("NhatKyHeThong", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_NhatKyHeThong_1", "(LoaiTacNhan = 'CUSTOMER' AND MaKhachHang IS NOT NULL AND MaQuanLy IS NULL AND MaCaLamViec IS NULL) OR (LoaiTacNhan = 'ADMIN' AND MaQuanLy IS NOT NULL AND MaKhachHang IS NULL AND MaCaLamViec IS NULL) OR (LoaiTacNhan = 'STAFF' AND MaCaLamViec IS NOT NULL AND MaKhachHang IS NULL AND MaQuanLy IS NULL) OR (LoaiTacNhan = 'SYSTEM' AND MaKhachHang IS NULL AND MaQuanLy IS NULL AND MaCaLamViec IS NULL)");
            table.HasCheckConstraint("CK_NhatKyHeThong_2", "DuLieuTruocJson IS NULL OR ISJSON(DuLieuTruocJson) = 1");
            table.HasCheckConstraint("CK_NhatKyHeThong_3", "DuLieuSauJson IS NULL OR ISJSON(DuLieuSauJson) = 1");
            table.HasCheckConstraint("CK_NhatKyHeThong_4", "KetQuaDaChotJson IS NULL OR ISJSON(KetQuaDaChotJson) = 1");
        });
        b.HasKey("MaNhatKy");
        b.Property(x => x.MaNhatKy).UseIdentityColumn();
        b.Property(x => x.MaNhatKy).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaLienKetXuLy).HasColumnType("uniqueidentifier").IsRequired();
        b.Property(x => x.MaThaoTac).HasColumnType("uniqueidentifier");
        b.Property(x => x.LoaiTacNhan).HasColumnType("varchar(15)").IsRequired();
        b.Property(x => x.MaKhachHang).HasColumnType("bigint");
        b.Property(x => x.MaQuanLy).HasColumnType("bigint");
        b.Property(x => x.MaCaLamViec).HasColumnType("bigint");
        b.Property(x => x.TenNguoiThucHienDaChot).HasColumnType("nvarchar(120)").IsRequired();
        b.Property(x => x.MaMayDaChot).HasColumnType("varchar(50)");
        b.Property(x => x.HanhDong).HasColumnType("varchar(100)").IsRequired();
        b.Property(x => x.LoaiDoiTuong).HasColumnType("varchar(100)").IsRequired();
        b.Property(x => x.MaDoiTuong).HasColumnType("varchar(100)");
        b.Property(x => x.DuLieuTruocJson).HasColumnType("nvarchar(max)");
        b.Property(x => x.DuLieuSauJson).HasColumnType("nvarchar(max)");
        b.Property(x => x.LyDo).HasColumnType("nvarchar(500)");
        b.Property(x => x.KetQua).HasColumnType("varchar(20)").IsRequired();
        b.Property(x => x.MaBamYeuCau).HasColumnType("binary(32)");
        b.Property(x => x.KetQuaDaChotJson).HasColumnType("nvarchar(max)");
        b.Property(x => x.PhatSinhLuc).HasColumnType("datetime2(3)").IsRequired();
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.HasIndex(new[] { "MaThaoTac" }).IsUnique().HasDatabaseName("UX_NhatKy_ThaoTac").HasFilter("MaThaoTac IS NOT NULL");
        b.HasOne<TaiKhoanKhachHang>().WithMany().HasForeignKey("MaKhachHang").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TaiKhoanQuanLy>().WithMany().HasForeignKey("MaQuanLy").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<CaLamViec>().WithMany().HasForeignKey("MaCaLamViec").OnDelete(DeleteBehavior.NoAction);
    }
}
