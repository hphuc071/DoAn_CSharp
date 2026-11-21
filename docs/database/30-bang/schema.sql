-- SQL Server: 30 bảng nghiệp vụ. Khởi tạo qua ChuanBi-Database.ps1.

SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF;

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
 [SoDienThoaiChuanHoa] varchar(20) NOT NULL,
 [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 [PhienBanDuLieu] rowversion NOT NULL,
 CONSTRAINT [PK_TaiKhoanKhachHang] PRIMARY KEY ([MaKhachHang]),
 CONSTRAINT [UQ_TaiKhoanKhachHang_1] UNIQUE ([TenDangNhapChuanHoa]),
 CONSTRAINT [CK_TaiKhoanKhachHang_1] CHECK (TrangThai IN ('ACTIVE','LOCKED','INACTIVE')),
 CONSTRAINT [CK_TaiKhoanKhachHang_2] CHECK (SoLanDangNhapSai >= 0),
 CONSTRAINT [CK_TaiKhoanKhachHang_3] CHECK (EmailChuanHoa IS NOT NULL OR SoDienThoaiChuanHoa IS NOT NULL),
 CONSTRAINT [CK_TaiKhoanKhachHang_4] CHECK (LEN(LTRIM(RTRIM(HoTen))) > 0),
 CONSTRAINT [CK_TaiKhoanKhachHang_5] CHECK (DATALENGTH(SoDienThoaiChuanHoa)=10 AND SoDienThoaiChuanHoa COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9]%')
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
 [HetHanLuc] datetime2(3) NOT NULL,
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
 [GhiChuGiaoHang] nvarchar(500) NULL,
 [LaDiaChiMacDinh] bit NOT NULL,
 [DangSuDung] bit NOT NULL,
 [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 [PhienBanDuLieu] rowversion NOT NULL,
 CONSTRAINT [PK_DiaChiKhachHang] PRIMARY KEY ([MaDiaChi]),
 CONSTRAINT [CK_DiaChiKhachHang_1] CHECK (DATALENGTH(DienThoaiNguoiNhan)=10 AND DienThoaiNguoiNhan COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9]%')
);

CREATE UNIQUE INDEX [UX_DiaChi_MacDinh] ON dbo.[DiaChiKhachHang] ([MaKhachHang]) WHERE LaDiaChiMacDinh = 1 AND DangSuDung = 1;

CREATE TABLE dbo.[NhanVien] (
 [MaNhanVien] bigint IDENTITY(1,1) NOT NULL,
 [HoTen] nvarchar(120) NOT NULL,
 [SoDienThoai] varchar(20) NULL,
 [DiaChiLienHe] nvarchar(500) NULL,
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

CREATE INDEX [IX_Ca_MayDangMo] ON dbo.[CaLamViec] ([MaMay]) WHERE KetThucLuc IS NULL;

CREATE INDEX [IX_Ca_NhanVienDangMo] ON dbo.[CaLamViec] ([MaNhanVien]) WHERE KetThucLuc IS NULL;

CREATE TABLE dbo.[Mon] (
 [MaMon] bigint IDENTITY(1,1) NOT NULL,
 [LoaiMon] nvarchar(100) NOT NULL,
 [TenMon] nvarchar(150) NOT NULL,
 [MoTa] nvarchar(1000) NULL,
 [DuongDanAnh] nvarchar(500) NULL,
 [ThuTuHienThi] int NOT NULL,
 [DaCongBo] bit NOT NULL,
 [DangBan] bit NOT NULL,
 [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 [PhienBanDuLieu] rowversion NOT NULL,
 [CongBoTuLuc] datetime2(3) NULL,
 CONSTRAINT [PK_Mon] PRIMARY KEY ([MaMon]),
 CONSTRAINT [UQ_Mon_1] UNIQUE ([LoaiMon],[TenMon]),
 CONSTRAINT [CK_Mon_1] CHECK (LoaiMon IN (N'Cà phê',N'Trà',N'Đá xay'))
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
 [LaMacDinh] bit NOT NULL DEFAULT 0,
 CONSTRAINT [PK_CauHinhMon] PRIMARY KEY ([MaCauHinhMon]),
 CONSTRAINT [UQ_CauHinhMon_1] UNIQUE ([MaMon],[KichCo],[NhietDo]),
 CONSTRAINT [CK_CauHinhMon_1] CHECK (KichCo IN ('S','M','L')),
 CONSTRAINT [CK_CauHinhMon_2] CHECK (NhietDo IN ('HOT','ICED')),
 CONSTRAINT [CK_CauHinhMon_3] CHECK (GiaBan >= 0)
);

CREATE UNIQUE INDEX [UX_CauHinhMon_MacDinh] ON dbo.[CauHinhMon] ([MaMon]) WHERE LaMacDinh=1;

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

CREATE TABLE dbo.[NguyenLieu] (
 [MaNguyenLieu] bigint IDENTITY(1,1) NOT NULL,
 [TenNguyenLieu] nvarchar(150) NOT NULL,
 [DonViCoSo] varchar(2) NOT NULL,
 [NguongCanhBao] decimal(18,4) NOT NULL,
 [MoTa] nvarchar(500) NULL,
 [DangSuDung] bit NOT NULL,
 [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 [PhienBanDuLieu] rowversion NOT NULL,
 CONSTRAINT [PK_NguyenLieu] PRIMARY KEY ([MaNguyenLieu]),
 CONSTRAINT [UQ_NguyenLieu_1] UNIQUE ([TenNguyenLieu]),
 CONSTRAINT [CK_NguyenLieu_1] CHECK (NguongCanhBao >= 0),
 CONSTRAINT [CK_NguyenLieu_2] CHECK (DonViCoSo IN ('g','ml'))
);

CREATE TABLE dbo.[CongThuc] (
 [MaCongThuc] bigint IDENTITY(1,1) NOT NULL,
 [MaCauHinhMon] bigint NULL,
 [MaMonThem] bigint NULL,
 [SoPhienBan] int NOT NULL,
 [PhanTramDuongMacDinh] int NOT NULL CONSTRAINT DF_CongThuc_PhanTramDuongMacDinh DEFAULT(50),
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
 CONSTRAINT [CK_CongThuc_DuongMacDinh] CHECK (PhanTramDuongMacDinh IN (0,25,50,75,100)),
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
 CONSTRAINT [PK_ChiTietCongThuc] PRIMARY KEY ([MaCongThuc],[MaNguyenLieu],[LoaiDinhMuc]),
 CONSTRAINT [CK_ChiTietCongThuc_1] CHECK (LoaiDinhMuc IN ('FIXED','SUGAR','ICE')),
 CONSTRAINT [CK_ChiTietCongThuc_2] CHECK (DinhMucCoSo > 0)
);

CREATE TABLE dbo.[Ban] (
 [MaBan] bigint IDENTITY(1,1) NOT NULL,
 [MaBanHienThi] varchar(20) NOT NULL,
 [TenBan] nvarchar(100) NOT NULL,
 [DangSuDung] bit NOT NULL,
 [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 [PhienBanDuLieu] rowversion NOT NULL,
 CONSTRAINT [PK_Ban] PRIMARY KEY ([MaBan]),
 CONSTRAINT [UQ_Ban_1] UNIQUE ([MaBanHienThi])
);

CREATE TABLE dbo.[DonHang] (
 [MaDonHang] bigint IDENTITY(1,1) NOT NULL,
 [MaDonHienThi] varchar(30) NOT NULL,
 [MaKhachHang] bigint NULL,
 [MaCaLamViec] bigint NULL,
 [KenhDatHang] varchar(10) NOT NULL,
 [HinhThucNhanMon] varchar(15) NOT NULL,
 [MaBan] bigint NULL,
 [MaBanDaChot] varchar(20) NULL,
 [TenNguoiNhan] nvarchar(120) NULL,
 [DienThoaiNguoiNhan] varchar(20) NULL,
 [DiaChiGiaoDaChot] nvarchar(500) NULL,
 [ThoiDiemHenNhan] datetime2(3) NULL,
 [ChinhSachGiaoHangDaChotJson] nvarchar(max) NULL,
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
 [MaXuLyNgoaiLe] varchar(30) NULL,
 CONSTRAINT [PK_DonHang] PRIMARY KEY ([MaDonHang]),
 CONSTRAINT [UQ_DonHang_1] UNIQUE ([MaDonHienThi]),
 CONSTRAINT [CK_DonHang_1] CHECK (KenhDatHang IN ('WEB','POS')),
 CONSTRAINT [CK_DonHang_2] CHECK (HinhThucNhanMon IN ('DINE_IN','TAKEAWAY','DELIVERY')),
 CONSTRAINT [CK_DonHang_3] CHECK ((KenhDatHang = 'WEB' AND MaKhachHang IS NOT NULL) OR (KenhDatHang = 'POS' AND MaCaLamViec IS NOT NULL)),
 CONSTRAINT [CK_DonHang_4] CHECK ((HinhThucNhanMon = 'DINE_IN' AND MaBan IS NOT NULL) OR (HinhThucNhanMon <> 'DINE_IN' AND MaBan IS NULL)),
 CONSTRAINT [CK_DonHang_5] CHECK (HinhThucNhanMon <> 'DELIVERY' OR (TenNguoiNhan IS NOT NULL AND DienThoaiNguoiNhan IS NOT NULL AND DiaChiGiaoDaChot IS NOT NULL)),
 CONSTRAINT [CK_DonHang_6] CHECK (PhiGiaoHangBanDau >= 0 AND PhiGiaoHangHienTai >= 0 AND PhiGiaoHangHienTai <= PhiGiaoHangBanDau),
 CONSTRAINT [CK_DonHang_7] CHECK (TrangThai IN ('DRAFT','PENDING_PAYMENT','PENDING_CONFIRMATION','ACCEPTED','PREPARING','READY','DELIVERING','COMPLETED','REJECTED','CANCELLED','DELIVERY_FAILED','EXPIRED')),
 CONSTRAINT [CK_DonHang_8] CHECK (ChinhSachGiaoHangDaChotJson IS NULL OR ISJSON(ChinhSachGiaoHangDaChotJson) = 1)
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
 [SoLuongGiuCho] decimal(18,4) NOT NULL,
 [SoLuongDaTieuHao] decimal(18,4) NOT NULL,
 [SoLuongDaGiaiPhong] decimal(18,4) NOT NULL,
 [HetHanLuc] datetime2(3) NULL,
 [TrangThai] varchar(20) NOT NULL,
 [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 [PhienBanDuLieu] rowversion NOT NULL,
 CONSTRAINT [PK_NguyenLieuChiTietDonHang] PRIMARY KEY ([MaChiTietDonHang],[MaNguyenLieu]),
 CONSTRAINT [CK_NguyenLieuChiTietDonHang_1] CHECK (DinhMucMoiLy > 0),
 CONSTRAINT [CK_NguyenLieuChiTietDonHang_2] CHECK (SoLuongGiuCho >= 0 AND SoLuongDaTieuHao >= 0 AND SoLuongDaGiaiPhong >= 0),
 CONSTRAINT [CK_NguyenLieuChiTietDonHang_3] CHECK (SoLuongDaTieuHao + SoLuongDaGiaiPhong <= SoLuongGiuCho),
 CONSTRAINT [CK_NguyenLieuChiTietDonHang_4] CHECK (TrangThai IN ('RESERVED','PARTIAL','CONSUMED','RELEASED','EXPIRED'))
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
 CONSTRAINT [CK_ThanhToan_4] CHECK (TrangThai IN ('PENDING','PARTIAL','VERIFIED','EXPIRED','FAILED','RECONCILING'))
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
 [LoaiNguon] varchar(20) NOT NULL DEFAULT 'LEGACY',
 [ChungCuYeuCau] nvarchar(500) NULL,
 CONSTRAINT [PK_HoSoHoanTien] PRIMARY KEY ([MaHoanTien]),
 CONSTRAINT [UQ_HoSoHoanTien_1] UNIQUE ([MaHoanTienHienThi]),
 CONSTRAINT [CK_HoSoHoanTien_1] CHECK (SoTienYeuCau > 0),
 CONSTRAINT [CK_HoSoHoanTien_2] CHECK (SoTienDuocDuyet IS NULL OR (SoTienDuocDuyet > 0 AND SoTienDuocDuyet <= SoTienYeuCau)),
 CONSTRAINT [CK_HoSoHoanTien_3] CHECK (TrangThai IN ('REQUESTED','APPROVED','REJECTED','PROCESSING','PARTIALLY_REFUNDED','REFUNDED','RECONCILING')),
 CONSTRAINT [CK_HoSoHoanTien_4] CHECK ((CASE WHEN MaKhachHangYeuCau IS NULL THEN 0 ELSE 1 END + CASE WHEN MaCaYeuCau IS NULL THEN 0 ELSE 1 END + CASE WHEN MaQuanLyYeuCau IS NULL THEN 0 ELSE 1 END) = 1),
 CONSTRAINT [CK_HoSoHoanTien_5] CHECK (PhuongThucHoan IN ('CASH','BANK_TRANSFER')),
 CONSTRAINT [CK_HoSoHoanTien_6] CHECK (LoaiNguon IN ('LEGACY','CANCELLATION','OVERPAYMENT','LATE_PAYMENT','COMPLAINT'))
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
 [MaHoanTien] bigint NULL,
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
 [LoiXuLy] nvarchar(500) NULL,
 [NgayThucHien] date NULL,
 [MaPhieuKho] bigint IDENTITY(1,1) NOT NULL,
 [SoPhieuKho] varchar(40) NOT NULL,
 [LoaiPhieu] varchar(20) NOT NULL,
 [TrangThai] varchar(15) NOT NULL,
 [TenNhaCungCapDaChot] nvarchar(200) NULL,
 [MaMucDich] varchar(30) NULL,
 [LyDo] nvarchar(500) NULL,
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
 CONSTRAINT [CK_PhieuKho_2] CHECK (TrangThai IN ('DRAFT','SCHEDULED','FAILED','POSTED','CANCELLED')),
 CONSTRAINT [CK_PhieuKho_3] CHECK (TrangThai <> 'POSTED' OR (GhiSoLuc IS NOT NULL AND MaQuanLyDuyet IS NOT NULL)),
 CONSTRAINT [CK_PhieuKho_4] CHECK (MaPhieuBiDao IS NULL OR MaPhieuBiDao <> MaPhieuKho)
);

CREATE TABLE dbo.[ChiTietPhieuKho] (
 [MaDongPhieuKho] bigint IDENTITY(1,1) NOT NULL,
 [MaPhieuKho] bigint NOT NULL,
 [MaNguyenLieu] bigint NOT NULL,
 [DonViNhapDaChot] nvarchar(60) NOT NULL,
 [SoLuongTheoDonViNhap] decimal(18,4) NOT NULL,
 [ChenhLechDonViCoSo] decimal(18,4) NOT NULL,
 [DonGiaNhap] decimal(18,2) NULL,
 [GhiChu] nvarchar(500) NULL,
 [ThoiDiemTao] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 [ThoiDiemCapNhat] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 [PhienBanDuLieu] rowversion NOT NULL,
 CONSTRAINT [PK_ChiTietPhieuKho] PRIMARY KEY ([MaDongPhieuKho]),
 CONSTRAINT [UQ_ChiTietPhieuKho_1] UNIQUE ([MaPhieuKho],[MaNguyenLieu]),
 CONSTRAINT [CK_ChiTietPhieuKho_1] CHECK (SoLuongTheoDonViNhap >= 0),
 CONSTRAINT [CK_ChiTietPhieuKho_2] CHECK (DonGiaNhap IS NULL OR DonGiaNhap >= 0)
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
 CONSTRAINT [PK_NhatKyHeThong] PRIMARY KEY ([MaNhatKy]),
 CONSTRAINT [CK_NhatKyHeThong_1] CHECK ((LoaiTacNhan = 'CUSTOMER' AND MaKhachHang IS NOT NULL AND MaQuanLy IS NULL AND MaCaLamViec IS NULL) OR (LoaiTacNhan = 'ADMIN' AND MaQuanLy IS NOT NULL AND MaKhachHang IS NULL AND MaCaLamViec IS NULL) OR (LoaiTacNhan = 'STAFF' AND MaCaLamViec IS NOT NULL AND MaKhachHang IS NULL AND MaQuanLy IS NULL) OR (LoaiTacNhan = 'SYSTEM' AND MaKhachHang IS NULL AND MaQuanLy IS NULL AND MaCaLamViec IS NULL)),
 CONSTRAINT [CK_NhatKyHeThong_2] CHECK (DuLieuTruocJson IS NULL OR ISJSON(DuLieuTruocJson) = 1),
 CONSTRAINT [CK_NhatKyHeThong_3] CHECK (DuLieuSauJson IS NULL OR ISJSON(DuLieuSauJson) = 1),
 CONSTRAINT [CK_NhatKyHeThong_4] CHECK (KetQuaDaChotJson IS NULL OR ISJSON(KetQuaDaChotJson) = 1)
);

CREATE UNIQUE INDEX [UX_NhatKy_ThaoTac] ON dbo.[NhatKyHeThong] ([MaThaoTac]) WHERE MaThaoTac IS NOT NULL;

CREATE TABLE dbo.[Banner] (
 [MaBanner] bigint IDENTITY(1,1) NOT NULL,
 [TenBanner] nvarchar(150) NOT NULL,
 [NoiDung] nvarchar(1000) NULL,
 [DuongDanAnh] nvarchar(500) NOT NULL,
 [LienKet] nvarchar(500) NULL,
 [ViTri] varchar(30) NOT NULL,
 [ThuTuHienThi] int NOT NULL,
 [BatDauLuc] datetime2(3) NOT NULL,
 [KetThucLuc] datetime2(3) NULL,
 [DaCongBo] bit NOT NULL,
 [ThoiDiemTao] datetime2(3) NOT NULL,
 [ThoiDiemCapNhat] datetime2(3) NOT NULL,
 [PhienBanDuLieu] rowversion NOT NULL,
 CONSTRAINT [PK_Banner] PRIMARY KEY ([MaBanner]),
 CONSTRAINT [CK_Banner_1] CHECK (LEN(LTRIM(RTRIM(TenBanner))) > 0),
 CONSTRAINT [CK_Banner_2] CHECK (LEN(LTRIM(RTRIM(DuongDanAnh))) > 0),
 CONSTRAINT [CK_Banner_3] CHECK (ThuTuHienThi >= 0),
 CONSTRAINT [CK_Banner_4] CHECK (KetThucLuc IS NULL OR KetThucLuc > BatDauLuc),
 CONSTRAINT [CK_Banner_5] CHECK (ViTri = 'HOME_HERO')
);

CREATE TABLE dbo.[SuKienDongBo] (
 [MaSuKien] uniqueidentifier NOT NULL,
 [MaThaoTac] uniqueidentifier NOT NULL,
 [LoaiSuKien] varchar(50) NOT NULL,
 [NoiDungJson] nvarchar(max) NOT NULL,
 [PhatSinhLuc] datetime2(3) NOT NULL,
 [XuLyXongLuc] datetime2(3) NULL,
 [SoLanThu] int NOT NULL,
 [ThuLaiLuc] datetime2(3) NULL,
 [KhoaDenLuc] datetime2(3) NULL,
 [MaLanXuLy] uniqueidentifier NULL,
 [LoiGanNhat] nvarchar(500) NULL,
 [PhienBanDuLieu] rowversion NOT NULL,
 CONSTRAINT [PK_SuKienDongBo] PRIMARY KEY ([MaSuKien]),
 CONSTRAINT [UQ_SuKienDongBo_1] UNIQUE ([MaThaoTac]),
 CONSTRAINT [CK_SuKienDongBo_1] CHECK (ISJSON(NoiDungJson) = 1),
 CONSTRAINT [CK_SuKienDongBo_2] CHECK (SoLanThu >= 0),
 CONSTRAINT [CK_SuKienDongBo_3] CHECK ((KhoaDenLuc IS NULL AND MaLanXuLy IS NULL) OR (KhoaDenLuc IS NOT NULL AND MaLanXuLy IS NOT NULL))
);

CREATE INDEX [IX_SuKienDongBo_ChoXuLy] ON dbo.[SuKienDongBo] ([ThuLaiLuc],[PhatSinhLuc]) WHERE XuLyXongLuc IS NULL;

ALTER TABLE dbo.[PhienDangNhap] WITH CHECK ADD CONSTRAINT [FK_PhienDangNhap_1] FOREIGN KEY ([MaKhachHang]) REFERENCES dbo.[TaiKhoanKhachHang] ([MaKhachHang]);

CREATE INDEX [IX_FK_PhienDangNhap_1] ON dbo.[PhienDangNhap] ([MaKhachHang]);

ALTER TABLE dbo.[PhienDangNhap] WITH CHECK ADD CONSTRAINT [FK_PhienDangNhap_2] FOREIGN KEY ([MaQuanLy]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]);

CREATE INDEX [IX_FK_PhienDangNhap_2] ON dbo.[PhienDangNhap] ([MaQuanLy]);

ALTER TABLE dbo.[DiaChiKhachHang] WITH CHECK ADD CONSTRAINT [FK_DiaChiKhachHang_3] FOREIGN KEY ([MaKhachHang]) REFERENCES dbo.[TaiKhoanKhachHang] ([MaKhachHang]);

CREATE INDEX [IX_FK_DiaChiKhachHang_3] ON dbo.[DiaChiKhachHang] ([MaKhachHang]);

ALTER TABLE dbo.[CaLamViec] WITH CHECK ADD CONSTRAINT [FK_CaLamViec_4] FOREIGN KEY ([MaNhanVien]) REFERENCES dbo.[NhanVien] ([MaNhanVien]);

CREATE INDEX [IX_FK_CaLamViec_4] ON dbo.[CaLamViec] ([MaNhanVien]);

ALTER TABLE dbo.[CaLamViec] WITH CHECK ADD CONSTRAINT [FK_CaLamViec_5] FOREIGN KEY ([MaCaBanGiaoTruoc]) REFERENCES dbo.[CaLamViec] ([MaCaLamViec]);

CREATE INDEX [IX_FK_CaLamViec_5] ON dbo.[CaLamViec] ([MaCaBanGiaoTruoc]);

ALTER TABLE dbo.[CaLamViec] WITH CHECK ADD CONSTRAINT [FK_CaLamViec_6] FOREIGN KEY ([MaQuanLyDuyet]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]);

CREATE INDEX [IX_FK_CaLamViec_6] ON dbo.[CaLamViec] ([MaQuanLyDuyet]);

ALTER TABLE dbo.[CauHinhMon] WITH CHECK ADD CONSTRAINT [FK_CauHinhMon_7] FOREIGN KEY ([MaMon]) REFERENCES dbo.[Mon] ([MaMon]);

CREATE INDEX [IX_FK_CauHinhMon_7] ON dbo.[CauHinhMon] ([MaMon]);

ALTER TABLE dbo.[CongThuc] WITH CHECK ADD CONSTRAINT [FK_CongThuc_8] FOREIGN KEY ([MaCauHinhMon]) REFERENCES dbo.[CauHinhMon] ([MaCauHinhMon]);

CREATE INDEX [IX_FK_CongThuc_8] ON dbo.[CongThuc] ([MaCauHinhMon]);

ALTER TABLE dbo.[CongThuc] WITH CHECK ADD CONSTRAINT [FK_CongThuc_9] FOREIGN KEY ([MaMonThem]) REFERENCES dbo.[MonThem] ([MaMonThem]);

CREATE INDEX [IX_FK_CongThuc_9] ON dbo.[CongThuc] ([MaMonThem]);

ALTER TABLE dbo.[CongThuc] WITH CHECK ADD CONSTRAINT [FK_CongThuc_10] FOREIGN KEY ([MaQuanLyTao]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]);

CREATE INDEX [IX_FK_CongThuc_10] ON dbo.[CongThuc] ([MaQuanLyTao]);

ALTER TABLE dbo.[CongThuc] WITH CHECK ADD CONSTRAINT [FK_CongThuc_11] FOREIGN KEY ([MaQuanLyDuyet]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]);

CREATE INDEX [IX_FK_CongThuc_11] ON dbo.[CongThuc] ([MaQuanLyDuyet]);

ALTER TABLE dbo.[ChiTietCongThuc] WITH CHECK ADD CONSTRAINT [FK_ChiTietCongThuc_12] FOREIGN KEY ([MaCongThuc]) REFERENCES dbo.[CongThuc] ([MaCongThuc]);

ALTER TABLE dbo.[ChiTietCongThuc] WITH CHECK ADD CONSTRAINT [FK_ChiTietCongThuc_13] FOREIGN KEY ([MaNguyenLieu]) REFERENCES dbo.[NguyenLieu] ([MaNguyenLieu]);

CREATE INDEX [IX_FK_ChiTietCongThuc_13] ON dbo.[ChiTietCongThuc] ([MaNguyenLieu]);

ALTER TABLE dbo.[DonHang] WITH CHECK ADD CONSTRAINT [FK_DonHang_14] FOREIGN KEY ([MaKhachHang]) REFERENCES dbo.[TaiKhoanKhachHang] ([MaKhachHang]);

CREATE INDEX [IX_FK_DonHang_14] ON dbo.[DonHang] ([MaKhachHang]);

ALTER TABLE dbo.[DonHang] WITH CHECK ADD CONSTRAINT [FK_DonHang_15] FOREIGN KEY ([MaCaLamViec]) REFERENCES dbo.[CaLamViec] ([MaCaLamViec]);

CREATE INDEX [IX_FK_DonHang_15] ON dbo.[DonHang] ([MaCaLamViec]);



ALTER TABLE dbo.[DonHang] WITH CHECK ADD CONSTRAINT [FK_DonHang_17] FOREIGN KEY ([MaBan]) REFERENCES dbo.[Ban] ([MaBan]);

CREATE INDEX [IX_FK_DonHang_17] ON dbo.[DonHang] ([MaBan]);

ALTER TABLE dbo.[ChiTietDonHang] WITH CHECK ADD CONSTRAINT [FK_ChiTietDonHang_18] FOREIGN KEY ([MaDonHang]) REFERENCES dbo.[DonHang] ([MaDonHang]);

CREATE INDEX [IX_FK_ChiTietDonHang_18] ON dbo.[ChiTietDonHang] ([MaDonHang]);

ALTER TABLE dbo.[ChiTietDonHang] WITH CHECK ADD CONSTRAINT [FK_ChiTietDonHang_19] FOREIGN KEY ([MaCauHinhMon]) REFERENCES dbo.[CauHinhMon] ([MaCauHinhMon]);

CREATE INDEX [IX_FK_ChiTietDonHang_19] ON dbo.[ChiTietDonHang] ([MaCauHinhMon]);

ALTER TABLE dbo.[ChiTietDonHang] WITH CHECK ADD CONSTRAINT [FK_ChiTietDonHang_20] FOREIGN KEY ([MaCongThuc]) REFERENCES dbo.[CongThuc] ([MaCongThuc]);

CREATE INDEX [IX_FK_ChiTietDonHang_20] ON dbo.[ChiTietDonHang] ([MaCongThuc]);

ALTER TABLE dbo.[MonThemChiTietDonHang] WITH CHECK ADD CONSTRAINT [FK_MonThemChiTietDonHang_21] FOREIGN KEY ([MaChiTietDonHang]) REFERENCES dbo.[ChiTietDonHang] ([MaChiTietDonHang]);

ALTER TABLE dbo.[MonThemChiTietDonHang] WITH CHECK ADD CONSTRAINT [FK_MonThemChiTietDonHang_22] FOREIGN KEY ([MaMonThem]) REFERENCES dbo.[MonThem] ([MaMonThem]);

CREATE INDEX [IX_FK_MonThemChiTietDonHang_22] ON dbo.[MonThemChiTietDonHang] ([MaMonThem]);

ALTER TABLE dbo.[MonThemChiTietDonHang] WITH CHECK ADD CONSTRAINT [FK_MonThemChiTietDonHang_23] FOREIGN KEY ([MaCongThuc]) REFERENCES dbo.[CongThuc] ([MaCongThuc]);

CREATE INDEX [IX_FK_MonThemChiTietDonHang_23] ON dbo.[MonThemChiTietDonHang] ([MaCongThuc]);

ALTER TABLE dbo.[NguyenLieuChiTietDonHang] WITH CHECK ADD CONSTRAINT [FK_NguyenLieuChiTietDonHang_24] FOREIGN KEY ([MaChiTietDonHang]) REFERENCES dbo.[ChiTietDonHang] ([MaChiTietDonHang]);

ALTER TABLE dbo.[NguyenLieuChiTietDonHang] WITH CHECK ADD CONSTRAINT [FK_NguyenLieuChiTietDonHang_25] FOREIGN KEY ([MaNguyenLieu]) REFERENCES dbo.[NguyenLieu] ([MaNguyenLieu]);

CREATE INDEX [IX_FK_NguyenLieuChiTietDonHang_25] ON dbo.[NguyenLieuChiTietDonHang] ([MaNguyenLieu]);

ALTER TABLE dbo.[HuyMon] WITH CHECK ADD CONSTRAINT [FK_HuyMon_26] FOREIGN KEY ([MaChiTietDonHang]) REFERENCES dbo.[ChiTietDonHang] ([MaChiTietDonHang]);

CREATE INDEX [IX_FK_HuyMon_26] ON dbo.[HuyMon] ([MaChiTietDonHang]);

ALTER TABLE dbo.[HuyMon] WITH CHECK ADD CONSTRAINT [FK_HuyMon_27] FOREIGN KEY ([MaKhachHangYeuCau]) REFERENCES dbo.[TaiKhoanKhachHang] ([MaKhachHang]);

CREATE INDEX [IX_FK_HuyMon_27] ON dbo.[HuyMon] ([MaKhachHangYeuCau]);

ALTER TABLE dbo.[HuyMon] WITH CHECK ADD CONSTRAINT [FK_HuyMon_28] FOREIGN KEY ([MaCaLamViec]) REFERENCES dbo.[CaLamViec] ([MaCaLamViec]);

CREATE INDEX [IX_FK_HuyMon_28] ON dbo.[HuyMon] ([MaCaLamViec]);

ALTER TABLE dbo.[HuyMon] WITH CHECK ADD CONSTRAINT [FK_HuyMon_29] FOREIGN KEY ([MaQuanLyYeuCau]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]);

CREATE INDEX [IX_FK_HuyMon_29] ON dbo.[HuyMon] ([MaQuanLyYeuCau]);

ALTER TABLE dbo.[HuyMon] WITH CHECK ADD CONSTRAINT [FK_HuyMon_30] FOREIGN KEY ([MaQuanLyDuyet]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]);

