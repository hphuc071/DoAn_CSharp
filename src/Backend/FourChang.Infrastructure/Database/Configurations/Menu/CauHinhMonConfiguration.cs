using FourChang.Domain.Entities.Menu;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Database.Configurations.Menu;

public sealed class CauHinhMonConfiguration : IEntityTypeConfiguration<CauHinhMon>
{
    public void Configure(EntityTypeBuilder<CauHinhMon> b)
    {
        b.ToTable("CauHinhMon", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_CauHinhMon_1", "KichCo IN ('S','M','L')");
            table.HasCheckConstraint("CK_CauHinhMon_2", "NhietDo IN ('HOT','ICED')");
            table.HasCheckConstraint("CK_CauHinhMon_3", "GiaBan >= 0");
        });
        b.Property(x => x.LaMacDinh).HasColumnType("bit").HasDefaultValue(false);
        b.HasIndex(x => x.MaMon).IsUnique().HasFilter("LaMacDinh=1").HasDatabaseName("UX_CauHinhMon_MacDinh");
        b.HasKey("MaCauHinhMon");
        b.Property(x => x.MaCauHinhMon).UseIdentityColumn();
        b.Property(x => x.MaCauHinhMon).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaMon).HasColumnType("bigint").IsRequired();
        b.Property(x => x.KichCo).HasColumnType("varchar(5)").IsRequired();
        b.Property(x => x.NhietDo).HasColumnType("varchar(10)").IsRequired();
        b.Property(x => x.GiaBan).HasColumnType("decimal(18,0)").IsRequired();
        b.Property(x => x.DangBan).HasColumnType("bit").IsRequired();
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasIndex(new[] { "MaMon", "KichCo", "NhietDo" }).IsUnique().HasDatabaseName("UQ_CauHinhMon_1");
        b.HasOne<Mon>().WithMany().HasForeignKey("MaMon").OnDelete(DeleteBehavior.NoAction);
    }
}
