# Từ điển dữ liệu — đúng 34 bảng

Nguồn thiết kế hiện tại. Nhân viên không có tài khoản. Mọi FK ON DELETE NO ACTION.

## 1. TaiKhoanKhachHang

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaKhachHang | bigint | Không | PK |
| TenDangNhapChuanHoa | nvarchar(100) | Không |  |
| MatKhauBam | nvarchar(512) | Không |  |
| TrangThai | varchar(15) | Không |  |
| BatBuocDoiMatKhau | bit | Không |  |
| SoLanDangNhapSai | int | Không |  |
| KhoaDenLuc | datetime2(3) | Có |  |
| DangNhapLanCuoiLuc | datetime2(3) | Có |  |
| HoTen | nvarchar(120) | Không |  |
| EmailChuanHoa | nvarchar(254) | Có |  |
| SoDienThoaiChuanHoa | varchar(20) | Có |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `TrangThai IN ('ACTIVE','LOCKED','INACTIVE')`
- `SoLanDangNhapSai >= 0`
- `EmailChuanHoa IS NOT NULL OR SoDienThoaiChuanHoa IS NOT NULL`
- `LEN(LTRIM(RTRIM(HoTen))) > 0`

## 2. TaiKhoanQuanLy

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaQuanLy | bigint | Không | PK |
| TenDangNhapChuanHoa | nvarchar(100) | Không |  |
| MatKhauBam | nvarchar(512) | Không |  |
| TrangThai | varchar(15) | Không |  |
| BatBuocDoiMatKhau | bit | Không |  |
| SoLanDangNhapSai | int | Không |  |
| KhoaDenLuc | datetime2(3) | Có |  |
| DangNhapLanCuoiLuc | datetime2(3) | Có |  |
| HoTen | nvarchar(120) | Không |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `MaQuanLy = 1`
- `TrangThai IN ('ACTIVE','LOCKED','INACTIVE')`
- `SoLanDangNhapSai >= 0`

## 3. PhienDangNhap

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaPhienDangNhap | bigint | Không | PK |
| MaKhachHang | bigint | Có | FK → TaiKhoanKhachHang |
| MaQuanLy | bigint | Có | FK → TaiKhoanQuanLy |
| MaLamMoiDaBam | binary(32) | Không |  |
| PhamViPhien | varchar(20) | Không |  |
| LoaiUngDung | varchar(15) | Không |  |
| ThongTinThietBi | nvarchar(300) | Có |  |
| HetHanLuc | datetime2(3) | Không |  |
| HoatDongLanCuoiLuc | datetime2(3) | Có |  |
| ThuHoiLuc | datetime2(3) | Có |  |
| LyDoThuHoi | nvarchar(250) | Có |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `(MaKhachHang IS NOT NULL AND MaQuanLy IS NULL) OR (MaKhachHang IS NULL AND MaQuanLy IS NOT NULL)`
- `HetHanLuc > ThoiDiemTao`
- `PhamViPhien IN ('FULL','PASSWORD_CHANGE')`

## 4. DiaChiKhachHang

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaDiaChi | bigint | Không | PK |
| MaKhachHang | bigint | Không | FK → TaiKhoanKhachHang |
| NhanDiaChi | nvarchar(50) | Có |  |
| TenNguoiNhan | nvarchar(120) | Không |  |
| DienThoaiNguoiNhan | varchar(20) | Không |  |
| DiaChiChiTiet | nvarchar(500) | Không |  |
| ViDo | decimal(9,6) | Có |  |
| KinhDo | decimal(9,6) | Có |  |
| GhiChuGiaoHang | nvarchar(500) | Có |  |
| LaDiaChiMacDinh | bit | Không |  |
| DangSuDung | bit | Không |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `ViDo IS NULL OR ViDo BETWEEN -90 AND 90`
- `KinhDo IS NULL OR KinhDo BETWEEN -180 AND 180`
- `(ViDo IS NULL AND KinhDo IS NULL) OR (ViDo IS NOT NULL AND KinhDo IS NOT NULL)`

## 5. NhanVien

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaNhanVien | bigint | Không | PK |
| HoTen | nvarchar(120) | Không |  |
| SoDienThoai | varchar(20) | Có |  |
| NgayVaoLam | datetime2(3) | Không |  |
| TrangThai | varchar(15) | Không |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `TrangThai IN ('ACTIVE','INACTIVE')`
- `LEN(LTRIM(RTRIM(HoTen))) > 0`

