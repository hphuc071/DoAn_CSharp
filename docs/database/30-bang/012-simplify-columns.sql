-- Schema30: 423 -> 402 columns; archive removed values in the existing audit journal.
SET XACT_ABORT ON;
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRY
BEGIN TRANSACTION;
DECLARE @lock int; EXEC @lock=sys.sp_getapplock @Resource=N'FourChang:ColumnSimplification', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
IF @lock<0 THROW 51200,N'Không khóa được migration tinh gọn thuộc tính.',1;
DECLARE @legacyColumns int = CASE WHEN COL_LENGTH(N'dbo.PhienDangNhap',N'ThongTinThietBi') IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN COL_LENGTH(N'dbo.PhienDangNhap',N'HoatDongLanCuoiLuc') IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN COL_LENGTH(N'dbo.PhienDangNhap',N'LoaiUngDung') IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN COL_LENGTH(N'dbo.NguyenLieu',N'TonAnToan') IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN COL_LENGTH(N'dbo.ChiTietCongThuc',N'TyLeHaoHut') IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN COL_LENGTH(N'dbo.ChiTietCongThuc',N'ThoiDiemTao') IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN COL_LENGTH(N'dbo.ChiTietCongThuc',N'ThoiDiemCapNhat') IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN COL_LENGTH(N'dbo.ChiTietCongThuc',N'PhienBanDuLieu') IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN COL_LENGTH(N'dbo.MonThemChiTietDonHang',N'ThoiDiemTao') IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN COL_LENGTH(N'dbo.MonThemChiTietDonHang',N'ThoiDiemCapNhat') IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN COL_LENGTH(N'dbo.MonThemChiTietDonHang',N'PhienBanDuLieu') IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN COL_LENGTH(N'dbo.Ban',N'TenKhuVuc') IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN COL_LENGTH(N'dbo.Ban',N'SoChoNgoi') IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN COL_LENGTH(N'dbo.DonHang',N'MaDonLienQuan') IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN COL_LENGTH(N'dbo.HoaDon',N'MaSoThueNguoiMua') IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN COL_LENGTH(N'dbo.PhieuKho',N'MocKiemKe') IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN COL_LENGTH(N'dbo.ChiTietPhieuKho',N'TonSoDaChot') IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN COL_LENGTH(N'dbo.ChiTietPhieuKho',N'SoLuongThucDem') IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN COL_LENGTH(N'dbo.ChiTietPhieuKho',N'PhienBanTonKhoDaChot') IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN COL_LENGTH(N'dbo.BienDongKho',N'ThoiDiemTao') IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN COL_LENGTH(N'dbo.NhatKyHeThong',N'ThoiDiemTao') IS NOT NULL THEN 1 ELSE 0 END;
IF @legacyColumns=0 BEGIN COMMIT TRANSACTION; RETURN; END;
IF @legacyColumns<>21 THROW 51201,N'Schema tinh gọn dở dang hoặc khác thiết kế; dừng để bảo vệ dữ liệu.',1;
EXEC(N'IF EXISTS(SELECT 1 FROM dbo.[NhatKyHeThong]) INSERT dbo.NhatKyHeThong(MaLienKetXuLy,LoaiTacNhan,TenNguoiThucHienDaChot,HanhDong,LoaiDoiTuong,MaDoiTuong,DuLieuTruocJson,LyDo,KetQua,PhatSinhLuc) SELECT NEWID(),''SYSTEM'',N''Migration 012'',''SCHEMA_COLUMNS_ARCHIVE'',N''NhatKyHeThong'',N''012'',(SELECT [MaNhatKy],[ThoiDiemTao] FROM dbo.[NhatKyHeThong] FOR JSON PATH,INCLUDE_NULL_VALUES),N''Lưu giá trị thuộc tính trước khi tinh gọn schema'',''SUCCEEDED'',SYSUTCDATETIME();');
EXEC(N'IF EXISTS(SELECT 1 FROM dbo.[PhienDangNhap]) INSERT dbo.NhatKyHeThong(MaLienKetXuLy,LoaiTacNhan,TenNguoiThucHienDaChot,HanhDong,LoaiDoiTuong,MaDoiTuong,DuLieuTruocJson,LyDo,KetQua,PhatSinhLuc) SELECT NEWID(),''SYSTEM'',N''Migration 012'',''SCHEMA_COLUMNS_ARCHIVE'',N''PhienDangNhap'',N''012'',(SELECT [MaPhienDangNhap],[ThongTinThietBi],[HoatDongLanCuoiLuc],[LoaiUngDung] FROM dbo.[PhienDangNhap] FOR JSON PATH,INCLUDE_NULL_VALUES),N''Lưu giá trị thuộc tính trước khi tinh gọn schema'',''SUCCEEDED'',SYSUTCDATETIME();');
EXEC(N'IF EXISTS(SELECT 1 FROM dbo.[NguyenLieu]) INSERT dbo.NhatKyHeThong(MaLienKetXuLy,LoaiTacNhan,TenNguoiThucHienDaChot,HanhDong,LoaiDoiTuong,MaDoiTuong,DuLieuTruocJson,LyDo,KetQua,PhatSinhLuc) SELECT NEWID(),''SYSTEM'',N''Migration 012'',''SCHEMA_COLUMNS_ARCHIVE'',N''NguyenLieu'',N''012'',(SELECT [MaNguyenLieu],[TonAnToan] FROM dbo.[NguyenLieu] FOR JSON PATH,INCLUDE_NULL_VALUES),N''Lưu giá trị thuộc tính trước khi tinh gọn schema'',''SUCCEEDED'',SYSUTCDATETIME();');
EXEC(N'IF EXISTS(SELECT 1 FROM dbo.[ChiTietCongThuc]) INSERT dbo.NhatKyHeThong(MaLienKetXuLy,LoaiTacNhan,TenNguoiThucHienDaChot,HanhDong,LoaiDoiTuong,MaDoiTuong,DuLieuTruocJson,LyDo,KetQua,PhatSinhLuc) SELECT NEWID(),''SYSTEM'',N''Migration 012'',''SCHEMA_COLUMNS_ARCHIVE'',N''ChiTietCongThuc'',N''012'',(SELECT [MaCongThuc],[MaNguyenLieu],[LoaiDinhMuc],[TyLeHaoHut],[ThoiDiemTao],[ThoiDiemCapNhat],[PhienBanDuLieu] FROM dbo.[ChiTietCongThuc] FOR JSON PATH,INCLUDE_NULL_VALUES),N''Lưu giá trị thuộc tính trước khi tinh gọn schema'',''SUCCEEDED'',SYSUTCDATETIME();');
EXEC(N'IF EXISTS(SELECT 1 FROM dbo.[MonThemChiTietDonHang]) INSERT dbo.NhatKyHeThong(MaLienKetXuLy,LoaiTacNhan,TenNguoiThucHienDaChot,HanhDong,LoaiDoiTuong,MaDoiTuong,DuLieuTruocJson,LyDo,KetQua,PhatSinhLuc) SELECT NEWID(),''SYSTEM'',N''Migration 012'',''SCHEMA_COLUMNS_ARCHIVE'',N''MonThemChiTietDonHang'',N''012'',(SELECT [MaChiTietDonHang],[MaMonThem],[ThoiDiemTao],[ThoiDiemCapNhat],[PhienBanDuLieu] FROM dbo.[MonThemChiTietDonHang] FOR JSON PATH,INCLUDE_NULL_VALUES),N''Lưu giá trị thuộc tính trước khi tinh gọn schema'',''SUCCEEDED'',SYSUTCDATETIME();');
EXEC(N'IF EXISTS(SELECT 1 FROM dbo.[Ban]) INSERT dbo.NhatKyHeThong(MaLienKetXuLy,LoaiTacNhan,TenNguoiThucHienDaChot,HanhDong,LoaiDoiTuong,MaDoiTuong,DuLieuTruocJson,LyDo,KetQua,PhatSinhLuc) SELECT NEWID(),''SYSTEM'',N''Migration 012'',''SCHEMA_COLUMNS_ARCHIVE'',N''Ban'',N''012'',(SELECT [MaBan],[TenKhuVuc],[SoChoNgoi] FROM dbo.[Ban] FOR JSON PATH,INCLUDE_NULL_VALUES),N''Lưu giá trị thuộc tính trước khi tinh gọn schema'',''SUCCEEDED'',SYSUTCDATETIME();');
EXEC(N'IF EXISTS(SELECT 1 FROM dbo.[DonHang]) INSERT dbo.NhatKyHeThong(MaLienKetXuLy,LoaiTacNhan,TenNguoiThucHienDaChot,HanhDong,LoaiDoiTuong,MaDoiTuong,DuLieuTruocJson,LyDo,KetQua,PhatSinhLuc) SELECT NEWID(),''SYSTEM'',N''Migration 012'',''SCHEMA_COLUMNS_ARCHIVE'',N''DonHang'',N''012'',(SELECT [MaDonHang],[MaDonLienQuan] FROM dbo.[DonHang] FOR JSON PATH,INCLUDE_NULL_VALUES),N''Lưu giá trị thuộc tính trước khi tinh gọn schema'',''SUCCEEDED'',SYSUTCDATETIME();');
EXEC(N'IF EXISTS(SELECT 1 FROM dbo.[HoaDon]) INSERT dbo.NhatKyHeThong(MaLienKetXuLy,LoaiTacNhan,TenNguoiThucHienDaChot,HanhDong,LoaiDoiTuong,MaDoiTuong,DuLieuTruocJson,LyDo,KetQua,PhatSinhLuc) SELECT NEWID(),''SYSTEM'',N''Migration 012'',''SCHEMA_COLUMNS_ARCHIVE'',N''HoaDon'',N''012'',(SELECT [MaHoaDon],[MaSoThueNguoiMua] FROM dbo.[HoaDon] FOR JSON PATH,INCLUDE_NULL_VALUES),N''Lưu giá trị thuộc tính trước khi tinh gọn schema'',''SUCCEEDED'',SYSUTCDATETIME();');
EXEC(N'IF EXISTS(SELECT 1 FROM dbo.[PhieuKho]) INSERT dbo.NhatKyHeThong(MaLienKetXuLy,LoaiTacNhan,TenNguoiThucHienDaChot,HanhDong,LoaiDoiTuong,MaDoiTuong,DuLieuTruocJson,LyDo,KetQua,PhatSinhLuc) SELECT NEWID(),''SYSTEM'',N''Migration 012'',''SCHEMA_COLUMNS_ARCHIVE'',N''PhieuKho'',N''012'',(SELECT [MaPhieuKho],[MocKiemKe] FROM dbo.[PhieuKho] FOR JSON PATH,INCLUDE_NULL_VALUES),N''Lưu giá trị thuộc tính trước khi tinh gọn schema'',''SUCCEEDED'',SYSUTCDATETIME();');
EXEC(N'IF EXISTS(SELECT 1 FROM dbo.[ChiTietPhieuKho]) INSERT dbo.NhatKyHeThong(MaLienKetXuLy,LoaiTacNhan,TenNguoiThucHienDaChot,HanhDong,LoaiDoiTuong,MaDoiTuong,DuLieuTruocJson,LyDo,KetQua,PhatSinhLuc) SELECT NEWID(),''SYSTEM'',N''Migration 012'',''SCHEMA_COLUMNS_ARCHIVE'',N''ChiTietPhieuKho'',N''012'',(SELECT [MaDongPhieuKho],[TonSoDaChot],[SoLuongThucDem],[PhienBanTonKhoDaChot] FROM dbo.[ChiTietPhieuKho] FOR JSON PATH,INCLUDE_NULL_VALUES),N''Lưu giá trị thuộc tính trước khi tinh gọn schema'',''SUCCEEDED'',SYSUTCDATETIME();');
EXEC(N'IF EXISTS(SELECT 1 FROM dbo.[BienDongKho]) INSERT dbo.NhatKyHeThong(MaLienKetXuLy,LoaiTacNhan,TenNguoiThucHienDaChot,HanhDong,LoaiDoiTuong,MaDoiTuong,DuLieuTruocJson,LyDo,KetQua,PhatSinhLuc) SELECT NEWID(),''SYSTEM'',N''Migration 012'',''SCHEMA_COLUMNS_ARCHIVE'',N''BienDongKho'',N''012'',(SELECT [MaBienDongKho],[ThoiDiemTao] FROM dbo.[BienDongKho] FOR JSON PATH,INCLUDE_NULL_VALUES),N''Lưu giá trị thuộc tính trước khi tinh gọn schema'',''SUCCEEDED'',SYSUTCDATETIME();');
-- Constraint/index names differ between a fresh schema30 and an upgraded schema37.
DECLARE @dependencies nvarchar(max)=N'';
SELECT @dependencies+=N'ALTER TABLE dbo.DonHang DROP CONSTRAINT '+QUOTENAME(f.name)+N';'
FROM sys.foreign_keys f JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id
JOIN sys.columns c ON c.object_id=fc.parent_object_id AND c.column_id=fc.parent_column_id
WHERE f.parent_object_id=OBJECT_ID(N'dbo.DonHang') AND c.name=N'MaDonLienQuan';
SELECT @dependencies+=N'DROP INDEX '+QUOTENAME(i.name)+N' ON dbo.DonHang;'
FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
WHERE i.object_id=OBJECT_ID(N'dbo.DonHang') AND c.name=N'MaDonLienQuan';
EXEC sys.sp_executesql @dependencies;
ALTER TABLE dbo.[NguyenLieu] DROP CONSTRAINT [CK_NguyenLieu_1];
ALTER TABLE dbo.[ChiTietCongThuc] DROP CONSTRAINT [CK_ChiTietCongThuc_3];
ALTER TABLE dbo.[Ban] DROP CONSTRAINT [CK_Ban_1];
SET @dependencies=N'';
SELECT @dependencies+=N'ALTER TABLE dbo.ChiTietPhieuKho DROP CONSTRAINT '+QUOTENAME(name)+N';'
FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.ChiTietPhieuKho')
AND (definition LIKE N'%TonSoDaChot%' OR definition LIKE N'%SoLuongThucDem%');
EXEC sys.sp_executesql @dependencies;
DECLARE @default_PhienDangNhap_ThongTinThietBi sysname;
SELECT @default_PhienDangNhap_ThongTinThietBi=d.name FROM sys.default_constraints d JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id WHERE d.parent_object_id=OBJECT_ID(N'dbo.PhienDangNhap') AND c.name=N'ThongTinThietBi';
DECLARE @sql_PhienDangNhap_ThongTinThietBi nvarchar(max)=N'ALTER TABLE dbo.[PhienDangNhap] DROP CONSTRAINT '+QUOTENAME(@default_PhienDangNhap_ThongTinThietBi); IF @default_PhienDangNhap_ThongTinThietBi IS NOT NULL EXEC(@sql_PhienDangNhap_ThongTinThietBi);
ALTER TABLE dbo.[PhienDangNhap] DROP COLUMN [ThongTinThietBi];
DECLARE @default_PhienDangNhap_HoatDongLanCuoiLuc sysname;
SELECT @default_PhienDangNhap_HoatDongLanCuoiLuc=d.name FROM sys.default_constraints d JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id WHERE d.parent_object_id=OBJECT_ID(N'dbo.PhienDangNhap') AND c.name=N'HoatDongLanCuoiLuc';
DECLARE @sql_PhienDangNhap_HoatDongLanCuoiLuc nvarchar(max)=N'ALTER TABLE dbo.[PhienDangNhap] DROP CONSTRAINT '+QUOTENAME(@default_PhienDangNhap_HoatDongLanCuoiLuc); IF @default_PhienDangNhap_HoatDongLanCuoiLuc IS NOT NULL EXEC(@sql_PhienDangNhap_HoatDongLanCuoiLuc);
ALTER TABLE dbo.[PhienDangNhap] DROP COLUMN [HoatDongLanCuoiLuc];
DECLARE @default_PhienDangNhap_LoaiUngDung sysname;
SELECT @default_PhienDangNhap_LoaiUngDung=d.name FROM sys.default_constraints d JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id WHERE d.parent_object_id=OBJECT_ID(N'dbo.PhienDangNhap') AND c.name=N'LoaiUngDung';
DECLARE @sql_PhienDangNhap_LoaiUngDung nvarchar(max)=N'ALTER TABLE dbo.[PhienDangNhap] DROP CONSTRAINT '+QUOTENAME(@default_PhienDangNhap_LoaiUngDung); IF @default_PhienDangNhap_LoaiUngDung IS NOT NULL EXEC(@sql_PhienDangNhap_LoaiUngDung);
ALTER TABLE dbo.[PhienDangNhap] DROP COLUMN [LoaiUngDung];
DECLARE @default_NguyenLieu_TonAnToan sysname;
SELECT @default_NguyenLieu_TonAnToan=d.name FROM sys.default_constraints d JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id WHERE d.parent_object_id=OBJECT_ID(N'dbo.NguyenLieu') AND c.name=N'TonAnToan';
DECLARE @sql_NguyenLieu_TonAnToan nvarchar(max)=N'ALTER TABLE dbo.[NguyenLieu] DROP CONSTRAINT '+QUOTENAME(@default_NguyenLieu_TonAnToan); IF @default_NguyenLieu_TonAnToan IS NOT NULL EXEC(@sql_NguyenLieu_TonAnToan);
ALTER TABLE dbo.[NguyenLieu] DROP COLUMN [TonAnToan];
DECLARE @default_ChiTietCongThuc_TyLeHaoHut sysname;
SELECT @default_ChiTietCongThuc_TyLeHaoHut=d.name FROM sys.default_constraints d JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id WHERE d.parent_object_id=OBJECT_ID(N'dbo.ChiTietCongThuc') AND c.name=N'TyLeHaoHut';
DECLARE @sql_ChiTietCongThuc_TyLeHaoHut nvarchar(max)=N'ALTER TABLE dbo.[ChiTietCongThuc] DROP CONSTRAINT '+QUOTENAME(@default_ChiTietCongThuc_TyLeHaoHut); IF @default_ChiTietCongThuc_TyLeHaoHut IS NOT NULL EXEC(@sql_ChiTietCongThuc_TyLeHaoHut);
ALTER TABLE dbo.[ChiTietCongThuc] DROP COLUMN [TyLeHaoHut];
DECLARE @default_ChiTietCongThuc_ThoiDiemTao sysname;
SELECT @default_ChiTietCongThuc_ThoiDiemTao=d.name FROM sys.default_constraints d JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id WHERE d.parent_object_id=OBJECT_ID(N'dbo.ChiTietCongThuc') AND c.name=N'ThoiDiemTao';
DECLARE @sql_ChiTietCongThuc_ThoiDiemTao nvarchar(max)=N'ALTER TABLE dbo.[ChiTietCongThuc] DROP CONSTRAINT '+QUOTENAME(@default_ChiTietCongThuc_ThoiDiemTao); IF @default_ChiTietCongThuc_ThoiDiemTao IS NOT NULL EXEC(@sql_ChiTietCongThuc_ThoiDiemTao);
ALTER TABLE dbo.[ChiTietCongThuc] DROP COLUMN [ThoiDiemTao];
DECLARE @default_ChiTietCongThuc_ThoiDiemCapNhat sysname;
SELECT @default_ChiTietCongThuc_ThoiDiemCapNhat=d.name FROM sys.default_constraints d JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id WHERE d.parent_object_id=OBJECT_ID(N'dbo.ChiTietCongThuc') AND c.name=N'ThoiDiemCapNhat';
DECLARE @sql_ChiTietCongThuc_ThoiDiemCapNhat nvarchar(max)=N'ALTER TABLE dbo.[ChiTietCongThuc] DROP CONSTRAINT '+QUOTENAME(@default_ChiTietCongThuc_ThoiDiemCapNhat); IF @default_ChiTietCongThuc_ThoiDiemCapNhat IS NOT NULL EXEC(@sql_ChiTietCongThuc_ThoiDiemCapNhat);
ALTER TABLE dbo.[ChiTietCongThuc] DROP COLUMN [ThoiDiemCapNhat];
DECLARE @default_ChiTietCongThuc_PhienBanDuLieu sysname;
SELECT @default_ChiTietCongThuc_PhienBanDuLieu=d.name FROM sys.default_constraints d JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id WHERE d.parent_object_id=OBJECT_ID(N'dbo.ChiTietCongThuc') AND c.name=N'PhienBanDuLieu';
DECLARE @sql_ChiTietCongThuc_PhienBanDuLieu nvarchar(max)=N'ALTER TABLE dbo.[ChiTietCongThuc] DROP CONSTRAINT '+QUOTENAME(@default_ChiTietCongThuc_PhienBanDuLieu); IF @default_ChiTietCongThuc_PhienBanDuLieu IS NOT NULL EXEC(@sql_ChiTietCongThuc_PhienBanDuLieu);
ALTER TABLE dbo.[ChiTietCongThuc] DROP COLUMN [PhienBanDuLieu];
DECLARE @default_MonThemChiTietDonHang_ThoiDiemTao sysname;
SELECT @default_MonThemChiTietDonHang_ThoiDiemTao=d.name FROM sys.default_constraints d JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id WHERE d.parent_object_id=OBJECT_ID(N'dbo.MonThemChiTietDonHang') AND c.name=N'ThoiDiemTao';
DECLARE @sql_MonThemChiTietDonHang_ThoiDiemTao nvarchar(max)=N'ALTER TABLE dbo.[MonThemChiTietDonHang] DROP CONSTRAINT '+QUOTENAME(@default_MonThemChiTietDonHang_ThoiDiemTao); IF @default_MonThemChiTietDonHang_ThoiDiemTao IS NOT NULL EXEC(@sql_MonThemChiTietDonHang_ThoiDiemTao);
ALTER TABLE dbo.[MonThemChiTietDonHang] DROP COLUMN [ThoiDiemTao];
DECLARE @default_MonThemChiTietDonHang_ThoiDiemCapNhat sysname;
SELECT @default_MonThemChiTietDonHang_ThoiDiemCapNhat=d.name FROM sys.default_constraints d JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id WHERE d.parent_object_id=OBJECT_ID(N'dbo.MonThemChiTietDonHang') AND c.name=N'ThoiDiemCapNhat';
DECLARE @sql_MonThemChiTietDonHang_ThoiDiemCapNhat nvarchar(max)=N'ALTER TABLE dbo.[MonThemChiTietDonHang] DROP CONSTRAINT '+QUOTENAME(@default_MonThemChiTietDonHang_ThoiDiemCapNhat); IF @default_MonThemChiTietDonHang_ThoiDiemCapNhat IS NOT NULL EXEC(@sql_MonThemChiTietDonHang_ThoiDiemCapNhat);
ALTER TABLE dbo.[MonThemChiTietDonHang] DROP COLUMN [ThoiDiemCapNhat];
DECLARE @default_MonThemChiTietDonHang_PhienBanDuLieu sysname;
SELECT @default_MonThemChiTietDonHang_PhienBanDuLieu=d.name FROM sys.default_constraints d JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id WHERE d.parent_object_id=OBJECT_ID(N'dbo.MonThemChiTietDonHang') AND c.name=N'PhienBanDuLieu';
DECLARE @sql_MonThemChiTietDonHang_PhienBanDuLieu nvarchar(max)=N'ALTER TABLE dbo.[MonThemChiTietDonHang] DROP CONSTRAINT '+QUOTENAME(@default_MonThemChiTietDonHang_PhienBanDuLieu); IF @default_MonThemChiTietDonHang_PhienBanDuLieu IS NOT NULL EXEC(@sql_MonThemChiTietDonHang_PhienBanDuLieu);
ALTER TABLE dbo.[MonThemChiTietDonHang] DROP COLUMN [PhienBanDuLieu];
DECLARE @default_Ban_TenKhuVuc sysname;
SELECT @default_Ban_TenKhuVuc=d.name FROM sys.default_constraints d JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id WHERE d.parent_object_id=OBJECT_ID(N'dbo.Ban') AND c.name=N'TenKhuVuc';
DECLARE @sql_Ban_TenKhuVuc nvarchar(max)=N'ALTER TABLE dbo.[Ban] DROP CONSTRAINT '+QUOTENAME(@default_Ban_TenKhuVuc); IF @default_Ban_TenKhuVuc IS NOT NULL EXEC(@sql_Ban_TenKhuVuc);
ALTER TABLE dbo.[Ban] DROP COLUMN [TenKhuVuc];
DECLARE @default_Ban_SoChoNgoi sysname;
SELECT @default_Ban_SoChoNgoi=d.name FROM sys.default_constraints d JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id WHERE d.parent_object_id=OBJECT_ID(N'dbo.Ban') AND c.name=N'SoChoNgoi';
DECLARE @sql_Ban_SoChoNgoi nvarchar(max)=N'ALTER TABLE dbo.[Ban] DROP CONSTRAINT '+QUOTENAME(@default_Ban_SoChoNgoi); IF @default_Ban_SoChoNgoi IS NOT NULL EXEC(@sql_Ban_SoChoNgoi);
ALTER TABLE dbo.[Ban] DROP COLUMN [SoChoNgoi];
DECLARE @default_DonHang_MaDonLienQuan sysname;
SELECT @default_DonHang_MaDonLienQuan=d.name FROM sys.default_constraints d JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id WHERE d.parent_object_id=OBJECT_ID(N'dbo.DonHang') AND c.name=N'MaDonLienQuan';
DECLARE @sql_DonHang_MaDonLienQuan nvarchar(max)=N'ALTER TABLE dbo.[DonHang] DROP CONSTRAINT '+QUOTENAME(@default_DonHang_MaDonLienQuan); IF @default_DonHang_MaDonLienQuan IS NOT NULL EXEC(@sql_DonHang_MaDonLienQuan);
ALTER TABLE dbo.[DonHang] DROP COLUMN [MaDonLienQuan];
DECLARE @default_HoaDon_MaSoThueNguoiMua sysname;
SELECT @default_HoaDon_MaSoThueNguoiMua=d.name FROM sys.default_constraints d JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id WHERE d.parent_object_id=OBJECT_ID(N'dbo.HoaDon') AND c.name=N'MaSoThueNguoiMua';
DECLARE @sql_HoaDon_MaSoThueNguoiMua nvarchar(max)=N'ALTER TABLE dbo.[HoaDon] DROP CONSTRAINT '+QUOTENAME(@default_HoaDon_MaSoThueNguoiMua); IF @default_HoaDon_MaSoThueNguoiMua IS NOT NULL EXEC(@sql_HoaDon_MaSoThueNguoiMua);
ALTER TABLE dbo.[HoaDon] DROP COLUMN [MaSoThueNguoiMua];
DECLARE @default_PhieuKho_MocKiemKe sysname;
SELECT @default_PhieuKho_MocKiemKe=d.name FROM sys.default_constraints d JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id WHERE d.parent_object_id=OBJECT_ID(N'dbo.PhieuKho') AND c.name=N'MocKiemKe';
DECLARE @sql_PhieuKho_MocKiemKe nvarchar(max)=N'ALTER TABLE dbo.[PhieuKho] DROP CONSTRAINT '+QUOTENAME(@default_PhieuKho_MocKiemKe); IF @default_PhieuKho_MocKiemKe IS NOT NULL EXEC(@sql_PhieuKho_MocKiemKe);
ALTER TABLE dbo.[PhieuKho] DROP COLUMN [MocKiemKe];
DECLARE @default_ChiTietPhieuKho_TonSoDaChot sysname;
SELECT @default_ChiTietPhieuKho_TonSoDaChot=d.name FROM sys.default_constraints d JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id WHERE d.parent_object_id=OBJECT_ID(N'dbo.ChiTietPhieuKho') AND c.name=N'TonSoDaChot';
DECLARE @sql_ChiTietPhieuKho_TonSoDaChot nvarchar(max)=N'ALTER TABLE dbo.[ChiTietPhieuKho] DROP CONSTRAINT '+QUOTENAME(@default_ChiTietPhieuKho_TonSoDaChot); IF @default_ChiTietPhieuKho_TonSoDaChot IS NOT NULL EXEC(@sql_ChiTietPhieuKho_TonSoDaChot);
ALTER TABLE dbo.[ChiTietPhieuKho] DROP COLUMN [TonSoDaChot];
DECLARE @default_ChiTietPhieuKho_SoLuongThucDem sysname;
SELECT @default_ChiTietPhieuKho_SoLuongThucDem=d.name FROM sys.default_constraints d JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id WHERE d.parent_object_id=OBJECT_ID(N'dbo.ChiTietPhieuKho') AND c.name=N'SoLuongThucDem';
DECLARE @sql_ChiTietPhieuKho_SoLuongThucDem nvarchar(max)=N'ALTER TABLE dbo.[ChiTietPhieuKho] DROP CONSTRAINT '+QUOTENAME(@default_ChiTietPhieuKho_SoLuongThucDem); IF @default_ChiTietPhieuKho_SoLuongThucDem IS NOT NULL EXEC(@sql_ChiTietPhieuKho_SoLuongThucDem);
ALTER TABLE dbo.[ChiTietPhieuKho] DROP COLUMN [SoLuongThucDem];
DECLARE @default_ChiTietPhieuKho_PhienBanTonKhoDaChot sysname;
SELECT @default_ChiTietPhieuKho_PhienBanTonKhoDaChot=d.name FROM sys.default_constraints d JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id WHERE d.parent_object_id=OBJECT_ID(N'dbo.ChiTietPhieuKho') AND c.name=N'PhienBanTonKhoDaChot';
DECLARE @sql_ChiTietPhieuKho_PhienBanTonKhoDaChot nvarchar(max)=N'ALTER TABLE dbo.[ChiTietPhieuKho] DROP CONSTRAINT '+QUOTENAME(@default_ChiTietPhieuKho_PhienBanTonKhoDaChot); IF @default_ChiTietPhieuKho_PhienBanTonKhoDaChot IS NOT NULL EXEC(@sql_ChiTietPhieuKho_PhienBanTonKhoDaChot);
ALTER TABLE dbo.[ChiTietPhieuKho] DROP COLUMN [PhienBanTonKhoDaChot];
DECLARE @default_BienDongKho_ThoiDiemTao sysname;
SELECT @default_BienDongKho_ThoiDiemTao=d.name FROM sys.default_constraints d JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id WHERE d.parent_object_id=OBJECT_ID(N'dbo.BienDongKho') AND c.name=N'ThoiDiemTao';
DECLARE @sql_BienDongKho_ThoiDiemTao nvarchar(max)=N'ALTER TABLE dbo.[BienDongKho] DROP CONSTRAINT '+QUOTENAME(@default_BienDongKho_ThoiDiemTao); IF @default_BienDongKho_ThoiDiemTao IS NOT NULL EXEC(@sql_BienDongKho_ThoiDiemTao);
ALTER TABLE dbo.[BienDongKho] DROP COLUMN [ThoiDiemTao];
DECLARE @default_NhatKyHeThong_ThoiDiemTao sysname;
SELECT @default_NhatKyHeThong_ThoiDiemTao=d.name FROM sys.default_constraints d JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id WHERE d.parent_object_id=OBJECT_ID(N'dbo.NhatKyHeThong') AND c.name=N'ThoiDiemTao';
DECLARE @sql_NhatKyHeThong_ThoiDiemTao nvarchar(max)=N'ALTER TABLE dbo.[NhatKyHeThong] DROP CONSTRAINT '+QUOTENAME(@default_NhatKyHeThong_ThoiDiemTao); IF @default_NhatKyHeThong_ThoiDiemTao IS NOT NULL EXEC(@sql_NhatKyHeThong_ThoiDiemTao);
ALTER TABLE dbo.[NhatKyHeThong] DROP COLUMN [ThoiDiemTao];
ALTER TABLE dbo.NguyenLieu WITH CHECK ADD CONSTRAINT CK_NguyenLieu_1 CHECK(NguongCanhBao>=0);
COMMIT TRANSACTION;
END TRY
BEGIN CATCH
IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
THROW;
END CATCH;
