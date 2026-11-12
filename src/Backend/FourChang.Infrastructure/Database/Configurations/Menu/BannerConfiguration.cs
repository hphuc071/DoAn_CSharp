using FourChang.Domain.Entities.Menu;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace FourChang.Infrastructure.Database.Configurations.Menu;
/// <summary>Ánh xạ đúng schema ADM-03 đã duyệt; không dùng EnsureCreated để sửa database cũ.</summary>
public sealed class BannerConfiguration : IEntityTypeConfiguration<Banner>
{
    /// <summary>Khai báo khóa, kiểu SQL, CHECK và rowversion để DB và EF thống nhất.</summary>
    public void Configure(EntityTypeBuilder<Banner> b)
    {
        b.ToTable("Banner", "dbo", t =>
        {
            t.HasCheckConstraint("CK_Banner_1", "LEN(LTRIM(RTRIM(TenBanner))) > 0");
            t.HasCheckConstraint("CK_Banner_2", "LEN(LTRIM(RTRIM(DuongDanAnh))) > 0");
            t.HasCheckConstraint("CK_Banner_3", "ThuTuHienThi >= 0");
            t.HasCheckConstraint("CK_Banner_4", "KetThucLuc IS NULL OR KetThucLuc > BatDauLuc");
            t.HasCheckConstraint("CK_Banner_5", "ViTri = 'HOME_HERO'");
        });
        b.HasKey(x => x.MaBanner);
        b.Property(x => x.MaBanner).HasColumnType("bigint").UseIdentityColumn().IsRequired();
        b.Property(x => x.TenBanner).HasColumnType("nvarchar(150)").IsRequired();
        b.Property(x => x.NoiDung).HasColumnType("nvarchar(1000)");
        b.Property(x => x.DuongDanAnh).HasColumnType("nvarchar(500)").IsRequired();
        b.Property(x => x.LienKet).HasColumnType("nvarchar(500)");
        b.Property(x => x.ViTri).HasColumnType("varchar(30)").IsRequired();
        b.Property(x => x.ThuTuHienThi).HasColumnType("int").IsRequired();
        b.Property(x => x.BatDauLuc).HasColumnType("datetime2(3)").IsRequired();
        b.Property(x => x.KetThucLuc).HasColumnType("datetime2(3)");
        b.Property(x => x.DaCongBo).HasColumnType("bit").IsRequired();
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired();
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired();
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRowVersion().IsRequired();

    }
}
