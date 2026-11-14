-- Nâng cấp 37 -> 30 bảng. Chạy sau backup CHECKSUM; không xóa lịch sử đơn/tiền/ca.
SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;
SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF;
IF OBJECT_ID(N'dbo.GiuChoNguyenLieu',N'U') IS NULL
BEGIN
 IF COL_LENGTH(N'dbo.NguyenLieuChiTietDonHang',N'SoLuongGiuCho') IS NULL
    THROW 51100,N'Schema không phải phiên bản 37 hoặc 30 được hỗ trợ.',1;
 RETURN;
END;
EXEC(N'BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N''FourChang.Schema30'',@LockMode=''Exclusive'',@LockOwner=''Transaction'',@LockTimeout=30000;
 IF @lock<0 THROW 51101,N''Không lấy được khóa nâng cấp.'',1;
 IF EXISTS(SELECT 1 FROM dbo.DanhMucMon WHERE TenDanhMuc NOT IN(N''Cà phê'',N''Trà'',N''Đá xay''))
    THROW 51102,N''Có danh mục ngoài ba loại cố định; cần đối chiếu trước chuyển đổi.'',1;
 IF EXISTS(SELECT 1 FROM dbo.NguyenLieu n JOIN dbo.DonViTinh u ON n.MaDonViCoSo=u.MaDonVi WHERE u.KyHieu NOT IN(''g'',''ml''))
    THROW 51103,N''Có đơn vị ngoài g/ml; không tự đoán quy đổi.'',1;
 IF EXISTS(SELECT 1 FROM dbo.ChiTietPhieuKho WHERE HeSoQuyDoiDaChot<>1)
    THROW 51104,N''Phiếu cũ có hệ số khác 1; cần chuyển số lượng snapshot trước.'',1;
 IF EXISTS(SELECT 1 FROM dbo.NguyenLieuChiTietDonHang s FULL JOIN dbo.GiuChoNguyenLieu h ON s.MaChiTietDonHang=h.MaChiTietDonHang AND s.MaNguyenLieu=h.MaNguyenLieu WHERE s.MaChiTietDonHang IS NULL OR h.MaChiTietDonHang IS NULL)
    THROW 51105,N''Snapshot và giữ chỗ không khớp; dừng để đối soát.'',1;
 DECLARE @orders bigint=(SELECT COUNT_BIG(*) FROM dbo.DonHang),@payments bigint=(SELECT COUNT_BIG(*) FROM dbo.ThanhToan),
         @paid decimal(38,0)=(SELECT COALESCE(SUM(CAST(SoTienDaXacMinh AS decimal(38,0))),0) FROM dbo.ThanhToan),
         @stock decimal(38,4)=(SELECT COALESCE(SUM(CAST(TonThucTe AS decimal(38,4))),0) FROM dbo.TonKho);
EXEC(N''ALTER TABLE dbo.Mon ADD LoaiMon nvarchar(100) NULL;'');
EXEC(N''UPDATE m SET LoaiMon=c.TenDanhMuc,DaCongBo=CASE WHEN c.DaCongBo=1 AND c.DangSuDung=1 THEN m.DaCongBo ELSE 0 END FROM dbo.Mon m JOIN dbo.DanhMucMon c ON c.MaDanhMuc=m.MaDanhMuc; ALTER TABLE dbo.Mon ALTER COLUMN LoaiMon nvarchar(100) NOT NULL;'');
EXEC(N''ALTER TABLE dbo.NguyenLieu ADD DonViCoSo varchar(2) NULL;'');
EXEC(N''UPDATE n SET DonViCoSo=u.KyHieu FROM dbo.NguyenLieu n JOIN dbo.DonViTinh u ON u.MaDonVi=n.MaDonViCoSo; ALTER TABLE dbo.NguyenLieu ALTER COLUMN DonViCoSo varchar(2) NOT NULL;'');
DISABLE TRIGGER dbo.TR_NguyenLieuChiTietDonHang_BatBien ON dbo.NguyenLieuChiTietDonHang;
EXEC(N''ALTER TABLE dbo.NguyenLieuChiTietDonHang ADD [SoLuongGiuCho] decimal(18,4) NOT NULL CONSTRAINT [DF_NLDon_SoLuongGiuCho] DEFAULT 0;'');
EXEC(N''ALTER TABLE dbo.NguyenLieuChiTietDonHang ADD [SoLuongDaTieuHao] decimal(18,4) NOT NULL CONSTRAINT [DF_NLDon_SoLuongDaTieuHao] DEFAULT 0;'');
EXEC(N''ALTER TABLE dbo.NguyenLieuChiTietDonHang ADD [SoLuongDaGiaiPhong] decimal(18,4) NOT NULL CONSTRAINT [DF_NLDon_SoLuongDaGiaiPhong] DEFAULT 0;'');
EXEC(N''ALTER TABLE dbo.NguyenLieuChiTietDonHang ADD [HetHanLuc] datetime2(3) NULL;'');
EXEC(N''ALTER TABLE dbo.NguyenLieuChiTietDonHang ADD [TrangThai] varchar(20) NOT NULL CONSTRAINT [DF_NLDon_TrangThai] DEFAULT ''''RESERVED'''';'');
EXEC(N''ALTER TABLE dbo.NguyenLieuChiTietDonHang ADD [ThoiDiemCapNhat] datetime2(3) NOT NULL CONSTRAINT [DF_NLDon_ThoiDiemCapNhat] DEFAULT SYSUTCDATETIME();'');
EXEC(N''ALTER TABLE dbo.NguyenLieuChiTietDonHang ADD [PhienBanDuLieu] rowversion NOT NULL;'');
EXEC(N''UPDATE s SET SoLuongGiuCho=h.SoLuongGiuCho,SoLuongDaTieuHao=h.SoLuongDaTieuHao,SoLuongDaGiaiPhong=h.SoLuongDaGiaiPhong,HetHanLuc=h.HetHanLuc,TrangThai=h.TrangThai,ThoiDiemCapNhat=h.ThoiDiemCapNhat FROM dbo.NguyenLieuChiTietDonHang s JOIN dbo.GiuChoNguyenLieu h ON s.MaChiTietDonHang=h.MaChiTietDonHang AND s.MaNguyenLieu=h.MaNguyenLieu;'');
CREATE TABLE #RemoveTables(Name sysname PRIMARY KEY);
INSERT #RemoveTables VALUES (N''CauHinhMonThem''),(N''DanhMucMon''),(N''DonViTinh''),(N''GiuChoNguyenLieu''),(N''QuyDoiDonViNguyenLieu''),(N''ThietBiPOS''),(N''TuyChonPhaCheHopLe'');
CREATE TABLE #RemoveColumns(TableName sysname,ColumnName sysname);
INSERT #RemoveColumns VALUES (N''DiaChiKhachHang'',N''ViDo''),(N''DiaChiKhachHang'',N''KinhDo''),(N''DonHang'',N''ViDo''),(N''DonHang'',N''KinhDo''),(N''DonHang'',N''KhoangCachGiaoKm''),(N''DonHang'',N''HanBaoPhi''),(N''Ban'',N''MaBamQR''),(N''Ban'',N''PhienBanQR''),(N''Ban'',N''QRHetHanLuc''),(N''CaLamViec'',N''LyDoChenhLech''),(N''ThanhToan'',N''TienKhachDua''),(N''ChiTietPhieuKho'',N''MaDonViNhap''),(N''ChiTietPhieuKho'',N''HeSoQuyDoiDaChot''),(N''Mon'',N''MaDanhMuc''),(N''NguyenLieu'',N''MaDonViCoSo'');
-- Loại ràng buộc chỉ phụ thuộc bảng/cột đã bỏ; ràng buộc khác giữ nguyên.
 DECLARE @ddl nvarchar(max)=N'''';
 SELECT @ddl+=N''ALTER TABLE ''+QUOTENAME(OBJECT_SCHEMA_NAME(f.parent_object_id))+N''.''+QUOTENAME(OBJECT_NAME(f.parent_object_id))+N'' DROP CONSTRAINT ''+QUOTENAME(f.name)+N'';''
 FROM sys.foreign_keys f WHERE OBJECT_NAME(f.parent_object_id) IN(SELECT Name FROM #RemoveTables) OR OBJECT_NAME(f.referenced_object_id) IN(SELECT Name FROM #RemoveTables);
 EXEC sys.sp_executesql @ddl; SET @ddl=N'''';
 SELECT @ddl+=N''ALTER TABLE dbo.''+QUOTENAME(OBJECT_NAME(c.parent_object_id))+N'' DROP CONSTRAINT ''+QUOTENAME(c.name)+N'';''
 FROM sys.check_constraints c WHERE EXISTS(SELECT 1 FROM #RemoveColumns r WHERE r.TableName=OBJECT_NAME(c.parent_object_id) AND c.definition LIKE N''%''+r.ColumnName+N''%'');
 EXEC sys.sp_executesql @ddl; SET @ddl=N'''';
 SELECT @ddl+=N''ALTER TABLE dbo.''+QUOTENAME(OBJECT_NAME(d.parent_object_id))+N'' DROP CONSTRAINT ''+QUOTENAME(d.name)+N'';''
 FROM sys.default_constraints d JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id JOIN #RemoveColumns r ON r.TableName=OBJECT_NAME(c.object_id) AND r.ColumnName=c.name;
 EXEC sys.sp_executesql @ddl; SET @ddl=N'''';
 SELECT @ddl+=CASE WHEN i.is_unique_constraint=1 THEN N''ALTER TABLE dbo.''+QUOTENAME(OBJECT_NAME(i.object_id))+N'' DROP CONSTRAINT ''+QUOTENAME(i.name)+N'';'' ELSE N''DROP INDEX ''+QUOTENAME(i.name)+N'' ON dbo.''+QUOTENAME(OBJECT_NAME(i.object_id))+N'';'' END
 FROM sys.indexes i WHERE i.is_primary_key=0 AND EXISTS(SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id JOIN #RemoveColumns r ON r.TableName=OBJECT_NAME(c.object_id) AND r.ColumnName=c.name WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id);
 EXEC sys.sp_executesql @ddl;
ALTER TABLE dbo.[DiaChiKhachHang] DROP COLUMN [ViDo];
ALTER TABLE dbo.[DiaChiKhachHang] DROP COLUMN [KinhDo];
ALTER TABLE dbo.[DonHang] DROP COLUMN [ViDo];
ALTER TABLE dbo.[DonHang] DROP COLUMN [KinhDo];
ALTER TABLE dbo.[DonHang] DROP COLUMN [KhoangCachGiaoKm];
ALTER TABLE dbo.[DonHang] DROP COLUMN [HanBaoPhi];
ALTER TABLE dbo.[Ban] DROP COLUMN [MaBamQR];
ALTER TABLE dbo.[Ban] DROP COLUMN [PhienBanQR];
ALTER TABLE dbo.[Ban] DROP COLUMN [QRHetHanLuc];
ALTER TABLE dbo.[CaLamViec] DROP COLUMN [LyDoChenhLech];
ALTER TABLE dbo.[ThanhToan] DROP COLUMN [TienKhachDua];
ALTER TABLE dbo.[ChiTietPhieuKho] DROP COLUMN [MaDonViNhap];
ALTER TABLE dbo.[ChiTietPhieuKho] DROP COLUMN [HeSoQuyDoiDaChot];
ALTER TABLE dbo.[Mon] DROP COLUMN [MaDanhMuc];
ALTER TABLE dbo.[NguyenLieu] DROP COLUMN [MaDonViCoSo];
DROP TABLE dbo.[CauHinhMonThem];
DROP TABLE dbo.[DanhMucMon];
DROP TABLE dbo.[DonViTinh];
DROP TABLE dbo.[GiuChoNguyenLieu];
DROP TABLE dbo.[QuyDoiDonViNguyenLieu];
DROP TABLE dbo.[ThietBiPOS];
DROP TABLE dbo.[TuyChonPhaCheHopLe];
EXEC(N''ALTER TABLE dbo.Mon ADD CONSTRAINT UQ_Mon_1 UNIQUE(LoaiMon,TenMon); ALTER TABLE dbo.Mon WITH CHECK ADD CONSTRAINT CK_Mon_Loai CHECK (LoaiMon IN(N''''Cà phê'''',N''''Trà'''',N''''Đá xay'''')); ALTER TABLE dbo.NguyenLieu WITH CHECK ADD CONSTRAINT CK_NguyenLieu_DonVi CHECK (DonViCoSo IN(''''g'''',''''ml''''));'');
EXEC(N''ALTER TABLE dbo.NguyenLieuChiTietDonHang WITH CHECK ADD CONSTRAINT CK_NguyenLieuDon_1 CHECK (SoLuongGiuCho >= 0 AND SoLuongDaTieuHao >= 0 AND SoLuongDaGiaiPhong >= 0);'');
EXEC(N''ALTER TABLE dbo.NguyenLieuChiTietDonHang WITH CHECK ADD CONSTRAINT CK_NguyenLieuDon_2 CHECK (SoLuongDaTieuHao + SoLuongDaGiaiPhong <= SoLuongGiuCho);'');
EXEC(N''ALTER TABLE dbo.NguyenLieuChiTietDonHang WITH CHECK ADD CONSTRAINT CK_NguyenLieuDon_3 CHECK (TrangThai IN (''''RESERVED'''',''''PARTIAL'''',''''CONSUMED'''',''''RELEASED'''',''''EXPIRED''''));'');
EXEC(N''CREATE OR ALTER TRIGGER dbo.TR_NguyenLieuChiTietDonHang_BatBien
ON dbo.NguyenLieuChiTietDonHang AFTER UPDATE, DELETE AS
BEGIN
 SET NOCOUNT ON;
 -- Giữ nguyên công thức đã chốt; chỉ các lượng giữ/tiêu hao/giải phóng được cập nhật.
 IF EXISTS(SELECT MaChiTietDonHang,MaNguyenLieu,TenNguyenLieuDaChot,DonViCoSoDaChot,DinhMucMoiLy,ThoiDiemTao FROM deleted
           EXCEPT SELECT MaChiTietDonHang,MaNguyenLieu,TenNguyenLieuDaChot,DonViCoSoDaChot,DinhMucMoiLy,ThoiDiemTao FROM inserted)
    THROW 51001,N''''Snapshot nguyên liệu không được sửa hoặc xóa.'''',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON i.MaChiTietDonHang=d.MaChiTietDonHang AND i.MaNguyenLieu=d.MaNguyenLieu
           WHERE i.SoLuongGiuCho<>d.SoLuongGiuCho OR i.SoLuongDaTieuHao<d.SoLuongDaTieuHao OR i.SoLuongDaGiaiPhong<d.SoLuongDaGiaiPhong)
    THROW 51001,N''''Không giảm lịch sử tiêu hao/giải phóng hoặc thay số lượng đã giữ.'''',1;
END'');
ENABLE TRIGGER dbo.TR_NguyenLieuChiTietDonHang_BatBien ON dbo.NguyenLieuChiTietDonHang;
 IF @orders<>(SELECT COUNT_BIG(*) FROM dbo.DonHang) OR @payments<>(SELECT COUNT_BIG(*) FROM dbo.ThanhToan) OR @paid<>(SELECT COALESCE(SUM(CAST(SoTienDaXacMinh AS decimal(38,0))),0) FROM dbo.ThanhToan) OR @stock<>(SELECT COALESCE(SUM(CAST(TonThucTe AS decimal(38,4))),0) FROM dbo.TonKho)
 THROW 51106,N''Kiểm tra bảo toàn đơn/tiền/tồn thất bại.'',1;
 DROP TABLE #RemoveTables; DROP TABLE #RemoveColumns;
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
');