CREATE INDEX [IX_FK_HuyMon_30] ON dbo.[HuyMon] ([MaQuanLyDuyet]);

ALTER TABLE dbo.[ThanhToan] WITH CHECK ADD CONSTRAINT [FK_ThanhToan_31] FOREIGN KEY ([MaDonHang]) REFERENCES dbo.[DonHang] ([MaDonHang]);

CREATE INDEX [IX_FK_ThanhToan_31] ON dbo.[ThanhToan] ([MaDonHang]);

ALTER TABLE dbo.[ThanhToan] WITH CHECK ADD CONSTRAINT [FK_ThanhToan_32] FOREIGN KEY ([MaCaLamViec]) REFERENCES dbo.[CaLamViec] ([MaCaLamViec]);

CREATE INDEX [IX_FK_ThanhToan_32] ON dbo.[ThanhToan] ([MaCaLamViec]);

ALTER TABLE dbo.[ThanhToan] WITH CHECK ADD CONSTRAINT [FK_ThanhToan_33] FOREIGN KEY ([MaQuanLyXacMinh]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]);

CREATE INDEX [IX_FK_ThanhToan_33] ON dbo.[ThanhToan] ([MaQuanLyXacMinh]);

ALTER TABLE dbo.[HoSoHoanTien] WITH CHECK ADD CONSTRAINT [FK_HoSoHoanTien_34] FOREIGN KEY ([MaDonHang]) REFERENCES dbo.[DonHang] ([MaDonHang]);

