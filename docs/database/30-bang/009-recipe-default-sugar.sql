-- Bổ sung mức đường mặc định, không xóa/sửa dữ liệu đã chốt của đơn cũ.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF COL_LENGTH(N'dbo.CongThuc',N'PhanTramDuongMacDinh') IS NULL
BEGIN
 EXEC(N'ALTER TABLE dbo.CongThuc ADD PhanTramDuongMacDinh int NOT NULL CONSTRAINT DF_CongThuc_PhanTramDuongMacDinh DEFAULT(50) WITH VALUES;');
 EXEC(N'ALTER TABLE dbo.CongThuc WITH CHECK ADD CONSTRAINT CK_CongThuc_DuongMacDinh CHECK (PhanTramDuongMacDinh IN (0,25,50,75,100));');
END;
COMMIT;
