using System.Text.Json;
using FourChang.Tests.Shared;
using Microsoft.Data.SqlClient;

namespace FourChang.Tests.Integration;

/// <summary>Kiểm chuyển đổi dữ liệu có đơn/tiền/giữ chỗ; không dùng database thật của quán.</summary>
public sealed class Schema30MigrationTests
{
    /// <summary>Chỉ thêm ngày nullable vào schema30 hiện hữu, không cập nhật hay xóa chứng từ cũ.</summary>
    [Fact, Trait("Layer", "Database"), Trait("UseCase", "UC-ADM-04")]
    public async Task Schema30_ThemNgayChungTu_GiuDuLieu_ChayLaiAnToan()
    {
        string? original = null;
        await using var api = await TestApi.Start("Schema30-StockDate", beforeInitialize: async f =>
        {
            await f.Sql($"CREATE DATABASE [{f.Database}]", "master");
            var schema = await File.ReadAllTextAsync(Path.Combine(TestApi.Root, "docs/database/30-bang/schema.sql"));
            await f.Sql(schema.Replace(" [NgayThucHien] date NULL,", ""));
            await f.Sql("INSERT dbo.TaiKhoanQuanLy(TenDangNhapChuanHoa,HoTen,MatKhauBam,TrangThai,BatBuocDoiMatKhau,SoLanDangNhapSai) VALUES('quanly',N'Quản lý',REPLICATE('x',64),'ACTIVE',0,0); INSERT dbo.PhieuKho(SoPhieuKho,LoaiPhieu,TrangThai,MaQuanLyLap,MaThaoTac,LyDo) VALUES('PN-CU','RECEIPT','DRAFT',1,NEWID(),N'Nháp cũ'); INSERT dbo.PhieuKho(SoPhieuKho,LoaiPhieu,TrangThai,MaQuanLyLap,MaQuanLyDuyet,GhiSoLuc,MaThaoTac,LyDo) VALUES('PX-CU','ISSUE','POSTED',1,1,SYSUTCDATETIME(),NEWID(),N'Đã chốt');");
            original = await f.Sql("SELECT SoPhieuKho,TrangThai,LyDo,PhienBanDuLieu FROM dbo.PhieuKho WHERE SoPhieuKho IN ('PN-CU','PX-CU') ORDER BY MaPhieuKho FOR JSON PATH");
        });
        Assert.Equal(original, await api.Sql("SELECT SoPhieuKho,TrangThai,LyDo,PhienBanDuLieu FROM dbo.PhieuKho WHERE SoPhieuKho IN ('PN-CU','PX-CU') ORDER BY MaPhieuKho FOR JSON PATH"));
        Assert.Equal("0", await api.Sql("SELECT COUNT(*) FROM dbo.PhieuKho WHERE NgayThucHien IS NOT NULL"));
        await api.RunApiCommand("--init-db");
        Assert.Equal(original, await api.Sql("SELECT SoPhieuKho,TrangThai,LyDo,PhienBanDuLieu FROM dbo.PhieuKho WHERE SoPhieuKho IN ('PN-CU','PX-CU') ORDER BY MaPhieuKho FOR JSON PATH"));
        await api.RunApiCommand("--check-db");
    }

