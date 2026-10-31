using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using FourChang.Infrastructure.Configuration;
using FourChang.Infrastructure.Security;
namespace FourChang.Infrastructure.Persistence;

public static class DatabaseChecks
{
    public static async Task RunAsync()
    {
        await using var connection=new SqlConnection(DatabaseSettings.ConnectionString());await connection.OpenAsync();
        await DatabaseBootstrap.ValidateAsync(connection);Console.WriteLine("PASS: 34 bảng, đúng cột và 72 FK hoạt động.");
        await using var db=new CafeDbContext(new DbContextOptionsBuilder<CafeDbContext>().UseSqlServer(DatabaseSettings.ConnectionString()).Options);
        if(db.Model.GetEntityTypes().Count()!=34)throw new InvalidOperationException("EF Core ánh xạ sai số bảng.");
        if(!Passwords.Verify("Test-password-123!",Passwords.Hash("Test-password-123!"))||Passwords.Verify("Wrong",Passwords.Hash("Test-password-123!")))throw new InvalidOperationException("Password hashing failed.");
        Console.WriteLine("PASS: EF Core 34 entity; băm mật khẩu xác minh đúng/sai.");
        await Reject(connection,"FK khách hàng không tồn tại", "INSERT dbo.DiaChiKhachHang(MaKhachHang,TenNguoiNhan,DienThoaiNguoiNhan,DiaChiChiTiet,LaDiaChiMacDinh,DangSuDung) VALUES(9223372036854775800,N'Test','0900000000',N'Test',0,1)");
        await Reject(connection,"Không trùng danh mục", "INSERT dbo.DanhMucMon(TenDanhMuc,ThuTuHienThi,DaCongBo,DangSuDung) SELECT TOP(1) TenDanhMuc,0,1,1 FROM dbo.DanhMucMon");
        await Reject(connection,"Giá cấu hình không âm", "UPDATE dbo.CauHinhMon SET GiaBan=-1 WHERE MaCauHinhMon=(SELECT MIN(MaCauHinhMon) FROM dbo.CauHinhMon)");
        await Reject(connection,"Phiên đăng nhập có đúng một chủ sở hữu", "INSERT dbo.PhienDangNhap(MaLamMoiDaBam,PhamViPhien,LoaiUngDung,HetHanLuc) VALUES(CRYPT_GEN_RANDOM(32),'FULL','WEB',DATEADD(hour,1,SYSUTCDATETIME()))");
        await Reject(connection,"Tồn kho không âm", "UPDATE dbo.TonKho SET TonThucTe=-1 WHERE MaNguyenLieu=(SELECT MIN(MaNguyenLieu) FROM dbo.TonKho)");
        await Reject(connection,"Giữ chỗ không vượt tồn thực tế", "UPDATE dbo.TonKho SET TongGiuCho=TonThucTe+1 WHERE MaNguyenLieu=(SELECT MIN(MaNguyenLieu) FROM dbo.TonKho)");
        await Reject(connection,"Công thức có đúng một chủ sở hữu", "INSERT dbo.CongThuc(SoPhienBan,TenCongThuc,TrangThai,MaQuanLyTao) VALUES(1,N'Test','DRAFT',1)");
        await Reject(connection,"FK ghép giữ chỗ phải có snapshot dòng món", "INSERT dbo.GiuChoNguyenLieu(MaChiTietDonHang,MaNguyenLieu,SoLuongGiuCho,SoLuongDaTieuHao,SoLuongDaGiaiPhong,TrangThai) VALUES(9223372036854775800,9223372036854775800,1,0,0,'RESERVED')");
        await Reject(connection,"Audit Log bất biến", "UPDATE dbo.NhatKyHeThong SET KetQua='CHANGED' WHERE MaNhatKy=(SELECT MIN(MaNhatKy) FROM dbo.NhatKyHeThong)");
        await Reject(connection,"Phiếu đã ghi sổ bất biến", "UPDATE dbo.PhieuKho SET LyDo=N'Test' WHERE MaPhieuKho=(SELECT MIN(MaPhieuKho) FROM dbo.PhieuKho WHERE TrangThai='POSTED')");
        await Reject(connection,"Không sửa định mức đã công bố", "UPDATE dbo.ChiTietCongThuc SET DinhMucCoSo=DinhMucCoSo+1 WHERE MaCongThuc=(SELECT MIN(MaCongThuc) FROM dbo.CongThuc WHERE TrangThai='ACTIVE')");
        await Reject(connection,"Đơn Web không thanh toán tiền mặt", "DECLARE @kh bigint,@dh bigint,@nv bigint,@ca bigint; INSERT dbo.NhanVien(HoTen,NgayVaoLam,TrangThai) VALUES(N'Test',SYSUTCDATETIME(),'ACTIVE'); SET @nv=SCOPE_IDENTITY(); INSERT dbo.CaLamViec(MaCaHienThi,MaNhanVien,TenCa,MaMay,BatDauLuc,TrangThai,TienMatDauCa) VALUES(LEFT(CONVERT(varchar(36),NEWID()),30),@nv,N'Sáng',CONVERT(varchar(36),NEWID()),SYSUTCDATETIME(),'OPEN',0); SET @ca=SCOPE_IDENTITY(); INSERT dbo.TaiKhoanKhachHang(TenDangNhapChuanHoa,MatKhauBam,TrangThai,BatBuocDoiMatKhau,SoLanDangNhapSai,HoTen,EmailChuanHoa) VALUES(CONVERT(varchar(36),NEWID()),'test','ACTIVE',0,0,N'Test',CONVERT(varchar(36),NEWID())+'@example.test'); SET @kh=SCOPE_IDENTITY(); INSERT dbo.DonHang(MaDonHienThi,MaKhachHang,KenhDatHang,HinhThucNhanMon,PhiGiaoHangBanDau,PhiGiaoHangHienTai,TrangThai) VALUES(LEFT(CONVERT(varchar(36),NEWID()),30),@kh,'WEB','TAKEAWAY',0,0,'PENDING_PAYMENT'); SET @dh=SCOPE_IDENTITY(); INSERT dbo.ThanhToan(MaThanhToanHienThi,MaDonHang,MaCaLamViec,PhuongThuc,SoTienYeuCau,SoTienDaXacMinh,TrangThai,MaThaoTac) VALUES(CONVERT(varchar(36),NEWID()),@dh, @ca,'CASH',1,0,'PENDING',NEWID());");
        await using(var transaction=(SqlTransaction)await connection.BeginTransactionAsync())
        {
            await using var command=connection.CreateCommand();command.Transaction=transaction;
            command.CommandText="INSERT dbo.NhanVien(HoTen,NgayVaoLam,TrangThai) VALUES(N'Nhân viên kiểm thử',SYSUTCDATETIME(),'ACTIVE'),(N'Nhân viên kiểm thử',SYSUTCDATETIME(),'ACTIVE')";
            await command.ExecuteNonQueryAsync();await transaction.RollbackAsync();Console.WriteLine("PASS: nhân viên có thể trùng tên; không tạo tài khoản nhân viên.");
        }

        await using(var transaction=await connection.BeginTransactionAsync())
        {
            var options=new DbContextOptionsBuilder<CafeDbContext>().UseSqlServer(connection).Options;
            await using var first=new CafeDbContext(options);await using var second=new CafeDbContext(options);
            await first.Database.UseTransactionAsync(transaction);await second.Database.UseTransactionAsync(transaction);
            var employee=new FourChang.Domain.Entities.NhanVien{HoTen="Nhân viên kiểm tra đồng thời",NgayVaoLam=DateTime.UtcNow,TrangThai="ACTIVE"};
            first.NhanVien.Add(employee);await first.SaveChangesAsync();
            var stale=await second.NhanVien.SingleAsync(x=>x.MaNhanVien==employee.MaNhanVien);
            employee.HoTen="Cập nhật thứ nhất";await first.SaveChangesAsync();stale.HoTen="Cập nhật cũ";
            var conflict=false;try{await second.SaveChangesAsync();}catch(DbUpdateConcurrencyException){conflict=true;}
            if(!conflict)throw new InvalidOperationException("Rowversion chưa chặn ghi đè dữ liệu cũ.");
            await transaction.RollbackAsync();Console.WriteLine("PASS: EF Core rowversion chặn ghi đè đồng thời; rollback dữ liệu thử.");
        }
        await using(var permission=connection.CreateCommand())
        {
            permission.CommandText="SELECT CASE WHEN IS_SRVROLEMEMBER('sysadmin')=0 AND HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','ALTER')=0 THEN 1 ELSE 0 END";
            if(Convert.ToInt32(await permission.ExecuteScalarAsync())!=1)throw new InvalidOperationException("Ứng dụng có quyền quản trị quá cao.");
            Console.WriteLine("PASS: ứng dụng không dùng sa, không có quyền ALTER database.");
        }
        Console.WriteLine("DATABASE TESTS PASSED. Mọi dữ liệu kiểm thử đều rollback, không tạo thêm bảng.");
    }
    private static async Task Reject(SqlConnection connection,string label,string sql)
    {
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync();
        var rejected=false;
        try{await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText=sql;await command.ExecuteNonQueryAsync();}
        catch(SqlException exception) when(exception.Number is 547 or 2601 or 2627 or >=51000 and <=51099){rejected=true;}
        finally{try{await transaction.RollbackAsync();}catch(InvalidOperationException){}catch(SqlException){}}
        if(!rejected)throw new InvalidOperationException("FAIL: "+label);
        Console.WriteLine("PASS: "+label);
    }
}