CREATE INDEX [IX_FK_HoSoHoanTien_34] ON dbo.[HoSoHoanTien] ([MaDonHang]);

ALTER TABLE dbo.[HoSoHoanTien] WITH CHECK ADD CONSTRAINT [FK_HoSoHoanTien_35] FOREIGN KEY ([MaThanhToan]) REFERENCES dbo.[ThanhToan] ([MaThanhToan]);

CREATE INDEX [IX_FK_HoSoHoanTien_35] ON dbo.[HoSoHoanTien] ([MaThanhToan]);

ALTER TABLE dbo.[HoSoHoanTien] WITH CHECK ADD CONSTRAINT [FK_HoSoHoanTien_36] FOREIGN KEY ([MaHuyMon]) REFERENCES dbo.[HuyMon] ([MaHuyMon]);

CREATE INDEX [IX_FK_HoSoHoanTien_36] ON dbo.[HoSoHoanTien] ([MaHuyMon]);

ALTER TABLE dbo.[HoSoHoanTien] WITH CHECK ADD CONSTRAINT [FK_HoSoHoanTien_37] FOREIGN KEY ([MaKhachHangYeuCau]) REFERENCES dbo.[TaiKhoanKhachHang] ([MaKhachHang]);

CREATE INDEX [IX_FK_HoSoHoanTien_37] ON dbo.[HoSoHoanTien] ([MaKhachHangYeuCau]);