## 6. CaLamViec

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaCaLamViec | bigint | Không | PK |
| MaCaHienThi | varchar(30) | Không |  |
| MaNhanVien | bigint | Không | FK → NhanVien |
| TenCa | nvarchar(20) | Không |  |
| MaMay | varchar(50) | Không |  |
| BatDauLuc | datetime2(3) | Không |  |
| KetThucLuc | datetime2(3) | Có |  |
| TrangThai | varchar(20) | Không |  |
| TienMatDauCa | decimal(18,0) | Không |  |
| TienMatDuKienCuoiCa | decimal(18,0) | Có |  |
| TienMatThucDem | decimal(18,0) | Có |  |
| LyDoChenhLech | nvarchar(500) | Có |  |
| MaCaBanGiaoTruoc | bigint | Có | FK → CaLamViec |
| MaQuanLyDuyet | bigint | Có | FK → TaiKhoanQuanLy |
| GhiChu | nvarchar(500) | Có |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `TenCa IN (N'Sáng',N'Chiều',N'Tối')`
- `TrangThai IN ('OPEN','LOCKED','HANDED_OVER','CLOSED')`
- `KetThucLuc IS NULL OR KetThucLuc >= BatDauLuc`
- `TienMatDauCa >= 0 AND (TienMatDuKienCuoiCa IS NULL OR TienMatDuKienCuoiCa >= 0) AND (TienMatThucDem IS NULL OR TienMatThucDem >= 0)`
- `(TrangThai IN ('OPEN','LOCKED') AND KetThucLuc IS NULL) OR (TrangThai IN ('CLOSED','HANDED_OVER') AND KetThucLuc IS NOT NULL)`
- `TrangThai <> 'CLOSED' OR (TienMatDuKienCuoiCa IS NOT NULL AND TienMatThucDem IS NOT NULL)`

## 7. DanhMucMon

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaDanhMuc | bigint | Không | PK |
| TenDanhMuc | nvarchar(100) | Không |  |
| MoTa | nvarchar(500) | Có |  |
| ThuTuHienThi | int | Không |  |
| DaCongBo | bit | Không |  |
| DangSuDung | bit | Không |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:



## 8. Mon

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaMon | bigint | Không | PK |
| MaDanhMuc | bigint | Không | FK → DanhMucMon |
| TenMon | nvarchar(150) | Không |  |
| MoTa | nvarchar(1000) | Có |  |
| DuongDanAnh | nvarchar(500) | Có |  |
| ThuTuHienThi | int | Không |  |
| DaCongBo | bit | Không |  |
| DangBan | bit | Không |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:



## 9. CauHinhMon

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaCauHinhMon | bigint | Không | PK |
| MaMon | bigint | Không | FK → Mon |
| KichCo | varchar(5) | Không |  |
| NhietDo | varchar(10) | Không |  |
| GiaBan | decimal(18,0) | Không |  |
| DangBan | bit | Không |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `KichCo IN ('S','M','L')`
- `NhietDo IN ('HOT','ICED')`
- `GiaBan >= 0`

## 10. MonThem

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaMonThem | bigint | Không | PK |
| TenMonThem | nvarchar(100) | Không |  |
| MoTa | nvarchar(500) | Có |  |
| GiaMoiPhan | decimal(18,0) | Không |  |
| GioiHanMacDinhMoiLy | int | Không |  |
| DuongDanAnh | nvarchar(500) | Có |  |
| DangSuDung | bit | Không |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `GiaMoiPhan >= 0`
- `GioiHanMacDinhMoiLy > 0`

## 11. CauHinhMonThem

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaCauHinhMon | bigint | Không | PK, FK → CauHinhMon |
| MaMonThem | bigint | Không | PK, FK → MonThem |
| GioiHanMoiLy | int | Không |  |
| DangSuDung | bit | Không |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `GioiHanMoiLy > 0`

## 12. TuyChonPhaCheHopLe

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaCauHinhMon | bigint | Không | PK, FK → CauHinhMon |
| PhanTramDuong | int | Không | PK |
| PhanTramDa | int | Không | PK |
| DangSuDung | bit | Không |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `PhanTramDuong IN (0,25,50,75,100)`
- `PhanTramDa IN (0,25,50,75,100)`

