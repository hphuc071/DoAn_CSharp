using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Customer;

public sealed class SessionTests
{
    [Fact, Trait("Actor", "KhachHang"), Trait("UseCase", "UC-WEB-02"), Trait("Layer", "API")]
    public async Task DangNhap_TruyCapHoSoVaDangXuatThuHoiPhien_ThanhCong()
    {
        await using var api = await TestApi.Start("UC-WEB-02");
        var customer = await api.NewCustomer("login");
        var headers = await api.Login(customer);

        // Luồng chính: Phiên hợp lệ đọc được hồ sơ cá nhân
        var profile = await api.Profile(headers);
        Check(profile.Number("maKhachHang") == customer.Id, "Đọc đúng hồ sơ của khách đang đăng nhập");

        // Nhánh A1: Đăng xuất thu hồi phiên hiện tại
        var logoutRes = await api.Send("POST", "/api/auth/customer/logout", headers: headers);
        logoutRes.Expect(200, "Đăng xuất thành công");

        // Ngoại lệ E3(a): Dùng lại phiên đã đăng xuất phải bị từ chối 401 Unauthorized
        var afterLogout = await api.Send("GET", "/api/customer/profile", headers: headers);
        afterLogout.Expect(401, "Phiên đã đăng xuất không được tiếp tục truy cập hồ sơ");
    }

    [Fact, Trait("Actor", "KhachHang"), Trait("UseCase", "UC-WEB-02"), Trait("Layer", "API")]
    public async Task DangNhap_SaiMatKhauQuaNguong5Lan_TamKhoa15Phut()
    {
        await using var api = await TestApi.Start("UC-WEB-02-E1");
        var customer = await api.NewCustomer("lockout");
        var username = customer.Input.Text("TenDangNhap");
        var validPassword = customer.Input.Text("MatKhau");

        // Ngoại lệ E1: Nhập sai mật khẩu 5 lần liên tiếp
        for (int i = 0; i < 5; i++)
        {
            var wrongRes = await api.Send("POST", "/api/auth/customer/login", Body(
                ("TenDangNhap", username),
                ("MatKhau", "SaiMatKhauHoanToan@999"),
                ("MaThaoTac", NewId())
            ));
            wrongRes.Expect(401, $"Lần thử sai thứ {i + 1} phải trả về 401");
        }

        // Lần thứ 6 nhập ĐÚNG mật khẩu vẫn bị chặn 401 do đang trong thời gian tạm khóa 15 phút (BR-07)
        var lockedRes = await api.Send("POST", "/api/auth/customer/login", Body(
            ("TenDangNhap", username),
            ("MatKhau", validPassword),
            ("MaThaoTac", NewId())
        ));
        lockedRes.Expect(401, "Tài khoản vượt ngưỡng 5 lần thử sai phải bị tạm khóa đăng nhập");
    }
}
