-- SQL Server 2022. Chỉ 34 bảng đã chốt. Không chạy schema 42 bảng cũ.

-- Khởi tạo qua ChuanBi-Database.ps1; không dùng EnsureCreated/Migrate để tránh bảng phụ.

SET XACT_ABORT ON;

BEGIN TRANSACTION;

CREATE TABLE dbo.[TaiKhoanKhachHang] (
    [MaKhachHang] bigint IDENTITY(1,1) NOT NULL,
    [TenDangNhapChuanHoa] nvarchar(100) NOT NULL,
    [MatKhauBam] nvarchar(512) NOT NULL,
    [TrangThai] varchar(15) NOT NULL,
    [BatBuocDoiMatKhau] bit NOT NULL,
    [SoLanDangNhapSai] int NOT NULL,
    [KhoaDenLuc] datetime2(3) NULL,
    [DangNhapLanCuoiLuc] datetime2(3) NULL,
    [HoTen] nvarchar(120) NOT NULL,
    [EmailChuanHoa] nvarchar(254) NULL,
    [SoDienThoaiChuanHoa] varchar(20) NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_TaiKhoanKhachHang] PRIMARY KEY ([MaKhachHang]),
    CONSTRAINT [UQ_TaiKhoanKhachHang_1] UNIQUE ([TenDangNhapChuanHoa]),
    CONSTRAINT [CK_TaiKhoanKhachHang_1] CHECK (TrangThai IN ('ACTIVE','LOCKED','INACTIVE')),
    CONSTRAINT [CK_TaiKhoanKhachHang_2] CHECK (SoLanDangNhapSai >= 0),
    CONSTRAINT [CK_TaiKhoanKhachHang_3] CHECK (EmailChuanHoa IS NOT NULL OR SoDienThoaiChuanHoa IS NOT NULL),
    CONSTRAINT [CK_TaiKhoanKhachHang_4] CHECK (LEN(LTRIM(RTRIM(HoTen))) > 0)
);

CREATE UNIQUE INDEX [UX_KhachHang_Email] ON dbo.[TaiKhoanKhachHang] ([EmailChuanHoa]) WHERE EmailChuanHoa IS NOT NULL;

CREATE UNIQUE INDEX [UX_KhachHang_DienThoai] ON dbo.[TaiKhoanKhachHang] ([SoDienThoaiChuanHoa]) WHERE SoDienThoaiChuanHoa IS NOT NULL;

CREATE TABLE dbo.[TaiKhoanQuanLy] (
    [MaQuanLy] bigint IDENTITY(1,1) NOT NULL,
    [TenDangNhapChuanHoa] nvarchar(100) NOT NULL,
    [MatKhauBam] nvarchar(512) NOT NULL,
    [TrangThai] varchar(15) NOT NULL,
    [BatBuocDoiMatKhau] bit NOT NULL,
    [SoLanDangNhapSai] int NOT NULL,
    [KhoaDenLuc] datetime2(3) NULL,
    [DangNhapLanCuoiLuc] datetime2(3) NULL,
    [HoTen] nvarchar(120) NOT NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_TaiKhoanQuanLy] PRIMARY KEY ([MaQuanLy]),
    CONSTRAINT [UQ_TaiKhoanQuanLy_1] UNIQUE ([TenDangNhapChuanHoa]),
    CONSTRAINT [CK_TaiKhoanQuanLy_1] CHECK (MaQuanLy = 1),
    CONSTRAINT [CK_TaiKhoanQuanLy_2] CHECK (TrangThai IN ('ACTIVE','LOCKED','INACTIVE')),
    CONSTRAINT [CK_TaiKhoanQuanLy_3] CHECK (SoLanDangNhapSai >= 0)
);

CREATE TABLE dbo.[PhienDangNhap] (
    [MaPhienDangNhap] bigint IDENTITY(1,1) NOT NULL,
    [MaKhachHang] bigint NULL,
    [MaQuanLy] bigint NULL,
    [MaLamMoiDaBam] binary(32) NOT NULL,
    [PhamViPhien] varchar(20) NOT NULL,
    [LoaiUngDung] varchar(15) NOT NULL,
    [ThongTinThietBi] nvarchar(300) NULL,
    [HetHanLuc] datetime2(3) NOT NULL,
    [HoatDongLanCuoiLuc] datetime2(3) NULL,
    [ThuHoiLuc] datetime2(3) NULL,
    [LyDoThuHoi] nvarchar(250) NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_PhienDangNhap] PRIMARY KEY ([MaPhienDangNhap]),
    CONSTRAINT [UQ_PhienDangNhap_1] UNIQUE ([MaLamMoiDaBam]),
    CONSTRAINT [CK_PhienDangNhap_1] CHECK ((MaKhachHang IS NOT NULL AND MaQuanLy IS NULL) OR (MaKhachHang IS NULL AND MaQuanLy IS NOT NULL)),
    CONSTRAINT [CK_PhienDangNhap_2] CHECK (HetHanLuc > ThoiDiemTao),
    CONSTRAINT [CK_PhienDangNhap_3] CHECK (PhamViPhien IN ('FULL','PASSWORD_CHANGE'))
);

CREATE TABLE dbo.[DiaChiKhachHang] (
    [MaDiaChi] bigint IDENTITY(1,1) NOT NULL,
    [MaKhachHang] bigint NOT NULL,
    [NhanDiaChi] nvarchar(50) NULL,
    [TenNguoiNhan] nvarchar(120) NOT NULL,
    [DienThoaiNguoiNhan] varchar(20) NOT NULL,
    [DiaChiChiTiet] nvarchar(500) NOT NULL,
    [ViDo] decimal(9,6) NULL,
    [KinhDo] decimal(9,6) NULL,
    [GhiChuGiaoHang] nvarchar(500) NULL,
    [LaDiaChiMacDinh] bit NOT NULL,
    [DangSuDung] bit NOT NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_DiaChiKhachHang] PRIMARY KEY ([MaDiaChi]),
    CONSTRAINT [CK_DiaChiKhachHang_1] CHECK (ViDo IS NULL OR ViDo BETWEEN -90 AND 90),
    CONSTRAINT [CK_DiaChiKhachHang_2] CHECK (KinhDo IS NULL OR KinhDo BETWEEN -180 AND 180),
    CONSTRAINT [CK_DiaChiKhachHang_3] CHECK ((ViDo IS NULL AND KinhDo IS NULL) OR (ViDo IS NOT NULL AND KinhDo IS NOT NULL))
);

CREATE UNIQUE INDEX [UX_DiaChi_MacDinh] ON dbo.[DiaChiKhachHang] ([MaKhachHang]) WHERE LaDiaChiMacDinh = 1 AND DangSuDung = 1;

CREATE TABLE dbo.[NhanVien] (
    [MaNhanVien] bigint IDENTITY(1,1) NOT NULL,
    [HoTen] nvarchar(120) NOT NULL,
    [SoDienThoai] varchar(20) NULL,
    [NgayVaoLam] datetime2(3) NOT NULL,
    [TrangThai] varchar(15) NOT NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_NhanVien] PRIMARY KEY ([MaNhanVien]),
    CONSTRAINT [CK_NhanVien_1] CHECK (TrangThai IN ('ACTIVE','INACTIVE')),
    CONSTRAINT [CK_NhanVien_2] CHECK (LEN(LTRIM(RTRIM(HoTen))) > 0)
);

CREATE TABLE dbo.[CaLamViec] (
    [MaCaLamViec] bigint IDENTITY(1,1) NOT NULL,
    [MaCaHienThi] varchar(30) NOT NULL,
    [MaNhanVien] bigint NOT NULL,
    [TenCa] nvarchar(20) NOT NULL,
    [MaMay] varchar(50) NOT NULL,
    [BatDauLuc] datetime2(3) NOT NULL,
    [KetThucLuc] datetime2(3) NULL,
    [TrangThai] varchar(20) NOT NULL,
    [TienMatDauCa] decimal(18,0) NOT NULL,
    [TienMatDuKienCuoiCa] decimal(18,0) NULL,
    [TienMatThucDem] decimal(18,0) NULL,
    [LyDoChenhLech] nvarchar(500) NULL,
    [MaCaBanGiaoTruoc] bigint NULL,
    [MaQuanLyDuyet] bigint NULL,
    [GhiChu] nvarchar(500) NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_CaLamViec] PRIMARY KEY ([MaCaLamViec]),
    CONSTRAINT [UQ_CaLamViec_1] UNIQUE ([MaCaHienThi]),
    CONSTRAINT [CK_CaLamViec_1] CHECK (TenCa IN (N'Sáng',N'Chiều',N'Tối')),
    CONSTRAINT [CK_CaLamViec_2] CHECK (TrangThai IN ('OPEN','LOCKED','HANDED_OVER','CLOSED')),
    CONSTRAINT [CK_CaLamViec_3] CHECK (KetThucLuc IS NULL OR KetThucLuc >= BatDauLuc),
    CONSTRAINT [CK_CaLamViec_4] CHECK (TienMatDauCa >= 0 AND (TienMatDuKienCuoiCa IS NULL OR TienMatDuKienCuoiCa >= 0) AND (TienMatThucDem IS NULL OR TienMatThucDem >= 0)),
    CONSTRAINT [CK_CaLamViec_5] CHECK ((TrangThai IN ('OPEN','LOCKED') AND KetThucLuc IS NULL) OR (TrangThai IN ('CLOSED','HANDED_OVER') AND KetThucLuc IS NOT NULL)),
    CONSTRAINT [CK_CaLamViec_6] CHECK (TrangThai <> 'CLOSED' OR (TienMatDuKienCuoiCa IS NOT NULL AND TienMatThucDem IS NOT NULL))
);

CREATE UNIQUE INDEX [UX_Ca_MayDangMo] ON dbo.[CaLamViec] ([MaMay]) WHERE KetThucLuc IS NULL;

