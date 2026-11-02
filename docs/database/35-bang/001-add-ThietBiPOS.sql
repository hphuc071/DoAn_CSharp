-- Đã được người dùng chấp thuận: chỉ thêm ThietBiPOS, không sửa/xóa 34 bảng hiện có.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
CREATE TABLE dbo.ThietBiPOS (
 MaThietBiPOS bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ThietBiPOS PRIMARY KEY,
 MaMay varchar(50) NOT NULL CONSTRAINT UQ_ThietBiPOS_MaMay UNIQUE,
 TenMay nvarchar(120) NOT NULL,
 MaBamKhoa binary(32) NOT NULL,
 TrangThai varchar(15) NOT NULL,
 MaQuanLyCap bigint NOT NULL,
 CapLuc datetime2(3) NOT NULL,
 ThuHoiLuc datetime2(3) NULL,
 LyDo nvarchar(500) NOT NULL,
 ThoiDiemTao datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 ThoiDiemCapNhat datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 PhienBanDuLieu rowversion NOT NULL,
 CONSTRAINT FK_ThietBiPOS_QuanLy FOREIGN KEY(MaQuanLyCap) REFERENCES dbo.TaiKhoanQuanLy(MaQuanLy),
 CONSTRAINT CK_ThietBiPOS_1 CHECK(TrangThai IN ('ACTIVE','REVOKED')),
 CONSTRAINT CK_ThietBiPOS_2 CHECK(LEN(LTRIM(RTRIM(MaMay)))>0 AND LEN(LTRIM(RTRIM(TenMay)))>0),
 CONSTRAINT CK_ThietBiPOS_3 CHECK((TrangThai='ACTIVE' AND ThuHoiLuc IS NULL) OR (TrangThai='REVOKED' AND ThuHoiLuc IS NOT NULL)),
 CONSTRAINT CK_ThietBiPOS_4 CHECK(LEN(LTRIM(RTRIM(LyDo)))>0)
);
CREATE UNIQUE INDEX UX_ThietBiPOS_Khoa ON dbo.ThietBiPOS(MaBamKhoa);
COMMIT;
