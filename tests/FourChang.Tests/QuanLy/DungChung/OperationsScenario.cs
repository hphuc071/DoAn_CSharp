using FourChang.Tests.Shared;

namespace FourChang.Tests.Admin;

internal sealed record OperationsScenario(long Order, long Line, long Payment, long Shift, long? Invoice, decimal Total);

/// <summary>Dữ liệu chuẩn bị riêng cho test các UC sau đặt đơn; không chứng minh luồng bán hàng POS đã tích hợp SQL.</summary>
internal static class OperationsScenarioSeed
{
    /// <summary>Tạo ca/đơn/snapshot/giữ chỗ/thu có kiểm soát trong database fixture GUID để test hành vi quản trị.</summary>
    internal static Task<OperationsScenario> Create(AdminFeatureFixture fixture, string state = "PENDING_CONFIRMATION", bool paid = true, bool invoice = false) => Create(fixture.Api, state, paid, invoice);

    /// <summary>Tái sử dụng seed cho fixture API độc lập với flag theo từng luồng; không ảnh hưởng môi trường ứng dụng.</summary>
    internal static async Task<OperationsScenario> Create(TestApi api, string state = "PENDING_CONFIRMATION", bool paid = true, bool invoice = false)
    {
        var key = Guid.NewGuid().ToString("N")[..16];
        var sql = $"""
            DECLARE @nv bigint,@ca bigint,@dh bigint,@line bigint,@pay bigint,@inv bigint=NULL,@variant bigint,@recipe bigint,@price decimal(18,0);
            INSERT dbo.NhanVien(HoTen,NgayVaoLam,TrangThai) VALUES(N'Nguyễn Minh Phúc',SYSUTCDATETIME(),'ACTIVE'); SET @nv=SCOPE_IDENTITY();
            INSERT dbo.CaLamViec(MaCaHienThi,MaNhanVien,TenCa,MaMay,BatDauLuc,TrangThai,TienMatDauCa)
              VALUES('CA-{key}',@nv,N'Sáng','QA-{key}',SYSUTCDATETIME(),'OPEN',500000); SET @ca=SCOPE_IDENTITY();
            SELECT TOP(1) @variant=c.MaCauHinhMon,@recipe=r.MaCongThuc,@price=c.GiaBan FROM dbo.CauHinhMon c JOIN dbo.CongThuc r ON r.MaCauHinhMon=c.MaCauHinhMon
              WHERE r.TrangThai='ACTIVE' AND c.NhietDo='ICED' ORDER BY c.MaCauHinhMon;
            INSERT dbo.DonHang(MaDonHienThi,MaCaLamViec,KenhDatHang,HinhThucNhanMon,TenNguoiNhan,DienThoaiNguoiNhan,PhiGiaoHangBanDau,PhiGiaoHangHienTai,TrangThai,TiepNhanLuc,HoanTatLuc)
              VALUES('POS-{key}',@ca,'POS','TAKEAWAY',N'Nguyễn Tuấn Kiệt','0907000001',0,0,'{state}',{(state == "PENDING_CONFIRMATION" ? "NULL" : "SYSUTCDATETIME()")},{(state == "COMPLETED" ? "SYSUTCDATETIME()" : "NULL")}); SET @dh=SCOPE_IDENTITY();
            INSERT dbo.ChiTietDonHang(MaDonHang,MaCauHinhMon,MaCongThuc,TenMonDaChot,KichCoDaChot,NhietDoDaChot,PhanTramDuong,PhanTramDa,GiaMonDaChot,SoLuongDat,SoLuongBatDauPha,SoLuongPhaXong,SoLuongDaHuy)
              SELECT @dh,@variant,@recipe,m.TenMon,c.KichCo,c.NhietDo,100,100,@price,2,{(state == "COMPLETED" ? 2 : 0)},{(state == "COMPLETED" ? 2 : 0)},0 FROM dbo.CauHinhMon c JOIN dbo.Mon m ON m.MaMon=c.MaMon WHERE c.MaCauHinhMon=@variant; SET @line=SCOPE_IDENTITY();
            INSERT dbo.NguyenLieuChiTietDonHang(MaChiTietDonHang,MaNguyenLieu,TenNguyenLieuDaChot,DonViCoSoDaChot,DinhMucMoiLy)
              SELECT @line,d.MaNguyenLieu,n.TenNguyenLieu,u.KyHieu,CEILING(SUM(d.DinhMucCoSo/(1-d.TyLeHaoHut))*10000)/10000 FROM dbo.ChiTietCongThuc d
              JOIN dbo.NguyenLieu n ON n.MaNguyenLieu=d.MaNguyenLieu JOIN dbo.DonViTinh u ON u.MaDonVi=n.MaDonViCoSo WHERE d.MaCongThuc=@recipe GROUP BY d.MaNguyenLieu,n.TenNguyenLieu,u.KyHieu;
            INSERT dbo.GiuChoNguyenLieu(MaChiTietDonHang,MaNguyenLieu,SoLuongGiuCho,SoLuongDaTieuHao,SoLuongDaGiaiPhong,TrangThai)
              SELECT @line,MaNguyenLieu,2*DinhMucMoiLy,{(state == "COMPLETED" ? "2*DinhMucMoiLy" : "0")},0,'{(state == "COMPLETED" ? "CONSUMED" : "RESERVED")}' FROM dbo.NguyenLieuChiTietDonHang WHERE MaChiTietDonHang=@line;
            UPDATE t SET TongGiuCho=TongGiuCho+{(state == "COMPLETED" ? "0" : "g.SoLuongGiuCho")},TonThucTe=TonThucTe-{(state == "COMPLETED" ? "g.SoLuongDaTieuHao" : "0")}
              FROM dbo.TonKho t JOIN dbo.GiuChoNguyenLieu g ON g.MaNguyenLieu=t.MaNguyenLieu WHERE g.MaChiTietDonHang=@line;
            INSERT dbo.ThanhToan(MaThanhToanHienThi,MaDonHang,MaCaLamViec,PhuongThuc,SoTienYeuCau,SoTienDaXacMinh,TienKhachDua,TrangThai,XacMinhLuc,MaThaoTac)
              VALUES('TT-{key}',@dh,@ca,'CASH',2*@price,{(paid ? "2*@price" : "0")},2*@price,'{(paid ? "VERIFIED" : "PENDING")}',{(paid ? "SYSUTCDATETIME()" : "NULL")},NEWID()); SET @pay=SCOPE_IDENTITY();
            {(invoice ? "INSERT dbo.HoaDon(SoHoaDon,MaDonHang,LoaiHoaDon,TenNguoiMua,SoTien,NoiDungHoaDonDaChotJson,TrangThai,PhatHanhLuc,MaQuanLyPhatHanh,MaCaLamViec) VALUES('HD-" + key + "',@dh,'ORIGINAL',N'Nguyễn Tuấn Kiệt',2*@price,N'{\"Version\":1,\"Mon\":\"Cà phê đã chốt\",\"SoLy\":2}', 'ISSUED',SYSUTCDATETIME(),1,@ca); SET @inv=SCOPE_IDENTITY();" : "")}
            SELECT CONCAT(@dh,',',@line,',',@pay,',',@ca,',',ISNULL(CONVERT(varchar(30),@inv),'0'),',',CONVERT(varchar(30),2*@price));
            """;
        var values = (await api.Sql(sql)).Split(',');
        return new(long.Parse(values[0]), long.Parse(values[1]), long.Parse(values[2]), long.Parse(values[3]), values[4] == "0" ? null : long.Parse(values[4]), decimal.Parse(values[5], System.Globalization.CultureInfo.InvariantCulture));
    }
}