CREATE UNIQUE INDEX [UX_Ca_NhanVienDangMo] ON dbo.[CaLamViec] ([MaNhanVien]) WHERE KetThucLuc IS NULL;

CREATE TABLE dbo.[DanhMucMon] (
    [MaDanhMuc] bigint IDENTITY(1,1) NOT NULL,
    [TenDanhMuc] nvarchar(100) NOT NULL,
    [MoTa] nvarchar(500) NULL,
    [ThuTuHienThi] int NOT NULL,
    [DaCongBo] bit NOT NULL,
    [DangSuDung] bit NOT NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_DanhMucMon] PRIMARY KEY ([MaDanhMuc]),
    CONSTRAINT [UQ_DanhMucMon_1] UNIQUE ([TenDanhMuc])
);

CREATE TABLE dbo.[Mon] (
    [MaMon] bigint IDENTITY(1,1) NOT NULL,
    [MaDanhMuc] bigint NOT NULL,
    [TenMon] nvarchar(150) NOT NULL,
    [MoTa] nvarchar(1000) NULL,
    [DuongDanAnh] nvarchar(500) NULL,
    [ThuTuHienThi] int NOT NULL,
    [DaCongBo] bit NOT NULL,
    [DangBan] bit NOT NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_Mon] PRIMARY KEY ([MaMon]),
    CONSTRAINT [UQ_Mon_1] UNIQUE ([MaDanhMuc],[TenMon])
);

CREATE TABLE dbo.[CauHinhMon] (
    [MaCauHinhMon] bigint IDENTITY(1,1) NOT NULL,
    [MaMon] bigint NOT NULL,
    [KichCo] varchar(5) NOT NULL,
    [NhietDo] varchar(10) NOT NULL,
    [GiaBan] decimal(18,0) NOT NULL,
    [DangBan] bit NOT NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_CauHinhMon] PRIMARY KEY ([MaCauHinhMon]),
    CONSTRAINT [UQ_CauHinhMon_1] UNIQUE ([MaMon],[KichCo],[NhietDo]),
    CONSTRAINT [CK_CauHinhMon_1] CHECK (KichCo IN ('S','M','L')),
    CONSTRAINT [CK_CauHinhMon_2] CHECK (NhietDo IN ('HOT','ICED')),
    CONSTRAINT [CK_CauHinhMon_3] CHECK (GiaBan >= 0)
);

CREATE TABLE dbo.[MonThem] (
    [MaMonThem] bigint IDENTITY(1,1) NOT NULL,
    [TenMonThem] nvarchar(100) NOT NULL,
    [MoTa] nvarchar(500) NULL,
    [GiaMoiPhan] decimal(18,0) NOT NULL,
    [GioiHanMacDinhMoiLy] int NOT NULL,
    [DuongDanAnh] nvarchar(500) NULL,
    [DangSuDung] bit NOT NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_MonThem] PRIMARY KEY ([MaMonThem]),
    CONSTRAINT [UQ_MonThem_1] UNIQUE ([TenMonThem]),
    CONSTRAINT [CK_MonThem_1] CHECK (GiaMoiPhan >= 0),
    CONSTRAINT [CK_MonThem_2] CHECK (GioiHanMacDinhMoiLy > 0)
);

CREATE TABLE dbo.[CauHinhMonThem] (
    [MaCauHinhMon] bigint NOT NULL,
    [MaMonThem] bigint NOT NULL,
    [GioiHanMoiLy] int NOT NULL,
    [DangSuDung] bit NOT NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_CauHinhMonThem] PRIMARY KEY ([MaCauHinhMon],[MaMonThem]),
    CONSTRAINT [CK_CauHinhMonThem_1] CHECK (GioiHanMoiLy > 0)
);

CREATE TABLE dbo.[TuyChonPhaCheHopLe] (
    [MaCauHinhMon] bigint NOT NULL,
    [PhanTramDuong] int NOT NULL,
    [PhanTramDa] int NOT NULL,
    [DangSuDung] bit NOT NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_TuyChonPhaCheHopLe] PRIMARY KEY ([MaCauHinhMon],[PhanTramDuong],[PhanTramDa]),
    CONSTRAINT [CK_TuyChonPhaCheHopLe_1] CHECK (PhanTramDuong IN (0,25,50,75,100)),
    CONSTRAINT [CK_TuyChonPhaCheHopLe_2] CHECK (PhanTramDa IN (0,25,50,75,100))
);

CREATE TABLE dbo.[NguyenLieu] (
    [MaNguyenLieu] bigint IDENTITY(1,1) NOT NULL,
    [TenNguyenLieu] nvarchar(150) NOT NULL,
    [MaDonViCoSo] bigint NOT NULL,
    [TonAnToan] decimal(18,4) NOT NULL,
    [NguongCanhBao] decimal(18,4) NOT NULL,
    [MoTa] nvarchar(500) NULL,
    [DangSuDung] bit NOT NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_NguyenLieu] PRIMARY KEY ([MaNguyenLieu]),
    CONSTRAINT [UQ_NguyenLieu_1] UNIQUE ([TenNguyenLieu]),
    CONSTRAINT [CK_NguyenLieu_1] CHECK (TonAnToan >= 0 AND NguongCanhBao >= TonAnToan)
);

CREATE TABLE dbo.[DonViTinh] (
    [MaDonVi] bigint IDENTITY(1,1) NOT NULL,
    [KyHieu] varchar(20) NOT NULL,
    [TenDonVi] nvarchar(60) NOT NULL,
    [DangSuDung] bit NOT NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_DonViTinh] PRIMARY KEY ([MaDonVi]),
    CONSTRAINT [UQ_DonViTinh_1] UNIQUE ([KyHieu])
);

CREATE TABLE dbo.[QuyDoiDonViNguyenLieu] (
    [MaNguyenLieu] bigint NOT NULL,
    [MaDonVi] bigint NOT NULL,
    [HeSoVeDonViCoSo] decimal(18,6) NOT NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_QuyDoiDonViNguyenLieu] PRIMARY KEY ([MaNguyenLieu],[MaDonVi]),
    CONSTRAINT [CK_QuyDoiDonViNguyenLieu_1] CHECK (HeSoVeDonViCoSo > 0)
);

CREATE TABLE dbo.[CongThuc] (
    [MaCongThuc] bigint IDENTITY(1,1) NOT NULL,
    [MaCauHinhMon] bigint NULL,
    [MaMonThem] bigint NULL,
    [SoPhienBan] int NOT NULL,
    [TenCongThuc] nvarchar(150) NOT NULL,
    [TrangThai] varchar(15) NOT NULL,
    [HieuLucTu] datetime2(3) NULL,
    [HieuLucDen] datetime2(3) NULL,
    [MaQuanLyTao] bigint NOT NULL,
    [MaQuanLyDuyet] bigint NULL,
    [GhiChu] nvarchar(500) NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_CongThuc] PRIMARY KEY ([MaCongThuc]),
    CONSTRAINT [CK_CongThuc_1] CHECK ((MaCauHinhMon IS NOT NULL AND MaMonThem IS NULL) OR (MaCauHinhMon IS NULL AND MaMonThem IS NOT NULL)),
    CONSTRAINT [CK_CongThuc_2] CHECK (SoPhienBan > 0),
    CONSTRAINT [CK_CongThuc_3] CHECK (HieuLucDen IS NULL OR (HieuLucTu IS NOT NULL AND HieuLucDen > HieuLucTu)),
    CONSTRAINT [CK_CongThuc_4] CHECK (TrangThai IN ('DRAFT','SCHEDULED','ACTIVE','RETIRED')),
    CONSTRAINT [CK_CongThuc_5] CHECK (TrangThai <> 'ACTIVE' OR (HieuLucTu IS NOT NULL AND MaQuanLyDuyet IS NOT NULL))
);

CREATE UNIQUE INDEX [UX_CongThuc_CauHinhPhienBan] ON dbo.[CongThuc] ([MaCauHinhMon],[SoPhienBan]) WHERE MaCauHinhMon IS NOT NULL;

CREATE UNIQUE INDEX [UX_CongThuc_MonThemPhienBan] ON dbo.[CongThuc] ([MaMonThem],[SoPhienBan]) WHERE MaMonThem IS NOT NULL;

CREATE UNIQUE INDEX [UX_CongThuc_CauHinhHieuLuc] ON dbo.[CongThuc] ([MaCauHinhMon]) WHERE MaCauHinhMon IS NOT NULL AND TrangThai = 'ACTIVE';

CREATE UNIQUE INDEX [UX_CongThuc_MonThemHieuLuc] ON dbo.[CongThuc] ([MaMonThem]) WHERE MaMonThem IS NOT NULL AND TrangThai = 'ACTIVE';

CREATE TABLE dbo.[ChiTietCongThuc] (
    [MaCongThuc] bigint NOT NULL,
    [MaNguyenLieu] bigint NOT NULL,
    [LoaiDinhMuc] varchar(10) NOT NULL,
    [DinhMucCoSo] decimal(18,4) NOT NULL,
    [TyLeHaoHut] decimal(9,6) NOT NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_ChiTietCongThuc] PRIMARY KEY ([MaCongThuc],[MaNguyenLieu],[LoaiDinhMuc]),
    CONSTRAINT [CK_ChiTietCongThuc_1] CHECK (LoaiDinhMuc IN ('FIXED','SUGAR','ICE')),
    CONSTRAINT [CK_ChiTietCongThuc_2] CHECK (DinhMucCoSo > 0),
    CONSTRAINT [CK_ChiTietCongThuc_3] CHECK (TyLeHaoHut >= 0 AND TyLeHaoHut < 1)
);

