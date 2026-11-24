-- Chỉ đổi mã hiển thị; giữ PK/FK, số tiền, giao dịch ngân hàng và snapshot lịch sử.
-- Chạy bằng tài khoản nâng cấp, trong lúc API dừng. Bootstrap backup trước khi gọi.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'FourChang.DisplayCodes',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000;
IF @lock < 0 THROW 51000,N'Không khóa được bộ cấp mã.',1;
IF OBJECT_ID(N'dbo.DisplayCode_WEB',N'SO') IS NULL
BEGIN
    CREATE TABLE #Codes(TenBang sysname, Khoa sysname, Cot sysname, Ma bigint, Cu varchar(50), Moi varchar(50), Prefix varchar(5));
    INSERT #Codes SELECT 'DonHang','MaDonHang','MaDonHienThi',MaDonHang,MaDonHienThi,
        KenhDatHang+'-'+CONVERT(varchar(20),ROW_NUMBER() OVER(PARTITION BY KenhDatHang ORDER BY MaDonHang)),KenhDatHang FROM dbo.DonHang;
    INSERT #Codes SELECT 'HoaDon','MaHoaDon','SoHoaDon',MaHoaDon,SoHoaDon,
        CASE WHEN LoaiHoaDon='ADJUSTMENT' THEN 'DC' ELSE 'HD' END+'-'+CONVERT(varchar(20),ROW_NUMBER() OVER(PARTITION BY CASE WHEN LoaiHoaDon='ADJUSTMENT' THEN 'DC' ELSE 'HD' END ORDER BY MaHoaDon)),
        CASE WHEN LoaiHoaDon='ADJUSTMENT' THEN 'DC' ELSE 'HD' END FROM dbo.HoaDon;
    INSERT #Codes SELECT 'PhieuKho','MaPhieuKho','SoPhieuKho',MaPhieuKho,SoPhieuKho,
        CASE LoaiPhieu WHEN 'RECEIPT' THEN 'PN' WHEN 'ISSUE' THEN 'PX' ELSE 'PK' END+'-'+CONVERT(varchar(20),ROW_NUMBER() OVER(PARTITION BY CASE LoaiPhieu WHEN 'RECEIPT' THEN 'PN' WHEN 'ISSUE' THEN 'PX' ELSE 'PK' END ORDER BY MaPhieuKho)),
        CASE LoaiPhieu WHEN 'RECEIPT' THEN 'PN' WHEN 'ISSUE' THEN 'PX' ELSE 'PK' END FROM dbo.PhieuKho;
    INSERT #Codes SELECT 'CaLamViec','MaCaLamViec','MaCaHienThi',MaCaLamViec,MaCaHienThi,'CA-'+CONVERT(varchar(20),ROW_NUMBER() OVER(ORDER BY MaCaLamViec)),'CA' FROM dbo.CaLamViec;
    INSERT #Codes SELECT 'HoSoHoanTien','MaHoanTien','MaHoanTienHienThi',MaHoanTien,MaHoanTienHienThi,'HT-'+CONVERT(varchar(20),ROW_NUMBER() OVER(ORDER BY MaHoanTien)),'HT' FROM dbo.HoSoHoanTien;
    -- Lưu ánh xạ để tra mã cũ; không sửa/xóa nhật ký cũ hoặc snapshot hóa đơn.
    INSERT dbo.NhatKyHeThong(LoaiTacNhan,TenNguoiThucHienDaChot,HanhDong,LoaiDoiTuong,MaDoiTuong,DuLieuSauJson,KetQua,MaLienKetXuLy,PhatSinhLuc)
    VALUES('SYSTEM',N'Nâng cấp mã hiển thị','DISPLAY_CODES_RENUMBERED','Database','013',(SELECT TenBang,Ma,Cu,Moi FROM #Codes FOR JSON PATH),'SUCCEEDED',NEWID(),SYSUTCDATETIME());
    -- Ngoại lệ bảo trì chỉ đổi cột mã dưới transaction; rollback khôi phục cả trigger nếu lỗi.
    DISABLE TRIGGER dbo.TR_HoaDon_DaPhatHanh ON dbo.HoaDon;
    DISABLE TRIGGER dbo.TR_PhieuKho_DaGhiSo ON dbo.PhieuKho;
    DECLARE @table sysname,@key sysname,@column sysname,@sql nvarchar(max);
    DECLARE tables CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT TenBang,Khoa,Cot FROM #Codes;
    OPEN tables; FETCH NEXT FROM tables INTO @table,@key,@column;
    WHILE @@FETCH_STATUS=0
    BEGIN
        -- Hai lượt tránh va chạm unique giữa mã cũ và mã mới.
        SET @sql=N'UPDATE t SET '+QUOTENAME(@column)+N'=''~013-''+CONVERT(varchar(20),t.'+QUOTENAME(@key)+N') FROM dbo.'+QUOTENAME(@table)+N' t; UPDATE t SET '+QUOTENAME(@column)+N'=c.Moi FROM dbo.'+QUOTENAME(@table)+N' t JOIN #Codes c ON c.TenBang=@name AND c.Ma=t.'+QUOTENAME(@key)+N';';
        EXEC sys.sp_executesql @sql,N'@name sysname',@name=@table;
        FETCH NEXT FROM tables INTO @table,@key,@column;
    END
    CLOSE tables; DEALLOCATE tables;
    ENABLE TRIGGER dbo.TR_HoaDon_DaPhatHanh ON dbo.HoaDon;
    ENABLE TRIGGER dbo.TR_PhieuKho_DaGhiSo ON dbo.PhieuKho;
    DECLARE @prefix varchar(5),@next bigint;
    DECLARE prefixes CURSOR LOCAL FAST_FORWARD FOR SELECT v.p FROM (VALUES('WEB'),('POS'),('HD'),('DC'),('PN'),('PX'),('PK'),('CA'),('HT'))v(p);
    OPEN prefixes; FETCH NEXT FROM prefixes INTO @prefix;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SELECT @next=COUNT_BIG(*)+1 FROM #Codes WHERE Prefix=@prefix;
        SET @sql=N'CREATE SEQUENCE dbo.DisplayCode_'+@prefix+N' AS bigint START WITH '+CONVERT(varchar(20),@next)+N' INCREMENT BY 1 NO CACHE;';
        EXEC(@sql); FETCH NEXT FROM prefixes INTO @prefix;
    END
    CLOSE prefixes; DEALLOCATE prefixes;
END
COMMIT;