## 13. NguyenLieu

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaNguyenLieu | bigint | Không | PK |
| TenNguyenLieu | nvarchar(150) | Không |  |
| MaDonViCoSo | bigint | Không | FK → DonViTinh |
| TonAnToan | decimal(18,4) | Không |  |
| NguongCanhBao | decimal(18,4) | Không |  |
| MoTa | nvarchar(500) | Có |  |
| DangSuDung | bit | Không |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `TonAnToan >= 0 AND NguongCanhBao >= TonAnToan`

## 14. DonViTinh

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaDonVi | bigint | Không | PK |
| KyHieu | varchar(20) | Không |  |
| TenDonVi | nvarchar(60) | Không |  |
| DangSuDung | bit | Không |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:



## 15. QuyDoiDonViNguyenLieu

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaNguyenLieu | bigint | Không | PK, FK → NguyenLieu |
| MaDonVi | bigint | Không | PK, FK → DonViTinh |
| HeSoVeDonViCoSo | decimal(18,6) | Không |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `HeSoVeDonViCoSo > 0`

## 16. CongThuc

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaCongThuc | bigint | Không | PK |
| MaCauHinhMon | bigint | Có | FK → CauHinhMon |
| MaMonThem | bigint | Có | FK → MonThem |
| SoPhienBan | int | Không |  |
| TenCongThuc | nvarchar(150) | Không |  |
| TrangThai | varchar(15) | Không |  |
| HieuLucTu | datetime2(3) | Có |  |
| HieuLucDen | datetime2(3) | Có |  |
| MaQuanLyTao | bigint | Không | FK → TaiKhoanQuanLy |
| MaQuanLyDuyet | bigint | Có | FK → TaiKhoanQuanLy |
| GhiChu | nvarchar(500) | Có |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `(MaCauHinhMon IS NOT NULL AND MaMonThem IS NULL) OR (MaCauHinhMon IS NULL AND MaMonThem IS NOT NULL)`
- `SoPhienBan > 0`
- `HieuLucDen IS NULL OR (HieuLucTu IS NOT NULL AND HieuLucDen > HieuLucTu)`
- `TrangThai IN ('DRAFT','SCHEDULED','ACTIVE','RETIRED')`
- `TrangThai <> 'ACTIVE' OR (HieuLucTu IS NOT NULL AND MaQuanLyDuyet IS NOT NULL)`

## 17. ChiTietCongThuc

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaCongThuc | bigint | Không | PK, FK → CongThuc |
| MaNguyenLieu | bigint | Không | PK, FK → NguyenLieu |
| LoaiDinhMuc | varchar(10) | Không | PK |
| DinhMucCoSo | decimal(18,4) | Không |  |
| TyLeHaoHut | decimal(9,6) | Không |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `LoaiDinhMuc IN ('FIXED','SUGAR','ICE')`
- `DinhMucCoSo > 0`
- `TyLeHaoHut >= 0 AND TyLeHaoHut < 1`

## 18. Ban

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaBan | bigint | Không | PK |
| MaBanHienThi | varchar(20) | Không |  |
| TenBan | nvarchar(100) | Không |  |
| TenKhuVuc | nvarchar(100) | Có |  |
| SoChoNgoi | int | Có |  |
| MaBamQR | binary(32) | Không |  |
| PhienBanQR | int | Không |  |
| QRHetHanLuc | datetime2(3) | Có |  |
| DangSuDung | bit | Không |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `SoChoNgoi IS NULL OR SoChoNgoi > 0`
- `PhienBanQR > 0`

