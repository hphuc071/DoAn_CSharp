-- Ngày nghiệp vụ tách khỏi giờ ghi sổ thực tế; giữ nguyên dữ liệu lịch sử.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF COL_LENGTH(N'dbo.PhieuKho',N'NgayThucHien') IS NULL
 EXEC(N'ALTER TABLE dbo.PhieuKho ADD NgayThucHien date NULL;');
COMMIT;