ALTER TABLE dbo.[HoSoHoanTien] WITH CHECK ADD CONSTRAINT [FK_HoSoHoanTien_38] FOREIGN KEY ([MaCaYeuCau]) REFERENCES dbo.[CaLamViec] ([MaCaLamViec]);

CREATE INDEX [IX_FK_HoSoHoanTien_38] ON dbo.[HoSoHoanTien] ([MaCaYeuCau]);

ALTER TABLE dbo.[HoSoHoanTien] WITH CHECK ADD CONSTRAINT [FK_HoSoHoanTien_39] FOREIGN KEY ([MaQuanLyYeuCau]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]);

CREATE INDEX [IX_FK_HoSoHoanTien_39] ON dbo.[HoSoHoanTien] ([MaQuanLyYeuCau]);

ALTER TABLE dbo.[HoSoHoanTien] WITH CHECK ADD CONSTRAINT [FK_HoSoHoanTien_40] FOREIGN KEY ([MaQuanLyDuyet]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]);

CREATE INDEX [IX_FK_HoSoHoanTien_40] ON dbo.[HoSoHoanTien] ([MaQuanLyDuyet]);

ALTER TABLE dbo.[LanChiHoanTien] WITH CHECK ADD CONSTRAINT [FK_LanChiHoanTien_41] FOREIGN KEY ([MaHoanTien]) REFERENCES dbo.[HoSoHoanTien] ([MaHoanTien]);

