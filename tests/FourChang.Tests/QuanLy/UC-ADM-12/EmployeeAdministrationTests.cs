using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Admin;

/// <summary>ADM-12 kiểm thử HTTP/SQL thật: không giả lập EF hoặc bỏ qua ràng buộc của database.</summary>
public sealed class EmployeeAdministrationTests
{

    /// <summary>Mất phản hồi sau commit vẫn tra được receipt, gửi lại không tạo hồ sơ thứ hai; offline không ghi.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-12"), Trait("Layer", "API")]
    public async Task MatPhanHoiSauCommit_VaOffline_KhongTaoTrung()
    {
        await using var api = await TestApi.Start("UC-ADM-12-Network");
        var admin = await api.AdminLogin();
        await using var proxy = new NetworkFaultProxy(api.Url);
        using var http = new HttpClient { BaseAddress = new Uri(proxy.Url), Timeout = TimeSpan.FromSeconds(5) };
        var client = new FourChang.Presentation.Shared.ApiClients.CafeApiClient(http) { Token = admin["Authorization"][7..] };
        var input = new FourChang.Presentation.Shared.Contracts.Admin.EmployeeInput("Nhân viên lỗi mạng", null, new DateTime(2026, 11, 1), "ACTIVE", MaThaoTac: Guid.NewGuid());
        proxy.DropWorkforce = true;
        await Assert.ThrowsAsync<FourChang.Presentation.Shared.ApiClients.CafeApiException>(() => client.SaveEmployeeAsync(input));
        var saved = await client.EmployeeReceiptAsync(input.MaThaoTac);
        var repeated = await client.SaveEmployeeAsync(input);
        Assert.Equal(saved.MaNhanVien, repeated.MaNhanVien);
        Assert.Equal("1", await api.Sql("SELECT COUNT(*) FROM dbo.NhanVien WHERE HoTen=N'Nhân viên lỗi mạng'"));
        proxy.Offline = true;
        await Assert.ThrowsAsync<FourChang.Presentation.Shared.ApiClients.CafeApiException>(() => client.SaveEmployeeAsync(input with { MaThaoTac = Guid.NewGuid(), HoTen = "Không lưu offline" }));
        Assert.Equal("0", await api.Sql("SELECT COUNT(*) FROM dbo.NhanVien WHERE HoTen=N'Không lưu offline'"));
    }