    /// <summary>Nâng schema30 cũ có công thức, thêm mặc định50 và giữ dữ liệu; chạy lại không thay đổi bản ghi.</summary>
    [Fact, Trait("Layer", "Database"), Trait("UseCase", "Schema30")]
    public async Task Schema30Cu_ThemDuongMacDinh_GiuCongThuc_ChayLaiAnToan()
    {
        await using var api = await TestApi.Start("Schema30-RecipeDefaults", beforeInitialize: async f =>
        {
            await f.Sql($"CREATE DATABASE [{f.Database}]", "master");
            var schema = await File.ReadAllTextAsync(Path.Combine(TestApi.Root, "docs/database/30-bang/schema.sql"));
            schema = schema.Replace(" [NgayThucHien] date NULL,", "").Replace(" [PhanTramDuongMacDinh] int NOT NULL CONSTRAINT DF_CongThuc_PhanTramDuongMacDinh DEFAULT(50),", "")
                .Replace(" CONSTRAINT [CK_CongThuc_DuongMacDinh] CHECK (PhanTramDuongMacDinh IN (0,25,50,75,100)),", "");
            await f.Sql(schema);
            await f.Sql("INSERT dbo.TaiKhoanQuanLy(TenDangNhapChuanHoa,HoTen,MatKhauBam,TrangThai,BatBuocDoiMatKhau,SoLanDangNhapSai) VALUES('quanly',N'Quản lý',REPLICATE('x',64),'ACTIVE',0,0); INSERT dbo.Mon(LoaiMon,TenMon,ThuTuHienThi,DaCongBo,DangBan) VALUES(N'Cà phê',N'Món đã có',1,0,0); INSERT dbo.CauHinhMon(MaMon,KichCo,NhietDo,GiaBan,DangBan,LaMacDinh) VALUES(1,'M','ICED',39000,0,1); INSERT dbo.CongThuc(MaCauHinhMon,SoPhienBan,TenCongThuc,TrangThai,MaQuanLyTao,GhiChu) VALUES(1,1,N'Công thức cũ','DRAFT',1,N'Giữ nguyên');");
        });
        Assert.Equal("Công thức cũ\tGiữ nguyên\t50", await api.Sql("SELECT TenCongThuc,GhiChu,PhanTramDuongMacDinh FROM dbo.CongThuc WHERE MaCongThuc=1"));
        var before = await api.Sql("SELECT * FROM dbo.CongThuc FOR JSON PATH");
        await api.RunApiCommand("--init-db");
        Assert.Equal(before, await api.Sql("SELECT * FROM dbo.CongThuc FOR JSON PATH"));
        await api.RunApiCommand("--check-db");
    }

    /// <summary>Lỗi DDL ở cuối migration phải khôi phục đủ bảng, FK, tiền và trigger trước khi thử lại.</summary>
    [Fact, Trait("Layer", "Database"), Trait("UseCase", "Schema30")]
    public async Task LateDdlFailure_RollsBackAllChanges_ThenCanRetry()
    {
        await using var api = await TestApi.Start("Schema30-Rollback", beforeInitialize: async fixture =>
        {
            await Prepare37(fixture);
            await fixture.Sql("ALTER TABLE dbo.NguyenLieu ADD CONSTRAINT CK_Mon_Loai CHECK(TonAnToan>=0)");
            var script = await File.ReadAllTextAsync(Path.Combine(TestApi.Root, "docs", "database", "30-bang", "008-simplify-to-30.sql"));
            await Assert.ThrowsAsync<SqlException>(() => fixture.Sql(script));
            Assert.Equal("37", await fixture.Sql("SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0"));
            Assert.Equal("74", await fixture.Sql("SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=0 AND is_not_trusted=0"));
            Assert.Equal("0", await fixture.Sql("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Mon') AND name='LoaiMon'"));
            Assert.Equal("39000", await fixture.Sql("SELECT SoTienDaXacMinh FROM dbo.ThanhToan WHERE MaThanhToan=1"));
            Assert.Equal("18.0000", await fixture.Sql("SELECT SoLuongGiuCho FROM dbo.GiuChoNguyenLieu WHERE MaChiTietDonHang=1"));
            Assert.Equal("11", await fixture.Sql("SELECT COUNT(*) FROM sys.triggers WHERE is_disabled=0 AND parent_class=1"));
            await fixture.Sql("ALTER TABLE dbo.NguyenLieu DROP CONSTRAINT CK_Mon_Loai");
        });
        await api.RunApiCommand("--check-db");
        Assert.Equal("30", await api.Sql("SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0"));
    }

