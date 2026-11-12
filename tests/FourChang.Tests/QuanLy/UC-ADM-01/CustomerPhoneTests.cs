using FourChang.Tests.Shared;

namespace FourChang.Tests.Admin;

/// <summary>ADM-01: số điện thoại là chuỗi đúng 10 chữ số, không tự đổi +84 hoặc cắt dữ liệu.</summary>
[Collection("AdminFeatures"), Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-01")]
public sealed class CustomerPhoneTests(AdminFeatureFixture fixture)
{
    /// <summary>Chặn độ dài/ký tự sai tại API ngay cả khi client bỏ qua giới hạn TextBox.</summary>
    [Theory]
    [InlineData(null, 400)]
    [InlineData("", 400)]
    [InlineData("   ", 400)]
    [InlineData("012345678", 400)]
    [InlineData("01234567890", 400)]
    [InlineData("+84912345678", 400)]
    [InlineData("09123abc78", 400)]
    [InlineData("０９１２３４５６７８", 400)]
    [InlineData("0907654321", 200)]
    public async Task SoDienThoai_DungMuoiChuSoAscii(string? phone, int status)
    {
        var input = fixture.Api.CustomerInput(); input["SoDienThoai"] = phone;
        var result = (await fixture.Api.Send("POST", "/api/admin/customers", input, fixture.Admin)).Expect(status, "Điện thoại 10 chữ số");
        if (status == 200) Assert.Equal(phone, result.Json["account"]!.Text("soDienThoai"));
    }

    /// <summary>Seed gọi hai lần vẫn đúng 10 hồ sơ tự nhiên, không ghi mật khẩu rõ hoặc xóa dữ liệu cũ.</summary>
    [Fact]
    public async Task MuoiKhachHangMau_ThemLapAnToan_KhongXoaDuLieuCu()
    {
        var sampleBefore = long.Parse(await fixture.Api.Sql("SELECT COUNT(*) FROM dbo.TaiKhoanKhachHang WHERE TenDangNhapChuanHoa LIKE 'mau.%'"));
        var before = long.Parse(await fixture.Api.Sql("SELECT COUNT(*) FROM dbo.TaiKhoanKhachHang"));
        await fixture.Api.RunApiCommand("--seed-customers");
        await fixture.Api.RunApiCommand("--seed-customers");
        Assert.Equal("10", await fixture.Api.Sql("SELECT COUNT(*) FROM dbo.TaiKhoanKhachHang WHERE TenDangNhapChuanHoa LIKE 'mau.%'"));
        Assert.Equal((before + 10 - sampleBefore).ToString(), await fixture.Api.Sql("SELECT COUNT(*) FROM dbo.TaiKhoanKhachHang"));
        Assert.Equal("0", await fixture.Api.Sql("SELECT COUNT(*) FROM dbo.TaiKhoanKhachHang WHERE TenDangNhapChuanHoa LIKE 'mau.%' AND (LEN(SoDienThoaiChuanHoa)<>10 OR SoDienThoaiChuanHoa LIKE '%[^0-9]%' OR BatBuocDoiMatKhau<>1)"));
    }

    /// <summary>SQL vẫn chặn số sai khi bỏ qua UI/API; giao dịch lỗi không làm mất số đã lưu.</summary>
    [Fact]
    public async Task RangBuocSql_ChanSoSai_KhongCatSo()
    {
        var input = fixture.Api.CustomerInput("phoneconstraint");
        input["SoDienThoai"] = "0907888111";
        var customer = (await fixture.Api.Send("POST", "/api/admin/customers", input, fixture.Admin)).Expect(200, "Tạo hồ sơ kiểm tra SQL").Json["account"]!;
        var id = customer.Number("maKhachHang");
        foreach (var invalid in new[] { "123456789", "01234567890", "090788811x" })
        {
            var error = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => fixture.Api.Sql($"UPDATE dbo.TaiKhoanKhachHang SET SoDienThoaiChuanHoa='{invalid}' WHERE MaKhachHang={id}"));
            Assert.Equal(547, error.Number);
            Assert.Equal("0907888111", await fixture.Api.Sql($"SELECT SoDienThoaiChuanHoa FROM dbo.TaiKhoanKhachHang WHERE MaKhachHang={id}"));
        }
    }


    /// <summary>NOT NULL chặn bỏ trống trực tiếp SQL, còn API chặn null/rỗng ở cả tạo và sửa hồ sơ.</summary>
    [Fact]
    public async Task DienThoaiBatBuoc_ApiSuaVaSqlKhongNhanNull()
    {
        var input = fixture.Api.CustomerInput("required");
        var account = (await fixture.Api.Send("POST", "/api/admin/customers", input, fixture.Admin)).Expect(200, "Tạo khách có điện thoại").Json["account"]!;
        var id = account.Number("maKhachHang");
        var edit = input.Copy(); edit["MaThaoTac"] = TestData.NewId(); edit["PhienBanDuLieu"] = account.Text("phienBanDuLieu"); edit["MatKhauTam"] = null; edit["SoDienThoai"] = null;
        (await fixture.Api.Send("PUT", $"/api/admin/customers/{id}", edit, fixture.Admin)).Expect(400, "Không xóa điện thoại khi sửa");
        var error = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => fixture.Api.Sql($"UPDATE dbo.TaiKhoanKhachHang SET SoDienThoaiChuanHoa=NULL WHERE MaKhachHang={id}"));
        Assert.Equal(515, error.Number);
        Assert.Equal("0", await fixture.Api.Sql("SELECT CONVERT(int,is_nullable) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.TaiKhoanKhachHang') AND name='SoDienThoaiChuanHoa'"));
    }
}