    /// <summary>Kiểm tra các luồng hồ sơ, tên trùng, tìm tiếng Việt, phân trang, chống trùng, phiên bản, trạng thái và rollback audit.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-12"), Trait("Layer", "API")]
    public async Task HoSoNhanVien_TraCuu_ChongTrung_PhanQuyen_VaRollback()
    {
        await using var api = await TestApi.Start("UC-ADM-12");
        var admin = await api.AdminLogin();
        const string root = "/api/admin/employees";
        (await api.Send("GET", root + "/search")).Expect(401, "Ẩn danh không xem hồ sơ");
        var customer = await api.NewCustomer();
        (await api.Send("GET", root + "/search", headers: await api.Login(customer))).Expect(403, "Customer không có quyền");
        var input = Body(("MaThaoTac", NewId()), ("HoTen", "Nguyễn Ánh Workforce"), ("SoDienThoai", "0912345678"), ("NgayVaoLam", "2026-11-01"), ("TrangThai", "ACTIVE"));
        var invalid = input.Copy(); invalid["HoTen"] = " ";
        (await api.Send("POST", root, invalid, admin)).Expect(400, "Thiếu họ tên");
        invalid = input.Copy(); invalid["SoDienThoai"] = "abc";
        (await api.Send("POST", root, invalid, admin)).Expect(400, "Điện thoại sai");
        invalid = input.Copy(); invalid.Remove("NgayVaoLam");
        (await api.Send("POST", root, invalid, admin)).Expect(400, "Ngày chưa nhập");
        invalid = input.Copy(); invalid.Remove("MaThaoTac");
        (await api.Send("POST", root, invalid, admin)).Expect(400, "Thiếu mã thao tác");
        var competing = await Task.WhenAll(api.Send("POST", root, input, admin), api.Send("POST", root, input, admin));
        var first = competing[0].Expect(200, "Tạo hồ sơ cạnh tranh").Json;
        Assert.Equal(first.Number("maNhanVien"), competing[1].Expect(200, "Cùng mã hiệu ứng một lần").Json.Number("maNhanVien"));
        var repeated = (await api.Send("POST", root, input, admin)).Expect(200, "Receipt gửi lặp").Json;
        Assert.Equal(first.Number("maNhanVien"), repeated.Number("maNhanVien"));
        invalid = input.Copy(); invalid["HoTen"] = "Nội dung khác";
        (await api.Send("POST", root, invalid, admin)).Expect(409, "Cùng mã khác nội dung");
        (await api.Send("GET", root + "/operations/" + input.Text("MaThaoTac"), headers: admin)).Expect(200, "Tra receipt");
        input["MaThaoTac"] = NewId();
        var second = (await api.Send("POST", root, input, admin)).Expect(200, "Tên/số liên hệ trùng không tự gộp").Json;
        Assert.NotEqual(first.Number("maNhanVien"), second.Number("maNhanVien"));
        var found = (await api.Send("GET", root + "/search?search=nguyen%20anh%20workforce&page=1&pageSize=1", headers: admin)).Expect(200, "Tìm tiếng Việt không dấu").Json;
        Assert.Equal(2, found.Number("total")); Assert.Single(found["items"]!.AsArray());
        (await api.Send("GET", root + "/search?page=0", headers: admin)).Expect(400, "Trang không hợp lệ");
        (await api.Send("GET", root + "/search?status=UNKNOWN", headers: admin)).Expect(400, "Trạng thái lọc sai");
        var empty = (await api.Send("GET", root + "/search?search=KhongCoTenNayXyz", headers: admin)).Expect(200, "Rỗng hợp lệ").Json;
        Assert.Equal(0, empty.Number("total"));
        var id = first.Number("maNhanVien");
        input["HoTen"] = "Nguyễn Ánh đã sửa"; input["PhienBanDuLieu"] = first.Text("phienBanDuLieu"); input["MaThaoTac"] = NewId();
        first = (await api.Send("PUT", root + "/" + id, input, admin)).Expect(200, "Sửa hồ sơ").Json;
        input["MaThaoTac"] = NewId();
        (await api.Send("PUT", root + "/" + id, input, admin)).Expect(409, "Phiên bản cũ");
        var device = await api.NewDevice(admin);
        var session = (await api.Send("POST", "/api/pos/shifts", api.OpenBody(first, device), device.Headers)).Expect(200, "POS dùng hồ sơ mới").Json;
        var staff = TestApi.Bearer(session);
        (await api.Send("GET", root + "/search", headers: staff)).Expect(403, "Staff không xem Admin");
        var status = TestApi.Action(first, ("TrangThai", "INACTIVE"));
        (await api.Send("POST", root + $"/{id}/status", status, admin)).Expect(409, "Còn ca chặn ngừng làm");
        (await api.Send("POST", "/api/pos/shifts/current/close", Body(("TienMatThucDem", 500000)), staff)).Expect(200, "POS chốt trước ngừng");
        first = (await api.Send("POST", root + $"/{id}/status", status, admin)).Expect(200, "Ngừng làm giữ ca cũ").Json;
        (await api.Send("POST", "/api/pos/employees/verify", Body(("MaNhanVien", id), ("HoTen", first.Text("hoTen"))), device.Headers)).Expect(404, "Ngừng làm không mở ca");
        status = TestApi.Action(first, ("TrangThai", "ACTIVE"));
        first = (await api.Send("POST", root + $"/{id}/status", status, admin)).Expect(200, "Kích hoạt lại").Json;
        Assert.Equal("CLOSED", await api.Sql($"SELECT TrangThai FROM dbo.CaLamViec WHERE MaCaLamViec={session.Number("shiftId")}"));
        var beforeCount = await api.Sql("SELECT COUNT(*) FROM dbo.NhanVien");
        var atomic = Body(("MaThaoTac", NewId()), ("HoTen", "Rollback employee"), ("NgayVaoLam", "2026-11-01"), ("TrangThai", "ACTIVE"));
        await api.Sql("DENY INSERT ON OBJECT::dbo.NhatKyHeThong TO fourchang_app");
        try { Check((await api.Send("POST", root, atomic, admin)).Status >= 400, "Audit lỗi không tạo hồ sơ"); }
        finally { await api.Sql("REVOKE INSERT ON OBJECT::dbo.NhatKyHeThong FROM fourchang_app"); }
        Assert.Equal(beforeCount, await api.Sql("SELECT COUNT(*) FROM dbo.NhanVien"));
        Assert.Equal("0", await api.Sql("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.NhanVien') AND name IN ('TenDangNhap','MatKhau','MaNhanVienHienThi')"));
        (await api.Send("DELETE", root + "/" + id, headers: admin)).Expect(405, "Không thêm xóa hồ sơ ngoài đặc tả");
    }
}
