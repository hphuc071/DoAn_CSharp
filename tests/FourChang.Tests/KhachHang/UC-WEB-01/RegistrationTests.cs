using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Customer;
// UC-WEB-01: kiểm tra trực tiếp API, tính nguyên tử audit và chống gửi lặp.
public sealed class RegistrationTests
{

    /// <summary>
    /// Kiểm tra đăng ký hợp lệ, dữ liệu sai, trùng định danh, gửi lặp, rollback audit và giới hạn quyền Customer.
    /// </summary>
    [Fact, Trait("Actor", "KhachHang"), Trait("UseCase", "UC-WEB-01"), Trait("Layer", "API")]
    public async Task DangKy_DinhDanh_Quyen_Audit_VaGuiLap()
    {
        await using var api = await TestApi.Start("UC-WEB-01");
        var customer = await api.NewCustomer("register");
        var body = customer.Input;
        Check(customer.Id > 0, "Đăng ký trả mã khách");
        Assert.Equal("0", await api.Sql($"SELECT COUNT(*) FROM dbo.PhienDangNhap WHERE MaKhachHang={customer.Id}"));
        var receipt = (await api.Send("POST", "/api/auth/customer/register", body)).Expect(200, "Gửi lặp trả kết quả cũ");
        Assert.Equal(customer.Id, receipt.Json.Number("maKhachHang"));
        Check(!System.Text.RegularExpressions.Regex.IsMatch(receipt.Content, "token|MatKhau|Hash"), "Receipt không chứa bí mật");
        (await api.Send("GET", "/api/auth/customer/registrations/" + body.Text("MaThaoTac"))).Expect(200, "Tra kết quả sau mất phản hồi");
        var changed = body.Copy();
        changed["HoTen"] = "Nội dung khác";
        (await api.Send("POST", "/api/auth/customer/register", changed)).Expect(409, "Cùng mã khác nội dung");
        var duplicate = body.Copy();
        duplicate["MaThaoTac"] = NewId();
        (await api.Send("POST", "/api/auth/customer/register", duplicate)).Expect(409, "Chặn định danh trùng");
        var bad = body.Copy();
        bad["MaThaoTac"] = NewId();
        bad["XacNhanMatKhau"] = "sai";
        (await api.Send("POST", "/api/auth/customer/register", bad)).Expect(400, "Xác nhận mật khẩu sai");
        bad["XacNhanMatKhau"] = bad.Text("MatKhau");
        bad["Email"] = "khong-hop-le";
        (await api.Send("POST", "/api/auth/customer/register", bad)).Expect(400, "Email sai");
        bad["Email"] = null;
        bad["SoDienThoai"] = null;
        (await api.Send("POST", "/api/auth/customer/register", bad)).Expect(400, "Thiếu liên hệ");
        bad["MaThaoTac"] = Guid.Empty.ToString();
        (await api.Send("POST", "/api/auth/customer/register", bad)).Expect(400, "Mã thao tác rỗng");
        var failure = body.Copy();
        failure["MaThaoTac"] = NewId();
        failure["TenDangNhap"] = body.Text("TenDangNhap") + "atomic";
        failure["Email"] = "atomic" + body.Text("Email");
        // Cố tình làm audit lỗi trong database riêng, chứng minh không commit tài khoản dở dang.
        await api.Sql("DENY INSERT ON OBJECT::dbo.NhatKyHeThong TO fourchang_app");
        try
        {
            Check((await api.Send("POST", "/api/auth/customer/register", failure)).Status >= 400, "Audit lỗi không báo thành công");
        }
        finally
        {
            await api.Sql("REVOKE INSERT ON OBJECT::dbo.NhatKyHeThong FROM fourchang_app");
        }

        Assert.Equal("0", await api.Sql($"SELECT COUNT(*) FROM dbo.TaiKhoanKhachHang WHERE TenDangNhapChuanHoa='{failure.Text("TenDangNhap")}'"));
        var h = await api.Login(customer);
        (await api.Send("GET", "/api/admin/access/account", headers: h)).Expect(403, "Khách không có quyền Admin");
        (await api.Send("GET", "/api/pos/data/DonHang", headers: h)).Expect(403, "Khách không có quyền Staff");
        Assert.Equal("1", await api.Sql($"SELECT COUNT(*) FROM dbo.NhatKyHeThong WHERE MaThaoTac='{body.Text("MaThaoTac")}' AND HanhDong='REGISTER_CUSTOMER'"));
    }
}
