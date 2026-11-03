using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Integration;
// Kiểm tra liên thông Web/Admin/POS bằng cùng dữ liệu SQL và quyền API thật.
public sealed class AccountAccessTests
{

    /// <summary>
    /// Đối chiếu cùng hồ sơ Web/Admin, hiệu lực khóa và reset, quyền Staff và việc chặn ca khi máy bị thu hồi.
    /// </summary>
    [Fact, Trait("Actor", "TichHop"), Trait("UseCase", "AccountAccess"), Trait("Layer", "API")]
    public async Task HoSo_Khoa_Reset_VaThuHoiMay_DongBoBaPhanHe()
    {
        await using var api = await TestApi.Start("AccountAccess");
        var admin = await api.AdminLogin();
        var customer = await api.NewCustomer("cross");
        var h = await api.Login(customer);
        var id = customer.Id;
        var a = await api.CustomerAccount(id, admin);
        Assert.Equal(a.Text("hoTen"), (await api.Profile(h)).Text("hoTen"));
        var save = Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", a.Text("phienBanDuLieu")), ("HoTen", "Khách sửa qua Web"), ("Email", customer.Input.Text("Email")), ("SoDienThoai", null));
        (await api.Send("PUT", "/api/customer/profile", save, h)).Expect(200, "Web sửa hồ sơ");
        a = await api.CustomerAccount(id, admin);
        Assert.Equal("Khách sửa qua Web", a.Text("hoTen"));
        (await api.Send("POST", $"/api/admin/customers/{id}/status", TestApi.Action(a, ("TrangThai", "LOCKED")), admin)).Expect(200, "Admin khóa");
        (await api.Send("GET", "/api/customer/profile", headers: h)).Expect(401, "Phiên Web bị chặn ngay");
        a = await api.CustomerAccount(id, admin);
        (await api.Send("POST", $"/api/admin/customers/{id}/status", TestApi.Action(a, ("TrangThai", "ACTIVE")), admin)).Expect(200, "Mở lại");
        (await api.Send("GET", "/api/customer/profile", headers: h)).Expect(401, "Không phục hồi token cũ");
        h = await api.Login(customer);
        a = await api.CustomerAccount(id, admin);
        var temporary = Password();
        (await api.Send("POST", $"/api/admin/customers/{id}/reset-password", TestApi.Action(a, ("MatKhauTam", temporary)), admin)).Expect(200, "Đặt mật khẩu tạm");
        (await api.Send("GET", "/api/customer/profile", headers: h)).Expect(401, "Reset thu hồi phiên");
        customer.Input["MatKhau"] = temporary;
        var limited = await api.Login(customer);
        (await api.Send("GET", "/api/customer/profile", headers: limited)).Expect(403, "Phiên tạm hạn chế");
        (await api.Send("GET", "/api/admin/access/devices", headers: limited)).Expect(403, "Phiên tạm không quản trị");
        var context = (await api.Send("GET", "/api/auth/customer/temporary-password", headers: limited)).Expect(200, "Ngữ cảnh đổi mật khẩu tạm").Json;
        var newPassword = Password();
        var change = Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", context.Text("phienBanDuLieu")), ("MatKhauMoi", newPassword), ("XacNhanMatKhau", newPassword));
        (await api.Send("POST", "/api/auth/customer/temporary-password", change, limited)).Expect(200, "Đổi mật khẩu tạm");
        (await api.Send("GET", "/api/customer/profile", headers: limited)).Expect(401, "Thu hồi phiên tạm");
        (await api.Send("GET", "/api/customer/account/operations/" + change.Text("MaThaoTac"), headers: limited)).Expect(200, "Nguồn đọc receipt timeout");
        customer.Input["MatKhau"] = newPassword;
        h = await api.Login(customer);
        (await api.Send("GET", "/api/customer/profile", headers: h)).Expect(200, "Đăng nhập mới");
        (await api.Send("GET", "/api/admin/access/account", headers: h)).Expect(403, "Customer không có quyền Admin");
        var device = await api.NewDevice(admin);
        var employee = (await api.Send("GET", "/api/pos/employees", headers: device.Headers)).Expect(200, "Danh sách nhân viên fixture").Json[0]!;
        var shift = (await api.Send("POST", "/api/pos/shifts", api.OpenBody(employee, device), device.Headers)).Expect(200, "POS mở ca").Json;
        var staff = TestApi.Bearer(shift);
        (await api.Send("GET", "/api/customer/profile", headers: staff)).Expect(403, "Staff không đọc Customer");
        (await api.Send("GET", "/api/admin/customers", headers: staff)).Expect(403, "Staff không quản lý khách");
        (await api.Send("POST", "/api/admin/access/devices", device.Input, staff)).Expect(403, "Staff không cấp máy");
        (await api.Send("POST", $"/api/admin/access/devices/{device.Record.Number("maThietBiPOS")}/revoke", TestApi.Action(device.Record), admin)).Expect(200, "Admin thu hồi máy");
        (await api.Send("GET", "/api/pos/data/DonHang", headers: staff)).Expect(401, "Token ca bị chặn ngay");
        Assert.Equal("1", await api.Sql($"SELECT COUNT(*) FROM dbo.CaLamViec WHERE MaCaLamViec={shift.Number("shiftId")} AND TrangThai='OPEN'"));
        Assert.Equal("1", await api.Sql("SELECT COUNT(*) FROM dbo.TaiKhoanQuanLy"));
    }
}
