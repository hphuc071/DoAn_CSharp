using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using FourChang.Tests.Shared;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;
using static FourChang.Tests.Shared.TestData;
namespace FourChang.Tests.Admin;

/// <summary>ADM-02 chạy HTTP/SQL thật trên database GUID riêng, tuyệt đối không reset database đồ án.</summary>
public sealed class MenuAdministrationTests
{
    /// <summary>Chuẩn hóa payload test theo JSON thật; không gọi trực tiếp service để bỏ qua auth/transaction.</summary>
    private static JsonNode Json(object input) => JsonSerializer.SerializeToNode(input)!;
    /// <summary>Đọc snapshot typed để các assertion dùng cùng hợp đồng API mà Web/POS đang dùng.</summary>
    internal static async Task<MenuAdminSnapshot> Snapshot(TestApi api, Dictionary<string, string> admin) => JsonSerializer.Deserialize<MenuAdminSnapshot>((await api.Send("GET", "/api/admin/menu", headers: admin)).Expect(200, "Snapshot Admin").Content, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    /// <summary>Tạo payload món từ snapshot, giữ nguyên ID và trường khác khi thử thay giá/trạng thái.</summary>
    private static MenuDishInput Dish(MenuDish m, bool? publish = null, bool? sell = null) => new(m.MaDanhMuc, m.TenMon, m.MoTa, m.DuongDanAnh, m.ThuTuHienThi, publish ?? m.DaCongBo, sell ?? m.DangBan, m.CongBoTuLuc, m.PhienBanDuLieu, Guid.NewGuid());
    /// <summary>Chỉnh giá cấu hình không tự sửa tùy chọn, topping hoặc mặc định.</summary>
    internal static MenuVariantInput Variant(MenuVariant v, decimal? price = null) => new(v.MaMon, v.KichCo, v.NhietDo, price ?? v.GiaBan, v.DangBan, v.LaMacDinh, v.TuyChon, v.MonThem, v.PhienBanDuLieu, Guid.NewGuid());

    /// <summary>Kiểm tra quyền 401/403; hồ sơ khách và tên nhân viên không thể cấp quyền thực đơn.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-02")]
    public async Task PhanQuyen_ChiAdminDuocGhiMenu()
    {
        await using var api = await TestApi.Start("ADM03-Permissions");
        var admin = await api.AdminLogin();
        (await api.Send("GET", "/api/admin/menu")).Expect(401, "Chưa xác thực");
        var device = await api.NewDevice(admin);
        var employee = await api.Employee(admin);
        var staff = TestApi.Bearer((await api.Send("POST", "/api/pos/shifts", api.OpenBody(employee, device), device.Headers)).Expect(200, "Mở ca").Json);
        (await api.Send("GET", "/api/admin/menu", headers: staff)).Expect(403, "Staff không có quyền Admin");
        var input = new MenuCategoryInput("Nhóm bị chặn", null, 1, false, true, false, null, Guid.NewGuid());
        (await api.Send("POST", "/api/admin/menu/categories", Json(input), staff)).Expect(403, "Staff không tạo nhóm");
        Assert.Equal("0", await api.Sql("SELECT COUNT(*) FROM dbo.DanhMucMon WHERE TenDanhMuc=N'Nhóm bị chặn'"));
        (await api.Send("GET", "/api/catalog")).Expect(200, "Catalog công khai");
    }

    /// <summary>CRUD danh mục/món/cấu hình nháp; xác nhận tác động; từ chối trùng/version cũ và xóa có tham chiếu.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-02")]
    public async Task CRUD_DanhMuc_Mon_CauHinh_Version_VaThamChieu()
    {
        await using var api = await TestApi.Start("ADM03-CRUD");
        var admin = await api.AdminLogin();
        var cat = new MenuCategoryInput("Nhóm thử", null, 4, true, true, false, null, Guid.NewGuid());
        var receipt = (await api.Send("POST", "/api/admin/menu/categories", Json(cat), admin)).Expect(200, "Thêm danh mục").Json;
        var repeated = (await api.Send("POST", "/api/admin/menu/categories", Json(cat), admin)).Expect(200, "Gửi lại mã cũ").Json;
        Assert.Equal(receipt.Number("ma"), repeated.Number("ma"));
        var catId = receipt.Number("ma");
        (await api.Send("POST", "/api/admin/menu/categories", Json(cat with
        {
            MaThaoTac = Guid.NewGuid()
        }), admin)).Expect(400, "Chặn trùng tên");
        var dish = new MenuDishInput(catId, "Món thử", null, "sprite:1", 0, false, true, null, null, Guid.NewGuid());
        var saved = (await api.Send("POST", "/api/admin/menu/dishes", Json(dish), admin)).Expect(200, "Lưu món nháp").Json;
        var id = saved.Number("ma");
        var v = new MenuVariantInput(id, "M", "ICED", 40000, true, true, [new(50, 50, true)], [], null, Guid.NewGuid());
        var vr = (await api.Send("POST", "/api/admin/menu/variants", Json(v), admin)).Expect(200, "Lưu cấu hình nháp").Json;
        (await api.Send("POST", "/api/admin/menu/variants", Json(v with
        {
            MaThaoTac = Guid.NewGuid()
        }), admin)).Expect(400, "Không trùng size/nhiệt");
        var snap = await Snapshot(api, admin);
        var m = snap.Mon.Single(x => x.MaMon == id);
        var c = snap.DanhMuc.Single(x => x.MaDanhMuc == catId);
        (await api.Send("PUT", $"/api/admin/menu/dishes/{id}", Json(Dish(m, true)), admin)).Expect(400, "Thiếu BOM không công bố");
        var hide = cat with
        {
            DaCongBo = false,
            PhienBanDuLieu = c.PhienBanDuLieu,
            MaThaoTac = Guid.NewGuid()
        };
        (await api.Send("PUT", $"/api/admin/menu/categories/{catId}", Json(hide), admin)).Expect(409, "Cần xác nhận tác động");
        (await api.Send("PUT", $"/api/admin/menu/categories/{catId}", Json(hide with
        {
            XacNhanTacDong = true,
            MaThaoTac = Guid.NewGuid()
        }), admin)).Expect(200, "Ẩn có xác nhận");
        (await api.Send("PUT", $"/api/admin/menu/categories/{catId}", Json(hide with
        {
            XacNhanTacDong = true,
            MaThaoTac = Guid.NewGuid()
        }), admin)).Expect(409, "Chặn version cũ");
        snap = await Snapshot(api, admin);
        c = snap.DanhMuc.Single(x => x.MaDanhMuc == catId);
        (await api.Send("POST", $"/api/admin/menu/categories/{catId}/delete", Json(new MenuDeleteInput(c.PhienBanDuLieu, Guid.NewGuid())), admin)).Expect(409, "Chặn danh mục có món");
        (await api.Send("POST", $"/api/admin/menu/variants/{vr.Number("ma")}/delete", Json(new MenuDeleteInput(snap.CauHinh.Single(x => x.MaCauHinhMon == vr.Number("ma")).PhienBanDuLieu, Guid.NewGuid())), admin)).Expect(200, "Xóa cấu hình chưa dùng");
        (await api.Send("POST", $"/api/admin/menu/dishes/{id}/delete", Json(new MenuDeleteInput(m.PhienBanDuLieu, Guid.NewGuid())), admin)).Expect(200, "Xóa món chưa dùng");
        (await api.Send("POST", $"/api/admin/menu/categories/{catId}/delete", Json(new MenuDeleteInput(c.PhienBanDuLieu, Guid.NewGuid())), admin)).Expect(200, "Xóa danh mục rỗng");
    }

    /// <summary>Giá/ID thống nhất catalog, old endpoint; dữ liệu sai không làm đổi bản đã công bố.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-02")]
    public async Task GiaChung_TuyChon_Topping_Validation_VaReceipt()
    {
        await using var api = await TestApi.Start("ADM03-Prices");
        var admin = await api.AdminLogin();
        var snap = await Snapshot(api, admin);
        var v = snap.CauHinh.First(x => x.LaMacDinh);
        var input = Variant(v, 56000);
        var result = (await api.Send("PUT", $"/api/admin/menu/variants/{v.MaCauHinhMon}", Json(input), admin)).Expect(200, "Cập nhật giá").Json;
        var menu = (await api.Send("GET", "/api/menu")).Expect(200, "API menu cũ").Json.AsArray().Single(x => x!.Number("maMon") == v.MaMon)!;
        Assert.Equal(56000, menu.Number("price"));
        Assert.Equal(v.MaCauHinhMon, menu.Number("id"));
        var catalog = (await api.Send("GET", "/api/catalog")).Expect(200, "Catalog chung").Json;
        var publicV = catalog["mon"]!.AsArray().Single(x => x!.Number("maMon") == v.MaMon)!["cauHinh"]!.AsArray().Single(x => x!.Number("maCauHinhMon") == v.MaCauHinhMon)!;
        Assert.Equal(56000, publicV.Number("giaBan"));
        Assert.NotEmpty(publicV["monThem"]!.AsArray());
        (await api.Send("GET", $"/api/admin/menu/operations/{input.MaThaoTac}", headers: admin)).Expect(200, "Tra kết quả sau timeout");
        (await api.Send("PUT", $"/api/admin/menu/variants/{v.MaCauHinhMon}", Json(input), admin)).Expect(200, "Không cập nhật lần hai");
        Assert.Equal("1", await api.Sql($"SELECT COUNT(*) FROM dbo.SuKienDongBo WHERE MaThaoTac='{input.MaThaoTac}'"));
        var current = (await Snapshot(api, admin)).CauHinh.Single(x => x.MaCauHinhMon == v.MaCauHinhMon);
        (await api.Send("PUT", $"/api/admin/menu/variants/{v.MaCauHinhMon}", Json(Variant(current, -1)), admin)).Expect(400, "Giá âm");
        (await api.Send("PUT", $"/api/admin/menu/variants/{v.MaCauHinhMon}", Json(Variant(current, .5m)), admin)).Expect(400, "VND không làm tròn ngầm");
        (await api.Send("PUT", $"/api/admin/menu/variants/{v.MaCauHinhMon}", Json(Variant(current) with
        {
            TuyChon = [new(50, 50, true), new(50, 50, false)]
        }), admin)).Expect(400, "Không lặp cặp đường/đá");
        var invalid = Variant(current) with
        {
            MonThem = [new(current.MonThem[0].MaMonThem, int.MaxValue)]
        };
        (await api.Send("PUT", $"/api/admin/menu/variants/{v.MaCauHinhMon}", Json(invalid), admin)).Expect(400, "Giới hạn topping");
        (await api.Send("POST", $"/api/admin/menu/variants/{v.MaCauHinhMon}/delete", Json(new MenuDeleteInput(current.PhienBanDuLieu, Guid.NewGuid())), admin)).Expect(409, "Đã gắn BOM không xóa");
        var top = snap.MonThem[0];
        var updateTop = new MenuToppingInput(top.TenMonThem, top.MoTa, 12000, top.GioiHanMacDinhMoiLy, top.DuongDanAnh, top.DangSuDung, top.PhienBanDuLieu, Guid.NewGuid());
        (await api.Send("PUT", $"/api/admin/menu/toppings/{top.MaMonThem}", Json(updateTop), admin)).Expect(200, "Giá topping chung");
        catalog = (await api.Send("GET", "/api/catalog")).Json;
        Assert.Contains(catalog["mon"]!.AsArray().SelectMany(x => x!["cauHinh"]!.AsArray()).SelectMany(x => x!["monThem"]!.AsArray()), x => x!.Number("maMonThem") == top.MaMonThem && x!.Number("giaMoiPhan") == 12000);
    }

    /// <summary>Lịch/banner, món nháp và khóa thiếu tồn đều do server quyết định, không bị cờ bật bán vượt qua.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-02")]
    public async Task CongBo_Banner_Lich_VaKhaDungKho()
    {
        await using var api = await TestApi.Start("ADM03-Publish");
        var admin = await api.AdminLogin();
        var snap = await Snapshot(api, admin);
        var m = snap.Mon[0];
        var future = Dish(m) with
        {
            CongBoTuLuc = DateTime.UtcNow.AddHours(1)
        };
        (await api.Send("PUT", $"/api/admin/menu/dishes/{m.MaMon}", Json(future), admin)).Expect(200, "Hẹn công bố");
        (await api.Send("GET", $"/api/menu/{m.MaMon}/variants")).Expect(404, "Không lộ cấu hình món chưa đến lịch");
        m = (await Snapshot(api, admin)).Mon.Single(x => x.MaMon == m.MaMon);
        (await api.Send("PUT", $"/api/admin/menu/dishes/{m.MaMon}", Json(Dish(m) with
        {
            CongBoTuLuc = null
        }), admin)).Expect(200, "Công bố ngay");
        var banner = new MenuBannerInput("Món mới", "Nội dung Việt", "sprite:2", "/#menu", "HOME_HERO", 0, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddHours(1), true, null, Guid.NewGuid());
        var receipt = (await api.Send("POST", "/api/admin/menu/banners", Json(banner), admin)).Expect(200, "Công bố banner").Json;
        Assert.Single((await api.Send("GET", "/api/catalog")).Json["banner"]!.AsArray());
        var b = (await Snapshot(api, admin)).Banner.Single(x => x.MaBanner == receipt.Number("ma"));
        (await api.Send("PUT", $"/api/admin/menu/banners/{b.MaBanner}", Json(banner with
        {
            PhienBanDuLieu = b.PhienBanDuLieu,
            LienKet = "javascript:alert(1)",
            MaThaoTac = Guid.NewGuid()
        }), admin)).Expect(400, "Không công bố link chạy mã");
        Assert.Single((await api.Send("GET", "/api/catalog")).Json["banner"]!.AsArray());
        (await api.Send("POST", "/api/admin/menu/banners", Json(banner with
        {
            MaThaoTac = Guid.NewGuid(),
            KetThucLuc = DateTime.UtcNow.AddHours(-1)
        }), admin)).Expect(400, "Banner hết hạn");
        await api.Sql("UPDATE dbo.TonKho SET TonThucTe=0 WHERE TongGiuCho=0");
        var menu = (await api.Send("GET", "/api/menu")).Json.AsArray();
        Assert.All(menu, x => Assert.False(x!.Flag("available")));
    }

    /// <summary>Khi audit bị chặn, transaction rollback cả menu và outbox; không lưu nửa chừng.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-02")]
    public async Task AuditLoi_RollbackMenuVaOutbox()
    {
        await using var api = await TestApi.Start("ADM03-Rollback");
        var admin = await api.AdminLogin();
        var input = new MenuCategoryInput("Không được lưu", null, 1, false, true, false, null, Guid.NewGuid());
        await api.Sql("DENY INSERT ON dbo.NhatKyHeThong TO fourchang_app");
        try
        {
            Assert.True((await api.Send("POST", "/api/admin/menu/categories", Json(input), admin)).Status >= 400);
        }
        finally { await api.Sql("REVOKE INSERT ON dbo.NhatKyHeThong FROM fourchang_app"); }
        Assert.Equal("0", await api.Sql("SELECT COUNT(*) FROM dbo.DanhMucMon WHERE TenDanhMuc=N'Không được lưu'"));
        Assert.Equal("0", await api.Sql($"SELECT COUNT(*) FROM dbo.SuKienDongBo WHERE MaThaoTac='{input.MaThaoTac}'"));
    }

    /// <summary>Outbox sống qua lỗi worker; lease hết hạn được nhận lại và không tạo sự kiện thứ hai.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-02")]
    public async Task Outbox_LoiPhat_ThuLai_VaLease()
    {
        await using var api = await TestApi.Start("ADM03-Outbox");
        var admin = await api.AdminLogin();
        var input = new MenuCategoryInput("Sự kiện chờ", null, 1, false, true, false, null, Guid.NewGuid());
        await api.Sql("DENY UPDATE ON dbo.SuKienDongBo TO fourchang_app");
        try
        {
            (await api.Send("POST", "/api/admin/menu/categories", Json(input), admin)).Expect(200, "Commit không phụ thuộc phát sự kiện");
            await Task.Delay(1500);
            Assert.Equal("1", await api.Sql($"SELECT COUNT(*) FROM dbo.SuKienDongBo WHERE MaThaoTac='{input.MaThaoTac}' AND XuLyXongLuc IS NULL"));
            await api.Sql($"UPDATE dbo.SuKienDongBo SET MaLanXuLy=NEWID(),KhoaDenLuc=DATEADD(second,-1,SYSUTCDATETIME()) WHERE MaThaoTac='{input.MaThaoTac}'");
        }
        finally { await api.Sql("REVOKE UPDATE ON dbo.SuKienDongBo FROM fourchang_app; GRANT UPDATE ON dbo.SuKienDongBo TO fourchang_app"); }
        for (int i = 0; i < 40; i++)
        {
            if (await api.Sql($"SELECT COUNT(*) FROM dbo.SuKienDongBo WHERE MaThaoTac='{input.MaThaoTac}' AND XuLyXongLuc IS NOT NULL AND MaLanXuLy IS NULL") == "1")
                return;
            await Task.Delay(150);
        }
        Assert.Fail("Outbox không phục hồi sau lỗi/lease hết hạn.");
    }
    /// <summary>Mất phản hồi sau commit phải tra receipt, gửi lại cùng mã không tạo thêm đối tượng/sự kiện.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-02")]
    public async Task TimeoutSauCommit_TraReceipt_KhongTaoTrung()
    {
        await using var api = await TestApi.Start("ADM03-Timeout");
        var admin = await api.AdminLogin();
        await using var proxy = new NetworkFaultProxy(api.Url);
        using var http = new HttpClient { BaseAddress = new(proxy.Url), Timeout = TimeSpan.FromSeconds(5) };
        var client = new CafeApiClient(http) { Token = admin["Authorization"][7..] };
        var input = new MenuCategoryInput("Nhóm lỗi mạng", null, 1, false, true, false, null, Guid.NewGuid());
        proxy.DropMenu = true;
        await Assert.ThrowsAsync<CafeApiException>(() => client.SaveMenuAsync("categories", null, input));
        var receipt = await client.MenuReceiptAsync(input.MaThaoTac);
        var repeated = await client.SaveMenuAsync("categories", null, input);
        Assert.Equal(receipt.Ma, repeated.Ma);
        Assert.Equal("1", await api.Sql($"SELECT COUNT(*) FROM dbo.SuKienDongBo WHERE MaThaoTac='{input.MaThaoTac}'"));
        proxy.Offline = true;
        await Assert.ThrowsAsync<CafeApiException>(() => client.SaveMenuAsync("categories", null, input with { TenDanhMuc = "Không lưu offline", MaThaoTac = Guid.NewGuid() }));
        Assert.Equal("0", await api.Sql("SELECT COUNT(*) FROM dbo.DanhMucMon WHERE TenDanhMuc=N'Không lưu offline'"));
    }

    /// <summary>Đơn cũ có snapshot món/topping/BOM thật, sửa giá gốc không làm thay đổi các trường đã chốt.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-02")]
    public async Task SuaGia_KhongDoiSnapshotDonCu()
    {
        await using var api = await TestApi.Start("ADM03-Snapshots");
        var admin = await api.AdminLogin();
        var employee = await api.Employee(admin);
        var device = await api.NewDevice(admin);
        (await api.Send("POST", "/api/pos/shifts", api.OpenBody(employee, device), device.Headers)).Expect(200, "Mở ca nguồn đơn");
        var snapshot = await Snapshot(api, admin);
        var v = snapshot.CauHinh.First(x => x.LaMacDinh);
        var t = snapshot.MonThem.First();
        await api.Sql($"""
            DECLARE @order bigint,@line bigint;
            INSERT dbo.DonHang(MaDonHienThi,MaCaLamViec,KenhDatHang,HinhThucNhanMon,PhiGiaoHangBanDau,PhiGiaoHangHienTai,TrangThai)
            SELECT 'SNAPSHOT-ADM03',MAX(MaCaLamViec),'POS','TAKEAWAY',0,0,'PENDING_CONFIRMATION' FROM dbo.CaLamViec;
            SET @order=SCOPE_IDENTITY();
            INSERT dbo.ChiTietDonHang(MaDonHang,MaCauHinhMon,MaCongThuc,TenMonDaChot,KichCoDaChot,NhietDoDaChot,PhanTramDuong,PhanTramDa,GiaMonDaChot,SoLuongDat,SoLuongBatDauPha,SoLuongPhaXong,SoLuongDaHuy)
            SELECT @order,v.MaCauHinhMon,c.MaCongThuc,m.TenMon,v.KichCo,v.NhietDo,50,50,v.GiaBan,1,0,0,0 FROM dbo.CauHinhMon v JOIN dbo.Mon m ON m.MaMon=v.MaMon JOIN dbo.CongThuc c ON c.MaCauHinhMon=v.MaCauHinhMon AND c.TrangThai='ACTIVE' WHERE v.MaCauHinhMon={v.MaCauHinhMon};
            SET @line=SCOPE_IDENTITY();
            INSERT dbo.MonThemChiTietDonHang(MaChiTietDonHang,MaMonThem,MaCongThuc,TenMonThemDaChot,SoPhanMoiLy,GiaMoiPhanDaChot)
            SELECT @line,t.MaMonThem,c.MaCongThuc,t.TenMonThem,1,t.GiaMoiPhan FROM dbo.MonThem t JOIN dbo.CongThuc c ON c.MaMonThem=t.MaMonThem AND c.TrangThai='ACTIVE' WHERE t.MaMonThem={t.MaMonThem};
            """);
        var before = await api.Sql("SELECT CONCAT(TenMonDaChot,'|',GiaMonDaChot,'|',MaCongThuc) FROM dbo.ChiTietDonHang");
        var beforeTop = await api.Sql("SELECT CONCAT(TenMonThemDaChot,'|',GiaMoiPhanDaChot,'|',MaCongThuc) FROM dbo.MonThemChiTietDonHang");
        (await api.Send("PUT", $"/api/admin/menu/variants/{v.MaCauHinhMon}", Json(Variant(v, 90000)), admin)).Expect(200, "Đổi giá gốc món");
        var top = new MenuToppingInput(t.TenMonThem + " mới", t.MoTa, 20000, t.GioiHanMacDinhMoiLy, t.DuongDanAnh, t.DangSuDung, t.PhienBanDuLieu, Guid.NewGuid());
        (await api.Send("PUT", $"/api/admin/menu/toppings/{t.MaMonThem}", Json(top), admin)).Expect(200, "Đổi tên/giá topping gốc");
        Assert.Equal(before, await api.Sql("SELECT CONCAT(TenMonDaChot,'|',GiaMonDaChot,'|',MaCongThuc) FROM dbo.ChiTietDonHang"));
        Assert.Equal(beforeTop, await api.Sql("SELECT CONCAT(TenMonThemDaChot,'|',GiaMoiPhanDaChot,'|',MaCongThuc) FROM dbo.MonThemChiTietDonHang"));
    }

}
