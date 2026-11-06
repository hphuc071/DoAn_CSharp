using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Integration;
public sealed class HttpRegressionTests
{

    /// <summary>
    /// Kiểm tra hồi quy hồ sơ nhân viên, rowversion, ca làm, phân quyền và tài khoản khách qua API thật.
    /// </summary>
    [Fact, Trait("Actor", "TichHop"), Trait("UseCase", "HttpRegression"), Trait("Layer", "API")]
    public async Task HoiQuyNhanVien_Ca_QuyenVaKhachHang()
    {
        await using var api = await TestApi.Start("HttpRegression");
        (await api.Send("GET", "/health/ready")).Expect(200, "Ready 35 bảng");
        (await api.Send("GET", "/api/admin/employees")).Expect(401, "Ẩn danh không đọc Admin");
        (await api.Send("GET", "/api/pos/employees")).Expect(401, "Máy lạ không đọc POS");
        var admin = await api.AdminLogin();
        (await api.Send("GET", "/api/admin/dashboard", headers: admin)).Expect(200, "Dashboard SQL");
        var employee = await api.Employee(admin);
        var input = Body(("MaThaoTac", NewId()), ("LyDo", "Cập nhật kiểm thử"), ("HoTen", employee.Text("hoTen") + " đã sửa"), ("SoDienThoai", null), ("NgayVaoLam", "2026-10-31T00:00:00Z"), ("TrangThai", "ACTIVE"), ("PhienBanDuLieu", employee.Text("phienBanDuLieu")));
        var updated = (await api.Send("PUT", $"/api/admin/employees/{employee.Number("maNhanVien")}", input, admin)).Expect(200, "Sửa nhân viên");
        input["MaThaoTac"] = NewId();
        (await api.Send("PUT", $"/api/admin/employees/{employee.Number("maNhanVien")}", input, admin)).Expect(409, "Rowversion nhân viên cũ");
        employee = updated.Json;
        var device = await api.NewDevice(admin);
        var open = api.OpenBody(employee, device);
        var shift = (await api.Send("POST", "/api/pos/shifts", open, device.Headers)).Expect(200, "Mở ca không tài khoản Staff").Json;
        var staff = TestApi.Bearer(shift);
        (await api.Send("GET", "/api/admin/dashboard", headers: staff)).Expect(403, "Staff không vào Dashboard");
        (await api.Send("GET", "/api/pos/data/DonHang", headers: staff)).Expect(200, "Staff đọc đơn");
        var resume = (await api.Send("POST", "/api/pos/shifts", open, device.Headers)).Expect(200, "Tiếp tục ca").Json;
        Assert.Equal(shift.Number("shiftId"), resume.Number("shiftId"));
        (await api.Send("POST", "/api/pos/shifts/current/close", Body(("TienMatThucDem", 0), ("LyDoChenhLech", "")), staff)).Expect(400, "Lệch tiền cần lý do");
        (await api.Send("POST", "/api/pos/shifts/current/close", Body(("TienMatThucDem", 500000), ("LyDoChenhLech", "")), staff)).Expect(200, "Đóng ca đúng tiền");
        (await api.Send("GET", "/api/pos/data/DonHang", headers: staff)).Expect(401, "Ca đóng không truy cập");
        input["MaThaoTac"] = NewId();
        input["TrangThai"] = "INACTIVE";
        input["PhienBanDuLieu"] = employee.Text("phienBanDuLieu");
        (await api.Send("PUT", $"/api/admin/employees/{employee.Number("maNhanVien")}", input, admin)).Expect(200, "Ngừng nhân viên giữ lịch sử");
        var customer = await api.NewCustomer();
        var h = await api.Login(customer);
        (await api.Send("GET", "/api/customer/profile", headers: h)).Expect(200, "Customer đọc hồ sơ riêng");
        (await api.Send("GET", "/api/admin/data/TaiKhoanKhachHang", headers: h)).Expect(403, "Không đọc toàn bộ khách");
        (await api.Send("GET", "/api/customer/orders", headers: h)).Expect(200, "Lịch sử đơn riêng");
        var accounts = (await api.Send("GET", "/api/admin/data/TaiKhoanKhachHang", headers: admin)).Expect(200, "Admin đọc khách");
        Check(!accounts.Content.Contains("MatKhauBam") && !accounts.Content.Contains("PBKDF2") && !accounts.Content.Contains("MaLamMoiDaBam"), "DTO không lộ hash");
        (await api.Send("POST", "/api/auth/logout", headers: h)).Expect(200, "Customer đăng xuất");
        (await api.Send("GET", "/api/customer/profile", headers: h)).Expect(401, "Token đã đăng xuất");
        (await api.Send("POST", "/api/auth/logout", headers: admin)).Expect(200, "Admin đăng xuất");
    }
}
