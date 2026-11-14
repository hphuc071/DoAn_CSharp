using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Admin;
// UC-ADM-02: Admin duy nhất, mật khẩu/phiên và vòng đời thiết bị POS.
public sealed class AccessAdministrationTests
{

    /// <summary>
    /// Kiểm tra Admin duy nhất, đổi mật khẩu, phiên hạn chế, thu hồi phiên, receipt nguồn, rollback audit và khóa tạm.
    /// </summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "COMMON-ADMIN-AUTH"), Trait("Layer", "API")]
    public async Task MatKhauQuanLy_ThuHoiPhien_Receipt_Audit_VaKhoaTam()
    {
        await using var api = await TestApi.Start("UC-ADM-02-Password");
        (await api.Send("GET", "/api/admin/access/account")).Expect(401, "Ẩn danh");
        var original = api.AdminPassword;
        // Tạo payload đăng nhập Admin cho các tình huống mật khẩu đúng, sai hoặc tài khoản đang bị khóa.
        JsonObject LoginBody(string password) => Body(("TenDangNhap", "quanly"), ("MatKhau", password));
        (await api.Send("POST", "/api/auth/admin/login", LoginBody("Sai-mat-khau-test"))).Expect(401, "Sai mật khẩu");
        var admin = await api.AdminLogin();
        var second = await api.AdminLogin();
        var profile = (await api.Send("GET", "/api/admin/access/account", headers: admin)).Expect(200, "Hồ sơ Admin").Json;
        Assert.Equal(1, profile.Number("maQuanLy"));
        Check(!profile.Flag("batBuocDoiMatKhau"), "Admin chính thức");
        (await api.Send("POST", "/api/admin/access/account", Body(("TenDangNhap", "admin2")), admin)).Expect(405, "Không tạo Admin thứ hai");
        Assert.Equal("1", await api.Sql("SELECT COUNT(*) FROM dbo.TaiKhoanQuanLy"));
        // Lấy rowversion Admin hiện tại rồi chuẩn bị yêu cầu đổi mật khẩu có mã thao tác và xác nhận.
        async Task<JsonObject> PasswordBody(string old, string next)
        {
            var p = (await api.Send("GET", "/api/admin/access/account", headers: admin)).Expect(200, "Version Admin").Json;
            return Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", p.Text("phienBanDuLieu")), ("MatKhauHienTai", old), ("MatKhauMoi", next), ("XacNhanMatKhau", next));
        }

        var next = Password();
        (await api.Send("POST", "/api/auth/admin/password", await PasswordBody("Khong-dung-mat-khau", next), admin)).Expect(400, "Mật khẩu hiện tại sai");
        (await api.Send("POST", "/api/auth/admin/password", await PasswordBody(original, "1234"), admin)).Expect(400, "Mật khẩu mới ngắn");
        var bad = await PasswordBody(original, next);
        bad["XacNhanMatKhau"] = next + "khac";
        (await api.Send("POST", "/api/auth/admin/password", bad, admin)).Expect(400, "Xác nhận không khớp");
        var stale = await PasswordBody(original, next);
        stale["PhienBanDuLieu"] = Convert.ToBase64String([0, 0, 0, 0, 0, 0, 0, 1]);
        (await api.Send("POST", "/api/auth/admin/password", stale, admin)).Expect(409, "Version cũ");
        var device = await api.NewPosClient(admin);
        var employee = (await api.Send("GET", "/api/pos/employees", headers: device.Headers)).Expect(200, "Nhân viên fixture").Json[0]!;
        var staff = TestApi.Bearer((await api.Send("POST", "/api/pos/shifts", api.OpenBody(employee, device), device.Headers)).Expect(200, "Ca độc lập").Json);
        (await api.Send("GET", "/api/admin/access/account", headers: staff)).Expect(403, "Staff không xem Admin");
        (await api.Send("POST", "/api/auth/admin/password", await PasswordBody(original, next), staff)).Expect(403, "Staff không đổi mật khẩu Admin");
        (await api.Send("POST", "/api/admin/access/devices", device.Input, staff)).Expect(403, "Staff không cấp máy");
        var customer = await api.NewCustomer();
        (await api.Send("GET", "/api/admin/access/devices", headers: await api.Login(customer))).Expect(403, "Customer không cấu hình POS");
        var change = await PasswordBody(original, next);
        (await api.Send("POST", "/api/auth/admin/password", change, admin)).Expect(200, "Đổi mật khẩu Admin");
        (await api.Send("GET", "/api/admin/access/account", headers: admin)).Expect(401, "Thu hồi phiên hiện tại");
        (await api.Send("GET", "/api/admin/access/account", headers: second)).Expect(401, "Thu hồi phiên khác");
        (await api.Send("GET", "/api/auth/admin/password-operations/" + change.Text("MaThaoTac"), headers: admin)).Expect(200, "Nguồn đọc receipt");
        (await api.Send("GET", "/api/auth/admin/password-operations/" + change.Text("MaThaoTac"), headers: second)).Expect(401, "Phiên khác không đọc receipt");
        (await api.Send("POST", "/api/auth/admin/login", LoginBody(original))).Expect(401, "Mật khẩu cũ hết hiệu lực");
        admin = await api.AdminLogin(next);
        (await api.Send("GET", "/api/pos/data/DonHang", headers: staff)).Expect(200, "Đổi mật khẩu không kết thúc ca");
        (await api.Send("POST", "/api/auth/logout", headers: admin)).Expect(200, "Admin logout");
        (await api.Send("GET", "/api/admin/access/account", headers: admin)).Expect(401, "Token logout");
        (await api.Send("GET", "/api/pos/data/DonHang", headers: staff)).Expect(200, "Logout không kết thúc ca");
        await api.Sql("UPDATE dbo.TaiKhoanQuanLy SET BatBuocDoiMatKhau=1 WHERE MaQuanLy=1");
        var temporary = (await api.Send("POST", "/api/auth/admin/login", LoginBody(next))).Expect(200, "Admin tạm đăng nhập").Json;
        Check(temporary.Flag("mustChangePassword"), "Cờ phiên tạm");
        var limited = TestApi.Bearer(temporary);
        (await api.Send("GET", "/api/admin/dashboard", headers: limited)).Expect(403, "Admin tạm không dashboard");
        (await api.Send("GET", "/api/admin/access/devices", headers: limited)).Expect(403, "Admin tạm không cấu hình");
        var context = (await api.Send("GET", "/api/auth/admin/password-context", headers: limited)).Expect(200, "Ngữ cảnh tạm").Json;
        (await api.Send("POST", "/api/auth/admin/password", Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", context.Text("phienBanDuLieu")), ("MatKhauHienTai", next), ("MatKhauMoi", original), ("XacNhanMatKhau", original)), limited)).Expect(200, "Đổi mật khẩu tạm");
        (await api.Send("GET", "/api/auth/admin/password-context", headers: limited)).Expect(401, "Thu hồi phiên tạm");
        admin = await api.AdminLogin();
        var atomic = await PasswordBody(original, next);
        await api.Sql("DENY INSERT ON OBJECT::dbo.NhatKyHeThong TO fourchang_app");
        try
        {
            Check((await api.Send("POST", "/api/auth/admin/password", atomic, admin)).Status != 200, "Audit lỗi không đổi mật khẩu");
        }
        finally
        {
            await api.Sql("REVOKE INSERT ON OBJECT::dbo.NhatKyHeThong FROM fourchang_app");
        }

        (await api.Send("GET", "/api/admin/access/account", headers: admin)).Expect(200, "Rollback thu hồi phiên");
        var audit = await api.Sql("SELECT CONCAT(COALESCE(DuLieuTruocJson,''),COALESCE(DuLieuSauJson,''),COALESCE(KetQuaDaChotJson,'')) FROM dbo.NhatKyHeThong");
        Check(!audit.Contains(original) && !audit.Contains(next) && !audit.Contains("MatKhauBam"), "Audit không lưu mật khẩu/hash");
        for (int i = 0; i < 5; i++)
        {
            (await api.Send("POST", "/api/auth/admin/login", LoginBody("Sai-mat-khau-test"))).Expect(401, "Sai liên tiếp");
        }

        Assert.Equal("1", await api.Sql("SELECT COUNT(*) FROM dbo.TaiKhoanQuanLy WHERE MaQuanLy=1 AND SoLanDangNhapSai>=5 AND KhoaDenLuc>SYSUTCDATETIME()"));
        (await api.Send("POST", "/api/auth/admin/login", LoginBody(original))).Expect(401, "Đúng mật khẩu vẫn khóa tạm");
    }

    /// <summary>
    /// Kiểm tra vòng đời thiết bị POS, chốt ca máy thu hồi, khóa cũ không sống lại, rowversion và audit nguyên tử.
    /// </summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "COMMON-ADMIN-AUTH"), Trait("Layer", "API")]
    public async Task CapThuHoiCapLaiMay_ChotCa_Version_VaAuditNguyenTu()
    {
        await using var api = await TestApi.Start("Removed-Device-Access");
        var admin = await api.AdminLogin();
        var input = Body(("MaThaoTac", NewId()), ("MaMay", "POS-LEGACY"), ("TenMay", "Máy cũ"), ("KhoaThietBi", "unused-legacy-key"), ("LyDo", "Kiểm thử route đã bỏ"));
        (await api.Send("GET", "/api/admin/access/devices", headers: admin)).Expect(410, "Không còn danh sách thiết bị");
        (await api.Send("POST", "/api/admin/access/devices", input, admin)).Expect(410, "Không cấp thiết bị");
        var oldHeader = new Dictionary<string,string> { ["X-POS-Key"] = "unused-legacy-key" };
        (await api.Send("GET", "/api/pos/employees", headers: oldHeader)).Expect(401, "Khóa máy cũ không xác thực");
        var client = await api.NewPosClient(admin);
        var employee = await api.Employee(admin);
        var staff = TestApi.Bearer((await api.Send("POST", "/api/pos/shifts", api.OpenBody(employee, client))).Expect(200, "Mở ca không đăng ký máy").Json);
        (await api.Send("GET", "/api/pos/data/DonHang", headers: staff)).Expect(200, "Chữ ký ca cấp quyền Staff");
        (await api.Send("GET", "/api/admin/access/account", headers: staff)).Expect(403, "Không cấp quyền Admin");
        (await api.Send("GET", "/api/pos/data/DonHang", headers: new() { ["Authorization"] = "Bearer pos.invalid" })).Expect(401, "Token pos cũ bị từ chối");
        Assert.Equal("0", await api.Sql("SELECT COUNT(*) FROM sys.tables WHERE name='ThietBiPOS'"));
    }
}
