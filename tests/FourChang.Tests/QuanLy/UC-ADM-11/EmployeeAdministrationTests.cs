using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Admin;

/// <summary>ADM-11: kiểm thử HTTP/SQL thật trong fixture riêng, gồm idempotency, quyền, lịch sử và rollback audit.</summary>
[Collection("AdminFeatures"), Trait("UseCase", "UC-ADM-11"), Trait("Actor", "QuanLy")]
public sealed class EmployeeAdministrationTests(AdminFeatureFixture fixture)
{
    /// <summary>Tên trùng hợp lệ; mã thao tác lặp không sinh hồ sơ thứ hai, tìm tiếng Việt và địa chỉ đúng.</summary>
    [Fact]
    public async Task Them_Tim_PhanTrang_TenTrung_VaChongLap()
    {
        var input = WorkforceScenarios.EmployeeInput("Nguyễn Hoàng Minh " + Guid.NewGuid().ToString("N")[..8]);
        var first = await fixture.Send("POST", "/api/admin/employees", input);
        var repeated = await fixture.Send("POST", "/api/admin/employees", input);
        Assert.Equal(first.Number("maNhanVien"), repeated.Number("maNhanVien"));
        Assert.Equal(input.Text("DiaChiLienHe"), first.Text("diaChiLienHe"));
        var duplicateName = input.Copy(); duplicateName["MaThaoTac"] = NewId();
        var second = await fixture.Send("POST", "/api/admin/employees", duplicateName);
        Assert.NotEqual(first.Number("maNhanVien"), second.Number("maNhanVien"));
        var changed = input.Copy(); changed["HoTen"] = "Tên thay đổi";
        await fixture.Send("POST", "/api/admin/employees", changed, 409);
        var query = Uri.EscapeDataString(input.Text("HoTen"));
        var page = await fixture.Send("GET", "/api/admin/employees/search?search=" + query + "&page=1&pageSize=1");
        Assert.Equal(2, page.Number("total")); Assert.Single(page["items"]!.AsArray());
        await fixture.Send("GET", "/api/admin/employees/search?page=0", expected: 400);
        var empty = await fixture.Send("GET", "/api/admin/employees/search?search=NO-SUCH-" + Guid.NewGuid().ToString("N"));
        Assert.Empty(empty["items"]!.AsArray());
        Assert.Null(first["tenDangNhap"]); Assert.Null(first["maNhanVienHienThi"]);
    }