## 19. DonHang

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaDonHang | bigint | Không | PK |
| MaDonHienThi | varchar(30) | Không |  |
| MaKhachHang | bigint | Có | FK → TaiKhoanKhachHang |
| MaCaLamViec | bigint | Có | FK → CaLamViec |
| MaDonLienQuan | bigint | Có | FK → DonHang |
| KenhDatHang | varchar(10) | Không |  |
| HinhThucNhanMon | varchar(15) | Không |  |
| MaBan | bigint | Có | FK → Ban |
| MaBanDaChot | varchar(20) | Có |  |
| TenNguoiNhan | nvarchar(120) | Có |  |
| DienThoaiNguoiNhan | varchar(20) | Có |  |
| DiaChiGiaoDaChot | nvarchar(500) | Có |  |
| ViDo | decimal(9,6) | Có |  |
| KinhDo | decimal(9,6) | Có |  |
| ThoiDiemHenNhan | datetime2(3) | Có |  |
| KhoangCachGiaoKm | decimal(10,3) | Có |  |
| ChinhSachGiaoHangDaChotJson | nvarchar(max) | Có |  |
| HanBaoPhi | datetime2(3) | Có |  |
| PhiGiaoHangBanDau | decimal(18,0) | Không |  |
| PhiGiaoHangHienTai | decimal(18,0) | Không |  |
| TrangThai | varchar(25) | Không |  |
| GhiChu | nvarchar(500) | Có |  |
| HetHanLuc | datetime2(3) | Có |  |
| TiepNhanLuc | datetime2(3) | Có |  |
| HoanTatLuc | datetime2(3) | Có |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `KenhDatHang IN ('WEB','POS')`
- `HinhThucNhanMon IN ('DINE_IN','TAKEAWAY','DELIVERY')`
- `(KenhDatHang = 'WEB' AND MaKhachHang IS NOT NULL) OR (KenhDatHang = 'POS' AND MaCaLamViec IS NOT NULL)`
- `(HinhThucNhanMon = 'DINE_IN' AND MaBan IS NOT NULL) OR (HinhThucNhanMon <> 'DINE_IN' AND MaBan IS NULL)`
- `HinhThucNhanMon <> 'DELIVERY' OR (TenNguoiNhan IS NOT NULL AND DienThoaiNguoiNhan IS NOT NULL AND DiaChiGiaoDaChot IS NOT NULL)`
- `PhiGiaoHangBanDau >= 0 AND PhiGiaoHangHienTai >= 0 AND PhiGiaoHangHienTai <= PhiGiaoHangBanDau`
- `TrangThai IN ('DRAFT','PENDING_PAYMENT','PENDING_CONFIRMATION','ACCEPTED','PREPARING','READY','DELIVERING','COMPLETED','REJECTED','CANCELLED','DELIVERY_FAILED')`
- `ChinhSachGiaoHangDaChotJson IS NULL OR ISJSON(ChinhSachGiaoHangDaChotJson) = 1`
- `ViDo IS NULL OR ViDo BETWEEN -90 AND 90`
- `KinhDo IS NULL OR KinhDo BETWEEN -180 AND 180`
- `KhoangCachGiaoKm IS NULL OR KhoangCachGiaoKm >= 0`

## 20. ChiTietDonHang

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaChiTietDonHang | bigint | Không | PK |
| MaDonHang | bigint | Không | FK → DonHang |
| MaCauHinhMon | bigint | Không | FK → CauHinhMon |
| MaCongThuc | bigint | Không | FK → CongThuc |
| TenMonDaChot | nvarchar(150) | Không |  |
| KichCoDaChot | varchar(5) | Không |  |
| NhietDoDaChot | varchar(10) | Không |  |
| PhanTramDuong | int | Không |  |
| PhanTramDa | int | Không |  |
| GiaMonDaChot | decimal(18,0) | Không |  |
| SoLuongDat | int | Không |  |
| SoLuongBatDauPha | int | Không |  |
| SoLuongPhaXong | int | Không |  |
| SoLuongDaHuy | int | Không |  |
| GhiChu | nvarchar(500) | Có |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `GiaMonDaChot >= 0`
- `SoLuongDat > 0 AND SoLuongBatDauPha >= 0 AND SoLuongPhaXong >= 0 AND SoLuongDaHuy >= 0`
- `SoLuongBatDauPha + SoLuongDaHuy <= SoLuongDat`
- `SoLuongPhaXong <= SoLuongBatDauPha`
- `PhanTramDuong IN (0,25,50,75,100) AND PhanTramDa IN (0,25,50,75,100)`

## 21. MonThemChiTietDonHang

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaChiTietDonHang | bigint | Không | PK, FK → ChiTietDonHang |
| MaMonThem | bigint | Không | PK, FK → MonThem |
| MaCongThuc | bigint | Không | FK → CongThuc |
| TenMonThemDaChot | nvarchar(100) | Không |  |
| SoPhanMoiLy | int | Không |  |
| GiaMoiPhanDaChot | decimal(18,0) | Không |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `SoPhanMoiLy > 0`
- `GiaMoiPhanDaChot >= 0`