CREATE TABLE dbo.[Ban] (
    [MaBan] bigint IDENTITY(1,1) NOT NULL,
    [MaBanHienThi] varchar(20) NOT NULL,
    [TenBan] nvarchar(100) NOT NULL,
    [TenKhuVuc] nvarchar(100) NULL,
    [SoChoNgoi] int NULL,
    [MaBamQR] binary(32) NOT NULL,
    [PhienBanQR] int NOT NULL,
    [QRHetHanLuc] datetime2(3) NULL,
    [DangSuDung] bit NOT NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_Ban] PRIMARY KEY ([MaBan]),
    CONSTRAINT [UQ_Ban_1] UNIQUE ([MaBanHienThi]),
    CONSTRAINT [UQ_Ban_2] UNIQUE ([MaBamQR]),
    CONSTRAINT [CK_Ban_1] CHECK (SoChoNgoi IS NULL OR SoChoNgoi > 0),
    CONSTRAINT [CK_Ban_2] CHECK (PhienBanQR > 0)
);

CREATE TABLE dbo.[DonHang] (
    [MaDonHang] bigint IDENTITY(1,1) NOT NULL,
    [MaDonHienThi] varchar(30) NOT NULL,
    [MaKhachHang] bigint NULL,
    [MaCaLamViec] bigint NULL,
    [MaDonLienQuan] bigint NULL,
    [KenhDatHang] varchar(10) NOT NULL,
    [HinhThucNhanMon] varchar(15) NOT NULL,
    [MaBan] bigint NULL,
    [MaBanDaChot] varchar(20) NULL,
    [TenNguoiNhan] nvarchar(120) NULL,
    [DienThoaiNguoiNhan] varchar(20) NULL,
    [DiaChiGiaoDaChot] nvarchar(500) NULL,
    [ViDo] decimal(9,6) NULL,
    [KinhDo] decimal(9,6) NULL,
    [ThoiDiemHenNhan] datetime2(3) NULL,
    [KhoangCachGiaoKm] decimal(10,3) NULL,
    [ChinhSachGiaoHangDaChotJson] nvarchar(max) NULL,
    [HanBaoPhi] datetime2(3) NULL,
    [PhiGiaoHangBanDau] decimal(18,0) NOT NULL,
    [PhiGiaoHangHienTai] decimal(18,0) NOT NULL,
    [TrangThai] varchar(25) NOT NULL,
    [GhiChu] nvarchar(500) NULL,
    [HetHanLuc] datetime2(3) NULL,
    [TiepNhanLuc] datetime2(3) NULL,
    [HoanTatLuc] datetime2(3) NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_DonHang] PRIMARY KEY ([MaDonHang]),
    CONSTRAINT [UQ_DonHang_1] UNIQUE ([MaDonHienThi]),
    CONSTRAINT [CK_DonHang_1] CHECK (KenhDatHang IN ('WEB','POS')),
    CONSTRAINT [CK_DonHang_2] CHECK (HinhThucNhanMon IN ('DINE_IN','TAKEAWAY','DELIVERY')),
    CONSTRAINT [CK_DonHang_3] CHECK ((KenhDatHang = 'WEB' AND MaKhachHang IS NOT NULL) OR (KenhDatHang = 'POS' AND MaCaLamViec IS NOT NULL)),
    CONSTRAINT [CK_DonHang_4] CHECK ((HinhThucNhanMon = 'DINE_IN' AND MaBan IS NOT NULL) OR (HinhThucNhanMon <> 'DINE_IN' AND MaBan IS NULL)),
    CONSTRAINT [CK_DonHang_5] CHECK (HinhThucNhanMon <> 'DELIVERY' OR (TenNguoiNhan IS NOT NULL AND DienThoaiNguoiNhan IS NOT NULL AND DiaChiGiaoDaChot IS NOT NULL)),
    CONSTRAINT [CK_DonHang_6] CHECK (PhiGiaoHangBanDau >= 0 AND PhiGiaoHangHienTai >= 0 AND PhiGiaoHangHienTai <= PhiGiaoHangBanDau),
    CONSTRAINT [CK_DonHang_7] CHECK (TrangThai IN ('DRAFT','PENDING_PAYMENT','PENDING_CONFIRMATION','ACCEPTED','PREPARING','READY','DELIVERING','COMPLETED','REJECTED','CANCELLED','DELIVERY_FAILED')),
    CONSTRAINT [CK_DonHang_8] CHECK (ChinhSachGiaoHangDaChotJson IS NULL OR ISJSON(ChinhSachGiaoHangDaChotJson) = 1),
    CONSTRAINT [CK_DonHang_9] CHECK (ViDo IS NULL OR ViDo BETWEEN -90 AND 90),
    CONSTRAINT [CK_DonHang_10] CHECK (KinhDo IS NULL OR KinhDo BETWEEN -180 AND 180),
    CONSTRAINT [CK_DonHang_11] CHECK (KhoangCachGiaoKm IS NULL OR KhoangCachGiaoKm >= 0)
);

CREATE TABLE dbo.[ChiTietDonHang] (
    [MaChiTietDonHang] bigint IDENTITY(1,1) NOT NULL,
    [MaDonHang] bigint NOT NULL,
    [MaCauHinhMon] bigint NOT NULL,
    [MaCongThuc] bigint NOT NULL,
    [TenMonDaChot] nvarchar(150) NOT NULL,
    [KichCoDaChot] varchar(5) NOT NULL,
    [NhietDoDaChot] varchar(10) NOT NULL,
    [PhanTramDuong] int NOT NULL,
    [PhanTramDa] int NOT NULL,
    [GiaMonDaChot] decimal(18,0) NOT NULL,
    [SoLuongDat] int NOT NULL,
    [SoLuongBatDauPha] int NOT NULL,
    [SoLuongPhaXong] int NOT NULL,
    [SoLuongDaHuy] int NOT NULL,
    [GhiChu] nvarchar(500) NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_ChiTietDonHang] PRIMARY KEY ([MaChiTietDonHang]),
    CONSTRAINT [CK_ChiTietDonHang_1] CHECK (GiaMonDaChot >= 0),
    CONSTRAINT [CK_ChiTietDonHang_2] CHECK (SoLuongDat > 0 AND SoLuongBatDauPha >= 0 AND SoLuongPhaXong >= 0 AND SoLuongDaHuy >= 0),
    CONSTRAINT [CK_ChiTietDonHang_3] CHECK (SoLuongBatDauPha + SoLuongDaHuy <= SoLuongDat),
    CONSTRAINT [CK_ChiTietDonHang_4] CHECK (SoLuongPhaXong <= SoLuongBatDauPha),
    CONSTRAINT [CK_ChiTietDonHang_5] CHECK (PhanTramDuong IN (0,25,50,75,100) AND PhanTramDa IN (0,25,50,75,100))
);

CREATE TABLE dbo.[MonThemChiTietDonHang] (
    [MaChiTietDonHang] bigint NOT NULL,
    [MaMonThem] bigint NOT NULL,
    [MaCongThuc] bigint NOT NULL,
    [TenMonThemDaChot] nvarchar(100) NOT NULL,
    [SoPhanMoiLy] int NOT NULL,
    [GiaMoiPhanDaChot] decimal(18,0) NOT NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_MonThemChiTietDonHang] PRIMARY KEY ([MaChiTietDonHang],[MaMonThem]),
    CONSTRAINT [CK_MonThemChiTietDonHang_1] CHECK (SoPhanMoiLy > 0),
    CONSTRAINT [CK_MonThemChiTietDonHang_2] CHECK (GiaMoiPhanDaChot >= 0)
);

CREATE TABLE dbo.[NguyenLieuChiTietDonHang] (
    [MaChiTietDonHang] bigint NOT NULL,
    [MaNguyenLieu] bigint NOT NULL,
    [TenNguyenLieuDaChot] nvarchar(150) NOT NULL,
    [DonViCoSoDaChot] nvarchar(60) NOT NULL,
    [DinhMucMoiLy] decimal(18,4) NOT NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_NguyenLieuChiTietDonHang] PRIMARY KEY ([MaChiTietDonHang],[MaNguyenLieu]),
    CONSTRAINT [CK_NguyenLieuChiTietDonHang_1] CHECK (DinhMucMoiLy > 0)
);

CREATE TABLE dbo.[HuyMon] (
    [MaHuyMon] bigint IDENTITY(1,1) NOT NULL,
    [MaNhomHuy] uniqueidentifier NOT NULL,
    [MaChiTietDonHang] bigint NOT NULL,
    [SoLuongHuy] int NOT NULL,
    [LyDoHuy] nvarchar(500) NOT NULL,
    [MaKhachHangYeuCau] bigint NULL,
    [MaCaLamViec] bigint NULL,
    [MaQuanLyYeuCau] bigint NULL,
    [MaQuanLyDuyet] bigint NULL,
    [TrangThai] varchar(20) NOT NULL,
    [TienHangGiam] decimal(18,0) NOT NULL,
    [PhiGiaoHangGiam] decimal(18,0) NOT NULL,
    [XuLyLuc] datetime2(3) NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_HuyMon] PRIMARY KEY ([MaHuyMon]),
    CONSTRAINT [CK_HuyMon_1] CHECK (SoLuongHuy > 0),
    CONSTRAINT [CK_HuyMon_2] CHECK (LEN(LTRIM(RTRIM(LyDoHuy))) > 0),
    CONSTRAINT [CK_HuyMon_3] CHECK ((CASE WHEN MaKhachHangYeuCau IS NULL THEN 0 ELSE 1 END + CASE WHEN MaCaLamViec IS NULL THEN 0 ELSE 1 END + CASE WHEN MaQuanLyYeuCau IS NULL THEN 0 ELSE 1 END) = 1),
    CONSTRAINT [CK_HuyMon_4] CHECK (TrangThai IN ('REQUESTED','ACCEPTED','REJECTED')),
    CONSTRAINT [CK_HuyMon_5] CHECK (TienHangGiam >= 0 AND PhiGiaoHangGiam >= 0)
);