CREATE INDEX [IX_FK_LanChiHoanTien_41] ON dbo.[LanChiHoanTien] ([MaHoanTien]);

ALTER TABLE dbo.[LanChiHoanTien] WITH CHECK ADD CONSTRAINT [FK_LanChiHoanTien_42] FOREIGN KEY ([MaCaLamViec]) REFERENCES dbo.[CaLamViec] ([MaCaLamViec]);

CREATE INDEX [IX_FK_LanChiHoanTien_42] ON dbo.[LanChiHoanTien] ([MaCaLamViec]);

ALTER TABLE dbo.[LanChiHoanTien] WITH CHECK ADD CONSTRAINT [FK_LanChiHoanTien_43] FOREIGN KEY ([MaQuanLyXacMinh]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]);

CREATE INDEX [IX_FK_LanChiHoanTien_43] ON dbo.[LanChiHoanTien] ([MaQuanLyXacMinh]);

ALTER TABLE dbo.[GiaoDichNganHang] WITH CHECK ADD CONSTRAINT [FK_GiaoDichNganHang_44] FOREIGN KEY ([MaThanhToan]) REFERENCES dbo.[ThanhToan] ([MaThanhToan]);

CREATE INDEX [IX_FK_GiaoDichNganHang_44] ON dbo.[GiaoDichNganHang] ([MaThanhToan]);

