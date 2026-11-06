-- Địa chỉ minh họa được tạo cho đồ án, không đại diện địa chỉ thật của người có tên này.
-- Chỉ điền hồ sơ mẫu còn trống; giữ mọi địa chỉ người dùng đã nhập. Không đoán tọa độ.
SET XACT_ABORT ON;
BEGIN TRY
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource='FourChang:SampleContactAddresses', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
IF @lock<0 THROW 51090, 'Sample address lock timeout', 1;
DECLARE @samples TABLE(TenNhanVien nvarchar(120),DienThoaiNV varchar(20),DiaChiNV nvarchar(500),TenKhachHang nvarchar(120),TenDangNhap nvarchar(100),DiaChiKH nvarchar(500),Nhan nvarchar(50),GhiChu nvarchar(500));
INSERT @samples VALUES
(N'Nguyễn Hoàng Minh',N'0998100001',N'12 Nguyễn Văn Cừ, TP. Hồ Chí Minh',N'Nguyễn Tuấn Kiệt',N'mau.kh01',N'24 Nguyễn Đình Chiểu, TP. Hồ Chí Minh',N'Nhà riêng',N'Gọi người nhận trước khi giao.'),
(N'Trần Ngọc Anh',N'0998100002',N'28 Lê Văn Sỹ, TP. Hồ Chí Minh',N'Trần Bảo Ngọc',N'mau.kh02',N'48 Trần Quang Diệu, TP. Hồ Chí Minh',N'Nhà riêng',N'Gọi người nhận trước khi giao.'),
(N'Lê Đức Huy',N'0998100003',N'45 Phan Xích Long, TP. Hồ Chí Minh',N'Lê Minh Quân',N'mau.kh03',N'63 Nguyễn Thị Minh Khai, TP. Hồ Chí Minh',N'Văn phòng',N'Nhận tại quầy lễ tân.'),
(N'Phạm Khánh Linh',N'0998100004',N'16 Hoàng Văn Thụ, TP. Hồ Chí Minh',N'Phạm Phương Thảo',N'mau.kh04',N'17 Điện Biên Phủ, TP. Hồ Chí Minh',N'Nhà riêng',N'Gọi người nhận trước khi giao.'),
(N'Hoàng Gia Bảo',N'0998100005',N'82 Nguyễn Kiệm, TP. Hồ Chí Minh',N'Hoàng Nhật Nam',N'mau.kh05',N'92 Bạch Đằng, TP. Hồ Chí Minh',N'Nhà riêng',N'Gọi người nhận trước khi giao.'),
(N'Vũ Thanh Hà',N'0998100006',N'35 Đinh Tiên Hoàng, TP. Hồ Chí Minh',N'Vũ Quỳnh Chi',N'mau.kh06',N'31 Trường Sa, TP. Hồ Chí Minh',N'Nhà riêng',N'Gọi người nhận trước khi giao.'),
(N'Đặng Quang Phúc',N'0998100007',N'64 Xô Viết Nghệ Tĩnh, TP. Hồ Chí Minh',N'Đặng Hải Đăng',N'mau.kh07',N'76 Hoàng Sa, TP. Hồ Chí Minh',N'Văn phòng',N'Nhận tại quầy lễ tân.'),
(N'Bùi Thảo Vy',N'0998100008',N'21 Võ Thị Sáu, TP. Hồ Chí Minh',N'Bùi Ngọc Hân',N'mau.kh08',N'29 Lê Văn Sỹ, TP. Hồ Chí Minh',N'Nhà riêng',N'Gọi người nhận trước khi giao.'),
(N'Đỗ Minh Khang',N'0998100009',N'37 Lê Quang Định, TP. Hồ Chí Minh',N'Đỗ Quốc Bảo',N'mau.kh09',N'54 Phan Xích Long, TP. Hồ Chí Minh',N'Nhà riêng',N'Gọi người nhận trước khi giao.'),
(N'Ngô Diệu Nhi',N'0998100010',N'56 Phan Đăng Lưu, TP. Hồ Chí Minh',N'Ngô Thanh Trúc',N'mau.kh10',N'88 Nguyễn Văn Đậu, TP. Hồ Chí Minh',N'Nhà riêng',N'Gọi người nhận trước khi giao.');
IF EXISTS(SELECT 1 FROM @samples s WHERE NOT EXISTS(SELECT 1 FROM dbo.NhanVien n WHERE n.HoTen=s.TenNhanVien AND n.SoDienThoai=s.DienThoaiNV)
    OR NOT EXISTS(SELECT 1 FROM dbo.TaiKhoanKhachHang k WHERE k.HoTen=s.TenKhachHang AND k.TenDangNhapChuanHoa=s.TenDangNhap))
    THROW 51093, N'Bộ hồ sơ mẫu không khớp mã nhận diện/tên. Dừng, không sửa hồ sơ khác.', 1;
