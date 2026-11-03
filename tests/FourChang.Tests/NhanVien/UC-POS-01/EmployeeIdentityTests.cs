using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Staff;
// Nhân viên nhập mã và tên; không tạo tài khoản đăng nhập cá nhân cho Staff.
public sealed class EmployeeIdentityTests
{

    /// <summary>
    /// Kiểm tra nhận diện bằng mã và tên, trường hợp trùng tên, nhân viên ngừng làm và mở hoặc tiếp tục đúng ca.
    /// </summary>
    [Fact, Trait("Actor", "NhanVien"), Trait("UseCase", "UC-POS-01"), Trait("Layer", "API")]
    public async Task NhapMaVaTen_TrungTen_NgungLam_VaMoCa()
    {
        await using var api = await TestApi.Start("UC-POS-01");
        var admin = await api.AdminLogin();
        var name = "Nhân viên trùng tên kiểm thử";
        var first = await api.Employee(admin, name);
        var second = await api.Employee(admin, name);
        var device = await api.NewDevice(admin, Environment.MachineName.ToUpperInvariant());
        var lookup = Body(("MaNhanVien", first.Number("maNhanVien")), ("HoTen", name));
        (await api.Send("POST", "/api/pos/employees/verify", lookup)).Expect(401, "Máy chưa được cấp");
        var r = (await api.Send("POST", "/api/pos/employees/verify", lookup, device.Headers)).Expect(200, "Mã và tên đúng");
        Assert.Equal(first.Number("maNhanVien"), r.Json.Number("maNhanVien"));
        lookup["MaNhanVien"] = second.Number("maNhanVien");
        r = (await api.Send("POST", "/api/pos/employees/verify", lookup, device.Headers)).Expect(200, "Tên trùng mã thứ hai");
        Assert.Equal(second.Number("maNhanVien"), r.Json.Number("maNhanVien"));
        lookup["MaNhanVien"] = first.Number("maNhanVien");
        lookup["HoTen"] = "  NHÂN VIÊN  TRÙNG TÊN KIỂM THỬ  ";
        (await api.Send("POST", "/api/pos/employees/verify", lookup, device.Headers)).Expect(200, "Chuẩn hóa hoa thường/khoảng trắng");
        lookup["HoTen"] = "Nhan vien trung ten kiem thu";
        (await api.Send("POST", "/api/pos/employees/verify", lookup, device.Headers)).Expect(404, "Không tự bỏ dấu");
        lookup["HoTen"] = "Sai họ tên";
        (await api.Send("POST", "/api/pos/employees/verify", lookup, device.Headers)).Expect(404, "Tên sai");
        lookup["HoTen"] = name;
        lookup["MaNhanVien"] = 9223372036854775800;
        (await api.Send("POST", "/api/pos/employees/verify", lookup, device.Headers)).Expect(404, "Mã không tồn tại");
        lookup["MaNhanVien"] = 0;
        (await api.Send("POST", "/api/pos/employees/verify", lookup, device.Headers)).Expect(400, "Mã không hợp lệ");
        lookup["MaNhanVien"] = first.Number("maNhanVien");
        lookup["HoTen"] = "";
        (await api.Send("POST", "/api/pos/employees/verify", lookup, device.Headers)).Expect(400, "Tên bắt buộc");
        var open = api.OpenBody(first, device);
        open["HoTen"] = "Sai họ tên";
        (await api.Send("POST", "/api/pos/shifts", open, device.Headers)).Expect(400, "Mở ca xác thực lại tên");
        Assert.Equal("0", await api.Sql($"SELECT COUNT(*) FROM dbo.CaLamViec WHERE MaMay='{device.Input.Text("MaMay")}'"));
        var change = Body(("HoTen", name), ("SoDienThoai", "0912345678"), ("NgayVaoLam", "2026-11-01"), ("TrangThai", "INACTIVE"), ("PhienBanDuLieu", first.Text("phienBanDuLieu")));
        var inactive = (await api.Send("PUT", $"/api/admin/employees/{first.Number("maNhanVien")}", change, admin)).Expect(200, "Ngừng làm fixture").Json;
        lookup["HoTen"] = name;
        (await api.Send("POST", "/api/pos/employees/verify", lookup, device.Headers)).Expect(404, "Nhân viên ngừng làm không xác minh");
        open["HoTen"] = name;
        (await api.Send("POST", "/api/pos/shifts", open, device.Headers)).Expect(400, "Ngừng làm không mở ca");
        change["TrangThai"] = "ACTIVE";
        change["PhienBanDuLieu"] = inactive.Text("phienBanDuLieu");
        first = (await api.Send("PUT", $"/api/admin/employees/{first.Number("maNhanVien")}", change, admin)).Expect(200, "Kích hoạt lại fixture").Json;
        var shift = (await api.Send("POST", "/api/pos/shifts", open, device.Headers)).Expect(200, "Mở ca đúng danh tính").Json;
        var staff = TestApi.Bearer(shift);
        Assert.Equal(first.Number("maNhanVien"), shift.Number("id"));
        Assert.Equal(name, shift.Text("name"));
        (await api.Send("POST", "/api/pos/shifts", open, device.Headers)).Expect(200, "Tiếp tục cùng ca");
        Assert.Equal("1", await api.Sql($"SELECT COUNT(*) FROM dbo.CaLamViec WHERE MaMay='{device.Input.Text("MaMay")}' AND TrangThai='OPEN'"));
        (await api.Send("GET", "/api/admin/customers", headers: staff)).Expect(403, "Staff không có quyền Admin");
        (await api.Send("POST", "/api/pos/shifts/current/close", Body(("TienMatThucDem", 500000), ("LyDoChenhLech", null)), staff)).Expect(200, "Kết thúc ca đúng tiền");
    }
}
