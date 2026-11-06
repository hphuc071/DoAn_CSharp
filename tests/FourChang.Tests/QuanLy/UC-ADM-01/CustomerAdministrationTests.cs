using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Admin;
// UC-ADM-01: tạo/sửa/tìm/khóa/reset/xóa có điều kiện; các ràng buộc vẫn do API xác thực.
public sealed class CustomerAdministrationTests
{

    /// <summary>
    /// Kiểm tra quản trị khách hàng, tìm kiếm và phân trang thật, gửi đồng thời, ranh giới quyền và rollback audit.
    /// </summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-01"), Trait("Layer", "API")]
    public async Task TaoSua_TimKiem_PhanTrang_GuiLapDongThoi_VaQuyen()
    {
        await using var api = await TestApi.Start("UC-ADM-01-CRUD");
        Assert.Equal("37", await api.Sql("SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0"));
        Assert.Equal("1", await api.Sql("EXECUTE AS USER='fourchang_app'; SELECT CASE WHEN HAS_PERMS_BY_NAME('dbo.TaiKhoanKhachHang','OBJECT','DELETE')=1 AND HAS_PERMS_BY_NAME('dbo.DonHang','OBJECT','DELETE')=0 AND HAS_PERMS_BY_NAME('dbo.NhatKyHeThong','OBJECT','DELETE')=0 THEN 1 ELSE 0 END; REVERT;"));
        (await api.Send("GET", "/api/admin/customers")).Expect(401, "Ẩn danh bị chặn");
        var admin = await api.AdminLogin();
        (await api.Send("GET", "/api/admin/customers?page=0", headers: admin)).Expect(400, "Trang không hợp lệ");
        (await api.Send("GET", "/api/admin/customers?pageSize=101", headers: admin)).Expect(400, "Page size quá giới hạn");
        var invalid = api.CustomerInput();
        invalid["Email"] = "sai-email";
        (await api.Send("POST", "/api/admin/customers", invalid, admin)).Expect(400, "Email sai");
        invalid["Email"] = null;
        (await api.Send("POST", "/api/admin/customers", invalid, admin)).Expect(400, "Cần liên hệ");
        invalid["Email"] = "valid@example.test";
        invalid["MatKhauTam"] = "short";
        (await api.Send("POST", "/api/admin/customers", invalid, admin)).Expect(400, "Mật khẩu tạm ngắn");
        var create = api.CustomerInput();
        create["Email"] = "  " + create.Text("Email").ToUpperInvariant() + "  ";
        var customer = (await api.Send("POST", "/api/admin/customers", create, admin)).Expect(200, "Tạo khách").Json["account"]!;
        var id = customer.Number("maKhachHang");
        Check(customer.Flag("batBuocDoiMatKhau"), "Buộc đổi mật khẩu");
        Assert.Equal(create.Text("Email").Trim().ToLowerInvariant(), customer.Text("email"));
        var replay = (await api.Send("POST", "/api/admin/customers", create, admin)).Expect(200, "Gửi lặp").Json;
        Assert.Equal(id, replay["account"]!.Number("maKhachHang"));
        var different = create.Copy();
        different["HoTen"] = "Nội dung khác";
        (await api.Send("POST", "/api/admin/customers", different, admin)).Expect(409, "Cùng mã khác nội dung");
        (await api.Send("GET", "/api/admin/customers/operations/" + create.Text("MaThaoTac"), headers: admin)).Expect(200, "Receipt thao tác");
        var duplicate = api.CustomerInput();
        duplicate["Email"] = customer.Text("email");
        (await api.Send("POST", "/api/admin/customers", duplicate, admin)).Expect(409, "Email trùng");
        var search = (await api.Send("GET", $"/api/admin/customers?search={id}&page=1&pageSize=1", headers: admin)).Expect(200, "Tìm mã").Json;
        Assert.Equal(1, search.Number("total"));
        var safe = (await api.Send("GET", $"/api/admin/customers/{id}", headers: admin)).Content;
        Check(!safe.Contains("MatKhauBam") && !safe.Contains("PBKDF2") && !safe.Contains("MaLamMoiDaBam") && !safe.Contains(create.Text("MatKhauTam")), "Không lộ bí mật");
        var update = Body(("MaThaoTac", NewId()), ("TenDangNhap", customer.Text("tenDangNhap")), ("HoTen", "Khách đã sửa"), ("Email", customer.Text("email")), ("SoDienThoai", "+84912345678"), ("PhienBanDuLieu", customer.Text("phienBanDuLieu")));
        (await api.Send("PUT", $"/api/admin/customers/{id}", update, admin)).Expect(200, "Sửa hiện tại");
        var stale = update.Copy();
        stale["MaThaoTac"] = NewId();
        var conflict = (await api.Send("PUT", $"/api/admin/customers/{id}", stale, admin)).Expect(409, "Chặn version cũ");
        Assert.NotNull(conflict.Json["current"]);
        // Chạy hai yêu cầu thật đồng thời, không chỉ gọi nối tiếp với cùng mã.
        var parallel = api.CustomerInput("parallel");
        var results = await Task.WhenAll(api.Send("POST", "/api/admin/customers", parallel, admin), api.Send("POST", "/api/admin/customers", parallel, admin));
        foreach (var result in results)
        {
            result.Expect(200, "Hai yêu cầu cùng mã đồng thời");
        }

        Assert.Equal(results[0].Json["account"]!.Number("maKhachHang"), results[1].Json["account"]!.Number("maKhachHang"));
        for (int i = 0; i < 21; i++)
        {
            (await api.Send("POST", "/api/admin/customers", api.CustomerInput("page" + i), admin)).Expect(200, "Dữ liệu phân trang");
        }

        var page1 = (await api.Send("GET", "/api/admin/customers?page=1&pageSize=20", headers: admin)).Expect(200, "Trang một").Json;
        var page2 = (await api.Send("GET", "/api/admin/customers?page=2&pageSize=20", headers: admin)).Expect(200, "Trang hai").Json;
        Assert.Equal(20, page1["items"]!.AsArray().Count);
        Check(page2["items"]!.AsArray().Count > 0, "Trang hai có khách");
        Assert.Empty(page1["items"]!.AsArray().Select(x => x!.Number("maKhachHang")).Intersect(page2["items"]!.AsArray().Select(x => x!.Number("maKhachHang"))));
        var device = await api.NewDevice(admin);
        var employee = (await api.Send("GET", "/api/pos/employees", headers: device.Headers)).Expect(200, "Fixture nhân viên").Json[0]!;
        var open = api.OpenBody(employee, device);
        open["TienMatDauCa"] = 0;
        var staff = TestApi.Bearer((await api.Send("POST", "/api/pos/shifts", open, device.Headers)).Expect(200, "Mở ca kiểm tra quyền").Json);
        (await api.Send("GET", "/api/admin/customers", headers: staff)).Expect(403, "Staff không quản lý khách");
        (await api.Send("POST", "/api/pos/shifts/current/close", Body(("TienMatThucDem", 0), ("LyDoChenhLech", "")), staff)).Expect(200, "Đóng ca");
        // E6: audit bắt buộc thất bại phải rollback thay đổi và receipt cùng giao dịch.
        var before = await api.CustomerAccount(id, admin);
        var atomic = Body(("MaThaoTac", NewId()), ("TenDangNhap", before.Text("tenDangNhap")), ("HoTen", "KHONG DUOC LUU"), ("Email", before.Text("email")), ("SoDienThoai", before.Text("soDienThoai")), ("PhienBanDuLieu", before.Text("phienBanDuLieu")));
        await api.Sql("DENY INSERT ON OBJECT::dbo.NhatKyHeThong TO fourchang_app");
        try
        {
            var failed = await api.Send("PUT", $"/api/admin/customers/{id}", atomic, admin);
            Check(failed.Status >= 500 || failed.Status == 409, "Không báo lưu thành công khi audit lỗi");
        }
        finally
        {
            await api.Sql("REVOKE INSERT ON OBJECT::dbo.NhatKyHeThong FROM fourchang_app");
        }

        Assert.Equal(before.Text("hoTen"), (await api.CustomerAccount(id, admin)).Text("hoTen"));
        (await api.Send("GET", "/api/admin/customers/operations/" + atomic.Text("MaThaoTac"), headers: admin)).Expect(404, "Không receipt giả");
        Assert.Equal("1", await api.Sql($"SELECT COUNT(*) FROM dbo.NhatKyHeThong WHERE MaThaoTac='{create.Text("MaThaoTac")}'"));
        var audit = await api.Sql("SELECT CONCAT(COALESCE(DuLieuTruocJson,''),COALESCE(DuLieuSauJson,''),COALESCE(KetQuaDaChotJson,'')) FROM dbo.NhatKyHeThong WHERE HanhDong LIKE 'CUSTOMER_%'");
        Check(!audit.Contains("PBKDF2") && !audit.Contains("MatKhauBam") && !audit.Contains(create.Text("MatKhauTam")), "Audit không lưu mật khẩu/hash");
        (await api.Send("POST", "/api/auth/logout", headers: admin)).Expect(200, "Admin đăng xuất");
        (await api.Send("GET", "/api/admin/customers", headers: admin)).Expect(401, "Admin token đã thu hồi");
    }

