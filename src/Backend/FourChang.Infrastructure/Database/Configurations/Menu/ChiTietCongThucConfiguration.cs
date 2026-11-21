using FourChang.Domain.Entities.Inventory;
using FourChang.Domain.Entities.Menu;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Database.Configurations.Menu;

public sealed class ChiTietCongThucConfiguration : IEntityTypeConfiguration<ChiTietCongThuc>
{
    public void Configure(EntityTypeBuilder<ChiTietCongThuc> b)
    {
        b.ToTable("ChiTietCongThuc", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_ChiTietCongThuc_1", "LoaiDinhMuc IN ('FIXED','SUGAR','ICE')");
            table.HasCheckConstraint("CK_ChiTietCongThuc_2", "DinhMucCoSo > 0");
        });
        b.HasKey("MaCongThuc", "MaNguyenLieu", "LoaiDinhMuc");
        b.Property(x => x.MaCongThuc).ValueGeneratedNever();
        b.Property(x => x.MaNguyenLieu).ValueGeneratedNever();
        b.Property(x => x.MaCongThuc).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaNguyenLieu).HasColumnType("bigint").IsRequired();
        b.Property(x => x.LoaiDinhMuc).HasColumnType("varchar(10)").IsRequired();
        b.Property(x => x.DinhMucCoSo).HasColumnType("decimal(18,4)").IsRequired();
        b.HasOne<CongThuc>().WithMany().HasForeignKey("MaCongThuc").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<NguyenLieu>().WithMany().HasForeignKey("MaNguyenLieu").OnDelete(DeleteBehavior.NoAction);
    }
}
