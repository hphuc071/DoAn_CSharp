using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Admin;

/// <summary>Kiểm tra nâng cấp cột địa chỉ được chấp thuận và lưu hồ sơ nhân viên với schema mới.</summary>
public sealed class EmployeeContactAddressTests
{
    /// <summary>Nâng cấp schema 35 cũ giữ hồ sơ/mã, cột mới nullable; Admin lưu được địa chỉ và giới hạn 500 ký tự.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-12"), Trait("Layer", "Database")]
    public async Task NangCap35Cu_VaSuaDiaChi_KhongDoiMa()
    {
        await using var api = await TestApi.Start("EmployeeContactAddress");
        var admin = await api.AdminLogin(); var employee = await api.Employee(admin, "Nhân viên kiểm tra địa chỉ");
        var id = employee.Number("maNhanVien");
        // Dựng đúng schema 35 trước địa chỉ trên database GUID riêng; không tạo schema 37 thiếu cột rồi bắt bootstrap chấp nhận.
        await api.Sql("""
            DROP TABLE dbo.Banner;
            DROP TABLE dbo.SuKienDongBo;
            DROP INDEX UX_CauHinhMon_MacDinh ON dbo.CauHinhMon;
            ALTER TABLE dbo.CauHinhMon DROP CONSTRAINT DF_CauHinhMon_MacDinh;
            ALTER TABLE dbo.CauHinhMon DROP COLUMN LaMacDinh;
            DROP INDEX UX_TuyChon_MacDinh ON dbo.TuyChonPhaCheHopLe;
            ALTER TABLE dbo.TuyChonPhaCheHopLe DROP CONSTRAINT DF_TuyChon_MacDinh;
            ALTER TABLE dbo.TuyChonPhaCheHopLe DROP COLUMN LaMacDinh;
            ALTER TABLE dbo.Mon DROP COLUMN CongBoTuLuc;
            ALTER TABLE dbo.NhanVien DROP COLUMN DiaChiLienHe;
            """);
        await api.RunApiCommand("--init-db");
        Assert.Equal("1", await api.Sql("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.NhanVien') AND name='DiaChiLienHe' AND is_nullable=1"));
        Assert.Equal("Nhân viên kiểm tra địa chỉ", await api.Sql($"SELECT HoTen FROM dbo.NhanVien WHERE MaNhanVien={id}"));
        var input = Body(("MaThaoTac", NewId()), ("HoTen", employee.Text("hoTen")), ("NgayVaoLam", "2026-10-01"), ("TrangThai", "ACTIVE"),
            ("PhienBanDuLieu", employee.Text("phienBanDuLieu")), ("DiaChiLienHe", "123 Đường Hoa, Phường An Bình, TP Hồ Chí Minh"));
        var saved = (await api.Send("PUT", $"/api/admin/employees/{id}", input, admin)).Expect(200, "Lưu địa chỉ nhân viên").Json;
        Assert.Equal(input.Text("DiaChiLienHe"), saved.Text("diaChiLienHe"));
        input["MaThaoTac"] = NewId(); input["PhienBanDuLieu"] = saved.Text("phienBanDuLieu"); input["DiaChiLienHe"] = new string('a', 501);
        (await api.Send("PUT", $"/api/admin/employees/{id}", input, admin)).Expect(400, "Giới hạn cột");
        await api.RunApiCommand("--check-db");
    }
}
