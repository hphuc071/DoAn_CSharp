-- Đã chốt bỏ giới hạn một ca mở theo nhân viên/máy. Không xóa hoặc tự đóng ca nào.
SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;
SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF;
BEGIN TRANSACTION;
IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.CaLamViec') AND name=N'UX_Ca_MayDangMo')
 DROP INDEX UX_Ca_MayDangMo ON dbo.CaLamViec;
IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.CaLamViec') AND name=N'UX_Ca_NhanVienDangMo')
 DROP INDEX UX_Ca_NhanVienDangMo ON dbo.CaLamViec;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.CaLamViec') AND name=N'IX_Ca_MayDangMo')
 CREATE INDEX IX_Ca_MayDangMo ON dbo.CaLamViec(MaMay) WHERE KetThucLuc IS NULL;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.CaLamViec') AND name=N'IX_Ca_NhanVienDangMo')
 CREATE INDEX IX_Ca_NhanVienDangMo ON dbo.CaLamViec(MaNhanVien) WHERE KetThucLuc IS NULL;
COMMIT;
