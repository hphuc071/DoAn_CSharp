# Từ điển dữ liệu: 30 bảng

Schema hiện hành có 30 bảng, 64 FK; PK/FK, snapshot và rowversion được giữ để bảo vệ lịch sử. Hai mã lựa chọn MaDanhMuc/MaDonViCoSo trong DTO không phải cột SQL.

## 1. TaiKhoanKhachHang

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaKhachHang | bigint | Có | PK |
| TenDangNhapChuanHoa | nvarchar(100) | Có |  |
| MatKhauBam | nvarchar(512) | Có |  |
| TrangThai | varchar(15) | Có |  |
| BatBuocDoiMatKhau | bit | Có |  |
| SoLanDangNhapSai | int | Có |  |
| KhoaDenLuc | datetime2(3) | Không |  |
| DangNhapLanCuoiLuc | datetime2(3) | Không |  |
| HoTen | nvarchar(120) | Có |  |
| EmailChuanHoa | nvarchar(254) | Không |  |
| SoDienThoaiChuanHoa | varchar(20) | Có |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |

## 2. TaiKhoanQuanLy

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaQuanLy | bigint | Có | PK |
| TenDangNhapChuanHoa | nvarchar(100) | Có |  |
| MatKhauBam | nvarchar(512) | Có |  |
| TrangThai | varchar(15) | Có |  |
| BatBuocDoiMatKhau | bit | Có |  |
| SoLanDangNhapSai | int | Có |  |
| KhoaDenLuc | datetime2(3) | Không |  |
| DangNhapLanCuoiLuc | datetime2(3) | Không |  |
| HoTen | nvarchar(120) | Có |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |

## 3. PhienDangNhap

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaPhienDangNhap | bigint | Có | PK |
| MaKhachHang | bigint | Không | FK → TaiKhoanKhachHang |
| MaQuanLy | bigint | Không | FK → TaiKhoanQuanLy |
| MaLamMoiDaBam | binary(32) | Có |  |
| PhamViPhien | varchar(20) | Có |  |
| LoaiUngDung | varchar(15) | Có |  |
| ThongTinThietBi | nvarchar(300) | Không |  |
| HetHanLuc | datetime2(3) | Có |  |
| HoatDongLanCuoiLuc | datetime2(3) | Không |  |
| ThuHoiLuc | datetime2(3) | Không |  |
| LyDoThuHoi | nvarchar(250) | Không |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |

## 4. DiaChiKhachHang

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaDiaChi | bigint | Có | PK |
| MaKhachHang | bigint | Có | FK → TaiKhoanKhachHang |
| NhanDiaChi | nvarchar(50) | Không |  |
| TenNguoiNhan | nvarchar(120) | Có |  |
| DienThoaiNguoiNhan | varchar(20) | Có |  |
| DiaChiChiTiet | nvarchar(500) | Có |  |
| GhiChuGiaoHang | nvarchar(500) | Không |  |
| LaDiaChiMacDinh | bit | Có |  |
| DangSuDung | bit | Có |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |

## 5. NhanVien

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaNhanVien | bigint | Có | PK |
| HoTen | nvarchar(120) | Có |  |
| SoDienThoai | varchar(20) | Không |  |
| DiaChiLienHe | nvarchar(500) | Không |  |
| NgayVaoLam | datetime2(3) | Có |  |
| TrangThai | varchar(15) | Có |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |

## 6. CaLamViec

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaCaLamViec | bigint | Có | PK |
| MaCaHienThi | varchar(30) | Có |  |
| MaNhanVien | bigint | Có | FK → NhanVien |
| TenCa | nvarchar(20) | Có |  |
| MaMay | varchar(50) | Có |  |
| BatDauLuc | datetime2(3) | Có |  |
| KetThucLuc | datetime2(3) | Không |  |
| TrangThai | varchar(20) | Có |  |
| TienMatDauCa | decimal(18,0) | Có |  |
| TienMatDuKienCuoiCa | decimal(18,0) | Không |  |
| TienMatThucDem | decimal(18,0) | Không |  |
| MaCaBanGiaoTruoc | bigint | Không | FK → CaLamViec |
| MaQuanLyDuyet | bigint | Không | FK → TaiKhoanQuanLy |
| GhiChu | nvarchar(500) | Không |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |

## 7. Mon

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaMon | bigint | Có | PK |
| LoaiMon | nvarchar(100) | Có |  |
| TenMon | nvarchar(150) | Có |  |
| MoTa | nvarchar(1000) | Không |  |
| DuongDanAnh | nvarchar(500) | Không |  |
| ThuTuHienThi | int | Có |  |
| DaCongBo | bit | Có |  |
| DangBan | bit | Có |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |
| CongBoTuLuc | datetime2(3) | Không |  |

