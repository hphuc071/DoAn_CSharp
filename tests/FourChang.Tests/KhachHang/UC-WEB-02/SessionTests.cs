using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Customer;
// UC-WEB-02: phiên phải được thu hồi thật trong SQL, không chỉ ẩn nút trên Web.
public sealed class SessionTests
{

    /// <summary>
    /// Kiểm tra phiên đăng nhập, chống cấp phiên trùng, logout lặp, hết hạn, rollback audit và khóa khi thử sai.
    /// </summary>
    [Fact, Trait("Actor", "KhachHang"), Trait("UseCase", "UC-WEB-02"), Trait("Layer", "API")]
    public async Task DangNhap_GuiLap_DangXuat_HetHan_Audit_VaKhoaTam()
    {
        await using var api = await TestApi.Start("UC-WEB-02");
        var customer = await api.NewCustomer("login");
        var body = Body(("TenDangNhap", customer.Input.Text("TenDangNhap")), ("MatKhau", customer.Input.Text("MatKhau")), ("MaThaoTac", NewId()));
        var session = (await api.Send("POST", "/api/auth/customer/login", body)).Expect(200, "Đăng nhập hợp lệ").Json;
        var h = TestApi.Bearer(session);
        var again = (await api.Send("POST", "/api/auth/customer/login", body)).Expect(200, "Gửi lại cùng mã");
        Assert.Equal(session.Text("token"), again.Json.Text("token"));
        Assert.Equal("1", await api.Sql($"SELECT COUNT(*) FROM dbo.PhienDangNhap WHERE MaKhachHang={customer.Id}"));
        (await api.Send("GET", "/api/customer/profile", headers: h)).Expect(200, "Phiên dùng được");
        (await api.Send("POST", "/api/auth/logout", headers: h)).Expect(200, "Đăng xuất");
        (await api.Send("POST", "/api/auth/logout", headers: h)).Expect(200, "Đăng xuất lặp an toàn");
        (await api.Send("GET", "/api/customer/profile", headers: h)).Expect(401, "Token đã thu hồi");
        Assert.Equal("1", await api.Sql($"SELECT COUNT(*) FROM dbo.NhatKyHeThong WHERE MaKhachHang={customer.Id} AND HanhDong='CUSTOMER_LOGOUT'"));
        h = await api.Login(customer);
        await api.Sql($"UPDATE dbo.PhienDangNhap SET ThoiDiemTao=DATEADD(minute,-2,SYSUTCDATETIME()),HetHanLuc=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE MaKhachHang={customer.Id}");
        (await api.Send("GET", "/api/customer/profile", headers: h)).Expect(401, "Phiên hết hạn");
        var before = await api.Sql($"SELECT COUNT(*) FROM dbo.PhienDangNhap WHERE MaKhachHang={customer.Id}");
        await api.Sql("DENY INSERT ON OBJECT::dbo.NhatKyHeThong TO fourchang_app");
        try
        {
            Check((await api.Send("POST", "/api/auth/customer/login", Body(("TenDangNhap", customer.Input.Text("TenDangNhap")), ("MatKhau", customer.Input.Text("MatKhau"))))).Status >= 400, "Lỗi audit không cấp phiên");
        }
        finally
        {
            await api.Sql("REVOKE INSERT ON OBJECT::dbo.NhatKyHeThong FROM fourchang_app");
        }

        Assert.Equal(before, await api.Sql($"SELECT COUNT(*) FROM dbo.PhienDangNhap WHERE MaKhachHang={customer.Id}"));
        var wrong = Body(("TenDangNhap", customer.Input.Text("TenDangNhap")), ("MatKhau", "Sai-mat-khau"), ("MaThaoTac", NewId()));
        (await api.Send("POST", "/api/auth/customer/login", wrong)).Expect(401, "Sai mật khẩu");
        (await api.Send("POST", "/api/auth/customer/login", wrong)).Expect(401, "Lặp cùng lần thử sai");
        Assert.Equal("1", await api.Sql($"SELECT SoLanDangNhapSai FROM dbo.TaiKhoanKhachHang WHERE MaKhachHang={customer.Id}"));
        for (int i = 0; i < 4; i++)
        {
            wrong["MaThaoTac"] = NewId();
            (await api.Send("POST", "/api/auth/customer/login", wrong)).Expect(401, "Tăng lần sai độc lập");
        }

        (await api.Send("POST", "/api/auth/customer/login", body)).Expect(401, "Đúng mật khẩu vẫn khóa sau năm lần sai");
    }
}
