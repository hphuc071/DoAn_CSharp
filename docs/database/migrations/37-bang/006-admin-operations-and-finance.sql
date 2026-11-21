-- UC-ADM-06..10: đã được chấp thuận; giữ nguyên số bảng và dữ liệu cũ.
SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;
SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF;
BEGIN TRANSACTION;
IF COL_LENGTH(N'dbo.DonHang',N'MaXuLyNgoaiLe') IS NULL
 ALTER TABLE dbo.DonHang ADD MaXuLyNgoaiLe varchar(30) NULL;
IF COL_LENGTH(N'dbo.HoSoHoanTien',N'LoaiNguon') IS NULL
 ALTER TABLE dbo.HoSoHoanTien ADD LoaiNguon varchar(20) NOT NULL CONSTRAINT DF_HoanTien_LoaiNguon DEFAULT 'LEGACY';
IF COL_LENGTH(N'dbo.HoSoHoanTien',N'ChungCuYeuCau') IS NULL
 ALTER TABLE dbo.HoSoHoanTien ADD ChungCuYeuCau nvarchar(500) NULL;
IF COL_LENGTH(N'dbo.HoaDon',N'MaHoanTien') IS NULL
 ALTER TABLE dbo.HoaDon ADD MaHoanTien bigint NULL;
IF OBJECT_ID(N'dbo.CK_HoanTien_LoaiNguon',N'C') IS NULL
 EXEC(N'ALTER TABLE dbo.HoSoHoanTien WITH CHECK ADD CONSTRAINT CK_HoanTien_LoaiNguon CHECK (LoaiNguon IN (''LEGACY'',''CANCELLATION'',''OVERPAYMENT'',''LATE_PAYMENT'',''COMPLAINT''))');
IF OBJECT_ID(N'dbo.FK_HoaDon_HoSoHoanTien',N'F') IS NULL
 EXEC(N'ALTER TABLE dbo.HoaDon WITH CHECK ADD CONSTRAINT FK_HoaDon_HoSoHoanTien FOREIGN KEY(MaHoanTien) REFERENCES dbo.HoSoHoanTien(MaHoanTien)');
-- Không tự xóa/gộp chứng từ cũ nếu phát hiện nguồn trùng: SQL dừng và rollback để đối soát.
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.HoaDon') AND name=N'UX_HoaDon_NguonHuy')
 CREATE UNIQUE INDEX UX_HoaDon_NguonHuy ON dbo.HoaDon(MaHoaDonGoc,MaHuyMon) WHERE LoaiHoaDon='ADJUSTMENT' AND MaHuyMon IS NOT NULL;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.HoaDon') AND name=N'UX_HoaDon_NguonHoan')
 EXEC(N'CREATE UNIQUE INDEX UX_HoaDon_NguonHoan ON dbo.HoaDon(MaHoaDonGoc,MaHoanTien) WHERE LoaiHoaDon=''ADJUSTMENT'' AND MaHoanTien IS NOT NULL');
-- EXPIRED đã được chấp thuận, tách khỏi hủy chủ động; không cập nhật trạng thái đơn cũ.
ALTER TABLE dbo.DonHang DROP CONSTRAINT CK_DonHang_7;
ALTER TABLE dbo.DonHang WITH CHECK ADD CONSTRAINT CK_DonHang_7 CHECK (TrangThai IN ('DRAFT','PENDING_PAYMENT','PENDING_CONFIRMATION','ACCEPTED','PREPARING','READY','DELIVERING','COMPLETED','REJECTED','CANCELLED','DELIVERY_FAILED','EXPIRED'));
COMMIT TRANSACTION;
