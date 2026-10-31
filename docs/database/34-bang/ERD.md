# ERD 34 bảng

Không dùng ERD 25 bảng cũ để tạo schema hiện tại.

```mermaid
erDiagram
    TaiKhoanKhachHang {
        bigint MaKhachHang PK
        string TenDangNhapChuanHoa
        string MatKhauBam
        string TrangThai
        bit BatBuocDoiMatKhau
        int SoLanDangNhapSai
        datetime KhoaDenLuc
        datetime DangNhapLanCuoiLuc
        string HoTen
        string EmailChuanHoa
        string SoDienThoaiChuanHoa
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    TaiKhoanQuanLy {
        bigint MaQuanLy PK
        string TenDangNhapChuanHoa
        string MatKhauBam
        string TrangThai
        bit BatBuocDoiMatKhau
        int SoLanDangNhapSai
        datetime KhoaDenLuc
        datetime DangNhapLanCuoiLuc
        string HoTen
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    PhienDangNhap {
        bigint MaPhienDangNhap PK
        bigint MaKhachHang FK
        bigint MaQuanLy FK
        binary MaLamMoiDaBam
        string PhamViPhien
        string LoaiUngDung
        string ThongTinThietBi
        datetime HetHanLuc
        datetime HoatDongLanCuoiLuc
        datetime ThuHoiLuc
        string LyDoThuHoi
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    DiaChiKhachHang {
        bigint MaDiaChi PK
        bigint MaKhachHang FK
        string NhanDiaChi
        string TenNguoiNhan
        string DienThoaiNguoiNhan
        string DiaChiChiTiet
        decimal ViDo
        decimal KinhDo
        string GhiChuGiaoHang
        bit LaDiaChiMacDinh
        bit DangSuDung
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    NhanVien {
        bigint MaNhanVien PK
        string HoTen
        string SoDienThoai
        datetime NgayVaoLam
        string TrangThai
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    CaLamViec {
        bigint MaCaLamViec PK
        string MaCaHienThi
        bigint MaNhanVien FK
        string TenCa
        string MaMay
        datetime BatDauLuc
        datetime KetThucLuc
        string TrangThai
        decimal TienMatDauCa
        decimal TienMatDuKienCuoiCa
        decimal TienMatThucDem
        string LyDoChenhLech
        bigint MaCaBanGiaoTruoc FK
        bigint MaQuanLyDuyet FK
        string GhiChu
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    DanhMucMon {
        bigint MaDanhMuc PK
        string TenDanhMuc
        string MoTa
        int ThuTuHienThi
        bit DaCongBo
        bit DangSuDung
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    Mon {
        bigint MaMon PK
        bigint MaDanhMuc FK
        string TenMon
        string MoTa
        string DuongDanAnh
        int ThuTuHienThi
        bit DaCongBo
        bit DangBan
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    CauHinhMon {
        bigint MaCauHinhMon PK
        bigint MaMon FK
        string KichCo
        string NhietDo
        decimal GiaBan
        bit DangBan
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    MonThem {
        bigint MaMonThem PK
        string TenMonThem
        string MoTa
        decimal GiaMoiPhan
        int GioiHanMacDinhMoiLy
        string DuongDanAnh
        bit DangSuDung
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    CauHinhMonThem {
        bigint MaCauHinhMon PK,FK
        bigint MaMonThem PK,FK
        int GioiHanMoiLy
        bit DangSuDung
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    TuyChonPhaCheHopLe {
        bigint MaCauHinhMon PK,FK
        int PhanTramDuong PK
        int PhanTramDa PK
        bit DangSuDung
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    NguyenLieu {
        bigint MaNguyenLieu PK
        string TenNguyenLieu
        bigint MaDonViCoSo FK
        decimal TonAnToan
        decimal NguongCanhBao
        string MoTa
        bit DangSuDung
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    DonViTinh {
        bigint MaDonVi PK
        string KyHieu
        string TenDonVi
        bit DangSuDung
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    QuyDoiDonViNguyenLieu {
        bigint MaNguyenLieu PK,FK
        bigint MaDonVi PK,FK
        decimal HeSoVeDonViCoSo
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    CongThuc {
        bigint MaCongThuc PK
        bigint MaCauHinhMon FK
        bigint MaMonThem FK
        int SoPhienBan
        string TenCongThuc
        string TrangThai
        datetime HieuLucTu
        datetime HieuLucDen
        bigint MaQuanLyTao FK
        bigint MaQuanLyDuyet FK
        string GhiChu
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    ChiTietCongThuc {
        bigint MaCongThuc PK,FK
        bigint MaNguyenLieu PK,FK
        string LoaiDinhMuc PK
        decimal DinhMucCoSo
        decimal TyLeHaoHut
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    Ban {
        bigint MaBan PK
        string MaBanHienThi
        string TenBan
        string TenKhuVuc
        int SoChoNgoi
        binary MaBamQR
        int PhienBanQR
        datetime QRHetHanLuc
        bit DangSuDung
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    DonHang {
        bigint MaDonHang PK
        string MaDonHienThi
        bigint MaKhachHang FK
        bigint MaCaLamViec FK
        bigint MaDonLienQuan FK
        string KenhDatHang
        string HinhThucNhanMon
        bigint MaBan FK
        string MaBanDaChot
        string TenNguoiNhan
        string DienThoaiNguoiNhan
        string DiaChiGiaoDaChot
        decimal ViDo
        decimal KinhDo
        datetime ThoiDiemHenNhan
        decimal KhoangCachGiaoKm
        string ChinhSachGiaoHangDaChotJson
        datetime HanBaoPhi
        decimal PhiGiaoHangBanDau
        decimal PhiGiaoHangHienTai
        string TrangThai
        string GhiChu
        datetime HetHanLuc
        datetime TiepNhanLuc
        datetime HoanTatLuc
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    ChiTietDonHang {
        bigint MaChiTietDonHang PK
        bigint MaDonHang FK
        bigint MaCauHinhMon FK
        bigint MaCongThuc FK
        string TenMonDaChot
        string KichCoDaChot
        string NhietDoDaChot
        int PhanTramDuong
        int PhanTramDa
        decimal GiaMonDaChot
        int SoLuongDat
        int SoLuongBatDauPha
        int SoLuongPhaXong
        int SoLuongDaHuy
        string GhiChu
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    MonThemChiTietDonHang {
        bigint MaChiTietDonHang PK,FK
        bigint MaMonThem PK,FK
        bigint MaCongThuc FK
        string TenMonThemDaChot
        int SoPhanMoiLy
        decimal GiaMoiPhanDaChot
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    NguyenLieuChiTietDonHang {
        bigint MaChiTietDonHang PK,FK
        bigint MaNguyenLieu PK,FK
        string TenNguyenLieuDaChot
        string DonViCoSoDaChot
        decimal DinhMucMoiLy
        datetime ThoiDiemTao
    }
    HuyMon {
        bigint MaHuyMon PK
        uniqueidentifier MaNhomHuy
        bigint MaChiTietDonHang FK
        int SoLuongHuy
        string LyDoHuy
        bigint MaKhachHangYeuCau FK
        bigint MaCaLamViec FK
        bigint MaQuanLyYeuCau FK
        bigint MaQuanLyDuyet FK
        string TrangThai
        decimal TienHangGiam
        decimal PhiGiaoHangGiam
        datetime XuLyLuc
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    ThanhToan {
        bigint MaThanhToan PK
        string MaThanhToanHienThi
        bigint MaDonHang FK
        bigint MaCaLamViec FK
        string PhuongThuc
        decimal SoTienYeuCau
        decimal SoTienDaXacMinh
        decimal TienKhachDua
        string MaThamChieuChuyenKhoan
        string NoiDungQR
        string TrangThai
        datetime HetHanLuc
        datetime XacMinhLuc
        bigint MaQuanLyXacMinh FK
        string ThamChieuChungCu
        uniqueidentifier MaThaoTac
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    GiaoDichNganHang {
        bigint MaGiaoDichNganHang PK
        string TenNhaCungCap
        string MaTaiKhoanNganHangQuan
        string MaGiaoDichBenNgoai
        string ChieuGiaoDich
        decimal SoTien
        string NoiDungChuyenKhoan
        datetime ThoiDiemNganHang
        string TrangThaiXacMinh
        datetime XacMinhLuc
        bigint MaThanhToan FK
        bigint MaLanChiHoan FK
        binary MaBamNoiDung
        string ThamChieuChungCu
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    HoSoHoanTien {
        bigint MaHoanTien PK
        string MaHoanTienHienThi
        bigint MaDonHang FK
        bigint MaThanhToan FK
        bigint MaHuyMon FK
        string LyDoHoan
        decimal SoTienYeuCau
        decimal SoTienDuocDuyet
        string PhuongThucHoan
        string TenNguoiNhan
        string NganHangNguoiNhan
        varbinary TaiKhoanNhanMaHoa
        string TaiKhoanNhanDaChe
        string TrangThai
        bigint MaKhachHangYeuCau FK
        bigint MaCaYeuCau FK
        bigint MaQuanLyYeuCau FK
        bigint MaQuanLyDuyet FK
        datetime DuyetLuc
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    LanChiHoanTien {
        bigint MaLanChiHoan PK
        bigint MaHoanTien FK
        int SoThuTuLanChi
        uniqueidentifier MaChongChiLap
        decimal SoTien
        string PhuongThuc
        string NhaCungCap
        string MaThamChieuBenNgoai
        string TrangThai
        datetime BatDauLuc
        datetime CoKetQuaLuc
        bigint MaCaLamViec FK
        bigint MaQuanLyXacMinh FK
        string ThamChieuChungCu
        string LyDoThatBai
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    HoaDon {
        bigint MaHoaDon PK
        string SoHoaDon
        bigint MaDonHang FK
        string LoaiHoaDon
        bigint MaHoaDonGoc FK
        bigint MaHuyMon FK
        string TenNguoiMua
        string MaSoThueNguoiMua
        decimal SoTien
        string NoiDungHoaDonDaChotJson
        string TrangThai
        string LyDo
        datetime PhatHanhLuc
        bigint MaQuanLyPhatHanh FK
        bigint MaCaLamViec FK
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    PhieuKho {
        bigint MaPhieuKho PK
        string SoPhieuKho
        string LoaiPhieu
        string TrangThai
        string TenNhaCungCapDaChot
        string MaMucDich
        string LyDo
        datetime MocKiemKe
        bigint MaPhieuBiDao FK
        bigint MaQuanLyLap FK
        bigint MaQuanLyDuyet FK
        datetime GhiSoLuc
        uniqueidentifier MaThaoTac
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    ChiTietPhieuKho {
        bigint MaDongPhieuKho PK
        bigint MaPhieuKho FK
        bigint MaNguyenLieu FK
        bigint MaDonViNhap FK
        string DonViNhapDaChot
        decimal HeSoQuyDoiDaChot
        decimal SoLuongTheoDonViNhap
        decimal ChenhLechDonViCoSo
        decimal DonGiaNhap
        decimal TonSoDaChot
        decimal SoLuongThucDem
        binary PhienBanTonKhoDaChot
        string GhiChu
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    TonKho {
        bigint MaNguyenLieu PK,FK
        decimal TonThucTe
        decimal TongGiuCho
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    GiuChoNguyenLieu {
        bigint MaChiTietDonHang PK,FK
        bigint MaNguyenLieu PK,FK
        decimal SoLuongGiuCho
        decimal SoLuongDaTieuHao
        decimal SoLuongDaGiaiPhong
        datetime HetHanLuc
        string TrangThai
        datetime ThoiDiemTao
        datetime ThoiDiemCapNhat
        rowversion PhienBanDuLieu
    }
    BienDongKho {
        bigint MaBienDongKho PK
        bigint MaNguyenLieu FK
        bigint MaDongPhieuKho FK
        bigint MaChiTietDonHang FK
        string LoaiBienDong
        decimal SoLuongThayDoi
        uniqueidentifier MaThaoTac
        bigint MaBienDongBiDao FK
        datetime PhatSinhLuc
        string GhiChu
        datetime ThoiDiemTao
    }
    NhatKyHeThong {
        bigint MaNhatKy PK
        uniqueidentifier MaLienKetXuLy
        uniqueidentifier MaThaoTac
        string LoaiTacNhan
        bigint MaKhachHang FK
        bigint MaQuanLy FK
        bigint MaCaLamViec FK
        string TenNguoiThucHienDaChot
        string MaMayDaChot
        string HanhDong
        string LoaiDoiTuong
        string MaDoiTuong
        string DuLieuTruocJson
        string DuLieuSauJson
        string LyDo
        string KetQua
        binary MaBamYeuCau
        string KetQuaDaChotJson
        datetime PhatSinhLuc
        datetime ThoiDiemTao
    }
    TaiKhoanKhachHang |o--o{ PhienDangNhap : "MaKhachHang"
    TaiKhoanQuanLy |o--o{ PhienDangNhap : "MaQuanLy"
    TaiKhoanKhachHang ||--o{ DiaChiKhachHang : "MaKhachHang"
    NhanVien ||--o{ CaLamViec : "MaNhanVien"
    CaLamViec |o--o{ CaLamViec : "MaCaBanGiaoTruoc"
    TaiKhoanQuanLy |o--o{ CaLamViec : "MaQuanLyDuyet"
    DanhMucMon ||--o{ Mon : "MaDanhMuc"
    Mon ||--o{ CauHinhMon : "MaMon"
    CauHinhMon ||--o{ CauHinhMonThem : "MaCauHinhMon"
    MonThem ||--o{ CauHinhMonThem : "MaMonThem"
    CauHinhMon ||--o{ TuyChonPhaCheHopLe : "MaCauHinhMon"
    DonViTinh ||--o{ NguyenLieu : "MaDonViCoSo"
    NguyenLieu ||--o{ QuyDoiDonViNguyenLieu : "MaNguyenLieu"
    DonViTinh ||--o{ QuyDoiDonViNguyenLieu : "MaDonVi"
    CauHinhMon |o--o{ CongThuc : "MaCauHinhMon"
    MonThem |o--o{ CongThuc : "MaMonThem"
    TaiKhoanQuanLy ||--o{ CongThuc : "MaQuanLyTao"
    TaiKhoanQuanLy |o--o{ CongThuc : "MaQuanLyDuyet"
    CongThuc ||--o{ ChiTietCongThuc : "MaCongThuc"
    NguyenLieu ||--o{ ChiTietCongThuc : "MaNguyenLieu"
    TaiKhoanKhachHang |o--o{ DonHang : "MaKhachHang"
    CaLamViec |o--o{ DonHang : "MaCaLamViec"
    DonHang |o--o{ DonHang : "MaDonLienQuan"
    Ban |o--o{ DonHang : "MaBan"
    DonHang ||--o{ ChiTietDonHang : "MaDonHang"
    CauHinhMon ||--o{ ChiTietDonHang : "MaCauHinhMon"
    CongThuc ||--o{ ChiTietDonHang : "MaCongThuc"
    ChiTietDonHang ||--o{ MonThemChiTietDonHang : "MaChiTietDonHang"
    MonThem ||--o{ MonThemChiTietDonHang : "MaMonThem"
    CongThuc ||--o{ MonThemChiTietDonHang : "MaCongThuc"
    ChiTietDonHang ||--o{ NguyenLieuChiTietDonHang : "MaChiTietDonHang"
    NguyenLieu ||--o{ NguyenLieuChiTietDonHang : "MaNguyenLieu"
    ChiTietDonHang ||--o{ HuyMon : "MaChiTietDonHang"
    TaiKhoanKhachHang |o--o{ HuyMon : "MaKhachHangYeuCau"
    CaLamViec |o--o{ HuyMon : "MaCaLamViec"
    TaiKhoanQuanLy |o--o{ HuyMon : "MaQuanLyYeuCau"
    TaiKhoanQuanLy |o--o{ HuyMon : "MaQuanLyDuyet"
    DonHang ||--o{ ThanhToan : "MaDonHang"
    CaLamViec |o--o{ ThanhToan : "MaCaLamViec"
    TaiKhoanQuanLy |o--o{ ThanhToan : "MaQuanLyXacMinh"
    DonHang ||--o{ HoSoHoanTien : "MaDonHang"
    ThanhToan ||--o{ HoSoHoanTien : "MaThanhToan"
    HuyMon |o--o{ HoSoHoanTien : "MaHuyMon"
    TaiKhoanKhachHang |o--o{ HoSoHoanTien : "MaKhachHangYeuCau"
    CaLamViec |o--o{ HoSoHoanTien : "MaCaYeuCau"
    TaiKhoanQuanLy |o--o{ HoSoHoanTien : "MaQuanLyYeuCau"
    TaiKhoanQuanLy |o--o{ HoSoHoanTien : "MaQuanLyDuyet"
    HoSoHoanTien ||--o{ LanChiHoanTien : "MaHoanTien"
    CaLamViec |o--o{ LanChiHoanTien : "MaCaLamViec"
    TaiKhoanQuanLy |o--o{ LanChiHoanTien : "MaQuanLyXacMinh"
    ThanhToan |o--o{ GiaoDichNganHang : "MaThanhToan"
    LanChiHoanTien |o--o{ GiaoDichNganHang : "MaLanChiHoan"
    DonHang ||--o{ HoaDon : "MaDonHang"
    HoaDon |o--o{ HoaDon : "MaHoaDonGoc"
    HuyMon |o--o{ HoaDon : "MaHuyMon"
    TaiKhoanQuanLy |o--o{ HoaDon : "MaQuanLyPhatHanh"
    CaLamViec |o--o{ HoaDon : "MaCaLamViec"
    PhieuKho |o--o{ PhieuKho : "MaPhieuBiDao"
    TaiKhoanQuanLy ||--o{ PhieuKho : "MaQuanLyLap"
    TaiKhoanQuanLy |o--o{ PhieuKho : "MaQuanLyDuyet"
    PhieuKho ||--o{ ChiTietPhieuKho : "MaPhieuKho"
    NguyenLieu ||--o{ ChiTietPhieuKho : "MaNguyenLieu"
    DonViTinh ||--o{ ChiTietPhieuKho : "MaDonViNhap"
    NguyenLieu ||--o| TonKho : "MaNguyenLieu"
    NguyenLieu ||--o{ BienDongKho : "MaNguyenLieu"
    ChiTietPhieuKho |o--o{ BienDongKho : "MaDongPhieuKho"
    ChiTietDonHang |o--o{ BienDongKho : "MaChiTietDonHang"
    BienDongKho |o--o{ BienDongKho : "MaBienDongBiDao"
    TaiKhoanKhachHang |o--o{ NhatKyHeThong : "MaKhachHang"
    TaiKhoanQuanLy |o--o{ NhatKyHeThong : "MaQuanLy"
    CaLamViec |o--o{ NhatKyHeThong : "MaCaLamViec"
    NguyenLieuChiTietDonHang ||--o| GiuChoNguyenLieu : "MaChiTietDonHang,MaNguyenLieu"
```
