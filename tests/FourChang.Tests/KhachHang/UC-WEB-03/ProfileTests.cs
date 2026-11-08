using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Customer;

public sealed class ProfileTests
{
    [Fact, Trait("Actor", "KhachHang"), Trait("UseCase", "UC-WEB-03"), Trait("Layer", "API")]
    public async Task QuanLyHoSo_SoDiaChiMacDinhVaKiemSoatVersion_ThanhCong()
    {
        await using var api = await TestApi.Start("UC-WEB-03");
        var customer = await api.NewCustomer("profile");
        var headers = await api.Login(customer);

        // Bước 1: Đọc hồ sơ cá nhân và lấy PhienBanDuLieu hiện tại
        var profile1 = await api.Profile(headers);
        var version1 = profile1.Text("phienBanDuLieu");
        var email = customer.Input.Text("Email");
        var phone = "09" + Random.Shared.Next(10000000, 99999999);

        // Bước 2-4: Cập nhật hồ sơ với PhienBanDuLieu hợp lệ
        var saveRes = await api.Send("PUT", "/api/customer/profile", Body(
            ("MaThaoTac", NewId()),
            ("PhienBanDuLieu", version1),
            ("HoTen", "Nguyễn Đức Tài (Đã Cập Nhật)"),
            ("Email", email),
            ("SoDienThoai", phone)
        ), headers);
        saveRes.Expect(200, "Cập nhật hồ sơ với version hợp lệ thành công");
        var version2 = saveRes.Json["profile"]!.Text("phienBanDuLieu");

        // Ngoại lệ E2: Gửi lại version1 cũ -> Bị từ chối 409 Conflict
        var staleRes = await api.Send("PUT", "/api/customer/profile", Body(
            ("MaThaoTac", NewId()),
            ("PhienBanDuLieu", version1),
            ("HoTen", "Ghi Đè Trái Phép"),
            ("Email", email),
            ("SoDienThoai", phone)
        ), headers);
        staleRes.Expect(409, "Cập nhật hồ sơ bằng version cũ phải trả về 409 Conflict");

        // Nhánh A2: Thêm địa chỉ giao hàng mặc định (BR-28)
        var addAddrRes = await api.Send("POST", "/api/customer/addresses", Body(
            ("MaThaoTac", NewId()),
            ("PhienBanHoSo", version2),
            ("PhienBanDiaChi", null),
            ("NhanDiaChi", "Nhà riêng"),
            ("TenNguoiNhan", "Nguyễn Đức Tài"),
            ("DienThoaiNguoiNhan", phone),
            ("DiaChiChiTiet", "273 An Dương Vương, Phường 3, Quận 5, TP.HCM"),
            ("ViDo", 10.7599m),
            ("KinhDo", 106.6822m),
            ("GhiChuGiaoHang", "Gọi trước khi giao"),
            ("LaDiaChiMacDinh", true)
        ), headers);
        addAddrRes.Expect(200, "Thêm địa chỉ mặc định mới thành công");
        var addresses = addAddrRes.Json["profile"]!["diaChi"]!.AsArray();
        Assert.Single(addresses);
        Check(addresses[0]!.Flag("laDiaChiMacDinh"), "Địa chỉ vừa thêm phải là mặc định");
    }

    [Fact, Trait("Actor", "KhachHang"), Trait("UseCase", "UC-WEB-03"), Trait("Layer", "API")]
    public async Task DoiMatKhauCaNhan_ThuHoiToanBoPhienHienTai_ThanhCong()
    {
        await using var api = await TestApi.Start("UC-WEB-03-A1");
        var customer = await api.NewCustomer("pwdchange");
        var headers = await api.Login(customer);

        var profile = await api.Profile(headers);
        var version = profile.Text("phienBanDuLieu");
        var currentPassword = customer.Input.Text("MatKhau");
        var newPassword = Password();

        // Nhánh A1: Đổi mật khẩu cá nhân
        var changeRes = await api.Send("POST", "/api/customer/password", Body(
            ("MaThaoTac", NewId()),
            ("PhienBanDuLieu", version),
            ("MatKhauHienTai", currentPassword),
            ("MatKhauMoi", newPassword),
            ("XacNhanMatKhau", newPassword)
        ), headers);
        changeRes.Expect(200, "Đổi mật khẩu hợp lệ thành công");
        Check(changeRes.Json.Flag("canDangNhapLai"), "Phải yêu cầu đăng nhập lại sau khi đổi mật khẩu");

        // Hậu điều kiện A1: Toàn bộ phiên đăng nhập cũ bị thu hồi -> Trả 401
        var oldSessionRes = await api.Send("GET", "/api/customer/profile", headers: headers);
        oldSessionRes.Expect(401, "Phiên đăng nhập cũ phải bị thu hồi ngay sau khi đổi mật khẩu");
    }
}
