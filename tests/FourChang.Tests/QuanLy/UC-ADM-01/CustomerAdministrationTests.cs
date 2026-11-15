using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Admin;

/// <summary>UC-ADM-01 tinh gọn: sửa/tìm, khóa/mở/reset; chặn tạo/xóa/ngừng và bảo toàn lịch sử.</summary>
public sealed class CustomerAdministrationTests
{
    /// <summary>Kiểm tra danh sách, validation, cạnh tranh và rollback audit trên SQL/API thật.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-01"), Trait("Layer", "API")]
    public async Task SuaTimKiemPhanTrang_Version_Replay_VaRollback()
    {
        await using var api = await TestApi.Start("UC-ADM-01-Update");
        Assert.Equal("30", await api.Sql("SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0 AND name<>'sysdiagrams'"));
        var admin = await api.AdminLogin();
        var customer = await api.NewCustomer();
        var id = customer.Id;
        var account = await api.CustomerAccount(id, admin);
        (await api.Send("GET", "/api/admin/customers?page=0", headers: admin)).Expect(400, "Trang sai");
        (await api.Send("GET", "/api/admin/customers?pageSize=101", headers: admin)).Expect(400, "Quá kích thước trang");
        (await api.Send("GET", "/api/admin/customers?search=khongtontai-" + NewId(), headers: admin)).Expect(200, "Không có kết quả là hợp lệ");
        foreach (var term in new[] { id.ToString(), customer.Input.Text("TenDangNhap"), customer.Input.Text("Email"), customer.Input.Text("SoDienThoai") })
        {
            var found = (await api.Send("GET", "/api/admin/customers?search=" + Uri.EscapeDataString(term), headers: admin)).Expect(200, "Tìm khách").Json;
            Assert.Contains(found["items"]!.AsArray(), x => x!.Number("maKhachHang") == id);
        }
        var input = Edit(account); input["HoTen"] = "Nguyễn Minh Anh";
        var invalid = input.Copy(); invalid["Email"] = "email-sai";
        (await api.Send("PUT", $"/api/admin/customers/{id}", invalid, admin)).Expect(400, "Email sai không lưu");
        invalid = input.Copy(); invalid["HoTen"] = "   ";
        (await api.Send("PUT", $"/api/admin/customers/{id}", invalid, admin)).Expect(400, "Tên bắt buộc");
        invalid = input.Copy(); invalid["TenDangNhap"] = "doiten";
        (await api.Send("PUT", $"/api/admin/customers/{id}", invalid, admin)).Expect(409, "Tên đăng nhập bất biến");
        var other = await api.NewCustomer(); invalid = input.Copy(); invalid["Email"] = other.Input.Text("Email");
        (await api.Send("PUT", $"/api/admin/customers/{id}", invalid, admin)).Expect(409, "Email trùng");
        invalid = input.Copy(); invalid["SoDienThoai"] = other.Input.Text("SoDienThoai");
        (await api.Send("PUT", $"/api/admin/customers/{id}", invalid, admin)).Expect(409, "Điện thoại trùng");

        // Hai request cùng mã chỉ cập nhật một lần; receipt trả cùng rowversion.
        var results = await Task.WhenAll(api.Send("PUT", $"/api/admin/customers/{id}", input, admin), api.Send("PUT", $"/api/admin/customers/{id}", input, admin));
        foreach (var result in results) result.Expect(200, "Lưu đồng thời cùng mã");
        Assert.Equal(results[0].Json["account"]!.Text("phienBanDuLieu"), results[1].Json["account"]!.Text("phienBanDuLieu"));
        var different = input.Copy(); different["HoTen"] = "Khác nội dung";
        (await api.Send("PUT", $"/api/admin/customers/{id}", different, admin)).Expect(409, "Cùng mã khác nội dung");
        var stale = input.Copy(); stale["MaThaoTac"] = NewId();
        Assert.NotNull((await api.Send("PUT", $"/api/admin/customers/{id}", stale, admin)).Expect(409, "Version cũ").Json["current"]);
        (await api.Send("GET", "/api/admin/customers/operations/" + input.Text("MaThaoTac"), headers: admin)).Expect(200, "Tra kết quả sau mất phản hồi");
        Assert.Equal("1", await api.Sql($"SELECT COUNT(*) FROM dbo.NhatKyHeThong WHERE MaThaoTac='{input.Text("MaThaoTac")}'"));

        // Dữ liệu phân trang được nạp vào database fixture để không vượt rate limit đăng ký Web.
        // Đăng ký thật đã được kiểm tra ở đầu test; không mở lại route tạo khách của Admin.
        for (var i = 0; i < 21; i++)
        {
            var username = "page" + Guid.NewGuid().ToString("N");
            await api.Sql($"INSERT dbo.TaiKhoanKhachHang(TenDangNhapChuanHoa,HoTen,EmailChuanHoa,SoDienThoaiChuanHoa,MatKhauBam,TrangThai,BatBuocDoiMatKhau,SoLanDangNhapSai) SELECT '{username}',N'Khách phân trang','{username}@example.test','{Phone()}',MatKhauBam,'ACTIVE',0,0 FROM dbo.TaiKhoanKhachHang WHERE MaKhachHang={id}");
        }
        var p1 = (await api.Send("GET", "/api/admin/customers?page=1&pageSize=20", headers: admin)).Json;
        var p2 = (await api.Send("GET", "/api/admin/customers?page=2&pageSize=20", headers: admin)).Json;
        Assert.Equal(20, p1["items"]!.AsArray().Count);
        Assert.NotEmpty(p2["items"]!.AsArray());
        Assert.Empty(p1["items"]!.AsArray().Select(x => x!.Number("maKhachHang")).Intersect(p2["items"]!.AsArray().Select(x => x!.Number("maKhachHang"))));

        var before = await api.CustomerAccount(id, admin); var atomic = Edit(before); atomic["HoTen"] = "KHONG DUOC LUU";
        await api.Sql("DENY INSERT ON OBJECT::dbo.NhatKyHeThong TO fourchang_app");
        try { Assert.True((await api.Send("PUT", $"/api/admin/customers/{id}", atomic, admin)).Status >= 400); }
        finally { await api.Sql("REVOKE INSERT ON OBJECT::dbo.NhatKyHeThong FROM fourchang_app"); }
        Assert.Equal(before.Text("hoTen"), (await api.CustomerAccount(id, admin)).Text("hoTen"));
        (await api.Send("GET", "/api/admin/customers/operations/" + atomic.Text("MaThaoTac"), headers: admin)).Expect(404, "Không receipt giả sau rollback");
        var detail = (await api.Send("GET", $"/api/admin/customers/{id}", headers: admin)).Content;
        Assert.DoesNotContain("matKhauBam", detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coTheXoa", detail, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Khóa/mở không cần nhập lý do; reset xác nhận khớp, buộc đổi và thu hồi mọi phiên.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-01"), Trait("Layer", "API")]
    public async Task KhoaMo_Reset_XacNhanMatKhau_VaThuHoiPhien()
    {
        await using var api = await TestApi.Start("UC-ADM-01-Security");
        var admin = await api.AdminLogin(); var customer = await api.NewCustomer();
        var full = await api.Login(customer); var id = customer.Id;
        var account = await api.CustomerAccount(id, admin);
        var status = TestApi.Action(account, ("TrangThai", "LOCKED")); status.Remove("LyDo");
        (await api.Send("POST", $"/api/admin/customers/{id}/status", status, admin)).Expect(200, "Khóa không nhập lý do");
        (await api.Send("GET", "/api/customer/profile", headers: full)).Expect(401, "Khóa thu hồi phiên");
        (await api.Send("POST", "/api/auth/customer/login", Body(("TenDangNhap", customer.Input.Text("TenDangNhap")), ("MatKhau", customer.Input.Text("MatKhau"))))).Expect(401, "Khóa không đăng nhập");
        account = await api.CustomerAccount(id, admin);
        status = TestApi.Action(account, ("TrangThai", "ACTIVE")); status.Remove("LyDo");
        (await api.Send("POST", $"/api/admin/customers/{id}/status", status, admin)).Expect(200, "Mở khóa không nhập lý do");
        (await api.Send("GET", "/api/customer/profile", headers: full)).Expect(401, "Không phục hồi token cũ");
        full = await api.Login(customer);
        var second = await api.Login(customer);
        account = await api.CustomerAccount(id, admin);
        foreach (var password in new[] { "1234", new string('x', 129) })
            (await api.Send("POST", $"/api/admin/customers/{id}/reset-password", TestApi.Action(account, ("MatKhauTam", password)), admin)).Expect(400, "Mật khẩu ngoài giới hạn");
        var reset = TestApi.Action(account, ("MatKhauTam", "reset")); reset.Remove("LyDo"); reset.Remove("XacNhanMatKhau");
        (await api.Send("POST", $"/api/admin/customers/{id}/reset-password", reset, admin)).Expect(400, "Thiếu xác nhận");
        reset["XacNhanMatKhau"] = "khac";
        (await api.Send("POST", $"/api/admin/customers/{id}/reset-password", reset, admin)).Expect(400, "Xác nhận sai");
        reset["XacNhanMatKhau"] = "reset";
        (await api.Send("POST", $"/api/admin/customers/{id}/reset-password", reset, admin)).Expect(200, "Reset 5 ký tự không nhập lý do");
        (await api.Send("POST", $"/api/admin/customers/{id}/reset-password", reset, admin)).Expect(200, "Lặp reset trả receipt cũ");
        foreach (var headers in new[] { full, second }) (await api.Send("GET", "/api/customer/profile", headers: headers)).Expect(401, "Thu hồi mọi phiên");
        var limited = TestApi.Bearer((await api.Send("POST", "/api/auth/customer/login", Body(("TenDangNhap", customer.Input.Text("TenDangNhap")), ("MatKhau", "reset")))).Expect(200, "Đăng nhập tạm").Json);
        (await api.Send("GET", "/api/customer/profile", headers: limited)).Expect(403, "Phiên tạm không xem hồ sơ");
        (await api.Send("GET", "/api/customer/orders", headers: limited)).Expect(403, "Phiên tạm không xem đơn");
        var context = (await api.Send("GET", "/api/auth/customer/temporary-password", headers: limited)).Json;
        (await api.Send("POST", "/api/auth/customer/temporary-password", Body(("MaThaoTac", NewId()), ("PhienBanDuLieu", context.Text("phienBanDuLieu")), ("MatKhauMoi", "final"), ("XacNhanMatKhau", "final")), limited)).Expect(200, "Khách đổi mật khẩu tạm");
        var login = TestApi.Bearer((await api.Send("POST", "/api/auth/customer/login", Body(("TenDangNhap", customer.Input.Text("TenDangNhap")), ("MatKhau", "final")))).Expect(200, "Đăng nhập đầy đủ").Json);
        (await api.Send("GET", "/api/customer/profile", headers: login)).Expect(200, "Hồ sơ sau đổi mật khẩu");
        var audit = await api.Sql("SELECT CONCAT(COALESCE(DuLieuTruocJson,''),COALESCE(DuLieuSauJson,''),COALESCE(KetQuaDaChotJson,'')) FROM dbo.NhatKyHeThong WHERE HanhDong='CUSTOMER_RESET_PASSWORD'");
        Assert.DoesNotContain("reset", audit, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MatKhauBam", audit, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("1", await api.Sql($"SELECT COUNT(*) FROM dbo.NhatKyHeThong WHERE MaThaoTac='{reset.Text("MaThaoTac")}' AND LyDo IS NOT NULL"));
    }

    /// <summary>Route cũ không tạo/xóa/ngừng; khóa tài khoản vẫn bảo toàn đơn, thu, hóa đơn và khoản chờ hoàn.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-01"), Trait("Layer", "API")]
    public async Task BoTaoXoaNgung_BaoToanLichSu_VaPhanQuyen()
    {
        await using var api = await TestApi.Start("UC-ADM-01-Scope");
        var admin = await api.AdminLogin(); var customer = await api.NewCustomer(); var id = customer.Id;
        var account = await api.CustomerAccount(id, admin);
        var count = await api.Sql("SELECT COUNT(*) FROM dbo.TaiKhoanKhachHang");
        (await api.Send("POST", "/api/admin/customers", api.CustomerInput(), admin)).Expect(410, "Bỏ tạo khách Admin");
        (await api.Send("DELETE", $"/api/admin/customers/{id}", TestApi.Action(account), admin)).Expect(410, "Bỏ xóa khách");
        (await api.Send("POST", $"/api/admin/customers/{id}/status", TestApi.Action(account, ("TrangThai", "INACTIVE")), admin)).Expect(400, "Bỏ trạng thái ngừng");
        Assert.Equal(count, await api.Sql("SELECT COUNT(*) FROM dbo.TaiKhoanKhachHang"));
        Assert.Equal("ACTIVE", (await api.CustomerAccount(id, admin)).Text("trangThai"));
        await api.Sql($$"""
DECLARE @o bigint,@p bigint;
INSERT dbo.DonHang(MaDonHienThi,MaKhachHang,KenhDatHang,HinhThucNhanMon,PhiGiaoHangBanDau,PhiGiaoHangHienTai,TrangThai) VALUES('UCADM01-HISTORY',{{id}},'WEB','TAKEAWAY',0,0,'CANCELLED'); SET @o=SCOPE_IDENTITY();
INSERT dbo.ThanhToan(MaThanhToanHienThi,MaDonHang,PhuongThuc,SoTienYeuCau,SoTienDaXacMinh,TrangThai,MaThaoTac) VALUES('UCADM01-PAYMENT',@o,'BANK_TRANSFER',45000,45000,'VERIFIED',NEWID()); SET @p=SCOPE_IDENTITY();
INSERT dbo.HoaDon(SoHoaDon,MaDonHang,LoaiHoaDon,SoTien,NoiDungHoaDonDaChotJson,TrangThai,PhatHanhLuc,MaQuanLyPhatHanh) VALUES('UCADM01-INVOICE',@o,'ORIGINAL',45000,N'{}','ISSUED',SYSUTCDATETIME(),1);
INSERT dbo.HoSoHoanTien(MaHoanTienHienThi,MaDonHang,MaThanhToan,LyDoHoan,SoTienYeuCau,PhuongThucHoan,TrangThai,MaKhachHangYeuCau) VALUES('UCADM01-REFUND',@o,@p,N'Bảo toàn lịch sử',45000,'CASH','REQUESTED',{{id}});
""");
        (await api.Send("POST", $"/api/admin/customers/{id}/status", TestApi.Action(account, ("TrangThai", "LOCKED")), admin)).Expect(200, "Khóa không xóa lịch sử");
        Assert.Equal("1", await api.Sql($"SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.DonHang WHERE MaKhachHang={id} AND TrangThai='CANCELLED') AND EXISTS(SELECT 1 FROM dbo.ThanhToan WHERE MaThanhToanHienThi='UCADM01-PAYMENT' AND SoTienDaXacMinh=45000) AND EXISTS(SELECT 1 FROM dbo.HoaDon WHERE SoHoaDon='UCADM01-INVOICE' AND TrangThai='ISSUED') AND EXISTS(SELECT 1 FROM dbo.HoSoHoanTien WHERE MaHoanTienHienThi='UCADM01-REFUND' AND SoTienYeuCau=45000 AND TrangThai='REQUESTED') THEN 1 ELSE 0 END"));
        var visitor = await api.NewCustomer(); var user = await api.Login(visitor);
        var device = await api.NewPosClient(admin);
        var employee = (await api.Send("GET", "/api/pos/employees", headers: device.Headers)).Json[0]!;
        var open = api.OpenBody(employee, device); open["TienMatDauCa"] = 0;
        var staff = TestApi.Bearer((await api.Send("POST", "/api/pos/shifts", open, device.Headers)).Expect(200, "Mở ca kiểm quyền").Json);
        foreach (var headers in new Dictionary<string,string>?[] { null, user, staff })
        {
            var expected = headers is null ? 401 : 403;
            (await api.Send("GET", "/api/admin/customers", headers: headers)).Expect(expected, "Quyền danh sách");
            (await api.Send("GET", $"/api/admin/customers/{id}", headers: headers)).Expect(expected, "Quyền hồ sơ");
            (await api.Send("PUT", $"/api/admin/customers/{id}", Edit(account), headers)).Expect(expected, "Quyền cập nhật");
            (await api.Send("POST", $"/api/admin/customers/{id}/status", TestApi.Action(account, ("TrangThai", "ACTIVE")), headers)).Expect(expected, "Quyền khóa/mở");
            (await api.Send("POST", $"/api/admin/customers/{id}/reset-password", TestApi.Action(account, ("MatKhauTam", "reset")), headers)).Expect(expected, "Quyền reset");
        }
    }

    /// <summary>Chụp đúng dữ liệu sửa hồ sơ; mật khẩu và thay trạng thái thuộc chức năng riêng.</summary>
    private static JsonObject Edit(JsonNode account) => Body(("MaThaoTac", NewId()), ("TenDangNhap", account.Text("tenDangNhap")), ("HoTen", account.Text("hoTen")), ("Email", account.Text("email")), ("SoDienThoai", account.Text("soDienThoai")), ("PhienBanDuLieu", account.Text("phienBanDuLieu")));
}
