using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests;

/// <summary>Kiểm tra biên 4/5 ký tự ở đăng ký, đổi mật khẩu, cấp lại và tài khoản quản lý trên API thật.</summary>
public sealed class PasswordLengthTests
{
    /// <summary>Mật khẩu 5 ký tự phải dùng được xuyên suốt; 4 và 129 ký tự vẫn bị từ chối.</summary>
    [Fact, Trait("Feature", "PasswordMinimum"), Trait("Layer", "API")]
    public async Task MinimumFive_AppliesToCustomerAndAdmin()
    {
        await using var api = await TestApi.Start("Password-Minimum-Five");
        var stamp = Guid.NewGuid().ToString("N")[..12];
        var input = Body(("MaThaoTac", NewId()), ("TenDangNhap", "pwd" + stamp),
            ("HoTen", "Khách kiểm tra mật khẩu"), ("SoDienThoai", Phone()),
            ("MatKhau", "1234"), ("XacNhanMatKhau", "1234"));
        (await api.Send("POST", "/api/auth/customer/register", input)).Expect(400, "4 ký tự bị chặn");
        input["MatKhau"] = "12345"; input["XacNhanMatKhau"] = "12345";
        var registered = (await api.Send("POST", "/api/auth/customer/register", input)).Expect(200, "5 ký tự được đăng ký").Json;
        var id = registered.Number("maKhachHang");
        var session = (await api.Send("POST", "/api/auth/customer/login", Body(("TenDangNhap", input.Text("TenDangNhap")), ("MatKhau", "12345")))).Expect(200, "Đăng nhập mật khẩu 5 ký tự").Json;
        var headers = TestApi.Bearer(session);
        var profile = await api.Profile(headers);
        var change = Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", profile.Text("phienBanDuLieu")),
            ("MatKhauHienTai", "12345"), ("MatKhauMoi", "1234"), ("XacNhanMatKhau", "1234"));
        (await api.Send("POST", "/api/customer/password", change, headers)).Expect(400, "Đổi sang 4 ký tự bị chặn");
        change["MatKhauMoi"] = new string('x',129); change["XacNhanMatKhau"] = new string('x',129);
        (await api.Send("POST", "/api/customer/password", change, headers)).Expect(400, "Giữ giới hạn tối đa 128");
        change["MatKhauMoi"] = "abcde"; change["XacNhanMatKhau"] = "abcde";
        (await api.Send("POST", "/api/customer/password", change, headers)).Expect(200, "Đổi sang 5 ký tự thành công");
        (await api.Send("GET", "/api/customer/profile", headers:headers)).Expect(401, "Thu hồi phiên cũ vẫn có hiệu lực");

        var admin = await api.AdminLogin();
        var account = await api.CustomerAccount(id,admin);
        (await api.Send("POST", $"/api/admin/customers/{id}/reset-password", TestApi.Action(account,("MatKhauTam","1234")),admin)).Expect(400,"Admin không cấp mật khẩu 4 ký tự");
        (await api.Send("POST", $"/api/admin/customers/{id}/reset-password", TestApi.Action(account,("MatKhauTam","reset")),admin)).Expect(200,"Admin cấp lại mật khẩu 5 ký tự");
        var limited = TestApi.Bearer((await api.Send("POST", "/api/auth/customer/login", Body(("TenDangNhap",input.Text("TenDangNhap")),("MatKhau","reset")))).Expect(200,"Đăng nhập mật khẩu tạm").Json);
        var temporary = (await api.Send("GET", "/api/auth/customer/temporary-password",headers:limited)).Expect(200,"Phiên đổi mật khẩu tạm").Json;
        (await api.Send("POST", "/api/auth/customer/temporary-password", Body(("MaThaoTac",NewId()),("PhienBanDuLieu",temporary.Text("phienBanDuLieu")),("MatKhauMoi","final"),("XacNhanMatKhau","final")),limited)).Expect(200,"Đổi mật khẩu tạm thành 5 ký tự");

        var adminProfile = (await api.Send("GET", "/api/admin/access/account",headers:admin)).Expect(200,"Hồ sơ quản lý").Json;
        var adminChange = Body(("MaThaoTac",NewId()),("PhienBanDuLieu",adminProfile.Text("phienBanDuLieu")),("MatKhauHienTai",api.AdminPassword),("MatKhauMoi","1234"),("XacNhanMatKhau","1234"));
        (await api.Send("POST", "/api/auth/admin/password",adminChange,admin)).Expect(400,"Quản lý không đổi sang 4 ký tự");
        adminChange["MatKhauMoi"] = "admin"; adminChange["XacNhanMatKhau"] = "admin";
        (await api.Send("POST", "/api/auth/admin/password",adminChange,admin)).Expect(200,"Quản lý đổi sang 5 ký tự");
        (await api.Send("POST", "/api/auth/admin/login",Body(("TenDangNhap","quanly"),("MatKhau","admin")))).Expect(200,"Quản lý đăng nhập mật khẩu mới");
    }
}