## 8. CauHinhMon

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaCauHinhMon | bigint | Có | PK |
| MaMon | bigint | Có | FK → Mon |
| KichCo | varchar(5) | Có |  |
| NhietDo | varchar(10) | Có |  |
| GiaBan | decimal(18,0) | Có |  |
| DangBan | bit | Có |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |
| LaMacDinh | bit | Có |  |

## 9. MonThem

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaMonThem | bigint | Có | PK |
| TenMonThem | nvarchar(100) | Có |  |
| MoTa | nvarchar(500) | Không |  |
| GiaMoiPhan | decimal(18,0) | Có |  |
| GioiHanMacDinhMoiLy | int | Có |  |
| DuongDanAnh | nvarchar(500) | Không |  |
| DangSuDung | bit | Có |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |

## 10. NguyenLieu

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaNguyenLieu | bigint | Có | PK |
| TenNguyenLieu | nvarchar(150) | Có |  |
| DonViCoSo | varchar(2) | Có |  |
| TonAnToan | decimal(18,4) | Có |  |
| NguongCanhBao | decimal(18,4) | Có |  |
| MoTa | nvarchar(500) | Không |  |
| DangSuDung | bit | Có |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |

## 11. CongThuc

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaCongThuc | bigint | Có | PK |
| MaCauHinhMon | bigint | Không | FK → CauHinhMon |
| MaMonThem | bigint | Không | FK → MonThem |
| SoPhienBan | int | Có |  |
| TenCongThuc | nvarchar(150) | Có |  |
| TrangThai | varchar(15) | Có |  |
| HieuLucTu | datetime2(3) | Không |  |
| HieuLucDen | datetime2(3) | Không |  |
| MaQuanLyTao | bigint | Có | FK → TaiKhoanQuanLy |
| MaQuanLyDuyet | bigint | Không | FK → TaiKhoanQuanLy |
| GhiChu | nvarchar(500) | Không |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |

## 12. ChiTietCongThuc

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaCongThuc | bigint | Có | PK / FK → CongThuc |
| MaNguyenLieu | bigint | Có | PK / FK → NguyenLieu |
| LoaiDinhMuc | varchar(10) | Có | PK |
| DinhMucCoSo | decimal(18,4) | Có |  |
| TyLeHaoHut | decimal(9,6) | Có |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |

## 13. Ban

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaBan | bigint | Có | PK |
| MaBanHienThi | varchar(20) | Có |  |
| TenBan | nvarchar(100) | Có |  |
| TenKhuVuc | nvarchar(100) | Không |  |
| SoChoNgoi | int | Không |  |
| DangSuDung | bit | Có |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |

## 14. DonHang

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaDonHang | bigint | Có | PK |
| MaDonHienThi | varchar(30) | Có |  |
| MaKhachHang | bigint | Không | FK → TaiKhoanKhachHang |
| MaCaLamViec | bigint | Không | FK → CaLamViec |
| MaDonLienQuan | bigint | Không | FK → DonHang |
| KenhDatHang | varchar(10) | Có |  |
| HinhThucNhanMon | varchar(15) | Có |  |
| MaBan | bigint | Không | FK → Ban |
| MaBanDaChot | varchar(20) | Không |  |
| TenNguoiNhan | nvarchar(120) | Không |  |
| DienThoaiNguoiNhan | varchar(20) | Không |  |
| DiaChiGiaoDaChot | nvarchar(500) | Không |  |
| ThoiDiemHenNhan | datetime2(3) | Không |  |
| ChinhSachGiaoHangDaChotJson | nvarchar(max) | Không |  |
| PhiGiaoHangBanDau | decimal(18,0) | Có |  |
| PhiGiaoHangHienTai | decimal(18,0) | Có |  |
| TrangThai | varchar(25) | Có |  |
| GhiChu | nvarchar(500) | Không |  |
| HetHanLuc | datetime2(3) | Không |  |
| TiepNhanLuc | datetime2(3) | Không |  |
| HoanTatLuc | datetime2(3) | Không |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |
| MaXuLyNgoaiLe | varchar(30) | Không |  |