CREATE TABLE dbo.[ThanhToan] (
    [MaThanhToan] bigint IDENTITY(1,1) NOT NULL,
    [MaThanhToanHienThi] varchar(40) NOT NULL,
    [MaDonHang] bigint NOT NULL,
    [MaCaLamViec] bigint NULL,
    [PhuongThuc] varchar(15) NOT NULL,
    [SoTienYeuCau] decimal(18,0) NOT NULL,
    [SoTienDaXacMinh] decimal(18,0) NOT NULL,
    [TienKhachDua] decimal(18,0) NULL,
    [MaThamChieuChuyenKhoan] varchar(100) NULL,
    [NoiDungQR] nvarchar(max) NULL,
    [TrangThai] varchar(20) NOT NULL,
    [HetHanLuc] datetime2(3) NULL,
    [XacMinhLuc] datetime2(3) NULL,
    [MaQuanLyXacMinh] bigint NULL,
    [ThamChieuChungCu] nvarchar(500) NULL,
    [MaThaoTac] uniqueidentifier NOT NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_ThanhToan] PRIMARY KEY ([MaThanhToan]),
    CONSTRAINT [UQ_ThanhToan_1] UNIQUE ([MaThanhToanHienThi]),
    CONSTRAINT [UQ_ThanhToan_2] UNIQUE ([MaThaoTac]),
    CONSTRAINT [CK_ThanhToan_1] CHECK (PhuongThuc IN ('CASH','BANK_TRANSFER')),
    CONSTRAINT [CK_ThanhToan_2] CHECK (SoTienYeuCau >= 0 AND SoTienDaXacMinh >= 0),
    CONSTRAINT [CK_ThanhToan_3] CHECK (PhuongThuc <> 'CASH' OR MaCaLamViec IS NOT NULL),
    CONSTRAINT [CK_ThanhToan_4] CHECK (TienKhachDua IS NULL OR TienKhachDua >= SoTienDaXacMinh),
    CONSTRAINT [CK_ThanhToan_5] CHECK (TrangThai IN ('PENDING','PARTIAL','VERIFIED','EXPIRED','FAILED','RECONCILING'))
);

CREATE UNIQUE INDEX [UX_ThanhToan_ThamChieu] ON dbo.[ThanhToan] ([MaThamChieuChuyenKhoan]) WHERE MaThamChieuChuyenKhoan IS NOT NULL;

CREATE TABLE dbo.[GiaoDichNganHang] (
    [MaGiaoDichNganHang] bigint IDENTITY(1,1) NOT NULL,
    [TenNhaCungCap] varchar(50) NOT NULL,
    [MaTaiKhoanNganHangQuan] varchar(100) NOT NULL,
    [MaGiaoDichBenNgoai] varchar(150) NOT NULL,
    [ChieuGiaoDich] varchar(5) NOT NULL,
    [SoTien] decimal(18,0) NOT NULL,
    [NoiDungChuyenKhoan] nvarchar(500) NULL,
    [ThoiDiemNganHang] datetime2(3) NOT NULL,
    [TrangThaiXacMinh] varchar(20) NOT NULL,
    [XacMinhLuc] datetime2(3) NULL,
    [MaThanhToan] bigint NULL,
    [MaLanChiHoan] bigint NULL,
    [MaBamNoiDung] binary(32) NULL,
    [ThamChieuChungCu] nvarchar(500) NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_GiaoDichNganHang] PRIMARY KEY ([MaGiaoDichNganHang]),
    CONSTRAINT [UQ_GiaoDichNganHang_1] UNIQUE ([TenNhaCungCap],[MaTaiKhoanNganHangQuan],[MaGiaoDichBenNgoai]),
    CONSTRAINT [CK_GiaoDichNganHang_1] CHECK (SoTien > 0),
    CONSTRAINT [CK_GiaoDichNganHang_2] CHECK (ChieuGiaoDich IN ('IN','OUT')),
    CONSTRAINT [CK_GiaoDichNganHang_3] CHECK (MaThanhToan IS NULL OR MaLanChiHoan IS NULL),
    CONSTRAINT [CK_GiaoDichNganHang_4] CHECK (MaThanhToan IS NULL OR ChieuGiaoDich = 'IN'),
    CONSTRAINT [CK_GiaoDichNganHang_5] CHECK (MaLanChiHoan IS NULL OR ChieuGiaoDich = 'OUT')
);

CREATE TABLE dbo.[HoSoHoanTien] (
    [MaHoanTien] bigint IDENTITY(1,1) NOT NULL,
    [MaHoanTienHienThi] varchar(40) NOT NULL,
    [MaDonHang] bigint NOT NULL,
    [MaThanhToan] bigint NOT NULL,
    [MaHuyMon] bigint NULL,
    [LyDoHoan] nvarchar(500) NOT NULL,
    [SoTienYeuCau] decimal(18,0) NOT NULL,
    [SoTienDuocDuyet] decimal(18,0) NULL,
    [PhuongThucHoan] varchar(15) NOT NULL,
    [TenNguoiNhan] nvarchar(120) NULL,
    [NganHangNguoiNhan] nvarchar(100) NULL,
    [TaiKhoanNhanMaHoa] varbinary(max) NULL,
    [TaiKhoanNhanDaChe] nvarchar(50) NULL,
    [TrangThai] varchar(25) NOT NULL,
    [MaKhachHangYeuCau] bigint NULL,
    [MaCaYeuCau] bigint NULL,
    [MaQuanLyYeuCau] bigint NULL,
    [MaQuanLyDuyet] bigint NULL,
    [DuyetLuc] datetime2(3) NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_HoSoHoanTien] PRIMARY KEY ([MaHoanTien]),
    CONSTRAINT [UQ_HoSoHoanTien_1] UNIQUE ([MaHoanTienHienThi]),
    CONSTRAINT [CK_HoSoHoanTien_1] CHECK (SoTienYeuCau > 0),
    CONSTRAINT [CK_HoSoHoanTien_2] CHECK (SoTienDuocDuyet IS NULL OR (SoTienDuocDuyet > 0 AND SoTienDuocDuyet <= SoTienYeuCau)),
    CONSTRAINT [CK_HoSoHoanTien_3] CHECK (TrangThai IN ('REQUESTED','APPROVED','REJECTED','PROCESSING','PARTIALLY_REFUNDED','REFUNDED','RECONCILING')),
    CONSTRAINT [CK_HoSoHoanTien_4] CHECK ((CASE WHEN MaKhachHangYeuCau IS NULL THEN 0 ELSE 1 END + CASE WHEN MaCaYeuCau IS NULL THEN 0 ELSE 1 END + CASE WHEN MaQuanLyYeuCau IS NULL THEN 0 ELSE 1 END) = 1),
    CONSTRAINT [CK_HoSoHoanTien_5] CHECK (PhuongThucHoan IN ('CASH','BANK_TRANSFER'))
);

CREATE TABLE dbo.[LanChiHoanTien] (
    [MaLanChiHoan] bigint IDENTITY(1,1) NOT NULL,
    [MaHoanTien] bigint NOT NULL,
    [SoThuTuLanChi] int NOT NULL,
    [MaChongChiLap] uniqueidentifier NOT NULL,
    [SoTien] decimal(18,0) NOT NULL,
    [PhuongThuc] varchar(15) NOT NULL,
    [NhaCungCap] varchar(50) NULL,
    [MaThamChieuBenNgoai] varchar(150) NULL,
    [TrangThai] varchar(20) NOT NULL,
    [BatDauLuc] datetime2(3) NOT NULL,
    [CoKetQuaLuc] datetime2(3) NULL,
    [MaCaLamViec] bigint NULL,
    [MaQuanLyXacMinh] bigint NULL,
    [ThamChieuChungCu] nvarchar(500) NULL,
    [LyDoThatBai] nvarchar(500) NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_LanChiHoanTien] PRIMARY KEY ([MaLanChiHoan]),
    CONSTRAINT [UQ_LanChiHoanTien_1] UNIQUE ([MaHoanTien],[SoThuTuLanChi]),
    CONSTRAINT [UQ_LanChiHoanTien_2] UNIQUE ([MaChongChiLap]),
    CONSTRAINT [CK_LanChiHoanTien_1] CHECK (SoThuTuLanChi > 0 AND SoTien > 0),
    CONSTRAINT [CK_LanChiHoanTien_2] CHECK (TrangThai IN ('PENDING','SUCCEEDED','FAILED','RECONCILING')),
    CONSTRAINT [CK_LanChiHoanTien_3] CHECK (PhuongThuc IN ('CASH','BANK_TRANSFER')),
    CONSTRAINT [CK_LanChiHoanTien_4] CHECK (CoKetQuaLuc IS NULL OR CoKetQuaLuc >= BatDauLuc)
);

CREATE TABLE dbo.[HoaDon] (
    [MaHoaDon] bigint IDENTITY(1,1) NOT NULL,
    [SoHoaDon] varchar(40) NOT NULL,
    [MaDonHang] bigint NOT NULL,
    [LoaiHoaDon] varchar(15) NOT NULL,
    [MaHoaDonGoc] bigint NULL,
    [MaHuyMon] bigint NULL,
    [TenNguoiMua] nvarchar(150) NULL,
    [MaSoThueNguoiMua] varchar(30) NULL,
    [SoTien] decimal(18,0) NOT NULL,
    [NoiDungHoaDonDaChotJson] nvarchar(max) NOT NULL,
    [TrangThai] varchar(15) NOT NULL,
    [LyDo] nvarchar(500) NULL,
    [PhatHanhLuc] datetime2(3) NULL,
    [MaQuanLyPhatHanh] bigint NULL,
    [MaCaLamViec] bigint NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_HoaDon] PRIMARY KEY ([MaHoaDon]),
    CONSTRAINT [UQ_HoaDon_1] UNIQUE ([SoHoaDon]),
    CONSTRAINT [CK_HoaDon_1] CHECK (LoaiHoaDon IN ('ORIGINAL','ADJUSTMENT')),
    CONSTRAINT [CK_HoaDon_2] CHECK ((LoaiHoaDon = 'ORIGINAL' AND MaHoaDonGoc IS NULL AND SoTien >= 0) OR (LoaiHoaDon = 'ADJUSTMENT' AND MaHoaDonGoc IS NOT NULL AND MaHoaDonGoc <> MaHoaDon)),
    CONSTRAINT [CK_HoaDon_3] CHECK (TrangThai IN ('DRAFT','ISSUED')),
    CONSTRAINT [CK_HoaDon_4] CHECK (TrangThai <> 'ISSUED' OR PhatHanhLuc IS NOT NULL),
    CONSTRAINT [CK_HoaDon_5] CHECK (ISJSON(NoiDungHoaDonDaChotJson) = 1)
);