DECLARE @employees TABLE(MaNhanVien bigint);
DECLARE @addresses TABLE(MaDiaChi bigint,MaKhachHang bigint);
UPDATE n WITH(UPDLOCK,HOLDLOCK) SET DiaChiLienHe=s.DiaChiNV,ThoiDiemCapNhat=SYSUTCDATETIME()
OUTPUT INSERTED.MaNhanVien INTO @employees
FROM dbo.NhanVien n JOIN @samples s ON n.HoTen=s.TenNhanVien AND n.SoDienThoai=s.DienThoaiNV
WHERE NULLIF(LTRIM(RTRIM(n.DiaChiLienHe)),N'') IS NULL;
INSERT dbo.DiaChiKhachHang(MaKhachHang,NhanDiaChi,TenNguoiNhan,DienThoaiNguoiNhan,DiaChiChiTiet,ViDo,KinhDo,GhiChuGiaoHang,LaDiaChiMacDinh,DangSuDung)
OUTPUT INSERTED.MaDiaChi,INSERTED.MaKhachHang INTO @addresses
SELECT k.MaKhachHang,s.Nhan,k.HoTen,k.SoDienThoaiChuanHoa,s.DiaChiKH,NULL,NULL,s.GhiChu,1,1
FROM dbo.TaiKhoanKhachHang k WITH(UPDLOCK,HOLDLOCK) JOIN @samples s ON k.TenDangNhapChuanHoa=s.TenDangNhap AND k.HoTen=s.TenKhachHang
WHERE NOT EXISTS(SELECT 1 FROM dbo.DiaChiKhachHang a WITH(UPDLOCK,HOLDLOCK) WHERE a.MaKhachHang=k.MaKhachHang);
-- Tăng version hồ sơ như thao tác địa chỉ trên Web/Admin, không để cửa sổ cũ ghi đè dữ liệu mới.
UPDATE dbo.TaiKhoanKhachHang SET ThoiDiemCapNhat=SYSUTCDATETIME() WHERE MaKhachHang IN(SELECT MaKhachHang FROM @addresses);
DECLARE @employeeCount int=(SELECT COUNT(*) FROM @employees), @addressCount int=(SELECT COUNT(*) FROM @addresses);
IF @employeeCount+@addressCount>0
BEGIN
    DECLARE @summary nvarchar(max)=(SELECT @employeeCount AS NhanVienDaBoSung,@addressCount AS DiaChiGiaoDaThem FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
    INSERT dbo.NhatKyHeThong(MaLienKetXuLy,LoaiTacNhan,TenNguoiThucHienDaChot,HanhDong,LoaiDoiTuong,DuLieuSauJson,LyDo,KetQua,PhatSinhLuc)
    VALUES(NEWID(),'SYSTEM',N'Nạp địa chỉ mẫu','SEED_CONTACT_ADDRESSES','Database',@summary,N'Bổ sung địa chỉ cho bộ 10 nhân viên và 10 khách hàng theo yêu cầu.','SUCCEEDED',SYSUTCDATETIME());
END;
COMMIT;
SELECT @employeeCount AS NhanVienDaBoSung,@addressCount AS DiaChiGiaoDaThem;
SELECT n.MaNhanVien,n.HoTen,n.DiaChiLienHe FROM dbo.NhanVien n JOIN @samples s ON n.HoTen=s.TenNhanVien AND n.SoDienThoai=s.DienThoaiNV ORDER BY n.MaNhanVien;
SELECT k.MaKhachHang,k.HoTen,a.MaDiaChi,a.NhanDiaChi,a.TenNguoiNhan,a.DienThoaiNguoiNhan,a.DiaChiChiTiet,a.LaDiaChiMacDinh,a.DangSuDung
FROM dbo.TaiKhoanKhachHang k JOIN @samples s ON k.TenDangNhapChuanHoa=s.TenDangNhap AND k.HoTen=s.TenKhachHang
JOIN dbo.DiaChiKhachHang a ON a.MaKhachHang=k.MaKhachHang ORDER BY k.MaKhachHang,a.MaDiaChi;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
