using FourChang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourChang.Infrastructure.Persistence.Configurations;

public sealed class DonHangConfiguration : IEntityTypeConfiguration<DonHang>
{
    public void Configure(EntityTypeBuilder<DonHang> b)
    {
        b.ToTable("DonHang", "dbo", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_DonHang_1", "KenhDatHang IN ('WEB','POS')");
            table.HasCheckConstraint("CK_DonHang_2", "HinhThucNhanMon IN ('DINE_IN','TAKEAWAY','DELIVERY')");
            table.HasCheckConstraint("CK_DonHang_3", "(KenhDatHang = 'WEB' AND MaKhachHang IS NOT NULL) OR (KenhDatHang = 'POS' AND MaCaLamViec IS NOT NULL)");
            table.HasCheckConstraint("CK_DonHang_4", "(HinhThucNhanMon = 'DINE_IN' AND MaBan IS NOT NULL) OR (HinhThucNhanMon <> 'DINE_IN' AND MaBan IS NULL)");
            table.HasCheckConstraint("CK_DonHang_5", "HinhThucNhanMon <> 'DELIVERY' OR (TenNguoiNhan IS NOT NULL AND DienThoaiNguoiNhan IS NOT NULL AND DiaChiGiaoDaChot IS NOT NULL)");
            table.HasCheckConstraint("CK_DonHang_6", "PhiGiaoHangBanDau >= 0 AND PhiGiaoHangHienTai >= 0 AND PhiGiaoHangHienTai <= PhiGiaoHangBanDau");
            table.HasCheckConstraint("CK_DonHang_7", "TrangThai IN ('DRAFT','PENDING_PAYMENT','PENDING_CONFIRMATION','ACCEPTED','PREPARING','READY','DELIVERING','COMPLETED','REJECTED','CANCELLED','DELIVERY_FAILED')");
            table.HasCheckConstraint("CK_DonHang_8", "ChinhSachGiaoHangDaChotJson IS NULL OR ISJSON(ChinhSachGiaoHangDaChotJson) = 1");
            table.HasCheckConstraint("CK_DonHang_9", "ViDo IS NULL OR ViDo BETWEEN -90 AND 90");
            table.HasCheckConstraint("CK_DonHang_10", "KinhDo IS NULL OR KinhDo BETWEEN -180 AND 180");
            table.HasCheckConstraint("CK_DonHang_11", "KhoangCachGiaoKm IS NULL OR KhoangCachGiaoKm >= 0");
        });
        b.HasKey("MaDonHang");
        b.Property(x => x.MaDonHang).UseIdentityColumn();
        b.Property(x => x.MaDonHang).HasColumnType("bigint").IsRequired();
        b.Property(x => x.MaDonHienThi).HasColumnType("varchar(30)").IsRequired();
        b.Property(x => x.MaKhachHang).HasColumnType("bigint");
        b.Property(x => x.MaCaLamViec).HasColumnType("bigint");
        b.Property(x => x.MaDonLienQuan).HasColumnType("bigint");
        b.Property(x => x.KenhDatHang).HasColumnType("varchar(10)").IsRequired();
        b.Property(x => x.HinhThucNhanMon).HasColumnType("varchar(15)").IsRequired();
        b.Property(x => x.MaBan).HasColumnType("bigint");
        b.Property(x => x.MaBanDaChot).HasColumnType("varchar(20)");
        b.Property(x => x.TenNguoiNhan).HasColumnType("nvarchar(120)");
        b.Property(x => x.DienThoaiNguoiNhan).HasColumnType("varchar(20)");
        b.Property(x => x.DiaChiGiaoDaChot).HasColumnType("nvarchar(500)");
        b.Property(x => x.ViDo).HasColumnType("decimal(9,6)");
        b.Property(x => x.KinhDo).HasColumnType("decimal(9,6)");
        b.Property(x => x.ThoiDiemHenNhan).HasColumnType("datetime2(3)");
        b.Property(x => x.KhoangCachGiaoKm).HasColumnType("decimal(10,3)");
        b.Property(x => x.ChinhSachGiaoHangDaChotJson).HasColumnType("nvarchar(max)");
        b.Property(x => x.HanBaoPhi).HasColumnType("datetime2(3)");
        b.Property(x => x.PhiGiaoHangBanDau).HasColumnType("decimal(18,0)").IsRequired();
        b.Property(x => x.PhiGiaoHangHienTai).HasColumnType("decimal(18,0)").IsRequired();
        b.Property(x => x.TrangThai).HasColumnType("varchar(25)").IsRequired();
        b.Property(x => x.GhiChu).HasColumnType("nvarchar(500)");
        b.Property(x => x.HetHanLuc).HasColumnType("datetime2(3)");
        b.Property(x => x.TiepNhanLuc).HasColumnType("datetime2(3)");
        b.Property(x => x.HoanTatLuc).HasColumnType("datetime2(3)");
        b.Property(x => x.ThoiDiemTao).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.ThoiDiemCapNhat).HasColumnType("datetime2(3)").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.PhienBanDuLieu).HasColumnType("rowversion").IsRequired().IsRowVersion();
        b.HasIndex(new[] { "MaDonHienThi" }).IsUnique().HasDatabaseName("UQ_DonHang_1");
        b.HasOne<TaiKhoanKhachHang>().WithMany().HasForeignKey("MaKhachHang").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<CaLamViec>().WithMany().HasForeignKey("MaCaLamViec").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<DonHang>().WithMany().HasForeignKey("MaDonLienQuan").OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Ban>().WithMany().HasForeignKey("MaBan").OnDelete(DeleteBehavior.NoAction);
    }
}
