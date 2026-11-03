using FourChang.Tests.Shared;
using Microsoft.Playwright;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Integration;
// Chuyển toàn bộ test Web liên phân hệ sang C#, giữ lỗi mạng sau commit và cookie thu hồi chờ.
public sealed class AccountAccessBrowserTests
{

    /// <summary>
    /// Kiểm tra tài khoản trên Web thật, responsive, CSRF, mất phản hồi sau commit và hoàn tất thu hồi khi có mạng trở lại.
    /// </summary>
    [Fact, Trait("Actor", "TichHop"), Trait("UseCase", "AccountAccess"), Trait("Layer", "Web")]
    public async Task WebDangKy_HoSo_DiaChi_MatKhau_LogoutOffline_VaReceipt()
    {
        await using var f = await BrowserFixture.Start("AccountAccess-Web");
        var page = f.Page;
        var api = f.Api;
        await page.GotoAsync(f.WebUrl + "/");
        await f.Screenshot("web-home-desktop");
        await page.SetViewportSizeAsync(390, 844);
        await page.ReloadAsync();
        Check(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth<=innerWidth+1"), "Trang chủ mobile không tràn ngang");
        await f.Screenshot("web-home-mobile");
        await page.SetViewportSizeAsync(1440, 1080);
        await page.GotoAsync(f.WebUrl + "/Account/Login");
        Assert.Equal(1, await page.Locator(".auth-brand-mark").CountAsync());
        Assert.Equal(0, await page.Locator(".auth-visual .drink-photo").CountAsync());
        Check(await page.Locator(".auth-form .validation-summary-valid").IsHiddenAsync(), "Không dải thông báo trống");
        await f.Screenshot("web-login");
        await page.SetViewportSizeAsync(390, 844);
        Check(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth<=innerWidth+1"), "Login mobile không tràn ngang");
        await f.Screenshot("web-login-mobile");
        await page.SetViewportSizeAsync(1440, 1080);
        await page.GotoAsync(f.WebUrl + "/Account/Register");
        await f.Screenshot("web-register");
        var username = "ui" + Guid.NewGuid().ToString("N")[..12];
        var password = Password();
        await f.Field("Họ tên").FillAsync("Khách kiểm tra liên phân hệ");
        await f.Field("Tên đăng nhập").FillAsync(username);
        await f.Field("Email").FillAsync(username + "@example.test");
        await f.Field("Mật khẩu").FillAsync(password);
        await f.Field("Xác nhận mật khẩu").FillAsync(password);
        await f.Button("Tạo tài khoản").ClickAsync();
        await page.WaitForURLAsync("**/Account/Login");
        Check(!await f.HasCookie("fourchang_session"), "Đăng ký không tự cấp cookie");
        await f.Login(username, password);
        await page.GotoAsync(f.WebUrl + "/Account/Profile");
        Check(await page.GetByRole(AriaRole.Heading, new() { Name = "Hồ sơ của tôi" }).IsVisibleAsync(), "Mở hồ sơ thật");
        var token = await f.Token();
        var form = f.Context.APIRequest.CreateFormData();
        form.Set("HoTen", "Tấn công thiếu CSRF");
        var csrf = await f.Context.APIRequest.PostAsync(f.WebUrl + "/Account/Profile?handler=Save", new() { Form = form });
        Assert.Equal(400, csrf.Status);
        await f.Field("Họ tên").FillAsync("Tên đổi từ giao diện Web");
        f.Proxy.DropProfile = true;
        await f.Button("Lưu hồ sơ").ClickAsync();
        await page.GetByRole(AriaRole.Alert).WaitForAsync();
        Assert.Equal(1, f.Proxy.ProfileWrites);
        Assert.Equal("Tên đổi từ giao diện Web", await f.Field("Họ tên").InputValueAsync());
        await f.Screenshot("profile-lost-reply");
        await f.Button("Lưu hồ sơ").ClickAsync();
        await page.WaitForURLAsync("**/Account/Profile");
        Assert.Equal(1, f.Proxy.ProfileWrites);
        Assert.Equal("Tên đổi từ giao diện Web", await f.Field("Họ tên").InputValueAsync());
        await f.Field("Nhãn địa chỉ").FillAsync("Nhà riêng");
        await f.Field("Người nhận").FillAsync("Người nhận kiểm thử");
        await f.Field("Điện thoại nhận hàng").FillAsync("0987654321");
        await f.Field("Địa chỉ nhận hàng").FillAsync("Địa chỉ kiểm tra Web");
        await f.Field("Đặt làm địa chỉ mặc định").CheckAsync();
        await f.Button("Lưu địa chỉ").ClickAsync();
        await page.WaitForURLAsync("**/Account/Profile");
        Assert.Equal(1, await page.Locator(".account-address").CountAsync());
        await f.Screenshot("profile-address");
        await page.GetByRole(AriaRole.Link, new() { Name = "Sửa địa chỉ", Exact = true }).ClickAsync();
        await f.Field("Địa chỉ nhận hàng").FillAsync("Địa chỉ Web đã chỉnh sửa");
        await f.Button("Lưu địa chỉ").ClickAsync();
        await page.WaitForURLAsync("**/Account/Profile");
        Assert.Contains("Địa chỉ Web đã chỉnh sửa", await page.Locator(".account-address").InnerTextAsync());
        await f.Button("Bỏ địa chỉ").ClickAsync();
        await page.WaitForURLAsync("**/Account/Profile");
        Assert.Equal(0, await page.Locator(".account-address").CountAsync());
        var next = Password();
        await f.Field("Mật khẩu hiện tại").FillAsync(password);
        await f.Field("Mật khẩu mới").FillAsync(next);
        await f.Field("Xác nhận mật khẩu mới").FillAsync("Khong-khop-123");
        await f.Button("Đổi mật khẩu").ClickAsync();
        await page.GetByRole(AriaRole.Alert).WaitForAsync();
        Assert.Equal("", await f.Field("Mật khẩu hiện tại").InputValueAsync());
        Assert.Equal("", await f.Field("Mật khẩu mới").InputValueAsync());
        await f.Field("Mật khẩu hiện tại").FillAsync(password);
        await f.Field("Mật khẩu mới").FillAsync(next);
        await f.Field("Xác nhận mật khẩu mới").FillAsync(next);
        await f.Button("Đổi mật khẩu").ClickAsync();
        await page.WaitForURLAsync("**/Account/Login");
        Check(!await f.HasCookie("fourchang_session"), "Đổi mật khẩu xóa cookie");
        (await api.Send("GET", "/api/customer/profile", headers: new() { ["Authorization"] = "Bearer " + token })).Expect(401, "Token đổi mật khẩu bị thu hồi");
        await f.Login(username, next);
        var logoutToken = await f.Token();
        await page.GotoAsync(f.WebUrl + "/Account/Logout");
        f.Proxy.Offline = true;
        await f.Button("Đăng xuất").ClickAsync();
        await page.WaitForURLAsync(f.WebUrl + "/");
        Check(!await f.HasCookie("fourchang_session"), "Logout offline xóa cookie ngay");
        var pending = (await f.Context.CookiesAsync()).Single(x => x.Name == "fourchang_pending_logout");
        Check(pending.HttpOnly && !pending.Value.Contains(logoutToken), "Thu hồi chờ mã hóa HttpOnly");
        f.Proxy.Offline = false;
        await page.GotoAsync(f.WebUrl + "/");
        Check(!await f.HasCookie("fourchang_pending_logout"), "Có mạng tự hoàn tất thu hồi");
        (await api.Send("GET", "/api/customer/profile", headers: new() { ["Authorization"] = "Bearer " + logoutToken })).Expect(401, "Thu hồi logout phía API");
        await page.GotoAsync(f.WebUrl + "/Account/Profile");
        await page.WaitForURLAsync("**/Account/Login");
        var admin = await api.AdminLogin();
        var input = api.CustomerInput();
        input["TenDangNhap"] = username + "temp";
        input["HoTen"] = "Khách kiểm tra mật khẩu tạm";
        (await api.Send("POST", "/api/admin/customers", input, admin)).Expect(200, "Admin tạo khách tạm");
        await f.Login(input.Text("TenDangNhap"), input.Text("MatKhauTam"), "/Account/ChangeTemporaryPassword");
        var limitedToken = await f.Token();
        (await api.Send("GET", "/api/customer/profile", headers: new() { ["Authorization"] = "Bearer " + limitedToken })).Expect(403, "Web phiên tạm không đọc hồ sơ");
        var finalPassword = Password();
        await f.Field("Mật khẩu mới").FillAsync(finalPassword);
        await f.Field("Xác nhận mật khẩu mới").FillAsync(finalPassword);
        f.Proxy.DropTemporary = true;
        await f.Button("Lưu mật khẩu mới").ClickAsync();
        await f.Button("Kiểm tra / thử lại").WaitForAsync();
        Assert.Equal(1, f.Proxy.TemporaryWrites);
        await f.Button("Kiểm tra / thử lại").ClickAsync();
        await page.WaitForURLAsync("**/Account/Login");
        Assert.Equal(1, f.Proxy.TemporaryWrites);
        await f.Login(input.Text("TenDangNhap"), finalPassword);
        (await api.Send("POST", "/api/auth/logout", headers: admin)).Expect(200, "Dọn phiên Admin fixture");
    }
}