    /// <summary>Nâng cấp schema 37 có lịch sử, giữ số tiền/định mức, chạy lại an toàn và chặn sửa snapshot.</summary>
    [Fact, Trait("Layer", "Database"), Trait("UseCase", "Schema30")]
    public async Task Upgrade37_PreservesHistory_MergesHolds_AndProtectsSnapshots()
    {
        await using var api = await TestApi.Start("Schema30-PopulatedUpgrade", beforeInitialize: Prepare37);
        Assert.Equal("30", await api.Sql("SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0"));
        Assert.Equal("64", await api.Sql("SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=0 AND is_not_trusted=0"));
        Assert.Equal("39000", await api.Sql("SELECT SoTienDaXacMinh FROM dbo.ThanhToan WHERE MaThanhToan=1"));
        Assert.Equal("39000", await api.Sql("SELECT SoTien FROM dbo.HoaDon WHERE MaHoaDon=1"));
        Assert.Equal("1000.0000|18.0000", await api.Sql("SELECT CONCAT(TonThucTe,'|',TongGiuCho) FROM dbo.TonKho WHERE MaNguyenLieu=1"));
        Assert.Equal("18.0000|18.0000|0.0000|0.0000|RESERVED", await api.Sql("SELECT CONCAT(DinhMucMoiLy,'|',SoLuongGiuCho,'|',SoLuongDaTieuHao,'|',SoLuongDaGiaiPhong,'|',TrangThai) FROM dbo.NguyenLieuChiTietDonHang WHERE MaChiTietDonHang=1"));
        Assert.Equal("Cà phê|g", await api.Sql("SELECT CONCAT(m.LoaiMon,'|',n.DonViCoSo) FROM dbo.Mon m CROSS JOIN dbo.NguyenLieu n"));
        Assert.Equal("0", await api.Sql("SELECT COUNT(*) FROM sys.tables WHERE name IN('ThietBiPOS','DanhMucMon','DonViTinh','QuyDoiDonViNguyenLieu','TuyChonPhaCheHopLe','CauHinhMonThem','GiuChoNguyenLieu')"));
        var menu = (await api.Send("GET", "/api/menu")).Expect(200, "Catalog sau nâng cấp").Json;
        Assert.NotEmpty(menu.AsArray());
        // Đường/đá không còn phụ thuộc một cặp duy nhất trong dữ liệu cũ.
        var variants = (await api.Send("GET", "/api/menu/1/variants")).Expect(200, "Cấu hình sau nâng cấp").Json;
        Assert.Equal(25, variants[0]!["tuyChonPhaChe"]!.AsArray().Count);
        var migration = await File.ReadAllTextAsync(Path.Combine(TestApi.Root, "docs", "database", "30-bang", "008-simplify-to-30.sql"));
        await api.Sql(migration);
        await api.RunApiCommand("--init-db");
        Assert.Equal("1|39000", await api.Sql("SELECT CONCAT(COUNT(*),'|',SUM(SoTienDaXacMinh)) FROM dbo.ThanhToan"));
        await Assert.ThrowsAsync<SqlException>(() => api.Sql("UPDATE dbo.NguyenLieuChiTietDonHang SET DinhMucMoiLy=19 WHERE MaChiTietDonHang=1"));
        await Assert.ThrowsAsync<SqlException>(() => api.Sql("DELETE dbo.NguyenLieuChiTietDonHang WHERE MaChiTietDonHang=1"));
        // Chỉ thay lượng xử lý, sau đó rollback: trigger cho phép nghiệp vụ giữ/tiêu hao hoạt động.
        Assert.Equal("18.0000", await api.Sql("BEGIN TRAN; UPDATE dbo.NguyenLieuChiTietDonHang SET SoLuongDaTieuHao=18,TrangThai='CONSUMED' WHERE MaChiTietDonHang=1; SELECT SoLuongDaTieuHao FROM dbo.NguyenLieuChiTietDonHang WHERE MaChiTietDonHang=1; ROLLBACK;"));
        Assert.Equal("0.0000", await api.Sql("SELECT SoLuongDaTieuHao FROM dbo.NguyenLieuChiTietDonHang WHERE MaChiTietDonHang=1"));
        await api.RunApiCommand("--check-db");
    }

