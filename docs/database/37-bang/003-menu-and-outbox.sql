SET XACT_ABORT ON;
BEGIN TRANSACTION;
-- Nâng cấp đã được duyệt: không xóa/reset dữ liệu, kiểm tra schema trước ở bootstrap.
CREATE TABLE dbo.Banner(
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
 CONSTRAINT PK_Banner PRIMARY KEY (MaBanner),
 CONSTRAINT CK_Banner_1 CHECK (LEN(LTRIM(RTRIM(TenBanner))) > 0),
 CONSTRAINT CK_Banner_2 CHECK (LEN(LTRIM(RTRIM(DuongDanAnh))) > 0),
 CONSTRAINT CK_Banner_3 CHECK (ThuTuHienThi >= 0),
 CONSTRAINT CK_Banner_4 CHECK (KetThucLuc IS NULL OR KetThucLuc > BatDauLuc),
 CONSTRAINT CK_Banner_5 CHECK (ViTri = 'HOME_HERO')
);
CREATE TABLE dbo.SuKienDongBo(
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
 CONSTRAINT PK_SuKienDongBo PRIMARY KEY (MaSuKien),
 CONSTRAINT CK_SuKienDongBo_1 CHECK (ISJSON(NoiDungJson) = 1),
 CONSTRAINT CK_SuKienDongBo_2 CHECK (SoLanThu >= 0),
 CONSTRAINT CK_SuKienDongBo_3 CHECK ((KhoaDenLuc IS NULL AND MaLanXuLy IS NULL) OR (KhoaDenLuc IS NOT NULL AND MaLanXuLy IS NOT NULL))
);
CREATE UNIQUE INDEX UX_SuKienDongBo_ThaoTac ON dbo.SuKienDongBo(MaThaoTac);
CREATE INDEX IX_SuKienDongBo_ChoXuLy ON dbo.SuKienDongBo(ThuLaiLuc,PhatSinhLuc) WHERE XuLyXongLuc IS NULL;
ALTER TABLE dbo.Mon ADD CongBoTuLuc datetime2(3) NULL;
ALTER TABLE dbo.CauHinhMon ADD LaMacDinh bit NOT NULL CONSTRAINT DF_CauHinhMon_MacDinh DEFAULT 0;
ALTER TABLE dbo.TuyChonPhaCheHopLe ADD LaMacDinh bit NOT NULL CONSTRAINT DF_TuyChon_MacDinh DEFAULT 0;
EXEC sys.sp_executesql N'-- Chọn từ cấu hình đang có, không sinh giá/món mới; giữ ưu tiên cũ M/lạnh.
;WITH ranked AS (SELECT *, ROW_NUMBER() OVER(PARTITION BY MaMon ORDER BY CASE WHEN DangBan=1 THEN 0 ELSE 1 END,CASE WHEN KichCo=''M'' THEN 0 ELSE 1 END,CASE WHEN NhietDo=''ICED'' THEN 0 ELSE 1 END,MaCauHinhMon) rn FROM dbo.CauHinhMon)
UPDATE ranked SET LaMacDinh=1 WHERE rn=1;
;WITH ranked AS (SELECT *, ROW_NUMBER() OVER(PARTITION BY MaCauHinhMon ORDER BY CASE WHEN DangSuDung=1 THEN 0 ELSE 1 END,CASE WHEN PhanTramDuong=50 THEN 0 ELSE 1 END,CASE WHEN PhanTramDa=50 THEN 0 ELSE 1 END,PhanTramDuong,PhanTramDa) rn FROM dbo.TuyChonPhaCheHopLe)
UPDATE ranked SET LaMacDinh=1 WHERE rn=1;
CREATE UNIQUE INDEX UX_CauHinhMon_MacDinh ON dbo.CauHinhMon(MaMon) WHERE LaMacDinh=1;
CREATE UNIQUE INDEX UX_TuyChon_MacDinh ON dbo.TuyChonPhaCheHopLe(MaCauHinhMon) WHERE LaMacDinh=1;
';
COMMIT;