## 22. NguyenLieuChiTietDonHang

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaChiTietDonHang | bigint | Không | PK, FK → ChiTietDonHang |
| MaNguyenLieu | bigint | Không | PK, FK → NguyenLieu |
| TenNguyenLieuDaChot | nvarchar(150) | Không |  |
| DonViCoSoDaChot | nvarchar(60) | Không |  |
| DinhMucMoiLy | decimal(18,4) | Không |  |
| ThoiDiemTao | datetime2(3) | Không |  |

Ràng buộc:

- `DinhMucMoiLy > 0`

## 23. HuyMon

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaHuyMon | bigint | Không | PK |
| MaNhomHuy | uniqueidentifier | Không |  |
| MaChiTietDonHang | bigint | Không | FK → ChiTietDonHang |
| SoLuongHuy | int | Không |  |
| LyDoHuy | nvarchar(500) | Không |  |
| MaKhachHangYeuCau | bigint | Có | FK → TaiKhoanKhachHang |
| MaCaLamViec | bigint | Có | FK → CaLamViec |
| MaQuanLyYeuCau | bigint | Có | FK → TaiKhoanQuanLy |
| MaQuanLyDuyet | bigint | Có | FK → TaiKhoanQuanLy |
| TrangThai | varchar(20) | Không |  |
| TienHangGiam | decimal(18,0) | Không |  |
| PhiGiaoHangGiam | decimal(18,0) | Không |  |
| XuLyLuc | datetime2(3) | Có |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `SoLuongHuy > 0`
- `LEN(LTRIM(RTRIM(LyDoHuy))) > 0`
- `(CASE WHEN MaKhachHangYeuCau IS NULL THEN 0 ELSE 1 END + CASE WHEN MaCaLamViec IS NULL THEN 0 ELSE 1 END + CASE WHEN MaQuanLyYeuCau IS NULL THEN 0 ELSE 1 END) = 1`
- `TrangThai IN ('REQUESTED','ACCEPTED','REJECTED')`
- `TienHangGiam >= 0 AND PhiGiaoHangGiam >= 0`

## 24. ThanhToan

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaThanhToan | bigint | Không | PK |
| MaThanhToanHienThi | varchar(40) | Không |  |
| MaDonHang | bigint | Không | FK → DonHang |
| MaCaLamViec | bigint | Có | FK → CaLamViec |
| PhuongThuc | varchar(15) | Không |  |
| SoTienYeuCau | decimal(18,0) | Không |  |
| SoTienDaXacMinh | decimal(18,0) | Không |  |
| TienKhachDua | decimal(18,0) | Có |  |
| MaThamChieuChuyenKhoan | varchar(100) | Có |  |
| NoiDungQR | nvarchar(max) | Có |  |
| TrangThai | varchar(20) | Không |  |
| HetHanLuc | datetime2(3) | Có |  |
| XacMinhLuc | datetime2(3) | Có |  |
| MaQuanLyXacMinh | bigint | Có | FK → TaiKhoanQuanLy |
| ThamChieuChungCu | nvarchar(500) | Có |  |
| MaThaoTac | uniqueidentifier | Không |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `PhuongThuc IN ('CASH','BANK_TRANSFER')`
- `SoTienYeuCau >= 0 AND SoTienDaXacMinh >= 0`
- `PhuongThuc <> 'CASH' OR MaCaLamViec IS NOT NULL`
- `TienKhachDua IS NULL OR TienKhachDua >= SoTienDaXacMinh`
- `TrangThai IN ('PENDING','PARTIAL','VERIFIED','EXPIRED','FAILED','RECONCILING')`

## 25. GiaoDichNganHang

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaGiaoDichNganHang | bigint | Không | PK |
| TenNhaCungCap | varchar(50) | Không |  |
| MaTaiKhoanNganHangQuan | varchar(100) | Không |  |
| MaGiaoDichBenNgoai | varchar(150) | Không |  |
| ChieuGiaoDich | varchar(5) | Không |  |
| SoTien | decimal(18,0) | Không |  |
| NoiDungChuyenKhoan | nvarchar(500) | Có |  |
| ThoiDiemNganHang | datetime2(3) | Không |  |
| TrangThaiXacMinh | varchar(20) | Không |  |
| XacMinhLuc | datetime2(3) | Có |  |
| MaThanhToan | bigint | Có | FK → ThanhToan |
| MaLanChiHoan | bigint | Có | FK → LanChiHoanTien |
| MaBamNoiDung | binary(32) | Có |  |
| ThamChieuChungCu | nvarchar(500) | Có |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `SoTien > 0`
- `ChieuGiaoDich IN ('IN','OUT')`
- `MaThanhToan IS NULL OR MaLanChiHoan IS NULL`
- `MaThanhToan IS NULL OR ChieuGiaoDich = 'IN'`
- `MaLanChiHoan IS NULL OR ChieuGiaoDich = 'OUT'`

