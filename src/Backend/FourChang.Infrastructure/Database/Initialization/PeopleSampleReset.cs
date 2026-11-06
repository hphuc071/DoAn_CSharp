using System.Security.Cryptography;
using System.Text.Json;
using FourChang.Domain.Entities.Audit;
using FourChang.Infrastructure.Database.Context;
using FourChang.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FourChang.Infrastructure.Database.Initialization;

/// <summary>Công cụ bảo trì chủ động cho bộ mẫu 10/10/10; không gọi từ API HTTP hoặc khởi động thường.</summary>
public static class PeopleSampleReset
{
    public sealed record SampleShift(long MaCaLamViec, string MaCaHienThi, long MaNhanVien, string HoTen, string TenCa, string MaMay, string TrangThai);
    public sealed record ResetResult(int NhanVien, int KhachHang, int CaLamViec,
        IReadOnlyList<PeopleSampleSeed.SamplePerson> DanhSachNhanVien,
        IReadOnlyList<PeopleSampleSeed.SamplePerson> DanhSachKhachHang, IReadOnlyList<SampleShift> DanhSachCa);

    /// <summary>Chỉ cho xóa khi không có tham chiếu giao dịch; toàn bộ thay bộ mẫu và audit nằm trong một transaction.</summary>
    public static async Task<ResetResult> RunAsync(CafeDbContext db)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='FourChang:Workforce', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r<0 THROW 51090, 'Reset lock timeout', 1;");
        // Khóa cả ba bảng khi đổi bộ mẫu; không được có đăng ký/mở ca xen vào giữa xóa và nạp lại.
        await db.Database.ExecuteSqlRawAsync("SELECT COUNT_BIG(*) FROM dbo.NhanVien WITH(TABLOCKX,HOLDLOCK); SELECT COUNT_BIG(*) FROM dbo.TaiKhoanKhachHang WITH(TABLOCKX,HOLDLOCK); SELECT COUNT_BIG(*) FROM dbo.CaLamViec WITH(TABLOCKX,HOLDLOCK);");
        await CheckReferences(db);
        // Chỉ dọn dữ liệu thuộc ba nhóm được người dùng yêu cầu. Phiên Admin và nhật ký nhóm khác vẫn giữ nguyên.
        // Riêng trigger audit được tạm tắt trong transaction để xóa lịch sử mẫu có liên quan, rồi bật lại ngay.
        // Nếu có lỗi, rollback khôi phục cả dữ liệu lẫn trạng thái trigger; không tắt bất kỳ khóa ngoại nào.
        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID(N'dbo.TR_NhatKyHeThong_BatBien') AND parent_id=OBJECT_ID(N'dbo.NhatKyHeThong') AND is_disabled=0) THROW 51092, N'Audit protection must be enabled before sample reset', 1;
            DISABLE TRIGGER dbo.TR_NhatKyHeThong_BatBien ON dbo.NhatKyHeThong;
            DELETE FROM dbo.NhatKyHeThong WHERE MaKhachHang IS NOT NULL OR MaCaLamViec IS NOT NULL
                OR LoaiDoiTuong IN ('NhanVien','CaLamViec','TaiKhoanKhachHang','DiaChiKhachHang') OR HanhDong IN ('SEED_SAMPLE_PEOPLE','RESET_SAMPLE_PEOPLE');
            ENABLE TRIGGER dbo.TR_NhatKyHeThong_BatBien ON dbo.NhatKyHeThong;
            DELETE FROM dbo.PhienDangNhap WHERE MaKhachHang IS NOT NULL;
            DELETE FROM dbo.DiaChiKhachHang;
            DELETE FROM dbo.CaLamViec;
            DELETE FROM dbo.NhanVien;
            DELETE FROM dbo.TaiKhoanKhachHang;
            """);
        await InsertEmployees(db);
        await InsertCustomers(db);
        var shifts = await InsertShifts(db);
        var employees = await db.NhanVien.AsNoTracking().OrderBy(x => x.MaNhanVien)
            .Select(x => new PeopleSampleSeed.SamplePerson(x.MaNhanVien, x.HoTen, x.SoDienThoai!, null)).ToListAsync();
        var customers = await db.TaiKhoanKhachHang.AsNoTracking().OrderBy(x => x.MaKhachHang)
            .Select(x => new PeopleSampleSeed.SamplePerson(x.MaKhachHang, x.HoTen, x.SoDienThoaiChuanHoa!, x.TenDangNhapChuanHoa)).ToListAsync();
        var result = new ResetResult(employees.Count, customers.Count, shifts.Count, employees, customers, shifts);
        db.NhatKyHeThong.Add(new NhatKyHeThong
        {
            MaLienKetXuLy = Guid.NewGuid(),
            LoaiTacNhan = "SYSTEM",
            TenNguoiThucHienDaChot = "Thay bộ dữ liệu mẫu theo yêu cầu",
            HanhDong = "RESET_SAMPLE_PEOPLE",
            LoaiDoiTuong = "Database",
            DuLieuSauJson = JsonSerializer.Serialize(result),
            LyDo = "Nạp lại 10 nhân viên, 10 khách hàng và 10 ca, mã 1–10.",
            KetQua = "SUCCEEDED",
            PhatSinhLuc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        // Gán tường minh 1–10 tránh khác biệt RESEED trên bảng rỗng/chưa từng có dòng; lần thêm tiếp theo là 11.
        await db.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('dbo.NhanVien', RESEED, 10) WITH NO_INFOMSGS; DBCC CHECKIDENT ('dbo.TaiKhoanKhachHang', RESEED, 10) WITH NO_INFOMSGS; DBCC CHECKIDENT ('dbo.CaLamViec', RESEED, 10) WITH NO_INFOMSGS;");
        await tx.CommitAsync(); return result;
    }

    /// <summary>Đọc metadata FK thật và chặn mọi dữ liệu giao dịch, kể cả quan hệ mới được bổ sung về sau.</summary>
    private static async Task CheckReferences(CafeDbContext db)
    {
        var links = new List<(string Table, string Column)>();
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
            command.CommandText = """
                SELECT OBJECT_NAME(f.parent_object_id), COL_NAME(c.parent_object_id,c.parent_column_id)
                FROM sys.foreign_keys f JOIN sys.foreign_key_columns c ON c.constraint_object_id=f.object_id
                WHERE OBJECT_NAME(f.referenced_object_id) IN ('NhanVien','TaiKhoanKhachHang','CaLamViec')
                AND OBJECT_NAME(f.parent_object_id) NOT IN ('CaLamViec','PhienDangNhap','DiaChiKhachHang','NhatKyHeThong')
                """;
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) links.Add((reader.GetString(0), reader.GetString(1)));
        }
        foreach (var (table, column) in links)
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
            // Tên lấy từ metadata SQL Server, escape identifier; không ghép giá trị người dùng vào SQL.
            command.CommandText = $"SELECT COUNT_BIG(*) FROM dbo.[{table.Replace("]", "]]")}] WHERE [{column.Replace("]", "]]")}] IS NOT NULL";
            if (Convert.ToInt64(await command.ExecuteScalarAsync()) > 0)
                throw new InvalidOperationException($"Dừng thay dữ liệu mẫu: {table}.{column} đang tham chiếu hồ sơ/ca cũ. Không tự xóa giao dịch.");
        }
    }

    /// <summary>Nạp nhân viên mã nội bộ 1–10, không sinh username, mật khẩu hoặc mã hiển thị nhân viên riêng.</summary>
    private static async Task InsertEmployees(CafeDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.NhanVien ON;");
        try
        {
            for (var i = 1; i <= 10; i++)
            {
                var name = PeopleSampleSeed.EmployeeNames[i - 1]; var phone = $"099810{i:0000}"; var date = new DateTime(2026, 10, i);
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.NhanVien(MaNhanVien,HoTen,SoDienThoai,NgayVaoLam,TrangThai) VALUES({i},{name},{phone},{date},'ACTIVE');");
            }
        }
        finally { await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.NhanVien OFF;"); }
    }

    /// <summary>Nạp khách hàng mã 1–10, mật khẩu ngẫu nhiên được băm; không ghi bí mật vào log hoặc báo cáo.</summary>
    private static async Task InsertCustomers(CafeDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.TaiKhoanKhachHang ON;");
        try
        {
            for (var i = 1; i <= 10; i++)
            {
                var name = PeopleSampleSeed.CustomerNames[i - 1]; var phone = $"099820{i:0000}";
                var username = $"mau.kh{i:00}"; var email = $"khachhang.mau{i:00}@example.test";
                var passwordHash = Passwords.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.TaiKhoanKhachHang(MaKhachHang,TenDangNhapChuanHoa,MatKhauBam,TrangThai,BatBuocDoiMatKhau,SoLanDangNhapSai,HoTen,EmailChuanHoa,SoDienThoaiChuanHoa) VALUES({i},{username},{passwordHash},'ACTIVE',1,0,{name},{email},{phone});");
            }
        }
        finally { await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.TaiKhoanKhachHang OFF;"); }
    }

    /// <summary>Mỗi ca lịch sử gắn đúng nhân viên cùng mã; CLOSED không chặn nhân viên mở ca thật để thử POS.</summary>
    private static async Task<List<SampleShift>> InsertShifts(CafeDbContext db)
    {
        var result = new List<SampleShift>();
        await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.CaLamViec ON;");
        try
        {
            for (var i = 1; i <= 10; i++)
            {
                var slot = (i - 1) % 3; var shiftName = new[] { "Sáng", "Chiều", "Tối" }[slot]; var machine = $"POS-{slot + 1:00}";
                var code = $"CA{i:0000}"; var name = PeopleSampleSeed.EmployeeNames[i - 1];
                // Ngày mẫu 24/10–02/11/2026, giờ Việt Nam được chuyển UTC trước khi lưu theo quy tắc dự án.
                var local = new DateTime(2026, 11, 3).AddDays(-i).AddHours(new[] { 6, 12, 18 }[slot]);
                var start = DateTime.SpecifyKind(local.AddHours(-7), DateTimeKind.Utc); var end = start.AddHours(slot == 2 ? 5 : 6);
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.CaLamViec(MaCaLamViec,MaCaHienThi,MaNhanVien,TenCa,MaMay,BatDauLuc,KetThucLuc,TrangThai,TienMatDauCa,TienMatDuKienCuoiCa,TienMatThucDem,GhiChu) VALUES({i},{code},{i},{shiftName},{machine},{start},{end},'CLOSED',500000,500000,500000,N'Ca lịch sử mẫu để minh họa đối chiếu.');");
                // Snapshot tên ở OPEN_SHIFT để sửa hồ sơ sau này không đổi tên của ca lịch sử.
                db.NhatKyHeThong.Add(new NhatKyHeThong
                {
                    MaLienKetXuLy = Guid.NewGuid(),
                    LoaiTacNhan = "STAFF",
                    MaCaLamViec = i,
                    TenNguoiThucHienDaChot = name,
                    MaMayDaChot = machine,
                    HanhDong = "OPEN_SHIFT",
                    LoaiDoiTuong = "CaLamViec",
                    MaDoiTuong = i.ToString(),
                    LyDo = "Ca mẫu được nạp theo yêu cầu, không phải giao dịch vận hành thực tế.",
                    DuLieuSauJson = JsonSerializer.Serialize(new { MaCaLamViec = i, MaNhanVien = i, HoTen = name, TenCa = shiftName }),
                    KetQua = "SUCCEEDED",
                    PhatSinhLuc = start
                });
                db.NhatKyHeThong.Add(new NhatKyHeThong
                {
                    MaLienKetXuLy = Guid.NewGuid(),
                    LoaiTacNhan = "STAFF",
                    MaCaLamViec = i,
                    TenNguoiThucHienDaChot = name,
                    MaMayDaChot = machine,
                    HanhDong = "CLOSE_SHIFT",
                    LoaiDoiTuong = "CaLamViec",
                    MaDoiTuong = i.ToString(),
                    LyDo = "Kết thúc ca mẫu, quỹ đầu/cuối ca khớp; chưa có giao dịch thu hoặc hoàn.",
                    KetQua = "SUCCEEDED",
                    PhatSinhLuc = end
                });
                result.Add(new SampleShift(i, code, i, name, shiftName, machine, "CLOSED"));
            }
        }
        finally { await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.CaLamViec OFF;"); }
        return result;
    }
}