    /// <summary>Sửa địa chỉ/họ tên có version; ngừng/kích hoạt có lý do và không ghi đè bản cũ.</summary>
    [Fact]
    public async Task Sua_HoSo_TrangThai_VaVersion()
    {
        var input = WorkforceScenarios.EmployeeInput();
        var employee = await fixture.Send("POST", "/api/admin/employees", input);
        var id = employee.Number("maNhanVien");
        input["MaThaoTac"] = NewId(); input["PhienBanDuLieu"] = employee.Text("phienBanDuLieu"); input["DiaChiLienHe"] = "24 Đường Hoa Sữa, Hà Nội";
        var edited = await fixture.Send("PUT", "/api/admin/employees/" + id, input);
        Assert.Equal(input.Text("DiaChiLienHe"), edited.Text("diaChiLienHe"));
        input["MaThaoTac"] = NewId(); await fixture.Send("PUT", "/api/admin/employees/" + id, input, 409);
        var status = Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", edited.Text("phienBanDuLieu")), ("TrangThai", "INACTIVE"), ("LyDo", "Nhân viên xin nghỉ"));
        var inactive = await fixture.Send("POST", $"/api/admin/employees/{id}/status", status);
        Assert.Equal("INACTIVE", inactive.Text("trangThai"));
        status["MaThaoTac"] = NewId(); status["PhienBanDuLieu"] = inactive.Text("phienBanDuLieu"); status["TrangThai"] = "ACTIVE";
        Assert.Equal("ACTIVE", (await fixture.Send("POST", $"/api/admin/employees/{id}/status", status)).Text("trangThai"));
        Assert.Equal("0", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.CaLamViec WHERE MaNhanVien={id}"));
    }

    /// <summary>Có ca thì không ngừng/xóa, sửa tên không thay snapshot người đã mở ca.</summary>
    [Fact]
    public async Task CoCa_ChanNgung_Xoa_VaGiuSnapshot()
    {
        var s = await WorkforceScenarios.Open(fixture);
        var id = s.Employee.Number("maNhanVien");
        var status = Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", s.Employee.Text("phienBanDuLieu")), ("TrangThai", "INACTIVE"), ("LyDo", "Kiểm tra ca mở"));
        await fixture.Send("POST", $"/api/admin/employees/{id}/status", status, 409);
        await fixture.Send("POST", $"/api/admin/employees/{id}/delete", Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", s.Employee.Text("phienBanDuLieu"))), 409);
        var input = WorkforceScenarios.EmployeeInput("Tên mới sau khi đã mở ca"); input["PhienBanDuLieu"] = s.Employee.Text("phienBanDuLieu");
        await fixture.Send("PUT", "/api/admin/employees/" + id, input);
        var detail = await WorkforceScenarios.Detail(fixture, s.Session.Number("shiftId"));
        Assert.Equal(s.Employee.Text("hoTen"), detail["ca"]!.Text("hoTen"));
    }

    /// <summary>Chỉ xóa chưa tham chiếu; audit hỏng rollback toàn bộ và client không mất mã chống trùng.</summary>
    [Fact]
    public async Task XoaCoDieuKien_VaRollbackAudit()
    {
        var input = WorkforceScenarios.EmployeeInput();
        var employee = await fixture.Send("POST", "/api/admin/employees", input);
        var id = employee.Number("maNhanVien");
        var deletion = Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", employee.Text("phienBanDuLieu")));
        await fixture.Send("POST", $"/api/admin/employees/{id}/delete", deletion);
        await fixture.Send("POST", $"/api/admin/employees/{id}/delete", deletion);
        (await fixture.Api.Send("GET", "/api/admin/employees/" + id, headers: fixture.Admin)).Expect(404, "Hồ sơ đã xóa");
        var rollback = WorkforceScenarios.EmployeeInput();
        await fixture.Api.Sql("DENY INSERT ON OBJECT::dbo.NhatKyHeThong TO fourchang_app");
        try { Assert.True((await fixture.Api.Send("POST", "/api/admin/employees", rollback, fixture.Admin)).Status >= 400); }
        finally { await fixture.Api.Sql("REVOKE INSERT ON OBJECT::dbo.NhatKyHeThong FROM fourchang_app"); }
        Assert.Equal("0", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.NhanVien WHERE HoTen=N'{rollback.Text("HoTen")}'"));
        await fixture.Send("POST", "/api/admin/employees", rollback);
    }

    /// <summary>Dữ liệu sai không ghi; Customer/Staff không thể dùng chức năng Admin.</summary>
    [Fact]
    public async Task DuLieuSai_VaPhanQuyen()
    {
        var input = WorkforceScenarios.EmployeeInput(); input["HoTen"] = " ";
        await fixture.Send("POST", "/api/admin/employees", input, 400);
        input = WorkforceScenarios.EmployeeInput(); input["SoDienThoai"] = "abc";
        await fixture.Send("POST", "/api/admin/employees", input, 400);
        input = WorkforceScenarios.EmployeeInput(); input["DiaChiLienHe"] = new string('a', 501);
        await fixture.Send("POST", "/api/admin/employees", input, 400);
        (await fixture.Api.Send("GET", "/api/admin/employees/search")).Expect(401, "Ẩn danh");
        var customer = await fixture.Api.Login(await fixture.Api.NewCustomer("employee-rights"));
        (await fixture.Api.Send("POST", "/api/admin/employees", WorkforceScenarios.EmployeeInput(), customer)).Expect(403, "Customer không quản lý nhân viên");
        var shift = await WorkforceScenarios.Open(fixture);
        (await fixture.Api.Send("GET", "/api/admin/employees", headers: shift.Headers)).Expect(403, "Staff không quản lý nhân viên");
    }
}