    /// <summary>Tạo bộ 37 cũ độc lập, có hóa đơn bất biến và giữ chỗ, trước khi bootstrap mới chạy.</summary>
    private static async Task Prepare37(TestApi api)
    {
        await api.Sql($"CREATE DATABASE [{api.Database}]", "master");
        var schema = await File.ReadAllTextAsync(Path.Combine(TestApi.Root, "docs", "database", "37-bang", "schema.sql"));
        foreach (var batch in System.Text.RegularExpressions.Regex.Split(schema, @"^\s*GO\s*\r?$", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            if (!string.IsNullOrWhiteSpace(batch)) await api.Sql(batch);
        await api.Sql("""
            INSERT dbo.TaiKhoanQuanLy(TenDangNhapChuanHoa,MatKhauBam,TrangThai,BatBuocDoiMatKhau,SoLanDangNhapSai,HoTen) VALUES(N'quanly',N'unused-fixture-hash','ACTIVE',0,0,N'Quản lý mẫu');
            INSERT dbo.NhanVien(HoTen,NgayVaoLam,TrangThai) VALUES(N'Nguyễn Minh Anh',SYSUTCDATETIME(),'ACTIVE');
            INSERT dbo.DanhMucMon(TenDanhMuc,ThuTuHienThi,DaCongBo,DangSuDung) VALUES(N'Cà phê',0,1,1);
            INSERT dbo.DonViTinh(KyHieu,TenDonVi,DangSuDung) VALUES('g',N'Gam',1);
            INSERT dbo.NguyenLieu(TenNguyenLieu,MaDonViCoSo,TonAnToan,NguongCanhBao,DangSuDung) VALUES(N'Hạt cà phê',1,0,100,1);
            INSERT dbo.QuyDoiDonViNguyenLieu(MaNguyenLieu,MaDonVi,HeSoVeDonViCoSo) VALUES(1,1,1);
            INSERT dbo.TonKho(MaNguyenLieu,TonThucTe,TongGiuCho) VALUES(1,1000,18);
            INSERT dbo.Mon(MaDanhMuc,TenMon,ThuTuHienThi,DaCongBo,DangBan) VALUES(1,N'Cà phê mẫu',0,1,1);
            INSERT dbo.CauHinhMon(MaMon,KichCo,NhietDo,GiaBan,DangBan,LaMacDinh) VALUES(1,'M','ICED',39000,1,1);
            INSERT dbo.TuyChonPhaCheHopLe(MaCauHinhMon,PhanTramDuong,PhanTramDa,DangSuDung,LaMacDinh) VALUES(1,50,50,1,1);
            INSERT dbo.CongThuc(MaCauHinhMon,SoPhienBan,TenCongThuc,TrangThai,MaQuanLyTao) VALUES(1,1,N'Công thức mẫu','DRAFT',1);
            INSERT dbo.ChiTietCongThuc(MaCongThuc,MaNguyenLieu,LoaiDinhMuc,DinhMucCoSo,TyLeHaoHut) VALUES(1,1,'FIXED',18,0);
            UPDATE dbo.CongThuc SET TrangThai='ACTIVE',HieuLucTu='2020-01-01',MaQuanLyDuyet=1 WHERE MaCongThuc=1;
            INSERT dbo.CaLamViec(MaCaHienThi,MaNhanVien,TenCa,MaMay,BatDauLuc,TrangThai,TienMatDauCa) VALUES('CA-OLD-1',1,N'Sáng','TEST-OLD',SYSUTCDATETIME(),'OPEN',500000);
            INSERT dbo.DonHang(MaDonHienThi,MaCaLamViec,KenhDatHang,HinhThucNhanMon,TenNguoiNhan,DienThoaiNguoiNhan,PhiGiaoHangBanDau,PhiGiaoHangHienTai,TrangThai) VALUES('POS-OLD-1',1,'POS','TAKEAWAY',N'Trần Hoàng Nam','0900000001',0,0,'ACCEPTED');
            INSERT dbo.ChiTietDonHang(MaDonHang,MaCauHinhMon,MaCongThuc,TenMonDaChot,KichCoDaChot,NhietDoDaChot,PhanTramDuong,PhanTramDa,GiaMonDaChot,SoLuongDat,SoLuongBatDauPha,SoLuongPhaXong,SoLuongDaHuy) VALUES(1,1,1,N'Cà phê mẫu','M','ICED',50,50,39000,1,0,0,0);
            INSERT dbo.NguyenLieuChiTietDonHang(MaChiTietDonHang,MaNguyenLieu,TenNguyenLieuDaChot,DonViCoSoDaChot,DinhMucMoiLy) VALUES(1,1,N'Hạt cà phê','g',18);
            INSERT dbo.GiuChoNguyenLieu(MaChiTietDonHang,MaNguyenLieu,SoLuongGiuCho,SoLuongDaTieuHao,SoLuongDaGiaiPhong,TrangThai) VALUES(1,1,18,0,0,'RESERVED');
            INSERT dbo.ThanhToan(MaThanhToanHienThi,MaDonHang,MaCaLamViec,PhuongThuc,SoTienYeuCau,SoTienDaXacMinh,TienKhachDua,TrangThai,XacMinhLuc,MaThaoTac) VALUES('PAY-OLD-1',1,1,'CASH',39000,39000,39000,'VERIFIED',SYSUTCDATETIME(),NEWID());
            INSERT dbo.HoaDon(SoHoaDon,MaDonHang,LoaiHoaDon,TenNguoiMua,SoTien,NoiDungHoaDonDaChotJson,TrangThai,PhatHanhLuc,MaCaLamViec) VALUES('HD-OLD-1',1,'ORIGINAL',N'Trần Hoàng Nam',39000,N'{"original":true}','ISSUED',SYSUTCDATETIME(),1);
            """);
    }
}