ALTER TABLE dbo.[GiaoDichNganHang] WITH CHECK ADD CONSTRAINT [FK_GiaoDichNganHang_45] FOREIGN KEY ([MaLanChiHoan]) REFERENCES dbo.[LanChiHoanTien] ([MaLanChiHoan]);

CREATE INDEX [IX_FK_GiaoDichNganHang_45] ON dbo.[GiaoDichNganHang] ([MaLanChiHoan]);

ALTER TABLE dbo.[HoaDon] WITH CHECK ADD CONSTRAINT [FK_HoaDon_46] FOREIGN KEY ([MaDonHang]) REFERENCES dbo.[DonHang] ([MaDonHang]);

CREATE INDEX [IX_FK_HoaDon_46] ON dbo.[HoaDon] ([MaDonHang]);

ALTER TABLE dbo.[HoaDon] WITH CHECK ADD CONSTRAINT [FK_HoaDon_47] FOREIGN KEY ([MaHoaDonGoc]) REFERENCES dbo.[HoaDon] ([MaHoaDon]);

CREATE INDEX [IX_FK_HoaDon_47] ON dbo.[HoaDon] ([MaHoaDonGoc]);

ALTER TABLE dbo.[HoaDon] WITH CHECK ADD CONSTRAINT [FK_HoaDon_48] FOREIGN KEY ([MaHuyMon]) REFERENCES dbo.[HuyMon] ([MaHuyMon]);