    /// <summary>
    /// Kiểm tra bắt buộc đổi mật khẩu tạm, khóa hoặc mở khách, reset và thu hồi mọi phiên liên quan.
    /// </summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-01"), Trait("Layer", "API")]
    public async Task MatKhauTam_Khoa_Mo_Reset_VaThuHoiPhien()
    {
        await using var api = await TestApi.Start("UC-ADM-01-Security");
        var admin = await api.AdminLogin();
        var input = api.CustomerInput();
        var account = (await api.Send("POST", "/api/admin/customers", input, admin)).Expect(200, "Tạo khách tạm").Json["account"]!;
        var id = account.Number("maKhachHang");
        var loginBody = Body(("TenDangNhap", input.Text("TenDangNhap")), ("MatKhau", input.Text("MatKhauTam")));
        var temporary = (await api.Send("POST", "/api/auth/customer/login", loginBody)).Expect(200, "Mật khẩu tạm đăng nhập").Json;
        Check(temporary.Flag("mustChangePassword"), "Cờ phiên tạm");
        var limited = TestApi.Bearer(temporary);
        (await api.Send("GET", "/api/customer/profile", headers: limited)).Expect(403, "Phiên tạm không hồ sơ");
        (await api.Send("GET", "/api/customer/orders", headers: limited)).Expect(403, "Phiên tạm không đơn");
        (await api.Send("GET", "/api/admin/customers", headers: limited)).Expect(403, "Không quyền Admin");
        var context = (await api.Send("GET", "/api/auth/customer/temporary-password", headers: limited)).Expect(200, "Ngữ cảnh đổi tạm").Json;
        var change = Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", context.Text("phienBanDuLieu")), ("MatKhauMoi", input.Text("MatKhauTam")), ("XacNhanMatKhau", input.Text("MatKhauTam")));
        (await api.Send("POST", "/api/auth/customer/temporary-password", change, limited)).Expect(400, "Không dùng lại mật khẩu tạm");
        var password = Password();
        change["MaThaoTac"] = NewId();
        change["MatKhauMoi"] = password;
        change["XacNhanMatKhau"] = password;
        (await api.Send("POST", "/api/auth/customer/temporary-password", change, limited)).Expect(200, "Đổi mật khẩu tạm");
        (await api.Send("GET", "/api/auth/customer/temporary-password", headers: limited)).Expect(401, "Thu hồi phiên tạm");
        loginBody["MatKhau"] = password;
        var full = TestApi.Bearer((await api.Send("POST", "/api/auth/customer/login", loginBody)).Expect(200, "Mật khẩu mới đăng nhập").Json);
        (await api.Send("GET", "/api/customer/profile", headers: full)).Expect(200, "Phiên đầy đủ");
        (await api.Send("GET", "/api/admin/customers", headers: full)).Expect(403, "Vẫn không quản trị");
        account = await api.CustomerAccount(id, admin);
        (await api.Send("POST", $"/api/admin/customers/{id}/status", TestApi.Action(account, ("TrangThai", "LOCKED"), ("LyDo", "")), admin)).Expect(400, "Khóa cần lý do");
        (await api.Send("POST", $"/api/admin/customers/{id}/status", TestApi.Action(account, ("TrangThai", "LOCKED")), admin)).Expect(200, "Khóa khách");
        (await api.Send("GET", "/api/customer/profile", headers: full)).Expect(401, "Khóa thu hồi token");
        (await api.Send("POST", "/api/auth/customer/login", loginBody)).Expect(401, "Khách khóa không login");
        account = await api.CustomerAccount(id, admin);
        (await api.Send("POST", $"/api/admin/customers/{id}/status", TestApi.Action(account, ("TrangThai", "ACTIVE")), admin)).Expect(200, "Mở khách");
        (await api.Send("GET", "/api/customer/profile", headers: full)).Expect(401, "Không sống lại token cũ");
        full = TestApi.Bearer((await api.Send("POST", "/api/auth/customer/login", loginBody)).Expect(200, "Login lại sau mở").Json);
        var resetPassword = Password();
        account = await api.CustomerAccount(id, admin);
        (await api.Send("POST", $"/api/admin/customers/{id}/reset-password", TestApi.Action(account, ("MatKhauTam", resetPassword)), admin)).Expect(200, "Reset mật khẩu");
        (await api.Send("GET", "/api/customer/profile", headers: full)).Expect(401, "Reset thu hồi token");
        loginBody["MatKhau"] = resetPassword;
        var reset = (await api.Send("POST", "/api/auth/customer/login", loginBody)).Expect(200, "Mật khẩu reset đăng nhập").Json;
        Check(reset.Flag("mustChangePassword"), "Reset buộc đổi lại");
        var audit = await api.Sql("SELECT CONCAT(COALESCE(DuLieuTruocJson,''),COALESCE(DuLieuSauJson,''),COALESCE(KetQuaDaChotJson,'')) FROM dbo.NhatKyHeThong");
        Check(!audit.Contains(resetPassword) && !audit.Contains(input.Text("MatKhauTam")) && !audit.Contains("MatKhauBam"), "Reset không lộ mật khẩu audit");
    }

