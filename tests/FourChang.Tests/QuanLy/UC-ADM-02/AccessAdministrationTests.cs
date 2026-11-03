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
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-02"), Trait("Layer", "API")]
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
        (await api.Send("POST", "/api/auth/admin/password", await PasswordBody(original, "short"), admin)).Expect(400, "Mật khẩu mới ngắn");
        var bad = await PasswordBody(original, next);
        bad["XacNhanMatKhau"] = next + "khac";
        (await api.Send("POST", "/api/auth/admin/password", bad, admin)).Expect(400, "Xác nhận không khớp");
        var stale = await PasswordBody(original, next);
        stale["PhienBanDuLieu"] = Convert.ToBase64String([0, 0, 0, 0, 0, 0, 0, 1]);
        (await api.Send("POST", "/api/auth/admin/password", stale, admin)).Expect(409, "Version cũ");
        var device = await api.NewDevice(admin);
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
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-02"), Trait("Layer", "API")]
    public async Task CapThuHoiCapLaiMay_ChotCa_Version_VaAuditNguyenTu()
    {
        await using var api = await TestApi.Start("UC-ADM-02-Device");
        var admin = await api.AdminLogin();
        var input = Body(("MaThaoTac", NewId()), ("MaMay", "POS-UCADM02"), ("TenMay", "Quầy kiểm thử"), ("KhoaThietBi", "Device-" + Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")), ("LyDo", "Đăng ký kiểm thử"));
        var noReason = input.Copy();
        noReason["MaThaoTac"] = NewId();
        noReason["LyDo"] = "";
        (await api.Send("POST", "/api/admin/access/devices", noReason, admin)).Expect(400, "Cấp máy cần lý do");
        var record = (await api.Send("POST", "/api/admin/access/devices", input, admin)).Expect(200, "Cấp máy").Json["device"]!;
        var id = record.Number("maThietBiPOS");
        var replay = (await api.Send("POST", "/api/admin/access/devices", input, admin)).Expect(200, "Cấp lặp").Json;
        Assert.Equal(id, replay["device"]!.Number("maThietBiPOS"));
        var changed = input.Copy();
        changed["TenMay"] = "Nội dung khác";
        (await api.Send("POST", "/api/admin/access/devices", changed, admin)).Expect(409, "Cùng mã khác nội dung");
        var list = (await api.Send("GET", "/api/admin/access/devices", headers: admin)).Expect(200, "Danh sách máy");
        Check(!list.Content.Contains(input.Text("KhoaThietBi")) && !list.Content.Contains("MaBamKhoa") && !list.Content.Contains("MatKhauBam"), "Không lộ khóa/hash");
        var unknown = new Dictionary<string, string>
        {
            {
                "X-POS-Key",
                api.Settings["FOURCHANG_POS_KEY"]
            },
            {
                "X-POS-Machine",
                "CHUA-DANG-KY"
            }
        };
        (await api.Send("GET", "/api/pos/employees", headers: unknown)).Expect(401, "Khóa toàn cục không mở máy lạ");
        var headers = new Dictionary<string, string>
        {
            {
                "X-POS-Key",
                input.Text("KhoaThietBi")
            },
            {
                "X-POS-Machine",
                input.Text("MaMay")
            }
        };
        var device = new Device(input, record, headers);
        var employee = (await api.Send("GET", "/api/pos/employees", headers: headers)).Expect(200, "Máy được đọc nhân viên").Json[0]!;
        var open = api.OpenBody(employee, device);
        open["MaMay"] = "MAY-KHAC";
        (await api.Send("POST", "/api/pos/shifts", open, headers)).Expect(403, "Khóa gắn mã máy");
        open["MaMay"] = input.Text("MaMay");
        var shift = (await api.Send("POST", "/api/pos/shifts", open, headers)).Expect(200, "Mở ca tên/mã").Json;
        var shiftId = shift.Number("shiftId");
        var staff = TestApi.Bearer(shift);
        var revoke = TestApi.Action(record);
        record = (await api.Send("POST", $"/api/admin/access/devices/{id}/revoke", revoke, admin)).Expect(200, "Thu hồi khi còn ca").Json["device"]!;
        Assert.Equal(1, record.Number("soCaChuaKetThuc"));
        (await api.Send("GET", "/api/pos/data/DonHang", headers: staff)).Expect(401, "Chặn token ca ngay");
        (await api.Send("GET", "/api/pos/employees", headers: headers)).Expect(401, "Chặn khóa đã thu hồi");
        var grant = input.Copy();
        grant["MaThaoTac"] = NewId();
        grant["PhienBanDuLieu"] = record.Text("phienBanDuLieu");
        grant["KhoaThietBi"] = "Device-" + Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        (await api.Send("POST", "/api/admin/access/devices", grant, admin)).Expect(409, "Chưa chốt ca không cấp lại");
        var shifts = (await api.Send("GET", $"/api/admin/access/devices/{id}/shifts", headers: admin)).Expect(200, "Ca cần xử lý").Json;
        Assert.Equal(500000, shifts[0]!.Number("tienMatDuKien"));
        await api.Sql($"INSERT dbo.DonHang(MaDonHienThi,MaCaLamViec,KenhDatHang,HinhThucNhanMon,PhiGiaoHangBanDau,PhiGiaoHangHienTai,TrangThai) VALUES('UCADM02-PENDING',{shiftId},'POS','TAKEAWAY',0,0,'ACCEPTED')");
        var close = Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", shifts[0]!.Text("phienBanDuLieu")), ("TienMatThucDem", 500000), ("LyDo", "Đối chiếu ca máy thu hồi"));
        (await api.Send("POST", $"/api/admin/access/devices/{id}/shifts/{shiftId}/close", close, admin)).Expect(409, "Còn đơn chưa xử lý");
        await api.Sql("UPDATE dbo.DonHang SET TrangThai='CANCELLED' WHERE MaDonHienThi='UCADM02-PENDING'");
        close["MaThaoTac"] = NewId();
        (await api.Send("POST", $"/api/admin/access/devices/{id}/shifts/{shiftId}/close", close, admin)).Expect(200, "Chốt khi đủ điều kiện");
        Assert.Equal("1", await api.Sql($"SELECT COUNT(*) FROM dbo.CaLamViec WHERE MaCaLamViec={shiftId} AND TrangThai='CLOSED' AND MaQuanLyDuyet=1 AND TienMatThucDem=500000"));
        grant["MaThaoTac"] = NewId();
        (await api.Send("POST", "/api/admin/access/devices", grant, admin)).Expect(200, "Cấp lại sau chốt");
        (await api.Send("GET", "/api/pos/employees", headers: headers)).Expect(401, "Khóa cũ không sống lại");
        headers["X-POS-Key"] = grant.Text("KhoaThietBi");
        (await api.Send("GET", "/api/pos/employees", headers: headers)).Expect(200, "Khóa mới hợp lệ");
        record = (await api.Send("GET", "/api/admin/access/devices", headers: admin)).Json[0]!;
        var stale = revoke.Copy();
        stale["MaThaoTac"] = NewId();
        (await api.Send("POST", $"/api/admin/access/devices/{id}/revoke", stale, admin)).Expect(409, "Version cũ");
        var atomic = TestApi.Action(record);
        await api.Sql("DENY INSERT ON OBJECT::dbo.NhatKyHeThong TO fourchang_app");
        try
        {
            Check((await api.Send("POST", $"/api/admin/access/devices/{id}/revoke", atomic, admin)).Status != 200, "Audit lỗi không thu hồi");
        }
        finally
        {
            await api.Sql("REVOKE INSERT ON OBJECT::dbo.NhatKyHeThong FROM fourchang_app");
        }

        (await api.Send("GET", "/api/pos/employees", headers: headers)).Expect(200, "Rollback trạng thái máy");
        (await api.Send("GET", "/api/admin/access/devices/operations/" + atomic.Text("MaThaoTac"), headers: admin)).Expect(404, "Không receipt giả");
        var audit = await api.Sql("SELECT CONCAT(COALESCE(DuLieuTruocJson,''),COALESCE(DuLieuSauJson,''),COALESCE(KetQuaDaChotJson,'')) FROM dbo.NhatKyHeThong");
        Check(!audit.Contains(input.Text("KhoaThietBi")) && !audit.Contains(grant.Text("KhoaThietBi")) && !audit.Contains("MaBamKhoa") && !audit.Contains("MatKhauBam"), "Audit không lưu khóa/hash");
    }
}
