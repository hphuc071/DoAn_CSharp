using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Integration;

/// <summary>Kiểm tra nạp mẫu bằng lệnh C# thật trên database GUID riêng, không dùng database đang vận hành.</summary>
public sealed class PeopleSampleSeedTests
{
    /// <summary>10+10 mẫu có khóa thật, mật khẩu băm; chạy lại không nhân đôi hoặc mất hồ sơ/giao dịch cũ.</summary>
    [Fact, Trait("Actor", "TichHop"), Trait("Layer", "Database")]
    public async Task NapMauHaiLan_KhongTrung_KhongMatDuLieuCu()
    {
        await using var api = await TestApi.Start("PeopleSampleSeed");
        var admin = await api.AdminLogin(); var original = await api.Employee(admin, "Hồ sơ cần giữ nguyên");
        var before = long.Parse(await api.Sql("SELECT COUNT(*) FROM dbo.NhanVien"));
        await api.RunApiCommand("--seed-people");
        Assert.Equal("10", await api.Sql("SELECT COUNT(*) FROM dbo.NhanVien WHERE SoDienThoai LIKE '099810%'"));
        Assert.Equal("10", await api.Sql("SELECT COUNT(*) FROM dbo.TaiKhoanKhachHang WHERE TenDangNhapChuanHoa LIKE N'mau.kh%'"));
        Assert.Equal((before + 10).ToString(), await api.Sql("SELECT COUNT(*) FROM dbo.NhanVien"));
        await api.RunApiCommand("--seed-people");
        Assert.Equal((before + 10).ToString(), await api.Sql("SELECT COUNT(*) FROM dbo.NhanVien"));
        Assert.Equal("10", await api.Sql("SELECT COUNT(*) FROM dbo.TaiKhoanKhachHang WHERE TenDangNhapChuanHoa LIKE N'mau.kh%' AND MatKhauBam LIKE 'PBKDF2-SHA512:%' AND BatBuocDoiMatKhau=1"));
        Assert.Equal("1", await api.Sql("SELECT COUNT(*) FROM dbo.NhatKyHeThong WHERE HanhDong='SEED_SAMPLE_PEOPLE'"));
        (await api.Send("GET", "/api/admin/employees/" + original.Number("maNhanVien"), headers: admin)).Expect(200, "Hồ sơ cũ còn nguyên");
        await api.RunApiCommand("--check-db");
    }
}
