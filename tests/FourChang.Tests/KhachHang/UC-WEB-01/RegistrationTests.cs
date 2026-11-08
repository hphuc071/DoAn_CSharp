using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Customer;

public sealed class RegistrationTests
{
    [Fact, Trait("Actor", "KhachHang"), Trait("UseCase", "UC-WEB-01"), Trait("Layer", "API")]
    public async Task DangKyTaiKhoan_HopLeVaChongTrungIdempotency_ThanhCong()
    {
        await using var api = await TestApi.Start("UC-WEB-01");
        var stamp = Guid.NewGuid().ToString("N");
        var opId = NewId();
        var phone = "09" + Random.Shared.Next(10000000, 99999999);
        var email = stamp + "@example.test";
        var pwd = Password();

        var body = Body(
            ("MaThaoTac", opId),
            ("TenDangNhap", "web" + stamp[..12]),
            ("HoTen", "  Nguyễn Đức Tài  "),
            ("Email", email),
            ("SoDienThoai", phone),
            ("MatKhau", pwd),
            ("XacNhanMatKhau", pwd)
        );

        // Bước 1-5: Đăng ký tài khoản hợp lệ thành công
        var res = await api.Send("POST", "/api/auth/customer/register", body);
        res.Expect(200, "Đăng ký tài khoản hợp lệ thành công");
        var customerId = res.Json.Number("maKhachHang");
        Check(customerId > 0, "Mã khách hàng phải lớn hơn 0");

        // Ngoại lệ E3: Gửi lặp cùng MaThaoTac -> Trả lại đúng kết quả cũ, không tạo tài khoản thứ 2
        var retryRes = await api.Send("POST", "/api/auth/customer/register", body);
        retryRes.Expect(200, "Gửi lặp cùng mã thao tác phải trả lại biên nhận đã commit");
        Assert.Equal(customerId, retryRes.Json.Number("maKhachHang"));

        // Ngoại lệ E2: Đăng ký mới trùng Số điện thoại đã tồn tại -> Chặn 409 Conflict
        var duplicateBody = Body(
            ("MaThaoTac", NewId()),
            ("TenDangNhap", "dup" + stamp[..12]),
            ("HoTen", "Khách Trùng SĐT"),
            ("Email", "dup." + stamp + "@example.test"),
            ("SoDienThoai", phone),
            ("MatKhau", pwd),
            ("XacNhanMatKhau", pwd)
        );
        var dupRes = await api.Send("POST", "/api/auth/customer/register", duplicateBody);
        dupRes.Expect(409, "Số điện thoại đã tồn tại phải bị chặn với mã 409 Conflict");
    }

    [Fact, Trait("Actor", "KhachHang"), Trait("UseCase", "UC-WEB-01"), Trait("Layer", "API")]
    public async Task DangKyTaiKhoan_SaiChinhSachMatKhauHoacKhongKhop_BiChan400()
    {
        await using var api = await TestApi.Start("UC-WEB-01-E1");
        var stamp = Guid.NewGuid().ToString("N");

        // Ngoại lệ E1: Mật khẩu ngắn hơn 10 ký tự hoặc xác nhận không khớp
        var invalidBody = Body(
            ("MaThaoTac", NewId()),
            ("TenDangNhap", "err" + stamp[..12]),
            ("HoTen", "Nguyễn Văn B"),
            ("Email", stamp + "@example.test"),
            ("SoDienThoai", "09" + Random.Shared.Next(10000000, 99999999)),
            ("MatKhau", "Ngan123"),
            ("XacNhanMatKhau", "KhongKhop123")
        );

        var res = await api.Send("POST", "/api/auth/customer/register", invalidBody);
        res.Expect(400, "Dữ liệu không đạt chính sách mật khẩu phải trả về 400 BadRequest");
    }
}
