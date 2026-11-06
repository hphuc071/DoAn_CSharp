using FourChang.Tests.Shared;
using FourChang.Infrastructure.Database.Context;
using FourChang.Infrastructure.Database.Initialization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Integration;

/// <summary>Thay bộ mẫu chỉ trên database GUID riêng; kiểm tra FK, mã 1–10 và rollback trước khi thực hiện trên dự án.</summary>
public sealed class PeopleSampleResetTests
{
    /// <summary>Thay dữ liệu cũ, kiểm tra nhân viên–ca, audit snapshot, identity tiếp theo và chặn xóa khi đã có đơn.</summary>
    [Fact, Trait("Actor", "TichHop"), Trait("Layer", "Database")]
    public async Task ThayBoMau_10MoiBang_MaTuMot_DungLienKet_VaChanGiaoDich()
    {
        await using var api = await TestApi.Start("PeopleSampleReset");
        var admin = await api.AdminLogin(); await api.Employee(admin, "Hồ sơ cũ sẽ thay");
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = api.Settings.GetValueOrDefault("FOURCHANG_SQL_SERVER", "127.0.0.1,14330"),
            InitialCatalog = api.Database,
            UserID = "sa",
            Password = api.Settings["SQL_SA_PASSWORD"],
            Encrypt = true,
            TrustServerCertificate = true
        };
        await using var db = new CafeDbContext(new DbContextOptionsBuilder<CafeDbContext>().UseSqlServer(builder.ConnectionString).Options);
        await PeopleSampleReset.RunAsync(db);
        Assert.Equal("10\t1\t10", await api.Sql("SELECT COUNT(*),MIN(MaNhanVien),MAX(MaNhanVien) FROM dbo.NhanVien"));
        Assert.Equal("10\t1\t10", await api.Sql("SELECT COUNT(*),MIN(MaKhachHang),MAX(MaKhachHang) FROM dbo.TaiKhoanKhachHang"));
        Assert.Equal("10\t1\t10", await api.Sql("SELECT COUNT(*),MIN(MaCaLamViec),MAX(MaCaLamViec) FROM dbo.CaLamViec"));
        Assert.Equal("10", await api.Sql("SELECT COUNT(*) FROM dbo.CaLamViec c JOIN dbo.NhanVien n ON n.MaNhanVien=c.MaNhanVien JOIN dbo.NhatKyHeThong a ON a.MaCaLamViec=c.MaCaLamViec AND a.HanhDong='OPEN_SHIFT' WHERE a.TenNguoiThucHienDaChot=n.HoTen AND c.MaCaLamViec=n.MaNhanVien AND c.TrangThai='CLOSED' AND c.TienMatThucDem=c.TienMatDuKienCuoiCa"));
        (await api.Send("GET", "/api/admin/access/account", headers: admin)).Expect(200, "Tài khoản và phiên Admin được giữ");
        // Lỗi audit sau khi đã xóa/nạp phải rollback toàn bộ về đúng bộ trước đó.
        await api.Sql("CREATE TRIGGER dbo.TR_Test_BlockSampleReset ON dbo.NhatKyHeThong AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE HanhDong='RESET_SAMPLE_PEOPLE') THROW 51091, 'Test reset audit failure', 1; END;");
        try { await Assert.ThrowsAsync<DbUpdateException>(() => PeopleSampleReset.RunAsync(db)); }
        finally { await api.Sql("DROP TRIGGER dbo.TR_Test_BlockSampleReset"); db.ChangeTracker.Clear(); }
        Assert.Equal("10", await api.Sql("SELECT COUNT(*) FROM dbo.NhanVien"));
        Assert.Equal("20", await api.Sql("SELECT COUNT(*) FROM dbo.NhatKyHeThong WHERE HanhDong IN ('OPEN_SHIFT','CLOSE_SHIFT')"));
        await PeopleSampleReset.RunAsync(db);
        Assert.Equal("10\t1\t10", await api.Sql("SELECT COUNT(*),MIN(MaCaLamViec),MAX(MaCaLamViec) FROM dbo.CaLamViec"));
        Assert.Equal("0", await api.Sql("SELECT CONVERT(int,is_disabled) FROM sys.triggers WHERE name='TR_NhatKyHeThong_BatBien'"));
        await Assert.ThrowsAsync<SqlException>(() => api.Sql("DELETE TOP(1) FROM dbo.NhatKyHeThong WHERE HanhDong='OPEN_SHIFT'"));
        var extra = await api.Employee(admin, "Nhân viên tiếp theo"); Assert.Equal(11, extra.Number("maNhanVien"));
        await api.Sql("INSERT dbo.DonHang(MaDonHienThi,MaCaLamViec,KenhDatHang,HinhThucNhanMon,PhiGiaoHangBanDau,PhiGiaoHangHienTai,TrangThai) VALUES('RESET-PROTECTED',1,'POS','TAKEAWAY',0,0,'COMPLETED');");
        await Assert.ThrowsAsync<InvalidOperationException>(() => PeopleSampleReset.RunAsync(db));
        Assert.Equal("11", await api.Sql("SELECT COUNT(*) FROM dbo.NhanVien"));
        Assert.Equal("1", await api.Sql("SELECT COUNT(*) FROM dbo.DonHang WHERE MaDonHienThi='RESET-PROTECTED'"));
        await api.RunApiCommand("--check-db");
    }
}