## 26. HoSoHoanTien

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaHoanTien | bigint | Không | PK |
| MaHoanTienHienThi | varchar(40) | Không |  |
| MaDonHang | bigint | Không | FK → DonHang |
| MaThanhToan | bigint | Không | FK → ThanhToan |
| MaHuyMon | bigint | Có | FK → HuyMon |
| LyDoHoan | nvarchar(500) | Không |  |
| SoTienYeuCau | decimal(18,0) | Không |  |
| SoTienDuocDuyet | decimal(18,0) | Có |  |
| PhuongThucHoan | varchar(15) | Không |  |
| TenNguoiNhan | nvarchar(120) | Có |  |
| NganHangNguoiNhan | nvarchar(100) | Có |  |
| TaiKhoanNhanMaHoa | varbinary(max) | Có |  |
| TaiKhoanNhanDaChe | nvarchar(50) | Có |  |
| TrangThai | varchar(25) | Không |  |
| MaKhachHangYeuCau | bigint | Có | FK → TaiKhoanKhachHang |
| MaCaYeuCau | bigint | Có | FK → CaLamViec |
| MaQuanLyYeuCau | bigint | Có | FK → TaiKhoanQuanLy |
| MaQuanLyDuyet | bigint | Có | FK → TaiKhoanQuanLy |
| DuyetLuc | datetime2(3) | Có |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `SoTienYeuCau > 0`
- `SoTienDuocDuyet IS NULL OR (SoTienDuocDuyet > 0 AND SoTienDuocDuyet <= SoTienYeuCau)`
- `TrangThai IN ('REQUESTED','APPROVED','REJECTED','PROCESSING','PARTIALLY_REFUNDED','REFUNDED','RECONCILING')`
- `(CASE WHEN MaKhachHangYeuCau IS NULL THEN 0 ELSE 1 END + CASE WHEN MaCaYeuCau IS NULL THEN 0 ELSE 1 END + CASE WHEN MaQuanLyYeuCau IS NULL THEN 0 ELSE 1 END) = 1`
- `PhuongThucHoan IN ('CASH','BANK_TRANSFER')`

## 27. LanChiHoanTien

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaLanChiHoan | bigint | Không | PK |
| MaHoanTien | bigint | Không | FK → HoSoHoanTien |
| SoThuTuLanChi | int | Không |  |
| MaChongChiLap | uniqueidentifier | Không |  |
| SoTien | decimal(18,0) | Không |  |
| PhuongThuc | varchar(15) | Không |  |
| NhaCungCap | varchar(50) | Có |  |
| MaThamChieuBenNgoai | varchar(150) | Có |  |
| TrangThai | varchar(20) | Không |  |
| BatDauLuc | datetime2(3) | Không |  |
| CoKetQuaLuc | datetime2(3) | Có |  |
| MaCaLamViec | bigint | Có | FK → CaLamViec |
| MaQuanLyXacMinh | bigint | Có | FK → TaiKhoanQuanLy |
| ThamChieuChungCu | nvarchar(500) | Có |  |
| LyDoThatBai | nvarchar(500) | Có |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `SoThuTuLanChi > 0 AND SoTien > 0`
- `TrangThai IN ('PENDING','SUCCEEDED','FAILED','RECONCILING')`
- `PhuongThuc IN ('CASH','BANK_TRANSFER')`
- `CoKetQuaLuc IS NULL OR CoKetQuaLuc >= BatDauLuc`