CREATE UNIQUE INDEX [UX_HoaDon_Goc] ON dbo.[HoaDon] ([MaDonHang]) WHERE LoaiHoaDon = 'ORIGINAL';

CREATE TABLE dbo.[PhieuKho] (
    [MaPhieuKho] bigint IDENTITY(1,1) NOT NULL,
    [SoPhieuKho] varchar(40) NOT NULL,
    [LoaiPhieu] varchar(20) NOT NULL,
    [TrangThai] varchar(15) NOT NULL,
    [TenNhaCungCapDaChot] nvarchar(200) NULL,
    [MaMucDich] varchar(30) NULL,
    [LyDo] nvarchar(500) NULL,
    [MocKiemKe] datetime2(3) NULL,
    [MaPhieuBiDao] bigint NULL,
    [MaQuanLyLap] bigint NOT NULL,
    [MaQuanLyDuyet] bigint NULL,
    [GhiSoLuc] datetime2(3) NULL,
    [MaThaoTac] uniqueidentifier NOT NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_PhieuKho] PRIMARY KEY ([MaPhieuKho]),
    CONSTRAINT [UQ_PhieuKho_1] UNIQUE ([SoPhieuKho]),
    CONSTRAINT [UQ_PhieuKho_2] UNIQUE ([MaThaoTac]),
    CONSTRAINT [CK_PhieuKho_1] CHECK (LoaiPhieu IN ('RECEIPT','ISSUE','COUNT','ADJUSTMENT','REVERSAL')),
    CONSTRAINT [CK_PhieuKho_2] CHECK (TrangThai IN ('DRAFT','POSTED')),
    CONSTRAINT [CK_PhieuKho_3] CHECK (TrangThai <> 'POSTED' OR (GhiSoLuc IS NOT NULL AND MaQuanLyDuyet IS NOT NULL)),
    CONSTRAINT [CK_PhieuKho_4] CHECK (MaPhieuBiDao IS NULL OR MaPhieuBiDao <> MaPhieuKho)
);

CREATE TABLE dbo.[ChiTietPhieuKho] (
    [MaDongPhieuKho] bigint IDENTITY(1,1) NOT NULL,
    [MaPhieuKho] bigint NOT NULL,
    [MaNguyenLieu] bigint NOT NULL,
    [MaDonViNhap] bigint NOT NULL,
    [DonViNhapDaChot] nvarchar(60) NOT NULL,
    [HeSoQuyDoiDaChot] decimal(18,6) NOT NULL,
    [SoLuongTheoDonViNhap] decimal(18,4) NOT NULL,
    [ChenhLechDonViCoSo] decimal(18,4) NOT NULL,
    [DonGiaNhap] decimal(18,2) NULL,
    [TonSoDaChot] decimal(18,4) NULL,
    [SoLuongThucDem] decimal(18,4) NULL,
    [PhienBanTonKhoDaChot] binary(8) NULL,
    [GhiChu] nvarchar(500) NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_ChiTietPhieuKho] PRIMARY KEY ([MaDongPhieuKho]),
    CONSTRAINT [UQ_ChiTietPhieuKho_1] UNIQUE ([MaPhieuKho],[MaNguyenLieu]),
    CONSTRAINT [CK_ChiTietPhieuKho_1] CHECK (HeSoQuyDoiDaChot > 0),
    CONSTRAINT [CK_ChiTietPhieuKho_2] CHECK (SoLuongTheoDonViNhap >= 0),
    CONSTRAINT [CK_ChiTietPhieuKho_3] CHECK (DonGiaNhap IS NULL OR DonGiaNhap >= 0),
    CONSTRAINT [CK_ChiTietPhieuKho_4] CHECK (TonSoDaChot IS NULL OR TonSoDaChot >= 0),
    CONSTRAINT [CK_ChiTietPhieuKho_5] CHECK (SoLuongThucDem IS NULL OR SoLuongThucDem >= 0)
);

CREATE TABLE dbo.[TonKho] (
    [MaNguyenLieu] bigint NOT NULL,
    [TonThucTe] decimal(18,4) NOT NULL,
    [TongGiuCho] decimal(18,4) NOT NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_TonKho] PRIMARY KEY ([MaNguyenLieu]),
    CONSTRAINT [CK_TonKho_1] CHECK (TonThucTe >= 0 AND TongGiuCho >= 0 AND TongGiuCho <= TonThucTe)
);

CREATE TABLE dbo.[GiuChoNguyenLieu] (
    [MaChiTietDonHang] bigint NOT NULL,
    [MaNguyenLieu] bigint NOT NULL,
    [SoLuongGiuCho] decimal(18,4) NOT NULL,
    [SoLuongDaTieuHao] decimal(18,4) NOT NULL,
    [SoLuongDaGiaiPhong] decimal(18,4) NOT NULL,
    [HetHanLuc] datetime2(3) NULL,
    [TrangThai] varchar(20) NOT NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    [PhienBanDuLieu] rowversion NOT NULL,
    CONSTRAINT [PK_GiuChoNguyenLieu] PRIMARY KEY ([MaChiTietDonHang],[MaNguyenLieu]),
    CONSTRAINT [CK_GiuChoNguyenLieu_1] CHECK (SoLuongGiuCho >= 0 AND SoLuongDaTieuHao >= 0 AND SoLuongDaGiaiPhong >= 0),
    CONSTRAINT [CK_GiuChoNguyenLieu_2] CHECK (SoLuongDaTieuHao + SoLuongDaGiaiPhong <= SoLuongGiuCho),
    CONSTRAINT [CK_GiuChoNguyenLieu_3] CHECK (TrangThai IN ('RESERVED','PARTIAL','CONSUMED','RELEASED','EXPIRED'))
);

CREATE TABLE dbo.[BienDongKho] (
    [MaBienDongKho] bigint IDENTITY(1,1) NOT NULL,
    [MaNguyenLieu] bigint NOT NULL,
    [MaDongPhieuKho] bigint NULL,
    [MaChiTietDonHang] bigint NULL,
    [LoaiBienDong] varchar(25) NOT NULL,
    [SoLuongThayDoi] decimal(18,4) NOT NULL,
    [MaThaoTac] uniqueidentifier NOT NULL,
    [MaBienDongBiDao] bigint NULL,
    [PhatSinhLuc] datetime2(3) NOT NULL,
    [GhiChu] nvarchar(500) NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_BienDongKho] PRIMARY KEY ([MaBienDongKho]),
    CONSTRAINT [UQ_BienDongKho_1] UNIQUE ([MaThaoTac],[MaNguyenLieu]),
    CONSTRAINT [CK_BienDongKho_1] CHECK (SoLuongThayDoi <> 0),
    CONSTRAINT [CK_BienDongKho_2] CHECK ((MaDongPhieuKho IS NOT NULL AND MaChiTietDonHang IS NULL) OR (MaDongPhieuKho IS NULL AND MaChiTietDonHang IS NOT NULL)),
    CONSTRAINT [CK_BienDongKho_3] CHECK (MaBienDongBiDao IS NULL OR MaBienDongBiDao <> MaBienDongKho)
);

CREATE TABLE dbo.[NhatKyHeThong] (
    [MaNhatKy] bigint IDENTITY(1,1) NOT NULL,
    [MaLienKetXuLy] uniqueidentifier NOT NULL,
    [MaThaoTac] uniqueidentifier NULL,
    [LoaiTacNhan] varchar(15) NOT NULL,
    [MaKhachHang] bigint NULL,
    [MaQuanLy] bigint NULL,
    [MaCaLamViec] bigint NULL,
    [TenNguoiThucHienDaChot] nvarchar(120) NOT NULL,
    [MaMayDaChot] varchar(50) NULL,
    [HanhDong] varchar(100) NOT NULL,
    [LoaiDoiTuong] varchar(100) NOT NULL,
    [MaDoiTuong] varchar(100) NULL,
    [DuLieuTruocJson] nvarchar(max) NULL,
    [DuLieuSauJson] nvarchar(max) NULL,
    [LyDo] nvarchar(500) NULL,
    [KetQua] varchar(20) NOT NULL,
    [MaBamYeuCau] binary(32) NULL,
    [KetQuaDaChotJson] nvarchar(max) NULL,
    [PhatSinhLuc] datetime2(3) NOT NULL,
    [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_NhatKyHeThong] PRIMARY KEY ([MaNhatKy]),
    CONSTRAINT [CK_NhatKyHeThong_1] CHECK ((LoaiTacNhan = 'CUSTOMER' AND MaKhachHang IS NOT NULL AND MaQuanLy IS NULL AND MaCaLamViec IS NULL) OR (LoaiTacNhan = 'ADMIN' AND MaQuanLy IS NOT NULL AND MaKhachHang IS NULL AND MaCaLamViec IS NULL) OR (LoaiTacNhan = 'STAFF' AND MaCaLamViec IS NOT NULL AND MaKhachHang IS NULL AND MaQuanLy IS NULL) OR (LoaiTacNhan = 'SYSTEM' AND MaKhachHang IS NULL AND MaQuanLy IS NULL AND MaCaLamViec IS NULL)),
    CONSTRAINT [CK_NhatKyHeThong_2] CHECK (DuLieuTruocJson IS NULL OR ISJSON(DuLieuTruocJson) = 1),
    CONSTRAINT [CK_NhatKyHeThong_3] CHECK (DuLieuSauJson IS NULL OR ISJSON(DuLieuSauJson) = 1),
    CONSTRAINT [CK_NhatKyHeThong_4] CHECK (KetQuaDaChotJson IS NULL OR ISJSON(KetQuaDaChotJson) = 1)
);

