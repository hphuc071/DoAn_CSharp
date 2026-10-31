using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class CaLamViecConfiguration : IEntityTypeConfiguration<CaLamViec>
{
    public void Configure(EntityTypeBuilder<CaLamViec> b)
    {
        b.ToTable("CaLamViec", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_CaLamViec_1", "TenCa IN (N'Sáng',N'Chiều',N'Tối')");
            table.HasCheckConstraint("CK_CaLamViec_2", "TrangThai IN ('OPEN','LOCKED','HANDED_OVER','CLOSED')");
            table.HasCheckConstraint("CK_CaLamViec_3", "KetThucLuc IS NULL OR KetThucLuc >= BatDauLuc");
            table.HasCheckConstraint("CK_CaLamViec_4", "TienMatDauCa >= 0 AND (TienMatDuKienCuoiCa IS NULL OR TienMatDuKienCuoiCa >= 0) AND (TienMatThucDem IS NULL OR TienMatThucDem >= 0)");
            table.HasCheckConstraint("CK_CaLamViec_5", "(TrangThai IN ('OPEN','LOCKED') AND KetThucLuc IS NULL) OR (TrangThai IN ('CLOSED','HANDED_OVER') AND KetThucLuc IS NOT NULL)");
            table.HasCheckConstraint("CK_CaLamViec_6", "TrangThai <> 'CLOSED' OR (TienMatDuKienCuoiCa IS NOT NULL AND TienMatThucDem IS NOT NULL)");
        });
        b.HasKey("MaCaLamViec");
        b.Property(x => x.MaCaLamViec).UseIdentityColumn();
        b.Property(x => x.MaCaLamViec).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaCaHienThi).HasColumnType("varchar(30)").IsRequired();
        b.Property(x => x.MaNhanVien).HasColumnType("bigint").IsRequired();
        b.Property(x => x.TenCa).HasColumnType("nvarchar(20)").IsRequired();
        b.Property(x => x.MaMay).HasColumnType("varchar(50)").IsRequired();
        b.Property(x => x.BatDauLuc).HasColumnType("datetime2(3)").IsRequired();
        b.Property(x => x.KetThucLuc).HasColumnType("datetime2(3)");
        b.Property(x => x.TrangThai).HasColumnType("varchar(20)").IsRequired();
        b.Property(x => x.TienMatDauCa).HasColumnType("decimal(18,0)").IsRequired();
        b.Property(x => x.TienMatDuKienCuoiCa).HasColumnType("decimal(18,0)");
        b.Property(x => x.TienMatThucDem).HasColumnType("decimal(18,0)");
        b.Property(x => x.LyDoChenhLech).HasColumnType("nvarchar(500)");
        b.Property(x => x.MaCaBanGiaoTruoc).HasColumnType("bigint");
        b.Property(x => x.MaQuanLyDuyet).HasColumnType("bigint");
        b.Property(x => x.GhiChu).HasColumnType("nvarchar(500)");
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasIndex(new[] { "MaCaHienThi" }).IsUnique().HasDatabaseName("UQ_CaLamViec_1");
        b.HasIndex(new[] { "MaMay" }).IsUnique().HasDatabaseName("UX_Ca_MayDangMo").HasFilter("KetThucLuc IS NULL");
        b.HasIndex(new[] { "MaNhanVien" }).IsUnique().HasDatabaseName("UX_Ca_NhanVienDangMo").HasFilter("KetThucLuc IS NULL");
        b.HasOne<NhanVien>().WithMany().HasForeignKey("MaNhanVien").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<CaLamViec>().WithMany().HasForeignKey("MaCaBanGiaoTruoc").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TaiKhoanQuanLy>().WithMany().HasForeignKey("MaQuanLyDuyet").OnDelete(DeleteBehavior.NoAction);
    }
}
