using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace FourChang.Infrastructure.Persistence.Configurations;
public sealed class ThietBiPOSConfiguration : IEntityTypeConfiguration<ThietBiPOS>
{
 public void Configure(EntityTypeBuilder<ThietBiPOS> b)
 {
 b.ToTable("ThietBiPOS","dbo",t=>{t.UseSqlOutputClause(false);
 t.HasCheckConstraint("CK_ThietBiPOS_1","TrangThai IN ('ACTIVE','REVOKED')");
 t.HasCheckConstraint("CK_ThietBiPOS_2","LEN(LTRIM(RTRIM(MaMay)))>0 AND LEN(LTRIM(RTRIM(TenMay)))>0");
 t.HasCheckConstraint("CK_ThietBiPOS_3","(TrangThai='ACTIVE' AND ThuHoiLuc IS NULL) OR (TrangThai='REVOKED' AND ThuHoiLuc IS NOT NULL)");
 t.HasCheckConstraint("CK_ThietBiPOS_4","LEN(LTRIM(RTRIM(LyDo)))>0");
 });
 b.HasKey(x=>x.MaThietBiPOS); b.Property(x=>x.MaThietBiPOS).UseIdentityColumn();
 b.Property(x=>x.MaThietBiPOS).HasColumnType("bigint").IsRequired();
 b.Property(x=>x.MaMay).HasColumnType("varchar(50)").IsRequired();
 b.Property(x=>x.TenMay).HasColumnType("nvarchar(120)").IsRequired();
 b.Property(x=>x.MaBamKhoa).HasColumnType("binary(32)").IsRequired();
 b.Property(x=>x.TrangThai).HasColumnType("varchar(15)").IsRequired();
 b.Property(x=>x.MaQuanLyCap).HasColumnType("bigint").IsRequired();
 b.Property(x=>x.CapLuc).HasColumnType("datetime2(3)").IsRequired();
 b.Property(x=>x.ThuHoiLuc).HasColumnType("datetime2(3)");
 b.Property(x=>x.LyDo).HasColumnType("nvarchar(500)").IsRequired();
 b.Property(x=>x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
 b.Property(x=>x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
 b.Property(x=>x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
 b.HasIndex(x=>x.MaMay).IsUnique(); b.HasIndex(x=>x.MaBamKhoa).IsUnique();
 b.HasOne<TaiKhoanQuanLy>().WithMany().HasForeignKey(x=>x.MaQuanLyCap).OnDelete(DeleteBehavior.NoAction);
 }
}