    /// <summary>
    /// Chỉ cho xóa khách chưa có tham chiếu; khách có lịch sử được ngừng hoạt động nhưng đơn và nghĩa vụ tài chính phải giữ nguyên.
    /// </summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-01"), Trait("Layer", "API")]
    public async Task XoaCoDieuKien_BaoToanDonTienHoaDonVaHoanTien()
    {
        await using var api = await TestApi.Start("UC-ADM-01-Delete");
        var admin = await api.AdminLogin();
        var deletable = (await api.Send("POST", "/api/admin/customers", api.CustomerInput(), admin)).Expect(200, "Tạo khách xóa").Json["account"]!;
        var id = deletable.Number("maKhachHang");
        Check((await api.Send("GET", $"/api/admin/customers/{id}", headers: admin)).Json.Flag("coTheXoa"), "Khách không tham chiếu được xóa");
        var delete = TestApi.Action(deletable);
        (await api.Send("DELETE", $"/api/admin/customers/{id}", delete, admin)).Expect(200, "Xóa khách");
        (await api.Send("GET", $"/api/admin/customers/{id}", headers: admin)).Expect(404, "Không còn khách");
        (await api.Send("DELETE", $"/api/admin/customers/{id}", delete, admin)).Expect(200, "Lặp xóa receipt");
        var history = (await api.Send("POST", "/api/admin/customers", api.CustomerInput(), admin)).Expect(200, "Tạo khách lịch sử").Json["account"]!;
        var historyId = history.Number("maKhachHang");
        await api.Sql($$"""
DECLARE @o bigint,@p bigint;
INSERT dbo.DonHang(MaDonHienThi,MaKhachHang,KenhDatHang,HinhThucNhanMon,PhiGiaoHangBanDau,PhiGiaoHangHienTai,TrangThai) VALUES('UCADM01-HISTORY',{{historyId}},'WEB','TAKEAWAY',0,0,'CANCELLED'); SET @o=SCOPE_IDENTITY();
INSERT dbo.ThanhToan(MaThanhToanHienThi,MaDonHang,PhuongThuc,SoTienYeuCau,SoTienDaXacMinh,TrangThai,MaThaoTac) VALUES('UCADM01-PAYMENT',@o,'BANK_TRANSFER',45000,45000,'VERIFIED',NEWID()); SET @p=SCOPE_IDENTITY();
INSERT dbo.HoaDon(SoHoaDon,MaDonHang,LoaiHoaDon,SoTien,NoiDungHoaDonDaChotJson,TrangThai,PhatHanhLuc,MaQuanLyPhatHanh) VALUES('UCADM01-INVOICE',@o,'ORIGINAL',45000,N'{}','ISSUED',SYSUTCDATETIME(),1);
INSERT dbo.HoSoHoanTien(MaHoanTienHienThi,MaDonHang,MaThanhToan,LyDoHoan,SoTienYeuCau,PhuongThucHoan,TrangThai,MaKhachHangYeuCau) VALUES('UCADM01-REFUND',@o,@p,N'Kiểm thử bảo toàn nghĩa vụ',45000,'BANK_TRANSFER','REQUESTED',{{historyId}});
""");
        var detail = (await api.Send("GET", $"/api/admin/customers/{historyId}", headers: admin)).Expect(200, "Chi tiết lịch sử").Json;
        Check(!detail.Flag("coTheXoa"), "Chặn xóa có lịch sử");
        Assert.Equal(4, detail["giaoDichGanDay"]!.AsArray().Count);
        (await api.Send("DELETE", $"/api/admin/customers/{historyId}", TestApi.Action(detail["account"]!), admin)).Expect(409, "Không xóa khách có tham chiếu");
        (await api.Send("POST", $"/api/admin/customers/{historyId}/status", TestApi.Action(detail["account"]!, ("TrangThai", "INACTIVE")), admin)).Expect(200, "Cho ngừng khách giữ lịch sử");
        Assert.Equal("1", await api.Sql($"SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.DonHang WHERE MaKhachHang={historyId} AND TrangThai='CANCELLED') AND EXISTS(SELECT 1 FROM dbo.ThanhToan WHERE MaThanhToanHienThi='UCADM01-PAYMENT' AND SoTienDaXacMinh=45000) AND EXISTS(SELECT 1 FROM dbo.HoaDon WHERE SoHoaDon='UCADM01-INVOICE' AND TrangThai='ISSUED') AND EXISTS(SELECT 1 FROM dbo.HoSoHoanTien WHERE MaHoanTienHienThi='UCADM01-REFUND' AND SoTienYeuCau=45000 AND TrangThai='REQUESTED') THEN 1 ELSE 0 END"));
    }
}