## 28. HoaDon

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaHoaDon | bigint | Không | PK |
| SoHoaDon | varchar(40) | Không |  |
| MaDonHang | bigint | Không | FK → DonHang |
| LoaiHoaDon | varchar(15) | Không |  |
| MaHoaDonGoc | bigint | Có | FK → HoaDon |
| MaHuyMon | bigint | Có | FK → HuyMon |
| TenNguoiMua | nvarchar(150) | Có |  |
| MaSoThueNguoiMua | varchar(30) | Có |  |
| SoTien | decimal(18,0) | Không |  |
| NoiDungHoaDonDaChotJson | nvarchar(max) | Không |  |
| TrangThai | varchar(15) | Không |  |
| LyDo | nvarchar(500) | Có |  |
| PhatHanhLuc | datetime2(3) | Có |  |
| MaQuanLyPhatHanh | bigint | Có | FK → TaiKhoanQuanLy |
| MaCaLamViec | bigint | Có | FK → CaLamViec |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `LoaiHoaDon IN ('ORIGINAL','ADJUSTMENT')`
- `(LoaiHoaDon = 'ORIGINAL' AND MaHoaDonGoc IS NULL AND SoTien >= 0) OR (LoaiHoaDon = 'ADJUSTMENT' AND MaHoaDonGoc IS NOT NULL AND MaHoaDonGoc <> MaHoaDon)`
- `TrangThai IN ('DRAFT','ISSUED')`
- `TrangThai <> 'ISSUED' OR PhatHanhLuc IS NOT NULL`
- `ISJSON(NoiDungHoaDonDaChotJson) = 1`

## 29. PhieuKho

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaPhieuKho | bigint | Không | PK |
| SoPhieuKho | varchar(40) | Không |  |
| LoaiPhieu | varchar(20) | Không |  |
| TrangThai | varchar(15) | Không |  |
| TenNhaCungCapDaChot | nvarchar(200) | Có |  |
| MaMucDich | varchar(30) | Có |  |
| LyDo | nvarchar(500) | Có |  |
| MocKiemKe | datetime2(3) | Có |  |
| MaPhieuBiDao | bigint | Có | FK → PhieuKho |
| MaQuanLyLap | bigint | Không | FK → TaiKhoanQuanLy |
| MaQuanLyDuyet | bigint | Có | FK → TaiKhoanQuanLy |
| GhiSoLuc | datetime2(3) | Có |  |
| MaThaoTac | uniqueidentifier | Không |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `LoaiPhieu IN ('RECEIPT','ISSUE','COUNT','ADJUSTMENT','REVERSAL')`
- `TrangThai IN ('DRAFT','POSTED')`
- `TrangThai <> 'POSTED' OR (GhiSoLuc IS NOT NULL AND MaQuanLyDuyet IS NOT NULL)`
- `MaPhieuBiDao IS NULL OR MaPhieuBiDao <> MaPhieuKho`

## 30. ChiTietPhieuKho

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaDongPhieuKho | bigint | Không | PK |
| MaPhieuKho | bigint | Không | FK → PhieuKho |
| MaNguyenLieu | bigint | Không | FK → NguyenLieu |
| MaDonViNhap | bigint | Không | FK → DonViTinh |
| DonViNhapDaChot | nvarchar(60) | Không |  |
| HeSoQuyDoiDaChot | decimal(18,6) | Không |  |
| SoLuongTheoDonViNhap | decimal(18,4) | Không |  |
| ChenhLechDonViCoSo | decimal(18,4) | Không |  |
| DonGiaNhap | decimal(18,2) | Có |  |
| TonSoDaChot | decimal(18,4) | Có |  |
| SoLuongThucDem | decimal(18,4) | Có |  |
| PhienBanTonKhoDaChot | binary(8) | Có |  |
| GhiChu | nvarchar(500) | Có |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `HeSoQuyDoiDaChot > 0`
- `SoLuongTheoDonViNhap >= 0`
- `DonGiaNhap IS NULL OR DonGiaNhap >= 0`
- `TonSoDaChot IS NULL OR TonSoDaChot >= 0`
- `SoLuongThucDem IS NULL OR SoLuongThucDem >= 0`

## 31. TonKho

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaNguyenLieu | bigint | Không | PK, FK → NguyenLieu |
| TonThucTe | decimal(18,4) | Không |  |
| TongGiuCho | decimal(18,4) | Không |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `TonThucTe >= 0 AND TongGiuCho >= 0 AND TongGiuCho <= TonThucTe`