CREATE INDEX [IX_FK_HoaDon_48] ON dbo.[HoaDon] ([MaHuyMon]);

ALTER TABLE dbo.[HoaDon] WITH CHECK ADD CONSTRAINT [FK_HoaDon_49] FOREIGN KEY ([MaQuanLyPhatHanh]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]);

CREATE INDEX [IX_FK_HoaDon_49] ON dbo.[HoaDon] ([MaQuanLyPhatHanh]);

ALTER TABLE dbo.[HoaDon] WITH CHECK ADD CONSTRAINT [FK_HoaDon_50] FOREIGN KEY ([MaCaLamViec]) REFERENCES dbo.[CaLamViec] ([MaCaLamViec]);

CREATE INDEX [IX_FK_HoaDon_50] ON dbo.[HoaDon] ([MaCaLamViec]);

ALTER TABLE dbo.[PhieuKho] WITH CHECK ADD CONSTRAINT [FK_PhieuKho_51] FOREIGN KEY ([MaPhieuBiDao]) REFERENCES dbo.[PhieuKho] ([MaPhieuKho]);

CREATE INDEX [IX_FK_PhieuKho_51] ON dbo.[PhieuKho] ([MaPhieuBiDao]);

ALTER TABLE dbo.[PhieuKho] WITH CHECK ADD CONSTRAINT [FK_PhieuKho_52] FOREIGN KEY ([MaQuanLyLap]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]);

CREATE INDEX [IX_FK_PhieuKho_52] ON dbo.[PhieuKho] ([MaQuanLyLap]);

ALTER TABLE dbo.[PhieuKho] WITH CHECK ADD CONSTRAINT [FK_PhieuKho_53] FOREIGN KEY ([MaQuanLyDuyet]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]);

CREATE INDEX [IX_FK_PhieuKho_53] ON dbo.[PhieuKho] ([MaQuanLyDuyet]);

ALTER TABLE dbo.[ChiTietPhieuKho] WITH CHECK ADD CONSTRAINT [FK_ChiTietPhieuKho_54] FOREIGN KEY ([MaPhieuKho]) REFERENCES dbo.[PhieuKho] ([MaPhieuKho]);

CREATE INDEX [IX_FK_ChiTietPhieuKho_54] ON dbo.[ChiTietPhieuKho] ([MaPhieuKho]);

ALTER TABLE dbo.[ChiTietPhieuKho] WITH CHECK ADD CONSTRAINT [FK_ChiTietPhieuKho_55] FOREIGN KEY ([MaNguyenLieu]) REFERENCES dbo.[NguyenLieu] ([MaNguyenLieu]);

CREATE INDEX [IX_FK_ChiTietPhieuKho_55] ON dbo.[ChiTietPhieuKho] ([MaNguyenLieu]);

ALTER TABLE dbo.[TonKho] WITH CHECK ADD CONSTRAINT [FK_TonKho_56] FOREIGN KEY ([MaNguyenLieu]) REFERENCES dbo.[NguyenLieu] ([MaNguyenLieu]);

ALTER TABLE dbo.[BienDongKho] WITH CHECK ADD CONSTRAINT [FK_BienDongKho_57] FOREIGN KEY ([MaNguyenLieu]) REFERENCES dbo.[NguyenLieu] ([MaNguyenLieu]);

CREATE INDEX [IX_FK_BienDongKho_57] ON dbo.[BienDongKho] ([MaNguyenLieu]);

ALTER TABLE dbo.[BienDongKho] WITH CHECK ADD CONSTRAINT [FK_BienDongKho_58] FOREIGN KEY ([MaDongPhieuKho]) REFERENCES dbo.[ChiTietPhieuKho] ([MaDongPhieuKho]);

CREATE INDEX [IX_FK_BienDongKho_58] ON dbo.[BienDongKho] ([MaDongPhieuKho]);

ALTER TABLE dbo.[BienDongKho] WITH CHECK ADD CONSTRAINT [FK_BienDongKho_59] FOREIGN KEY ([MaChiTietDonHang]) REFERENCES dbo.[ChiTietDonHang] ([MaChiTietDonHang]);

CREATE INDEX [IX_FK_BienDongKho_59] ON dbo.[BienDongKho] ([MaChiTietDonHang]);

ALTER TABLE dbo.[BienDongKho] WITH CHECK ADD CONSTRAINT [FK_BienDongKho_60] FOREIGN KEY ([MaBienDongBiDao]) REFERENCES dbo.[BienDongKho] ([MaBienDongKho]);

CREATE INDEX [IX_FK_BienDongKho_60] ON dbo.[BienDongKho] ([MaBienDongBiDao]);

ALTER TABLE dbo.[NhatKyHeThong] WITH CHECK ADD CONSTRAINT [FK_NhatKyHeThong_61] FOREIGN KEY ([MaKhachHang]) REFERENCES dbo.[TaiKhoanKhachHang] ([MaKhachHang]);

CREATE INDEX [IX_FK_NhatKyHeThong_61] ON dbo.[NhatKyHeThong] ([MaKhachHang]);

ALTER TABLE dbo.[NhatKyHeThong] WITH CHECK ADD CONSTRAINT [FK_NhatKyHeThong_62] FOREIGN KEY ([MaQuanLy]) REFERENCES dbo.[TaiKhoanQuanLy] ([MaQuanLy]);

CREATE INDEX [IX_FK_NhatKyHeThong_62] ON dbo.[NhatKyHeThong] ([MaQuanLy]);