CREATE UNIQUE INDEX [UX_NhatKy_ThaoTac] ON dbo.[NhatKyHeThong] ([MaThaoTac]) WHERE MaThaoTac IS NOT NULL;

ALTER TABLE dbo.[PhienDangNhap] ADD CONSTRAINT [FK_PhienDangNhap_1] FOREIGN KEY ([MaKhachHang]) REFERENCES dbo.[TaiKhoanKhachHang] ([MaKhachHang]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_PhienDangNhap_1] ON dbo.[PhienDangNhap] ([MaKhachHang]);

ALTER TABLE dbo.[PhienDangNhap] ADD CONSTRAINT [FK_PhienDangNhap_2] FOREIGN KEY ([MaQuanLy]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_PhienDangNhap_2] ON dbo.[PhienDangNhap] ([MaQuanLy]);

ALTER TABLE dbo.[DiaChiKhachHang] ADD CONSTRAINT [FK_DiaChiKhachHang_3] FOREIGN KEY ([MaKhachHang]) REFERENCES dbo.[TaiKhoanKhachHang] ([MaKhachHang]) ON DELETE NO ACTION;

ALTER TABLE dbo.[CaLamViec] ADD CONSTRAINT [FK_CaLamViec_4] FOREIGN KEY ([MaNhanVien]) REFERENCES dbo.[NhanVien] ([MaNhanVien]) ON DELETE NO ACTION;

ALTER TABLE dbo.[CaLamViec] ADD CONSTRAINT [FK_CaLamViec_5] FOREIGN KEY ([MaCaBanGiaoTruoc]) REFERENCES dbo.[CaLamViec] ([MaCaLamViec]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_CaLamViec_5] ON dbo.[CaLamViec] ([MaCaBanGiaoTruoc]);

ALTER TABLE dbo.[CaLamViec] ADD CONSTRAINT [FK_CaLamViec_6] FOREIGN KEY ([MaQuanLyDuyet]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_CaLamViec_6] ON dbo.[CaLamViec] ([MaQuanLyDuyet]);

ALTER TABLE dbo.[Mon] ADD CONSTRAINT [FK_Mon_7] FOREIGN KEY ([MaDanhMuc]) REFERENCES dbo.[DanhMucMon] ([MaDanhMuc]) ON DELETE NO ACTION;

ALTER TABLE dbo.[CauHinhMon] ADD CONSTRAINT [FK_CauHinhMon_8] FOREIGN KEY ([MaMon]) REFERENCES dbo.[Mon] ([MaMon]) ON DELETE NO ACTION;

ALTER TABLE dbo.[CauHinhMonThem] ADD CONSTRAINT [FK_CauHinhMonThem_9] FOREIGN KEY ([MaCauHinhMon]) REFERENCES dbo.[CauHinhMon] ([MaCauHinhMon]) ON DELETE NO ACTION;

ALTER TABLE dbo.[CauHinhMonThem] ADD CONSTRAINT [FK_CauHinhMonThem_10] FOREIGN KEY ([MaMonThem]) REFERENCES dbo.[MonThem] ([MaMonThem]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_CauHinhMonThem_10] ON dbo.[CauHinhMonThem] ([MaMonThem]);

ALTER TABLE dbo.[TuyChonPhaCheHopLe] ADD CONSTRAINT [FK_TuyChonPhaCheHopLe_11] FOREIGN KEY ([MaCauHinhMon]) REFERENCES dbo.[CauHinhMon] ([MaCauHinhMon]) ON DELETE NO ACTION;

ALTER TABLE dbo.[NguyenLieu] ADD CONSTRAINT [FK_NguyenLieu_12] FOREIGN KEY ([MaDonViCoSo]) REFERENCES dbo.[DonViTinh] ([MaDonVi]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_NguyenLieu_12] ON dbo.[NguyenLieu] ([MaDonViCoSo]);

ALTER TABLE dbo.[QuyDoiDonViNguyenLieu] ADD CONSTRAINT [FK_QuyDoiDonViNguyenLieu_13] FOREIGN KEY ([MaNguyenLieu]) REFERENCES dbo.[NguyenLieu] ([MaNguyenLieu]) ON DELETE NO ACTION;

ALTER TABLE dbo.[QuyDoiDonViNguyenLieu] ADD CONSTRAINT [FK_QuyDoiDonViNguyenLieu_14] FOREIGN KEY ([MaDonVi]) REFERENCES dbo.[DonViTinh] ([MaDonVi]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_QuyDoiDonViNguyenLieu_14] ON dbo.[QuyDoiDonViNguyenLieu] ([MaDonVi]);

ALTER TABLE dbo.[CongThuc] ADD CONSTRAINT [FK_CongThuc_15] FOREIGN KEY ([MaCauHinhMon]) REFERENCES dbo.[CauHinhMon] ([MaCauHinhMon]) ON DELETE NO ACTION;

ALTER TABLE dbo.[CongThuc] ADD CONSTRAINT [FK_CongThuc_16] FOREIGN KEY ([MaMonThem]) REFERENCES dbo.[MonThem] ([MaMonThem]) ON DELETE NO ACTION;

ALTER TABLE dbo.[CongThuc] ADD CONSTRAINT [FK_CongThuc_17] FOREIGN KEY ([MaQuanLyTao]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_CongThuc_17] ON dbo.[CongThuc] ([MaQuanLyTao]);

ALTER TABLE dbo.[CongThuc] ADD CONSTRAINT [FK_CongThuc_18] FOREIGN KEY ([MaQuanLyDuyet]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_CongThuc_18] ON dbo.[CongThuc] ([MaQuanLyDuyet]);

ALTER TABLE dbo.[ChiTietCongThuc] ADD CONSTRAINT [FK_ChiTietCongThuc_19] FOREIGN KEY ([MaCongThuc]) REFERENCES dbo.[CongThuc] ([MaCongThuc]) ON DELETE NO ACTION;

ALTER TABLE dbo.[ChiTietCongThuc] ADD CONSTRAINT [FK_ChiTietCongThuc_20] FOREIGN KEY ([MaNguyenLieu]) REFERENCES dbo.[NguyenLieu] ([MaNguyenLieu]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_ChiTietCongThuc_20] ON dbo.[ChiTietCongThuc] ([MaNguyenLieu]);

ALTER TABLE dbo.[DonHang] ADD CONSTRAINT [FK_DonHang_21] FOREIGN KEY ([MaKhachHang]) REFERENCES dbo.[TaiKhoanKhachHang] ([MaKhachHang]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_DonHang_21] ON dbo.[DonHang] ([MaKhachHang]);

ALTER TABLE dbo.[DonHang] ADD CONSTRAINT [FK_DonHang_22] FOREIGN KEY ([MaCaLamViec]) REFERENCES dbo.[CaLamViec] ([MaCaLamViec]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_DonHang_22] ON dbo.[DonHang] ([MaCaLamViec]);

ALTER TABLE dbo.[DonHang] ADD CONSTRAINT [FK_DonHang_23] FOREIGN KEY ([MaDonLienQuan]) REFERENCES dbo.[DonHang] ([MaDonHang]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_DonHang_23] ON dbo.[DonHang] ([MaDonLienQuan]);

ALTER TABLE dbo.[DonHang] ADD CONSTRAINT [FK_DonHang_24] FOREIGN KEY ([MaBan]) REFERENCES dbo.[Ban] ([MaBan]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_DonHang_24] ON dbo.[DonHang] ([MaBan]);

ALTER TABLE dbo.[ChiTietDonHang] ADD CONSTRAINT [FK_ChiTietDonHang_25] FOREIGN KEY ([MaDonHang]) REFERENCES dbo.[DonHang] ([MaDonHang]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_ChiTietDonHang_25] ON dbo.[ChiTietDonHang] ([MaDonHang]);

ALTER TABLE dbo.[ChiTietDonHang] ADD CONSTRAINT [FK_ChiTietDonHang_26] FOREIGN KEY ([MaCauHinhMon]) REFERENCES dbo.[CauHinhMon] ([MaCauHinhMon]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_ChiTietDonHang_26] ON dbo.[ChiTietDonHang] ([MaCauHinhMon]);

ALTER TABLE dbo.[ChiTietDonHang] ADD CONSTRAINT [FK_ChiTietDonHang_27] FOREIGN KEY ([MaCongThuc]) REFERENCES dbo.[CongThuc] ([MaCongThuc]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_ChiTietDonHang_27] ON dbo.[ChiTietDonHang] ([MaCongThuc]);

ALTER TABLE dbo.[MonThemChiTietDonHang] ADD CONSTRAINT [FK_MonThemChiTietDonHang_28] FOREIGN KEY ([MaChiTietDonHang]) REFERENCES dbo.[ChiTietDonHang] ([MaChiTietDonHang]) ON DELETE NO ACTION;

ALTER TABLE dbo.[MonThemChiTietDonHang] ADD CONSTRAINT [FK_MonThemChiTietDonHang_29] FOREIGN KEY ([MaMonThem]) REFERENCES dbo.[MonThem] ([MaMonThem]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_MonThemChiTietDonHang_29] ON dbo.[MonThemChiTietDonHang] ([MaMonThem]);

ALTER TABLE dbo.[MonThemChiTietDonHang] ADD CONSTRAINT [FK_MonThemChiTietDonHang_30] FOREIGN KEY ([MaCongThuc]) REFERENCES dbo.[CongThuc] ([MaCongThuc]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_MonThemChiTietDonHang_30] ON dbo.[MonThemChiTietDonHang] ([MaCongThuc]);

