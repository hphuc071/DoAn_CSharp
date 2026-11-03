using FourChang.Tests.Shared;
using Microsoft.Playwright;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Admin;
// Test Web này thuộc UC-ADM-01: Admin cấp mật khẩu tạm, khách bắt buộc đổi trước khi dùng hồ sơ.
public sealed class TemporaryPasswordBrowserTests
{

    /// <summary>
    /// Kiểm tra biểu mẫu đổi mật khẩu tạm: xác nhận sai, xóa mật khẩu khỏi HTML, chống CSRF và đăng nhập bằng mật khẩu mới.
    /// </summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-01"), Trait("Layer", "Web")]
    public async Task MatKhauTam_FormLoi_CSRF_VaDangNhapLai()
    {
        await using var f = await BrowserFixture.Start("UC-ADM-01-Web");
        var admin = await f.Api.AdminLogin();
        var input = f.Api.CustomerInput();
        input["HoTen"] = "Khách kiểm tra đổi mật khẩu tạm";
        (await f.Api.Send("POST", "/api/admin/customers", input, admin)).Expect(200, "Tạo tài khoản tạm");
        await f.Login(input.Text("TenDangNhap"), input.Text("MatKhauTam"), "/Account/ChangeTemporaryPassword");
        await f.Screenshot("web-temporary-password");
        var password = Password();
        await f.Field("Mật khẩu mới").FillAsync(password);
        await f.Field("Xác nhận mật khẩu mới").FillAsync(password + "different");
        await f.Button("Lưu mật khẩu mới").ClickAsync();
        Check(await f.Page.GetByText("Xác nhận mật khẩu chưa khớp.", new() { Exact = true }).IsVisibleAsync(), "Thông báo xác nhận sai");
        Assert.Equal("", await f.Field("Mật khẩu mới").InputValueAsync());
        Assert.Equal("", await f.Field("Xác nhận mật khẩu mới").InputValueAsync());
        var form = f.Context.APIRequest.CreateFormData();
        form.Set("MatKhauMoi", password);
        form.Set("XacNhanMatKhau", password);
        var csrf = await f.Context.APIRequest.PostAsync(f.WebUrl + "/Account/ChangeTemporaryPassword", new() { Form = form });
        Assert.Equal(400, csrf.Status);
        await f.Field("Mật khẩu mới").FillAsync(password);
        await f.Field("Xác nhận mật khẩu mới").FillAsync(password);
        await f.Button("Lưu mật khẩu mới").ClickAsync();
        await f.Page.WaitForURLAsync("**/Account/Login");
        await f.Login(input.Text("TenDangNhap"), password);
        await f.Page.GotoAsync(f.WebUrl + "/Account/Profile");
        Assert.Equal(input.Text("HoTen"), await f.Field("Họ tên").InputValueAsync());
        (await f.Api.Send("POST", "/api/auth/logout", headers: admin)).Expect(200, "Đăng xuất Admin fixture");
    }
}
