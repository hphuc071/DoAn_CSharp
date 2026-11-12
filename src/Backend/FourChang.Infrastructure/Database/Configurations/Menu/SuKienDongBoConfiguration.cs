using FourChang.Domain.Entities.Menu;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace FourChang.Infrastructure.Database.Configurations.Menu;
/// <summary>Ánh xạ đúng schema ADM-03 đã duyệt; không dùng EnsureCreated để sửa database cũ.</summary>
public sealed class SuKienDongBoConfiguration : IEntityTypeConfiguration<SuKienDongBo>
{
    /// <summary>Khai báo khóa, kiểu SQL, CHECK và rowversion để DB và EF thống nhất.</summary>
    public void Configure(EntityTypeBuilder<SuKienDongBo> b)
    {
        b.ToTable("SuKienDongBo", "dbo", t =>
        {
            t.HasCheckConstraint("CK_SuKienDongBo_1", "ISJSON(NoiDungJson) = 1");
            t.HasCheckConstraint("CK_SuKienDongBo_2", "SoLanThu >= 0");
            t.HasCheckConstraint("CK_SuKienDongBo_3", "(KhoaDenLuc IS NULL AND MaLanXuLy IS NULL) OR (KhoaDenLuc IS NOT NULL AND MaLanXuLy IS NOT NULL)");
        });
        b.HasKey(x => x.MaSuKien);
        b.Property(x => x.MaSuKien).HasColumnType("uniqueidentifier").IsRequired();
        b.Property(x => x.MaThaoTac).HasColumnType("uniqueidentifier").IsRequired();
        b.Property(x => x.LoaiSuKien).HasColumnType("varchar(50)").IsRequired();
        b.Property(x => x.NoiDungJson).HasColumnType("nvarchar(max)").IsRequired();
        b.Property(x => x.PhatSinhLuc).HasColumnType("datetime2(3)").IsRequired();
        b.Property(x => x.XuLyXongLuc).HasColumnType("datetime2(3)");
        b.Property(x => x.SoLanThu).HasColumnType("int").IsRequired();
        b.Property(x => x.ThuLaiLuc).HasColumnType("datetime2(3)");
        b.Property(x => x.KhoaDenLuc).HasColumnType("datetime2(3)");
        b.Property(x => x.MaLanXuLy).HasColumnType("uniqueidentifier");
        b.Property(x => x.LoiGanNhat).HasColumnType("nvarchar(500)");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRowVersion().IsRequired();
        b.HasIndex(x => x.MaThaoTac).IsUnique().HasDatabaseName("UX_SuKienDongBo_ThaoTac");
        b.HasIndex(x => new { x.ThuLaiLuc, x.PhatSinhLuc }).HasFilter("XuLyXongLuc IS NULL").HasDatabaseName("IX_SuKienDongBo_ChoXuLy");
    }
}
