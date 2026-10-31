using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class ChiTietCongThucConfiguration : IEntityTypeConfiguration<ChiTietCongThuc>
{
    public void Configure(EntityTypeBuilder<ChiTietCongThuc> b)
    {
        b.ToTable("ChiTietCongThuc", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_ChiTietCongThuc_1", "LoaiDinhMuc IN ('FIXED','SUGAR','ICE')");
            table.HasCheckConstraint("CK_ChiTietCongThuc_2", "DinhMucCoSo > 0");
            table.HasCheckConstraint("CK_ChiTietCongThuc_3", "TyLeHaoHut >= 0 AND TyLeHaoHut < 1");
        });
        b.HasKey("MaCongThuc", "MaNguyenLieu", "LoaiDinhMuc");
        b.Property(x => x.MaCongThuc).ValueGeneratedNever();
        b.Property(x => x.MaNguyenLieu).ValueGeneratedNever();
        b.Property(x => x.MaCongThuc).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaNguyenLieu).HasColumnType("bigint").IsRequired();
        b.Property(x => x.LoaiDinhMuc).HasColumnType("varchar(10)").IsRequired();
        b.Property(x => x.DinhMucCoSo).HasColumnType("decimal(18,4)").IsRequired();
        b.Property(x => x.TyLeHaoHut).HasColumnType("decimal(9,6)").IsRequired();
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasOne<CongThuc>().WithMany().HasForeignKey("MaCongThuc").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<NguyenLieu>().WithMany().HasForeignKey("MaNguyenLieu").OnDelete(DeleteBehavior.NoAction);
    }
}