ALTER TABLE dbo.[NguyenLieuChiTietDonHang] ADD CONSTRAINT [FK_NguyenLieuChiTietDonHang_31] FOREIGN KEY ([MaChiTietDonHang]) REFERENCES dbo.[ChiTietDonHang] ([MaChiTietDonHang]) ON DELETE NO ACTION;

ALTER TABLE dbo.[NguyenLieuChiTietDonHang] ADD CONSTRAINT [FK_NguyenLieuChiTietDonHang_32] FOREIGN KEY ([MaNguyenLieu]) REFERENCES dbo.[NguyenLieu] ([MaNguyenLieu]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_NguyenLieuChiTietDonHang_32] ON dbo.[NguyenLieuChiTietDonHang] ([MaNguyenLieu]);

ALTER TABLE dbo.[HuyMon] ADD CONSTRAINT [FK_HuyMon_33] FOREIGN KEY ([MaChiTietDonHang]) REFERENCES dbo.[ChiTietDonHang] ([MaChiTietDonHang]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_HuyMon_33] ON dbo.[HuyMon] ([MaChiTietDonHang]);

ALTER TABLE dbo.[HuyMon] ADD CONSTRAINT [FK_HuyMon_34] FOREIGN KEY ([MaKhachHangYeuCau]) REFERENCES dbo.[TaiKhoanKhachHang] ([MaKhachHang]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_HuyMon_34] ON dbo.[HuyMon] ([MaKhachHangYeuCau]);

ALTER TABLE dbo.[HuyMon] ADD CONSTRAINT [FK_HuyMon_35] FOREIGN KEY ([MaCaLamViec]) REFERENCES dbo.[CaLamViec] ([MaCaLamViec]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_HuyMon_35] ON dbo.[HuyMon] ([MaCaLamViec]);

ALTER TABLE dbo.[HuyMon] ADD CONSTRAINT [FK_HuyMon_36] FOREIGN KEY ([MaQuanLyYeuCau]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_HuyMon_36] ON dbo.[HuyMon] ([MaQuanLyYeuCau]);

ALTER TABLE dbo.[HuyMon] ADD CONSTRAINT [FK_HuyMon_37] FOREIGN KEY ([MaQuanLyDuyet]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_HuyMon_37] ON dbo.[HuyMon] ([MaQuanLyDuyet]);

ALTER TABLE dbo.[ThanhToan] ADD CONSTRAINT [FK_ThanhToan_38] FOREIGN KEY ([MaDonHang]) REFERENCES dbo.[DonHang] ([MaDonHang]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_ThanhToan_38] ON dbo.[ThanhToan] ([MaDonHang]);

ALTER TABLE dbo.[ThanhToan] ADD CONSTRAINT [FK_ThanhToan_39] FOREIGN KEY ([MaCaLamViec]) REFERENCES dbo.[CaLamViec] ([MaCaLamViec]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_ThanhToan_39] ON dbo.[ThanhToan] ([MaCaLamViec]);

ALTER TABLE dbo.[ThanhToan] ADD CONSTRAINT [FK_ThanhToan_40] FOREIGN KEY ([MaQuanLyXacMinh]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_ThanhToan_40] ON dbo.[ThanhToan] ([MaQuanLyXacMinh]);

ALTER TABLE dbo.[HoSoHoanTien] ADD CONSTRAINT [FK_HoSoHoanTien_41] FOREIGN KEY ([MaDonHang]) REFERENCES dbo.[DonHang] ([MaDonHang]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_HoSoHoanTien_41] ON dbo.[HoSoHoanTien] ([MaDonHang]);

ALTER TABLE dbo.[HoSoHoanTien] ADD CONSTRAINT [FK_HoSoHoanTien_42] FOREIGN KEY ([MaThanhToan]) REFERENCES dbo.[ThanhToan] ([MaThanhToan]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_HoSoHoanTien_42] ON dbo.[HoSoHoanTien] ([MaThanhToan]);

ALTER TABLE dbo.[HoSoHoanTien] ADD CONSTRAINT [FK_HoSoHoanTien_43] FOREIGN KEY ([MaHuyMon]) REFERENCES dbo.[HuyMon] ([MaHuyMon]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_HoSoHoanTien_43] ON dbo.[HoSoHoanTien] ([MaHuyMon]);

ALTER TABLE dbo.[HoSoHoanTien] ADD CONSTRAINT [FK_HoSoHoanTien_44] FOREIGN KEY ([MaKhachHangYeuCau]) REFERENCES dbo.[TaiKhoanKhachHang] ([MaKhachHang]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_HoSoHoanTien_44] ON dbo.[HoSoHoanTien] ([MaKhachHangYeuCau]);

ALTER TABLE dbo.[HoSoHoanTien] ADD CONSTRAINT [FK_HoSoHoanTien_45] FOREIGN KEY ([MaCaYeuCau]) REFERENCES dbo.[CaLamViec] ([MaCaLamViec]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_HoSoHoanTien_45] ON dbo.[HoSoHoanTien] ([MaCaYeuCau]);

ALTER TABLE dbo.[HoSoHoanTien] ADD CONSTRAINT [FK_HoSoHoanTien_46] FOREIGN KEY ([MaQuanLyYeuCau]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_HoSoHoanTien_46] ON dbo.[HoSoHoanTien] ([MaQuanLyYeuCau]);

ALTER TABLE dbo.[HoSoHoanTien] ADD CONSTRAINT [FK_HoSoHoanTien_47] FOREIGN KEY ([MaQuanLyDuyet]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_HoSoHoanTien_47] ON dbo.[HoSoHoanTien] ([MaQuanLyDuyet]);

ALTER TABLE dbo.[LanChiHoanTien] ADD CONSTRAINT [FK_LanChiHoanTien_48] FOREIGN KEY ([MaHoanTien]) REFERENCES dbo.[HoSoHoanTien] ([MaHoanTien]) ON DELETE NO ACTION;

ALTER TABLE dbo.[LanChiHoanTien] ADD CONSTRAINT [FK_LanChiHoanTien_49] FOREIGN KEY ([MaCaLamViec]) REFERENCES dbo.[CaLamViec] ([MaCaLamViec]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_LanChiHoanTien_49] ON dbo.[LanChiHoanTien] ([MaCaLamViec]);

ALTER TABLE dbo.[LanChiHoanTien] ADD CONSTRAINT [FK_LanChiHoanTien_50] FOREIGN KEY ([MaQuanLyXacMinh]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_LanChiHoanTien_50] ON dbo.[LanChiHoanTien] ([MaQuanLyXacMinh]);

ALTER TABLE dbo.[GiaoDichNganHang] ADD CONSTRAINT [FK_GiaoDichNganHang_51] FOREIGN KEY ([MaThanhToan]) REFERENCES dbo.[ThanhToan] ([MaThanhToan]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_GiaoDichNganHang_51] ON dbo.[GiaoDichNganHang] ([MaThanhToan]);

ALTER TABLE dbo.[GiaoDichNganHang] ADD CONSTRAINT [FK_GiaoDichNganHang_52] FOREIGN KEY ([MaLanChiHoan]) REFERENCES dbo.[LanChiHoanTien] ([MaLanChiHoan]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_GiaoDichNganHang_52] ON dbo.[GiaoDichNganHang] ([MaLanChiHoan]);

ALTER TABLE dbo.[HoaDon] ADD CONSTRAINT [FK_HoaDon_53] FOREIGN KEY ([MaDonHang]) REFERENCES dbo.[DonHang] ([MaDonHang]) ON DELETE NO ACTION;

ALTER TABLE dbo.[HoaDon] ADD CONSTRAINT [FK_HoaDon_54] FOREIGN KEY ([MaHoaDonGoc]) REFERENCES dbo.[HoaDon] ([MaHoaDon]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_HoaDon_54] ON dbo.[HoaDon] ([MaHoaDonGoc]);

ALTER TABLE dbo.[HoaDon] ADD CONSTRAINT [FK_HoaDon_55] FOREIGN KEY ([MaHuyMon]) REFERENCES dbo.[HuyMon] ([MaHuyMon]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_HoaDon_55] ON dbo.[HoaDon] ([MaHuyMon]);

ALTER TABLE dbo.[HoaDon] ADD CONSTRAINT [FK_HoaDon_56] FOREIGN KEY ([MaQuanLyPhatHanh]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_HoaDon_56] ON dbo.[HoaDon] ([MaQuanLyPhatHanh]);

ALTER TABLE dbo.[HoaDon] ADD CONSTRAINT [FK_HoaDon_57] FOREIGN KEY ([MaCaLamViec]) REFERENCES dbo.[CaLamViec] ([MaCaLamViec]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_HoaDon_57] ON dbo.[HoaDon] ([MaCaLamViec]);

ALTER TABLE dbo.[PhieuKho] ADD CONSTRAINT [FK_PhieuKho_58] FOREIGN KEY ([MaPhieuBiDao]) REFERENCES dbo.[PhieuKho] ([MaPhieuKho]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_PhieuKho_58] ON dbo.[PhieuKho] ([MaPhieuBiDao]);

ALTER TABLE dbo.[PhieuKho] ADD CONSTRAINT [FK_PhieuKho_59] FOREIGN KEY ([MaQuanLyLap]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_PhieuKho_59] ON dbo.[PhieuKho] ([MaQuanLyLap]);

ALTER TABLE dbo.[PhieuKho] ADD CONSTRAINT [FK_PhieuKho_60] FOREIGN KEY ([MaQuanLyDuyet]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_PhieuKho_60] ON dbo.[PhieuKho] ([MaQuanLyDuyet]);

ALTER TABLE dbo.[ChiTietPhieuKho] ADD CONSTRAINT [FK_ChiTietPhieuKho_61] FOREIGN KEY ([MaPhieuKho]) REFERENCES dbo.[PhieuKho] ([MaPhieuKho]) ON DELETE NO ACTION;

