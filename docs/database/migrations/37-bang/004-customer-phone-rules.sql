-- Yêu cầu mới: điện thoại khách hàng đúng 10 chữ số ASCII, giữ số 0 ở đầu.
-- Không tự cắt/chuyển dữ liệu cũ. Nếu có dữ liệu sai thì dừng để chủ dự án quyết định.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF EXISTS(SELECT 1 FROM dbo.TaiKhoanKhachHang WHERE SoDienThoaiChuanHoa IS NOT NULL AND (DATALENGTH(SoDienThoaiChuanHoa)<>10 OR SoDienThoaiChuanHoa COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9]%'))
 OR EXISTS(SELECT 1 FROM dbo.DiaChiKhachHang WHERE DATALENGTH(DienThoaiNguoiNhan)<>10 OR DienThoaiNguoiNhan COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9]%')
 THROW 51110,N'Có số điện thoại cũ không đủ đúng 10 chữ số. Dừng nâng cấp; không tự thay đổi dữ liệu.',1;
IF OBJECT_ID(N'dbo.CK_KhachHang_DienThoai10',N'C') IS NULL ALTER TABLE dbo.[TaiKhoanKhachHang] WITH CHECK ADD CONSTRAINT [CK_KhachHang_DienThoai10] CHECK (SoDienThoaiChuanHoa IS NULL OR (DATALENGTH(SoDienThoaiChuanHoa)=10 AND SoDienThoaiChuanHoa COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9]%'));
IF OBJECT_ID(N'dbo.CK_DiaChiKhachHang_DienThoai10',N'C') IS NULL ALTER TABLE dbo.[DiaChiKhachHang] WITH CHECK ADD CONSTRAINT [CK_DiaChiKhachHang_DienThoai10] CHECK (DATALENGTH(DienThoaiNguoiNhan)=10 AND DienThoaiNguoiNhan COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9]%');
COMMIT TRANSACTION;