## 32. GiuChoNguyenLieu

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaChiTietDonHang | bigint | Không | PK, FK → NguyenLieuChiTietDonHang |
| MaNguyenLieu | bigint | Không | PK, FK → NguyenLieuChiTietDonHang |
| SoLuongGiuCho | decimal(18,4) | Không |  |
| SoLuongDaTieuHao | decimal(18,4) | Không |  |
| SoLuongDaGiaiPhong | decimal(18,4) | Không |  |
| HetHanLuc | datetime2(3) | Có |  |
| TrangThai | varchar(20) | Không |  |
| ThoiDiemTao | datetime2(3) | Không |  |
| ThoiDiemCapNhat | datetime2(3) | Không |  |
| PhienBanDuLieu | rowversion | Không |  |

Ràng buộc:

- `SoLuongGiuCho >= 0 AND SoLuongDaTieuHao >= 0 AND SoLuongDaGiaiPhong >= 0`
- `SoLuongDaTieuHao + SoLuongDaGiaiPhong <= SoLuongGiuCho`
- `TrangThai IN ('RESERVED','PARTIAL','CONSUMED','RELEASED','EXPIRED')`

## 33. BienDongKho

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaBienDongKho | bigint | Không | PK |
| MaNguyenLieu | bigint | Không | FK → NguyenLieu |
| MaDongPhieuKho | bigint | Có | FK → ChiTietPhieuKho |
| MaChiTietDonHang | bigint | Có | FK → ChiTietDonHang |
| LoaiBienDong | varchar(25) | Không |  |
| SoLuongThayDoi | decimal(18,4) | Không |  |
| MaThaoTac | uniqueidentifier | Không |  |
| MaBienDongBiDao | bigint | Có | FK → BienDongKho |
| PhatSinhLuc | datetime2(3) | Không |  |
| GhiChu | nvarchar(500) | Có |  |
| ThoiDiemTao | datetime2(3) | Không |  |

Ràng buộc:

- `SoLuongThayDoi <> 0`
- `(MaDongPhieuKho IS NOT NULL AND MaChiTietDonHang IS NULL) OR (MaDongPhieuKho IS NULL AND MaChiTietDonHang IS NOT NULL)`
- `MaBienDongBiDao IS NULL OR MaBienDongBiDao <> MaBienDongKho`

## 34. NhatKyHeThong

| Thuộc tính | Kiểu SQL | NULL | Khóa |
|---|---|---|---|
| MaNhatKy | bigint | Không | PK |
| MaLienKetXuLy | uniqueidentifier | Không |  |
| MaThaoTac | uniqueidentifier | Có |  |
| LoaiTacNhan | varchar(15) | Không |  |
| MaKhachHang | bigint | Có | FK → TaiKhoanKhachHang |
| MaQuanLy | bigint | Có | FK → TaiKhoanQuanLy |
| MaCaLamViec | bigint | Có | FK → CaLamViec |
| TenNguoiThucHienDaChot | nvarchar(120) | Không |  |
| MaMayDaChot | varchar(50) | Có |  |
| HanhDong | varchar(100) | Không |  |
| LoaiDoiTuong | varchar(100) | Không |  |
| MaDoiTuong | varchar(100) | Có |  |
| DuLieuTruocJson | nvarchar(max) | Có |  |
| DuLieuSauJson | nvarchar(max) | Có |  |
| LyDo | nvarchar(500) | Có |  |
| KetQua | varchar(20) | Không |  |
| MaBamYeuCau | binary(32) | Có |  |
| KetQuaDaChotJson | nvarchar(max) | Có |  |
| PhatSinhLuc | datetime2(3) | Không |  |
| ThoiDiemTao | datetime2(3) | Không |  |

Ràng buộc:

- `(LoaiTacNhan = 'CUSTOMER' AND MaKhachHang IS NOT NULL AND MaQuanLy IS NULL AND MaCaLamViec IS NULL) OR (LoaiTacNhan = 'ADMIN' AND MaQuanLy IS NOT NULL AND MaKhachHang IS NULL AND MaCaLamViec IS NULL) OR (LoaiTacNhan = 'STAFF' AND MaCaLamViec IS NOT NULL AND MaKhachHang IS NULL AND MaQuanLy IS NULL) OR (LoaiTacNhan = 'SYSTEM' AND MaKhachHang IS NULL AND MaQuanLy IS NULL AND MaCaLamViec IS NULL)`
- `DuLieuTruocJson IS NULL OR ISJSON(DuLieuTruocJson) = 1`
- `DuLieuSauJson IS NULL OR ISJSON(DuLieuSauJson) = 1`
- `KetQuaDaChotJson IS NULL OR ISJSON(KetQuaDaChotJson) = 1`