ALTER TABLE dbo.[NhatKyHeThong] WITH CHECK ADD CONSTRAINT [FK_NhatKyHeThong_63] FOREIGN KEY ([MaCaLamViec]) REFERENCES dbo.[CaLamViec] ([MaCaLamViec]);

CREATE INDEX [IX_FK_NhatKyHeThong_63] ON dbo.[NhatKyHeThong] ([MaCaLamViec]);

ALTER TABLE dbo.[HoaDon] WITH CHECK ADD CONSTRAINT [FK_HoaDon_64] FOREIGN KEY ([MaHoanTien]) REFERENCES dbo.[HoSoHoanTien] ([MaHoanTien]);

CREATE INDEX [IX_FK_HoaDon_64] ON dbo.[HoaDon] ([MaHoanTien]);

EXEC(N'CREATE TRIGGER dbo.TR_NhatKyHeThong_BatBien ON dbo.[NhatKyHeThong] AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM deleted) THROW 51001,N''Dữ liệu lịch sử không được sửa hoặc xóa trực tiếp.'',1; END');

EXEC(N'CREATE TRIGGER dbo.TR_BienDongKho_BatBien ON dbo.[BienDongKho] AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM deleted) THROW 51001,N''Dữ liệu lịch sử không được sửa hoặc xóa trực tiếp.'',1; END');

EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_NguyenLieuChiTietDonHang_BatBien
ON dbo.NguyenLieuChiTietDonHang AFTER UPDATE, DELETE AS
BEGIN
 SET NOCOUNT ON;
 -- Giữ nguyên công thức đã chốt; chỉ các lượng giữ/tiêu hao/giải phóng được cập nhật.
 IF EXISTS(SELECT MaChiTietDonHang,MaNguyenLieu,TenNguyenLieuDaChot,DonViCoSoDaChot,DinhMucMoiLy,ThoiDiemTao FROM deleted
           EXCEPT SELECT MaChiTietDonHang,MaNguyenLieu,TenNguyenLieuDaChot,DonViCoSoDaChot,DinhMucMoiLy,ThoiDiemTao FROM inserted)
    THROW 51001,N''Snapshot nguyên liệu không được sửa hoặc xóa.'',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON i.MaChiTietDonHang=d.MaChiTietDonHang AND i.MaNguyenLieu=d.MaNguyenLieu
           WHERE i.SoLuongGiuCho<>d.SoLuongGiuCho OR i.SoLuongDaTieuHao<d.SoLuongDaTieuHao OR i.SoLuongDaGiaiPhong<d.SoLuongDaGiaiPhong)
    THROW 51001,N''Không giảm lịch sử tiêu hao/giải phóng hoặc thay số lượng đã giữ.'',1;
END');

EXEC(N'CREATE TRIGGER dbo.TR_HoaDon_DaPhatHanh ON dbo.[HoaDon] AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM deleted WHERE TrangThai=''ISSUED'') THROW 51002,N''Hóa đơn đã phát hành không được sửa hoặc xóa.'',1; END');

EXEC(N'CREATE TRIGGER dbo.TR_PhieuKho_DaGhiSo ON dbo.[PhieuKho] AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM deleted WHERE TrangThai=''POSTED'') THROW 51003,N''Phiếu đã ghi sổ không được sửa hoặc xóa.'',1; END');

EXEC(N'CREATE TRIGGER dbo.TR_ChiTietPhieuKho_DaGhiSo ON dbo.[ChiTietPhieuKho] AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM (SELECT MaPhieuKho FROM inserted UNION SELECT MaPhieuKho FROM deleted) x JOIN dbo.PhieuKho p ON p.MaPhieuKho=x.MaPhieuKho WHERE p.TrangThai=''POSTED'') THROW 51004,N''Chi tiết phiếu đã ghi sổ không được thay đổi.'',1; END');

EXEC(N'CREATE TRIGGER dbo.TR_ChiTietCongThuc_DaCongBo ON dbo.[ChiTietCongThuc] AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM (SELECT MaCongThuc FROM inserted UNION SELECT MaCongThuc FROM deleted) x JOIN dbo.CongThuc c ON c.MaCongThuc=x.MaCongThuc WHERE c.TrangThai <> ''DRAFT'') THROW 51005,N''Định mức đã công bố không được sửa; tạo phiên bản mới.'',1; END');

EXEC(N'CREATE TRIGGER dbo.TR_ThanhToan_KenhWeb ON dbo.[ThanhToan] AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.DonHang d ON d.MaDonHang=i.MaDonHang WHERE d.KenhDatHang=''WEB'' AND i.PhuongThuc <> ''BANK_TRANSFER'') THROW 51006,N''Đơn Web chỉ thanh toán chuyển khoản.'',1; END');

EXEC(N'CREATE TRIGGER dbo.TR_ChiTietDonHang_CongThuc ON dbo.[ChiTietDonHang] AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.CongThuc c ON c.MaCongThuc=i.MaCongThuc WHERE c.MaCauHinhMon IS NULL OR c.MaCauHinhMon<>i.MaCauHinhMon) THROW 51007,N''Công thức không thuộc cấu hình món.'',1; END');

EXEC(N'CREATE TRIGGER dbo.TR_MonThemChiTiet_CongThuc ON dbo.[MonThemChiTietDonHang] AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.CongThuc c ON c.MaCongThuc=i.MaCongThuc WHERE c.MaMonThem IS NULL OR c.MaMonThem<>i.MaMonThem) THROW 51008,N''Công thức không thuộc topping.'',1; END');

EXEC(N'CREATE TRIGGER dbo.TR_HoanTien_LienKet ON dbo.[HoSoHoanTien] AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.ThanhToan t ON t.MaThanhToan=i.MaThanhToan WHERE t.MaDonHang<>i.MaDonHang) THROW 51009,N''Hồ sơ hoàn và thanh toán không cùng đơn.'',1; END');

CREATE UNIQUE INDEX UX_HoaDon_NguonHuy ON dbo.HoaDon(MaHoaDonGoc,MaHuyMon) WHERE LoaiHoaDon='ADJUSTMENT' AND MaHuyMon IS NOT NULL;

CREATE UNIQUE INDEX UX_HoaDon_NguonHoan ON dbo.HoaDon(MaHoaDonGoc,MaHoanTien) WHERE LoaiHoaDon='ADJUSTMENT' AND MaHoanTien IS NOT NULL;

COMMIT TRANSACTION;