ALTER TABLE dbo.[ChiTietPhieuKho] ADD CONSTRAINT [FK_ChiTietPhieuKho_62] FOREIGN KEY ([MaNguyenLieu]) REFERENCES dbo.[NguyenLieu] ([MaNguyenLieu]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_ChiTietPhieuKho_62] ON dbo.[ChiTietPhieuKho] ([MaNguyenLieu]);

ALTER TABLE dbo.[ChiTietPhieuKho] ADD CONSTRAINT [FK_ChiTietPhieuKho_63] FOREIGN KEY ([MaDonViNhap]) REFERENCES dbo.[DonViTinh] ([MaDonVi]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_ChiTietPhieuKho_63] ON dbo.[ChiTietPhieuKho] ([MaDonViNhap]);

ALTER TABLE dbo.[TonKho] ADD CONSTRAINT [FK_TonKho_64] FOREIGN KEY ([MaNguyenLieu]) REFERENCES dbo.[NguyenLieu] ([MaNguyenLieu]) ON DELETE NO ACTION;

ALTER TABLE dbo.[BienDongKho] ADD CONSTRAINT [FK_BienDongKho_65] FOREIGN KEY ([MaNguyenLieu]) REFERENCES dbo.[NguyenLieu] ([MaNguyenLieu]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_BienDongKho_65] ON dbo.[BienDongKho] ([MaNguyenLieu]);

ALTER TABLE dbo.[BienDongKho] ADD CONSTRAINT [FK_BienDongKho_66] FOREIGN KEY ([MaDongPhieuKho]) REFERENCES dbo.[ChiTietPhieuKho] ([MaDongPhieuKho]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_BienDongKho_66] ON dbo.[BienDongKho] ([MaDongPhieuKho]);

ALTER TABLE dbo.[BienDongKho] ADD CONSTRAINT [FK_BienDongKho_67] FOREIGN KEY ([MaChiTietDonHang]) REFERENCES dbo.[ChiTietDonHang] ([MaChiTietDonHang]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_BienDongKho_67] ON dbo.[BienDongKho] ([MaChiTietDonHang]);

ALTER TABLE dbo.[BienDongKho] ADD CONSTRAINT [FK_BienDongKho_68] FOREIGN KEY ([MaBienDongBiDao]) REFERENCES dbo.[BienDongKho] ([MaBienDongKho]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_BienDongKho_68] ON dbo.[BienDongKho] ([MaBienDongBiDao]);

ALTER TABLE dbo.[NhatKyHeThong] ADD CONSTRAINT [FK_NhatKyHeThong_69] FOREIGN KEY ([MaKhachHang]) REFERENCES dbo.[TaiKhoanKhachHang] ([MaKhachHang]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_NhatKyHeThong_69] ON dbo.[NhatKyHeThong] ([MaKhachHang]);

ALTER TABLE dbo.[NhatKyHeThong] ADD CONSTRAINT [FK_NhatKyHeThong_70] FOREIGN KEY ([MaQuanLy]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_NhatKyHeThong_70] ON dbo.[NhatKyHeThong] ([MaQuanLy]);

ALTER TABLE dbo.[NhatKyHeThong] ADD CONSTRAINT [FK_NhatKyHeThong_71] FOREIGN KEY ([MaCaLamViec]) REFERENCES dbo.[CaLamViec] ([MaCaLamViec]) ON DELETE NO ACTION;

CREATE INDEX [IX_FK_NhatKyHeThong_71] ON dbo.[NhatKyHeThong] ([MaCaLamViec]);

ALTER TABLE dbo.[GiuChoNguyenLieu] ADD CONSTRAINT [FK_GiuChoNguyenLieu_72] FOREIGN KEY ([MaChiTietDonHang],[MaNguyenLieu]) REFERENCES dbo.[NguyenLieuChiTietDonHang] ([MaChiTietDonHang],[MaNguyenLieu]) ON DELETE NO ACTION;

COMMIT TRANSACTION;

EXEC(N'CREATE VIEW dbo.vw_TongTienDonHang AS SELECT d.MaDonHang,
 COALESCE(SUM((ct.GiaMonDaChot + COALESCE(mt.TienMonThem,0)) * ct.SoLuongDat),0) AS TienHangBanDau,
 COALESCE(SUM((ct.GiaMonDaChot + COALESCE(mt.TienMonThem,0)) * (ct.SoLuongDat-ct.SoLuongDaHuy)),0) + d.PhiGiaoHangHienTai AS TongPhaiTraHienTai
FROM dbo.DonHang d LEFT JOIN dbo.ChiTietDonHang ct ON ct.MaDonHang=d.MaDonHang
OUTER APPLY (SELECT SUM(x.SoPhanMoiLy*x.GiaMoiPhanDaChot) AS TienMonThem FROM dbo.MonThemChiTietDonHang x WHERE x.MaChiTietDonHang=ct.MaChiTietDonHang) mt
GROUP BY d.MaDonHang,d.PhiGiaoHangHienTai');

EXEC(N'CREATE VIEW dbo.vw_TienTraLai AS SELECT MaThanhToan, CASE WHEN PhuongThuc=''CASH'' AND TienKhachDua IS NOT NULL THEN TienKhachDua-SoTienDaXacMinh ELSE 0 END AS TienTraLai FROM dbo.ThanhToan');

EXEC(N'CREATE VIEW dbo.vw_TonKhoKhaDung AS SELECT n.MaNguyenLieu,n.TenNguyenLieu,t.TonThucTe,t.TongGiuCho,n.TonAnToan,
 CASE WHEN t.TonThucTe-t.TongGiuCho-n.TonAnToan>0 THEN t.TonThucTe-t.TongGiuCho-n.TonAnToan ELSE 0 END AS KhaDung FROM dbo.NguyenLieu n JOIN dbo.TonKho t ON t.MaNguyenLieu=n.MaNguyenLieu');

EXEC(N'CREATE TRIGGER dbo.TR_NhatKyHeThong_BatBien ON dbo.[NhatKyHeThong] AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM deleted) THROW 51001,N''Dữ liệu lịch sử không được sửa hoặc xóa trực tiếp.'',1; END');

EXEC(N'CREATE TRIGGER dbo.TR_BienDongKho_BatBien ON dbo.[BienDongKho] AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM deleted) THROW 51001,N''Dữ liệu lịch sử không được sửa hoặc xóa trực tiếp.'',1; END');

EXEC(N'CREATE TRIGGER dbo.TR_NguyenLieuChiTietDonHang_BatBien ON dbo.[NguyenLieuChiTietDonHang] AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM deleted) THROW 51001,N''Dữ liệu lịch sử không được sửa hoặc xóa trực tiếp.'',1; END');

EXEC(N'CREATE TRIGGER dbo.TR_HoaDon_DaPhatHanh ON dbo.[HoaDon] AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM deleted WHERE TrangThai=''ISSUED'') THROW 51002,N''Hóa đơn đã phát hành không được sửa hoặc xóa.'',1; END');

EXEC(N'CREATE TRIGGER dbo.TR_PhieuKho_DaGhiSo ON dbo.[PhieuKho] AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM deleted WHERE TrangThai=''POSTED'') THROW 51003,N''Phiếu đã ghi sổ không được sửa hoặc xóa.'',1; END');

EXEC(N'CREATE TRIGGER dbo.TR_ChiTietPhieuKho_DaGhiSo ON dbo.[ChiTietPhieuKho] AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM (SELECT MaPhieuKho FROM inserted UNION SELECT MaPhieuKho FROM deleted) x JOIN dbo.PhieuKho p ON p.MaPhieuKho=x.MaPhieuKho WHERE p.TrangThai=''POSTED'') THROW 51004,N''Chi tiết phiếu đã ghi sổ không được thay đổi.'',1; END');

EXEC(N'CREATE TRIGGER dbo.TR_ChiTietCongThuc_DaCongBo ON dbo.[ChiTietCongThuc] AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM (SELECT MaCongThuc FROM inserted UNION SELECT MaCongThuc FROM deleted) x JOIN dbo.CongThuc c ON c.MaCongThuc=x.MaCongThuc WHERE c.TrangThai <> ''DRAFT'') THROW 51005,N''Định mức đã công bố không được sửa; tạo phiên bản mới.'',1; END');

EXEC(N'CREATE TRIGGER dbo.TR_ThanhToan_KenhWeb ON dbo.[ThanhToan] AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.DonHang d ON d.MaDonHang=i.MaDonHang WHERE d.KenhDatHang=''WEB'' AND i.PhuongThuc <> ''BANK_TRANSFER'') THROW 51006,N''Đơn Web chỉ thanh toán chuyển khoản.'',1; END');

EXEC(N'CREATE TRIGGER dbo.TR_ChiTietDonHang_CongThuc ON dbo.[ChiTietDonHang] AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.CongThuc c ON c.MaCongThuc=i.MaCongThuc WHERE c.MaCauHinhMon IS NULL OR c.MaCauHinhMon<>i.MaCauHinhMon) THROW 51007,N''Công thức không thuộc cấu hình món.'',1; END');

EXEC(N'CREATE TRIGGER dbo.TR_MonThemChiTiet_CongThuc ON dbo.[MonThemChiTietDonHang] AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.CongThuc c ON c.MaCongThuc=i.MaCongThuc WHERE c.MaMonThem IS NULL OR c.MaMonThem<>i.MaMonThem) THROW 51008,N''Công thức không thuộc topping.'',1; END');

EXEC(N'CREATE TRIGGER dbo.TR_HoanTien_LienKet ON dbo.[HoSoHoanTien] AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.ThanhToan t ON t.MaThanhToan=i.MaThanhToan WHERE t.MaDonHang<>i.MaDonHang) THROW 51009,N''Hồ sơ hoàn và thanh toán không cùng đơn.'',1; END');
