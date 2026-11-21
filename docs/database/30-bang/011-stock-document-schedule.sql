-- Giữ nguyên phiếu/lịch sử cũ; chỉ mở trạng thái hẹn ngày và cột lỗi nullable.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF COL_LENGTH(N'dbo.PhieuKho',N'LoiXuLy') IS NULL
BEGIN
 EXEC(N'ALTER TABLE dbo.PhieuKho ADD LoiXuLy nvarchar(500) NULL;');
 ALTER TABLE dbo.PhieuKho DROP CONSTRAINT CK_PhieuKho_2;
 ALTER TABLE dbo.PhieuKho WITH CHECK ADD CONSTRAINT CK_PhieuKho_2 CHECK (TrangThai IN ('DRAFT','SCHEDULED','FAILED','POSTED','CANCELLED'));
END;
COMMIT;