## 15. ChiTietDonHang

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaChiTietDonHang | bigint | Có | PK |
| MaDonHang | bigint | Có | FK → DonHang |
| MaCauHinhMon | bigint | Có | FK → CauHinhMon |
| MaCongThuc | bigint | Có | FK → CongThuc |
| TenMonDaChot | nvarchar(150) | Có |  |
| KichCoDaChot | varchar(5) | Có |  |
| NhietDoDaChot | varchar(10) | Có |  |
| PhanTramDuong | int | Có |  |
| PhanTramDa | int | Có |  |
| GiaMonDaChot | decimal(18,0) | Có |  |
| SoLuongDat | int | Có |  |
| SoLuongBatDauPha | int | Có |  |
| SoLuongPhaXong | int | Có |  |
| SoLuongDaHuy | int | Có |  |
| GhiChu | nvarchar(500) | Không |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |

## 16. MonThemChiTietDonHang

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaChiTietDonHang | bigint | Có | PK / FK → ChiTietDonHang |
| MaMonThem | bigint | Có | PK / FK → MonThem |
| MaCongThuc | bigint | Có | FK → CongThuc |
| TenMonThemDaChot | nvarchar(100) | Có |  |
| SoPhanMoiLy | int | Có |  |
| GiaMoiPhanDaChot | decimal(18,0) | Có |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |

## 17. NguyenLieuChiTietDonHang

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaChiTietDonHang | bigint | Có | PK / FK → ChiTietDonHang |
| MaNguyenLieu | bigint | Có | PK / FK → NguyenLieu |
| TenNguyenLieuDaChot | nvarchar(150) | Có |  |
| DonViCoSoDaChot | nvarchar(60) | Có |  |
| DinhMucMoiLy | decimal(18,4) | Có |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| SoLuongGiuCho | decimal(18,4) | Có |  |
| SoLuongDaTieuHao | decimal(18,4) | Có |  |
| SoLuongDaGiaiPhong | decimal(18,4) | Có |  |
| HetHanLuc | datetime2(3) | Không |  |
| TrangThai | varchar(20) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |

## 18. HuyMon

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaHuyMon | bigint | Có | PK |
| MaNhomHuy | uniqueidentifier | Có |  |
| MaChiTietDonHang | bigint | Có | FK → ChiTietDonHang |
| SoLuongHuy | int | Có |  |
| LyDoHuy | nvarchar(500) | Có |  |
| MaKhachHangYeuCau | bigint | Không | FK → TaiKhoanKhachHang |
| MaCaLamViec | bigint | Không | FK → CaLamViec |
| MaQuanLyYeuCau | bigint | Không | FK → TaiKhoanQuanLy |
| MaQuanLyDuyet | bigint | Không | FK → TaiKhoanQuanLy |
| TrangThai | varchar(20) | Có |  |
| TienHangGiam | decimal(18,0) | Có |  |
| PhiGiaoHangGiam | decimal(18,0) | Có |  |
| XuLyLuc | datetime2(3) | Không |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |

## 19. ThanhToan

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaThanhToan | bigint | Có | PK |
| MaThanhToanHienThi | varchar(40) | Có |  |
| MaDonHang | bigint | Có | FK → DonHang |
| MaCaLamViec | bigint | Không | FK → CaLamViec |
| PhuongThuc | varchar(15) | Có |  |
| SoTienYeuCau | decimal(18,0) | Có |  |
| SoTienDaXacMinh | decimal(18,0) | Có |  |
| MaThamChieuChuyenKhoan | varchar(100) | Không |  |
| NoiDungQR | nvarchar(max) | Không |  |
| TrangThai | varchar(20) | Có |  |
| HetHanLuc | datetime2(3) | Không |  |
| XacMinhLuc | datetime2(3) | Không |  |
| MaQuanLyXacMinh | bigint | Không | FK → TaiKhoanQuanLy |
| ThamChieuChungCu | nvarchar(500) | Không |  |
| MaThaoTac | uniqueidentifier | Có |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |

## 20. GiaoDichNganHang

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaGiaoDichNganHang | bigint | Có | PK |
| TenNhaCungCap | varchar(50) | Có |  |
| MaTaiKhoanNganHangQuan | varchar(100) | Có |  |
| MaGiaoDichBenNgoai | varchar(150) | Có |  |
| ChieuGiaoDich | varchar(5) | Có |  |
| SoTien | decimal(18,0) | Có |  |
| NoiDungChuyenKhoan | nvarchar(500) | Không |  |
| ThoiDiemNganHang | datetime2(3) | Có |  |
| TrangThaiXacMinh | varchar(20) | Có |  |
| XacMinhLuc | datetime2(3) | Không |  |
| MaThanhToan | bigint | Không | FK → ThanhToan |
| MaLanChiHoan | bigint | Không | FK → LanChiHoanTien |
| MaBamNoiDung | binary(32) | Không |  |
| ThamChieuChungCu | nvarchar(500) | Không |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |

## 21. HoSoHoanTien

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaHoanTien | bigint | Có | PK |
| MaHoanTienHienThi | varchar(40) | Có |  |
| MaDonHang | bigint | Có | FK → DonHang |
| MaThanhToan | bigint | Có | FK → ThanhToan |
| MaHuyMon | bigint | Không | FK → HuyMon |
| LyDoHoan | nvarchar(500) | Có |  |
| SoTienYeuCau | decimal(18,0) | Có |  |
| SoTienDuocDuyet | decimal(18,0) | Không |  |
| PhuongThucHoan | varchar(15) | Có |  |
| TenNguoiNhan | nvarchar(120) | Không |  |
| NganHangNguoiNhan | nvarchar(100) | Không |  |
| TaiKhoanNhanMaHoa | varbinary(max) | Không |  |
| TaiKhoanNhanDaChe | nvarchar(50) | Không |  |
| TrangThai | varchar(25) | Có |  |
| MaKhachHangYeuCau | bigint | Không | FK → TaiKhoanKhachHang |
| MaCaYeuCau | bigint | Không | FK → CaLamViec |
| MaQuanLyYeuCau | bigint | Không | FK → TaiKhoanQuanLy |
| MaQuanLyDuyet | bigint | Không | FK → TaiKhoanQuanLy |
| DuyetLuc | datetime2(3) | Không |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |
| LoaiNguon | varchar(20) | Có |  |
| ChungCuYeuCau | nvarchar(500) | Không |  |

## 22. LanChiHoanTien

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaLanChiHoan | bigint | Có | PK |
| MaHoanTien | bigint | Có | FK → HoSoHoanTien |
| SoThuTuLanChi | int | Có |  |
| MaChongChiLap | uniqueidentifier | Có |  |
| SoTien | decimal(18,0) | Có |  |
| PhuongThuc | varchar(15) | Có |  |
| NhaCungCap | varchar(50) | Không |  |
| MaThamChieuBenNgoai | varchar(150) | Không |  |
| TrangThai | varchar(20) | Có |  |
| BatDauLuc | datetime2(3) | Có |  |
| CoKetQuaLuc | datetime2(3) | Không |  |
| MaCaLamViec | bigint | Không | FK → CaLamViec |
| MaQuanLyXacMinh | bigint | Không | FK → TaiKhoanQuanLy |
| ThamChieuChungCu | nvarchar(500) | Không |  |
| LyDoThatBai | nvarchar(500) | Không |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |

## 23. HoaDon

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaHoaDon | bigint | Có | PK |
| SoHoaDon | varchar(40) | Có |  |
| MaDonHang | bigint | Có | FK → DonHang |
| LoaiHoaDon | varchar(15) | Có |  |
| MaHoaDonGoc | bigint | Không | FK → HoaDon |
| MaHuyMon | bigint | Không | FK → HuyMon |
| TenNguoiMua | nvarchar(150) | Không |  |
| MaSoThueNguoiMua | varchar(30) | Không |  |
| SoTien | decimal(18,0) | Có |  |
| NoiDungHoaDonDaChotJson | nvarchar(max) | Có |  |
| TrangThai | varchar(15) | Có |  |
| LyDo | nvarchar(500) | Không |  |
| PhatHanhLuc | datetime2(3) | Không |  |
| MaQuanLyPhatHanh | bigint | Không | FK → TaiKhoanQuanLy |
| MaCaLamViec | bigint | Không | FK → CaLamViec |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |
| MaHoanTien | bigint | Không | FK → HoSoHoanTien |

## 24. PhieuKho

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaPhieuKho | bigint | Có | PK |
| SoPhieuKho | varchar(40) | Có |  |
| LoaiPhieu | varchar(20) | Có |  |
| TrangThai | varchar(15) | Có |  |
| TenNhaCungCapDaChot | nvarchar(200) | Không |  |
| MaMucDich | varchar(30) | Không |  |
| LyDo | nvarchar(500) | Không |  |
| MocKiemKe | datetime2(3) | Không |  |
| MaPhieuBiDao | bigint | Không | FK → PhieuKho |
| MaQuanLyLap | bigint | Có | FK → TaiKhoanQuanLy |
| MaQuanLyDuyet | bigint | Không | FK → TaiKhoanQuanLy |
| GhiSoLuc | datetime2(3) | Không |  |
| MaThaoTac | uniqueidentifier | Có |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |

## 25. ChiTietPhieuKho

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaDongPhieuKho | bigint | Có | PK |
| MaPhieuKho | bigint | Có | FK → PhieuKho |
| MaNguyenLieu | bigint | Có | FK → NguyenLieu |
| DonViNhapDaChot | nvarchar(60) | Có |  |
| SoLuongTheoDonViNhap | decimal(18,4) | Có |  |
| ChenhLechDonViCoSo | decimal(18,4) | Có |  |
| DonGiaNhap | decimal(18,2) | Không |  |
| TonSoDaChot | decimal(18,4) | Không |  |
| SoLuongThucDem | decimal(18,4) | Không |  |
| PhienBanTonKhoDaChot | binary(8) | Không |  |
| GhiChu | nvarchar(500) | Không |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |

## 26. TonKho

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaNguyenLieu | bigint | Có | PK / FK → NguyenLieu |
| TonThucTe | decimal(18,4) | Có |  |
| TongGiuCho | decimal(18,4) | Có |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |

## 27. BienDongKho

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaBienDongKho | bigint | Có | PK |
| MaNguyenLieu | bigint | Có | FK → NguyenLieu |
| MaDongPhieuKho | bigint | Không | FK → ChiTietPhieuKho |
| MaChiTietDonHang | bigint | Không | FK → ChiTietDonHang |
| LoaiBienDong | varchar(25) | Có |  |
| SoLuongThayDoi | decimal(18,4) | Có |  |
| MaThaoTac | uniqueidentifier | Có |  |
| MaBienDongBiDao | bigint | Không | FK → BienDongKho |
| PhatSinhLuc | datetime2(3) | Có |  |
| GhiChu | nvarchar(500) | Không |  |
| ThoiDiemTao | datetime2(3) | Có |  |

## 28. NhatKyHeThong

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaNhatKy | bigint | Có | PK |
| MaLienKetXuLy | uniqueidentifier | Có |  |
| MaThaoTac | uniqueidentifier | Không |  |
| LoaiTacNhan | varchar(15) | Có |  |
| MaKhachHang | bigint | Không | FK → TaiKhoanKhachHang |
| MaQuanLy | bigint | Không | FK → TaiKhoanQuanLy |
| MaCaLamViec | bigint | Không | FK → CaLamViec |
| TenNguoiThucHienDaChot | nvarchar(120) | Có |  |
| MaMayDaChot | varchar(50) | Không |  |
| HanhDong | varchar(100) | Có |  |
| LoaiDoiTuong | varchar(100) | Có |  |
| MaDoiTuong | varchar(100) | Không |  |
| DuLieuTruocJson | nvarchar(max) | Không |  |
| DuLieuSauJson | nvarchar(max) | Không |  |
| LyDo | nvarchar(500) | Không |  |
| KetQua | varchar(20) | Có |  |
| MaBamYeuCau | binary(32) | Không |  |
| KetQuaDaChotJson | nvarchar(max) | Không |  |
| PhatSinhLuc | datetime2(3) | Có |  |
| ThoiDiemTao | datetime2(3) | Có |  |

## 29. Banner

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaBanner | bigint | Có | PK |
| TenBanner | nvarchar(150) | Có |  |
| NoiDung | nvarchar(1000) | Không |  |
| DuongDanAnh | nvarchar(500) | Có |  |
| LienKet | nvarchar(500) | Không |  |
| ViTri | varchar(30) | Có |  |
| ThuTuHienThi | int | Có |  |
| BatDauLuc | datetime2(3) | Có |  |
| KetThucLuc | datetime2(3) | Không |  |
| DaCongBo | bit | Có |  |
| ThoiDiemTao | datetime2(3) | Có |  |
| ThoiDiemCapNhat | datetime2(3) | Có |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |

## 30. SuKienDongBo

| Thuộc tính | Kiểu SQL | Bắt buộc | Khóa |
| --- | --- | --- | --- |
| MaSuKien | uniqueidentifier | Có | PK |
| MaThaoTac | uniqueidentifier | Có |  |
| LoaiSuKien | varchar(50) | Có |  |
| NoiDungJson | nvarchar(max) | Có |  |
| PhatSinhLuc | datetime2(3) | Có |  |
| XuLyXongLuc | datetime2(3) | Không |  |
| SoLanThu | int | Có |  |
| ThuLaiLuc | datetime2(3) | Không |  |
| KhoaDenLuc | datetime2(3) | Không |  |
| MaLanXuLy | uniqueidentifier | Không |  |
| LoiGanNhat | nvarchar(500) | Không |  |
| PhienBanDuLieu | rowversion | Có | Chống ghi đè đồng thời |
