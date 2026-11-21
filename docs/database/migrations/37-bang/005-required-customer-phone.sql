-- Điện thoại khách hàng bắt buộc: không tự sinh số cho khách cũ.
SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;
SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF;
BEGIN TRANSACTION;
IF EXISTS(SELECT 1 FROM dbo.TaiKhoanKhachHang WHERE SoDienThoaiChuanHoa IS NULL OR DATALENGTH(SoDienThoaiChuanHoa)<>10 OR SoDienThoaiChuanHoa COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9]%')
 THROW 51115,N'Có khách thiếu số điện thoại hoặc sai định dạng. Chưa nâng cấp, không tự điền số.',1;
IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TaiKhoanKhachHang') AND name=N'SoDienThoaiChuanHoa' AND is_nullable=1)
BEGIN
 -- Gỡ rồi tạo lại đúng index/ràng buộc hiện có trong cùng transaction để ALTER COLUMN an toàn.
 DROP INDEX [UX_KhachHang_DienThoai] ON dbo.TaiKhoanKhachHang;
 ALTER TABLE dbo.TaiKhoanKhachHang DROP CONSTRAINT [CK_KhachHang_DienThoai10];
 ALTER TABLE dbo.TaiKhoanKhachHang DROP CONSTRAINT [CK_TaiKhoanKhachHang_3];
 ALTER TABLE dbo.TaiKhoanKhachHang ALTER COLUMN SoDienThoaiChuanHoa varchar(20) NOT NULL;
 ALTER TABLE dbo.TaiKhoanKhachHang WITH CHECK ADD CONSTRAINT [CK_TaiKhoanKhachHang_3] CHECK (EmailChuanHoa IS NOT NULL OR SoDienThoaiChuanHoa IS NOT NULL);
 ALTER TABLE dbo.TaiKhoanKhachHang WITH CHECK ADD CONSTRAINT [CK_KhachHang_DienThoai10] CHECK (DATALENGTH(SoDienThoaiChuanHoa)=10 AND SoDienThoaiChuanHoa COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9]%');
 CREATE UNIQUE INDEX [UX_KhachHang_DienThoai] ON dbo.TaiKhoanKhachHang(SoDienThoaiChuanHoa) WHERE SoDienThoaiChuanHoa IS NOT NULL;
END;
COMMIT TRANSACTION;
