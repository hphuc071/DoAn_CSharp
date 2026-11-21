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
        var device = await api.NewPosClient(admin);
        var employee = await api.Employee(admin);
        var staff = TestApi.Bearer((await api.Send("POST", "/api/pos/shifts", api.OpenBody(employee, device), device.Headers)).Expect(200, "Mở ca").Json);
        (await api.Send("GET", "/api/admin/menu", headers: staff)).Expect(403, "Staff không có quyền Admin");
        var input = new MenuCategoryInput("Nhóm bị chặn", null, 1, false, true, false, null, Guid.NewGuid());
        (await api.Send("POST", "/api/admin/menu/categories", Json(input), staff)).Expect(403, "Staff không tạo nhóm");
        Assert.Equal("0", await api.Sql("SELECT COUNT(*) FROM sys.tables WHERE name='DanhMucMon'"));
        // Cả API công thức và route cũ đều kiểm quyền trước khi đọc/ghi.
        var recipeId = (await Snapshot(api, admin)).CauHinh.First().MaCauHinhMon;
        foreach (var caller in new[] { staff, await api.Login(await api.NewCustomer("menu")) })
        {
            (await api.Send("GET", "/api/admin/recipes", headers: caller)).Expect(403, "Không được đọc công thức Admin");
            (await api.Send("PUT", $"/api/admin/recipes/{recipeId}", Body(), caller)).Expect(403, "Không được sửa công thức");
            (await api.Send("POST", "/api/admin/menu/banners", Body(), caller)).Expect(403, "Route đã bỏ vẫn kiểm quyền");
        }
        (await api.Send("GET", "/api/admin/recipes")).Expect(401, "Công thức cần đăng nhập");
        (await api.Send("GET", "/api/catalog")).Expect(200, "Catalog công khai");
    }

    /// <summary>Topping và định lượng một phần lưu cùng lúc, bảo toàn BOM cũ và tính đúng lượng topping trong đơn.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-02")]
    public async Task Topping_DinhLuongTrongForm_LuuCungLuc_Replay_Kho()
    {
        await using var api = await TestApi.Start("ADM02-ToppingPortion");
        var admin = await api.AdminLogin();
        var materials = JsonSerializer.Deserialize<IngredientSnapshot>((await api.Send("GET", "/api/admin/ingredients", headers: admin)).Content, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var n = materials.NguyenLieu.Single(n => n.TenNguyenLieu == "Trân châu");
        var input = new MenuToppingInput("Trân châu định lượng", null, 10000, 3, null, true, null, Guid.NewGuid(), new(n.MaNguyenLieu, 30));
        var reply = (await api.Send("POST", "/api/admin/menu/toppings", Json(input), admin)).Expect(200, "Lưu topping và công thức một lần");
        var id = reply.Json.Number("ma");
        (await api.Send("POST", "/api/admin/menu/toppings", Json(input), admin)).Expect(200, "Retry không nhân bản");
        var recipes = JsonSerializer.Deserialize<List<RecipeView>>((await api.Send("GET", "/api/admin/recipes", headers: admin)).Content, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var recipe = Assert.Single(recipes, r => r.MaMonThem == id);
        var line = Assert.Single(recipe.ThanhPhan); Assert.Equal(30, line.DinhMucCoSo); Assert.Equal("FIXED", line.LoaiDinhMuc);
        var topping = (await Snapshot(api, admin)).MonThem.Single(t => t.MaMonThem == id); Assert.True(topping.DangSuDung);
        var edit = input with { PhienBanDuLieu = topping.PhienBanDuLieu, MaThaoTac = Guid.NewGuid(), DinhLuong = new(n.MaNguyenLieu, 45, recipe.MaCongThuc, recipe.PhienBanDuLieu) };
        (await api.Send("PUT", $"/api/admin/menu/toppings/{id}", Json(edit with { TenMonThem = "Không được lưu", DinhLuong = edit.DinhLuong! with { SoLuong = 0 } }), admin)).Expect(400, "Lỗi lượng rollback tên và topping");
        Assert.Equal(topping.TenMonThem, (await Snapshot(api, admin)).MonThem.Single(t => t.MaMonThem == id).TenMonThem);
        (await api.Send("PUT", $"/api/admin/menu/toppings/{id}", Json(edit), admin)).Expect(200, "Đổi lượng mỗi phần");
        Assert.Equal("30.0000", await api.Sql($"SELECT CONVERT(varchar(50),DinhMucCoSo) FROM dbo.ChiTietCongThuc WHERE MaCongThuc={recipe.MaCongThuc}"));
        topping = (await Snapshot(api, admin)).MonThem.Single(t => t.MaMonThem == id);
        (await api.Send("PUT", $"/api/admin/menu/toppings/{id}", Json(edit with { PhienBanDuLieu = topping.PhienBanDuLieu, MaThaoTac = Guid.NewGuid() }), admin)).Expect(409, "Phiên bản công thức cũ bị chặn");
        var employee = await api.Employee(admin); var device = await api.NewPosClient(admin);
        var staff = TestApi.Bearer((await api.Send("POST", "/api/pos/shifts", api.OpenBody(employee, device), device.Headers)).Expect(200, "Mở ca").Json);
        var variant = (await Snapshot(api, admin)).CauHinh.First(v => v.LaMacDinh && v.DangBan);
        var price = (variant.GiaBan + 20000) * 2;
        var checkout = Body(("MaThaoTac", NewId()), ("HinhThuc", "TAKEAWAY"), ("PhuongThuc", "CASH"), ("TongTienDaChapNhan", price), ("TienKhachDua", price),
            ("Items", new[] { Body(("MaCauHinhMon", variant.MaCauHinhMon), ("SoLuong", 2), ("PhanTramDuong", 50), ("PhanTramDa", variant.NhietDo == "HOT" ? 0 : 50), ("Toppings", new[] { Body(("MaMonThem", id), ("SoPhanMoiLy", 2)) })) }));
        var order = (await api.Send("POST", "/api/pos/checkout", checkout, staff)).Expect(200, "2ly mỗi ly2phần").Json.Number("maDonHang");
        Assert.Equal("180.0000", await api.Sql($"SELECT CONVERT(varchar(50),SUM(n.SoLuongGiuCho)) FROM dbo.NguyenLieuChiTietDonHang n JOIN dbo.ChiTietDonHang l ON n.MaChiTietDonHang=l.MaChiTietDonHang WHERE l.MaDonHang={order} AND n.MaNguyenLieu={n.MaNguyenLieu}"));
    }

    /// <summary>Bảng size lưu nguyên tử, giữ ID/giá/trạng thái cũ, retry không tạo trùng và lỗi cuối bảng rollback.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-02")]
    public async Task CauHinhNhieuSize_Gia_MacDinh_Replay_Rollback()
    {
        await using var api = await TestApi.Start("ADM02-VariantBatch");
        var admin = await api.AdminLogin();
        var dish = (await api.Send("POST", "/api/admin/menu/dishes", Json(new MenuDishInput(1, "Món nhiều size", null, null, 0, false, false, null, null, Guid.NewGuid())), admin)).Expect(200, "Tạo nháp").Json.Number("ma");
        var input = new MenuVariantBatchInput(dish, new[] { "S", "M", "L" }.Select((size, i) => new MenuVariantBatchLine(null, size, "ICED", 25000 + 5000 * i, size == "M", null)).ToList(), Guid.NewGuid());
        (await api.Send("POST", "/api/admin/menu/variants/batch", Json(input))).Expect(401, "Batch cần đăng nhập");
        var replies = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => api.Send("POST", "/api/admin/menu/variants/batch", Json(input), admin)));
        foreach (var reply in replies) reply.Expect(200, "Lưu3size một lần");
        Assert.Equal(replies[0].Json.Number("ma"), replies[1].Json.Number("ma"));
        var saved = (await Snapshot(api, admin)).CauHinh.Where(v => v.MaMon == dish).OrderBy(v => v.KichCo).ToList();
        Assert.Equal(3, saved.Count); Assert.All(saved, v => Assert.False(v.DangBan));
        Assert.Single(saved, v => v.LaMacDinh && v.KichCo == "M");
        var update = new MenuVariantBatchInput(dish, saved.Select(v => new MenuVariantBatchLine(v.MaCauHinhMon, v.KichCo, v.NhietDo, v.GiaBan + 1000, v.KichCo == "L", v.PhienBanDuLieu))
            .Concat(new[] { "S", "M", "L" }.Select(size => new MenuVariantBatchLine(null, size, "HOT", 30000, false, null))).ToList(), Guid.NewGuid());
        var bad = update.CauHinh.ToList(); bad[^1] = bad[^1] with { GiaBan = -1 };
        (await api.Send("POST", "/api/admin/menu/variants/batch", Json(update with { CauHinh = bad }), admin)).Expect(400, "Giá cuối sai không lưu cả bảng");
        var unchanged = (await Snapshot(api, admin)).CauHinh.Where(v => v.MaMon == dish).OrderBy(v => v.KichCo).ToList();
        Assert.Equal(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(unchanged));
        (await api.Send("POST", "/api/admin/menu/variants/batch", Json(update), admin)).Expect(200, "Thêm nóng và đổi mặc định");
        var after = (await Snapshot(api, admin)).CauHinh.Where(v => v.MaMon == dish).ToList(); Assert.Equal(6, after.Count);
        foreach (var old in saved) Assert.Equal(old.GiaBan + 1000, after.Single(v => v.MaCauHinhMon == old.MaCauHinhMon).GiaBan);
        Assert.Single(after, v => v.LaMacDinh && v.KichCo == "L" && v.NhietDo == "ICED");
        (await api.Send("POST", "/api/admin/menu/variants/batch", Json(update with { MaThaoTac = Guid.NewGuid() }), admin)).Expect(409, "Snapshot cũ không ghi đè");
        var activeDish = (await Snapshot(api, admin)).Mon.First(m => m.DaCongBo && m.DangBan);
        var active = (await Snapshot(api, admin)).CauHinh.Where(v => v.MaMon == activeDish.MaMon).ToList();
        var keep = new MenuVariantBatchInput(activeDish.MaMon, active.Select(v => new MenuVariantBatchLine(v.MaCauHinhMon, v.KichCo, v.NhietDo, v.GiaBan, v.LaMacDinh, v.PhienBanDuLieu)).ToList(), Guid.NewGuid());
        (await api.Send("POST", "/api/admin/menu/variants/batch", Json(keep), admin)).Expect(200, "Giữ cấu hình đang bán");
        var kept = (await Snapshot(api, admin)).CauHinh.Where(v => v.MaMon == activeDish.MaMon).ToList();
        foreach (var old in active) Assert.Equal(old.DangBan, kept.Single(v => v.MaCauHinhMon == old.MaCauHinhMon).DangBan);
    }

    /// <summary>Loại món cố định; thêm/sửa món ẩn, từ chối trùng/version cũ và chặn xóa qua client cũ.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-02")]
    public async Task CRUD_DanhMuc_Mon_CauHinh_Version_VaThamChieu()
    {
        await using var api = await TestApi.Start("Menu30-FixedCategories");
        var admin = await api.AdminLogin();
        var snapshot = await Snapshot(api, admin);
        Assert.Equal(new[] { "Cà phê", "Trà", "Đá xay" }, snapshot.DanhMuc.Select(x => x.TenDanhMuc));
        var category = new MenuCategoryInput("Nhóm thử", null, 0, true, true, false, null, Guid.NewGuid());
        (await api.Send("POST", "/api/admin/menu/categories", Json(category), admin)).Expect(409, "Loại món cố định");
        var input = new MenuDishInput(1, "Món thử", null, null, 0, false, true, null, null, Guid.NewGuid());
        var saved = (await api.Send("POST", "/api/admin/menu/dishes", Json(input), admin)).Expect(200, "Tạo món nháp").Json;
        var replay = (await api.Send("POST", "/api/admin/menu/dishes", Json(input), admin)).Expect(200, "Replay").Json;
        Assert.Equal(saved.Number("ma"), replay.Number("ma"));
        (await api.Send("POST", "/api/admin/menu/dishes", Json(input with { MaDanhMuc = 99, MaThaoTac = Guid.NewGuid() }), admin)).Expect(400, "Loại món không hợp lệ");
        (await api.Send("POST", "/api/admin/menu/dishes", Json(input with { MaThaoTac = Guid.NewGuid() }), admin)).Expect(400, "Không trùng tên cùng loại");
        var dish = (await Snapshot(api, admin)).Mon.Single(x => x.MaMon == saved.Number("ma"));
        // Nháp cho phép tắt cả hai cờ dù chưa có cấu hình/công thức.
        var edit = Dish(dish, publish: false, sell: false) with { TenMon = "Món đã sửa" };
        (await api.Send("PUT", $"/api/admin/menu/dishes/{dish.MaMon}", Json(edit), admin)).Expect(200, "Sửa nháp");
        (await api.Send("PUT", $"/api/admin/menu/dishes/{dish.MaMon}", Json(edit with { MaThaoTac = Guid.NewGuid() }), admin)).Expect(409, "Version cũ");
        dish = (await Snapshot(api, admin)).Mon.Single(x => x.MaMon == dish.MaMon);
        Assert.False(dish.DaCongBo); Assert.False(dish.DangBan);
        var newDraft = input with { TenMon = "Món nháp chưa chuẩn bị", DaCongBo = false, DangBan = false, MaThaoTac = Guid.NewGuid() };
        var draftId = (await api.Send("POST", "/api/admin/menu/dishes", Json(newDraft), admin)).Expect(200, "Lưu mới khi bỏ cả hai cờ").Json.Number("ma");
        var draft = (await Snapshot(api, admin)).Mon.Single(x => x.MaMon == draftId);
        Assert.False(draft.DaCongBo); Assert.False(draft.DangBan);
        (await api.Send("PUT", $"/api/admin/menu/dishes/{draftId}", Json(Dish(draft, true, true)), admin)).Expect(400, "Chưa đủ cấu hình/công thức thì chưa bật được");
        Assert.DoesNotContain((await api.Send("GET", "/api/menu")).Json.AsArray(), x => x!.Number("maMon") == draftId);
        (await api.Send("POST", $"/api/admin/menu/dishes/{dish.MaMon}/delete", Json(new MenuDeleteInput(dish.PhienBanDuLieu, Guid.NewGuid())), admin)).Expect(410, "Đã bỏ xóa, dùng ẩn/ngừng");
        Assert.Equal("0", await api.Sql("SELECT COUNT(*) FROM sys.tables WHERE name='DanhMucMon'"));
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
        (await api.Send("POST", $"/api/admin/menu/variants/{v.MaCauHinhMon}/delete", Json(new MenuDeleteInput(current.PhienBanDuLieu, Guid.NewGuid())), admin)).Expect(410, "Không có xóa cấu hình trong phạm vi tinh gọn");
        var top = snap.MonThem[0];
        var updateTop = new MenuToppingInput(top.TenMonThem, top.MoTa, 12000, top.GioiHanMacDinhMoiLy, top.DuongDanAnh, top.DangSuDung, top.PhienBanDuLieu, Guid.NewGuid());
        (await api.Send("PUT", $"/api/admin/menu/toppings/{top.MaMonThem}", Json(updateTop), admin)).Expect(200, "Giá topping chung");
        catalog = (await api.Send("GET", "/api/catalog")).Json;
        Assert.Contains(catalog["mon"]!.AsArray().SelectMany(x => x!["cauHinh"]!.AsArray()).SelectMany(x => x!["monThem"]!.AsArray()), x => x!.Number("maMonThem") == top.MaMonThem && x!.Number("giaMoiPhan") == 12000);
    }

    /// <summary>Chặn banner/lịch, tắt bán vẫn hiện Tạm hết; ẩn chủ động mới loại khỏi menu.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-02")]
    public async Task AnHien_TatBan_KhongBannerKhongLich()
    {
        await using var api = await TestApi.Start("ADM02-Visibility"); var admin = await api.AdminLogin();
        var snapshot = await Snapshot(api, admin); var m = snapshot.Mon.First();
        Assert.Empty(snapshot.Banner);
        (await api.Send("PUT", $"/api/admin/menu/dishes/{m.MaMon}", Json(Dish(m) with { CongBoTuLuc = DateTime.UtcNow.AddHours(1) }), admin)).Expect(400, "Không lên lịch công bố");
        (await api.Send("POST", "/api/admin/menu/banners", Body(), admin)).Expect(410, "Không quản lý banner");
        (await api.Send("PUT", "/api/admin/menu/banners/1", Body(), admin)).Expect(410, "Không sửa banner qua client cũ");
        (await api.Send("PUT", $"/api/admin/menu/dishes/{m.MaMon}", Json(Dish(m, true, false)), admin)).Expect(200, "Tắt bán");
        var card = (await api.Send("GET", "/api/menu")).Json.AsArray().Single(x => x!.Number("maMon") == m.MaMon)!;
        Assert.False(card.Flag("available"));
        m = (await Snapshot(api, admin)).Mon.Single(x => x.MaMon == m.MaMon);
        (await api.Send("PUT", $"/api/admin/menu/dishes/{m.MaMon}", Json(Dish(m, false, false)), admin)).Expect(200, "Ẩn chủ động");
        Assert.DoesNotContain((await api.Send("GET", "/api/menu")).Json.AsArray(), x => x!.Number("maMon") == m.MaMon);
        // Bật bán không vượt thiếu kho; thẻ vẫn còn và bị khóa đặt.
        await api.Sql("UPDATE dbo.TonKho SET TonThucTe=TongGiuCho");
        m = (await Snapshot(api, admin)).Mon.Single(x => x.MaMon == m.MaMon);
        (await api.Send("PUT", $"/api/admin/menu/dishes/{m.MaMon}", Json(Dish(m, true, true)), admin)).Expect(200, "Hiển thị/bật bán khi thiếu kho");
        card = (await api.Send("GET", "/api/menu")).Json.AsArray().Single(x => x!.Number("maMon") == m.MaMon)!;
        Assert.False(card.Flag("available"));
    }

    /// <summary>Khi audit bị chặn, transaction rollback cả menu và outbox; không lưu nửa chừng.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-02")]
    public async Task AuditLoi_RollbackMenuVaOutbox()
    {
        await using var api = await TestApi.Start("ADM03-Rollback");
        var admin = await api.AdminLogin();
        var input = new MenuDishInput(1, "Không được lưu", null, null, 1, false, true, null, null, Guid.NewGuid());
        await api.Sql("DENY INSERT ON dbo.NhatKyHeThong TO fourchang_app");
        try
        {
            Assert.True((await api.Send("POST", "/api/admin/menu/dishes", Json(input), admin)).Status >= 400);
        }
        finally { await api.Sql("REVOKE INSERT ON dbo.NhatKyHeThong FROM fourchang_app"); }
        Assert.Equal("0", await api.Sql("SELECT COUNT(*) FROM dbo.Mon WHERE TenMon=N'Không được lưu'"));
        Assert.Equal("0", await api.Sql($"SELECT COUNT(*) FROM dbo.SuKienDongBo WHERE MaThaoTac='{input.MaThaoTac}'"));
    }

    /// <summary>Outbox sống qua lỗi worker; lease hết hạn được nhận lại và không tạo sự kiện thứ hai.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-02")]
    public async Task Outbox_LoiPhat_ThuLai_VaLease()
    {
        await using var api = await TestApi.Start("ADM03-Outbox");
        var admin = await api.AdminLogin();
        var input = new MenuDishInput(1, "Sự kiện chờ", null, null, 1, false, true, null, null, Guid.NewGuid());
        await api.Sql("DENY UPDATE ON dbo.SuKienDongBo TO fourchang_app");
        try
        {
            (await api.Send("POST", "/api/admin/menu/dishes", Json(input), admin)).Expect(200, "Commit không phụ thuộc phát sự kiện");
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
        var input = new MenuDishInput(1, "Nhóm lỗi mạng", null, null, 1, false, true, null, null, Guid.NewGuid());
        proxy.DropMenu = true;
        await Assert.ThrowsAsync<CafeApiException>(() => client.SaveMenuAsync("dishes", null, input));
        var receipt = await client.MenuReceiptAsync(input.MaThaoTac);
        var repeated = await client.SaveMenuAsync("dishes", null, input);
        Assert.Equal(receipt.Ma, repeated.Ma);
        Assert.Equal("1", await api.Sql($"SELECT COUNT(*) FROM dbo.SuKienDongBo WHERE MaThaoTac='{input.MaThaoTac}'"));
        proxy.Offline = true;
        await Assert.ThrowsAsync<CafeApiException>(() => client.SaveMenuAsync("dishes", null, input with { TenMon = "Không lưu offline", MaThaoTac = Guid.NewGuid() }));
        Assert.Equal("0", await api.Sql("SELECT COUNT(*) FROM dbo.Mon WHERE TenMon=N'Không lưu offline'"));
    }

    /// <summary>Đơn cũ có snapshot món/topping/BOM thật, sửa giá gốc không làm thay đổi các trường đã chốt.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-02")]
    public async Task SuaGia_KhongDoiSnapshotDonCu()
    {
        await using var api = await TestApi.Start("ADM03-Snapshots");
        var admin = await api.AdminLogin();
        var employee = await api.Employee(admin);
        var device = await api.NewPosClient(admin);
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

    /// <summary>Upload ảnh thật có kiểm quyền/dung lượng; cùng nội dung không tạo file trùng và tên không thoát thư mục.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-02")]
    public async Task ThemAnh_PhanQuyen_DuongDanChung_ThuLaiKhongTrung()
    {
        await using var api = await TestApi.Start("ADM02-Images"); var admin = await api.AdminLogin();
        var png = ImageBytes(); var input = new MenuImageInput(Convert.ToBase64String(png));
        (await api.Send("POST", "/api/admin/menu/images", Json(input))).Expect(401, "Upload cần Admin");
        var customer = await api.Login(await api.NewCustomer("image"));
        (await api.Send("POST", "/api/admin/menu/images", Json(input), customer)).Expect(403, "Khách không tải ảnh menu");
        (await api.Send("POST", "/api/admin/menu/images", Json(new MenuImageInput("not-base64")), admin)).Expect(400, "Sai dữ liệu");
        (await api.Send("POST", "/api/admin/menu/images", Json(new MenuImageInput(Convert.ToBase64String("<svg/>"u8.ToArray()))), admin)).Expect(400, "Không nhận mã SVG");
        (await api.Send("POST", "/api/admin/menu/images", Json(new MenuImageInput(new string('A',7 * 1024 * 1024 + 1))), admin)).Expect(400, "Chặn ảnh quá lớn");
        var image = (await api.Send("POST", "/api/admin/menu/images", Json(input), admin)).Expect(200, "Upload ảnh thật").Json.Text("path");
        var replay = (await api.Send("POST", "/api/admin/menu/images", Json(input), admin)).Expect(200, "Thử lại cùng ảnh").Json.Text("path");
        Assert.Equal(image, replay); Assert.StartsWith("/menu-images/", image);
        Assert.Single(Directory.GetFiles(Path.Combine(api.Artifacts, "menu-images"), "*.png"));
        using var http = new HttpClient { BaseAddress = new(api.Url) };
        using var response = await http.GetAsync(image);
        Assert.Equal("image/png", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(png, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await http.GetAsync("/menu-images/no-file.png")).StatusCode);
        var dish = (await Snapshot(api, admin)).Mon.First();
        (await api.Send("PUT", $"/api/admin/menu/dishes/{dish.MaMon}", Json(Dish(dish) with { DuongDanAnh = image }), admin)).Expect(200, "Gắn ảnh đã tải");
        var card = (await api.Send("GET", "/api/menu")).Json.AsArray().Single(x => x!.Number("maMon") == dish.MaMon)!;
        Assert.Equal(image, card.Text("imagePath"));
    }

    /// <summary>Thứ tự do server cấp dưới khóa; sửa/gửi lại không đổi vị trí món.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-02")]
    public async Task ThuTuTuDong_TaoDongThoi_SuaKhongDoiViTri()
    {
        await using var api = await TestApi.Start("ADM02-AutoOrder"); var admin = await api.AdminLogin();
        var before = await Snapshot(api, admin); var last = before.Mon.Max(x => x.ThuTuHienThi);
        var first = new MenuDishInput(1, "Món thứ tự A", null, null, 99999, false, false, null, null, Guid.NewGuid());
        var second = first with { TenMon = "Món thứ tự B", ThuTuHienThi = 0, MaThaoTac = Guid.NewGuid() };
        var created = await Task.WhenAll(api.Send("POST", "/api/admin/menu/dishes", Json(first), admin), api.Send("POST", "/api/admin/menu/dishes", Json(second), admin));
        foreach (var response in created) response.Expect(200, "Tạo song song");
        var snapshot = await Snapshot(api, admin);
        var added = snapshot.Mon.Where(x => created.Any(c => c.Json.Number("ma") == x.MaMon)).ToList();
        Assert.Equal(new[] { last + 1, last + 2 }, added.Select(x => x.ThuTuHienThi).Order());
        var item = added[0];
        (await api.Send("PUT", $"/api/admin/menu/dishes/{item.MaMon}", Json(Dish(item) with { ThuTuHienThi = 0 }), admin)).Expect(200, "Sửa giữ vị trí");
        Assert.Equal(item.ThuTuHienThi, (await Snapshot(api, admin)).Mon.Single(x => x.MaMon == item.MaMon).ThuTuHienThi);
    }

    /// <summary>Tạo ảnh PNG nhỏ thật cho test upload/đọc, không dùng file dữ liệu của người dùng.</summary>
    internal static byte[] ImageBytes()
    {
        using var bitmap = new System.Drawing.Bitmap(12, 12);
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap)) graphics.Clear(System.Drawing.Color.Brown);
        using var stream = new MemoryStream(); bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png); return stream.ToArray();
    }

    /// <summary>Thêm món từ mẫu lưu nguyên tử, replay không nhân đôi; công thức/size/giá được sao chép độc lập.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-02")]
    public async Task MonMoi_DungMonMau_CongThucRieng_ReplayKhongTrung()
    {
        await using var api = await TestApi.Start("ADM02-DishTemplate"); var admin = await api.AdminLogin();
        var before = await Snapshot(api, admin); var source = before.Mon.First();
        var variants = before.CauHinh.Where(v => v.MaMon == source.MaMon && v.DangBan).ToList();
        var sourceLines = await api.Sql($"SELECT l.* FROM dbo.ChiTietCongThuc l JOIN dbo.CongThuc c ON l.MaCongThuc=c.MaCongThuc JOIN dbo.CauHinhMon v ON c.MaCauHinhMon=v.MaCauHinhMon WHERE v.MaMon={source.MaMon} ORDER BY l.MaCongThuc,l.MaNguyenLieu,l.LoaiDinhMuc FOR JSON PATH");
        var input = new MenuDishInput(1, "Cà phê công thức mẫu", null, null, 0, true, true, null, null, Guid.NewGuid(), source.MaMon);
        var result = await Task.WhenAll(api.Send("POST", "/api/admin/menu/dishes", Json(input), admin), api.Send("POST", "/api/admin/menu/dishes", Json(input), admin));
        foreach (var response in result) response.Expect(200, "Thêm món đủ cấu hình và công thức");
        Assert.Equal(result[0].Json.Number("ma"), result[1].Json.Number("ma"));
        var id = result[0].Json.Number("ma"); var copied = (await Snapshot(api, admin)).CauHinh.Where(v => v.MaMon == id).ToList();
        Assert.Equal(variants.Count, copied.Count); Assert.Single(copied, v => v.LaMacDinh);
        foreach (var old in variants)
        {
            var current = copied.Single(v => v.KichCo == old.KichCo && v.NhietDo == old.NhietDo);
            Assert.Equal(old.GiaBan, current.GiaBan); Assert.NotEqual(old.MaCauHinhMon, current.MaCauHinhMon);
            var oldRecipe = await api.Sql($"SELECT MaCongThuc FROM dbo.CongThuc WHERE MaCauHinhMon={old.MaCauHinhMon} AND TrangThai='ACTIVE'");
            var newRecipe = await api.Sql($"SELECT MaCongThuc FROM dbo.CongThuc WHERE MaCauHinhMon={current.MaCauHinhMon} AND TrangThai='ACTIVE'");
            Assert.NotEqual(oldRecipe, newRecipe);
            var projection = "MaNguyenLieu,DinhMucCoSo,LoaiDinhMuc";
            Assert.Equal(await api.Sql($"SELECT {projection} FROM dbo.ChiTietCongThuc WHERE MaCongThuc={oldRecipe} ORDER BY MaNguyenLieu,LoaiDinhMuc FOR JSON PATH"),
                await api.Sql($"SELECT {projection} FROM dbo.ChiTietCongThuc WHERE MaCongThuc={newRecipe} ORDER BY MaNguyenLieu,LoaiDinhMuc FOR JSON PATH"));
        }
        var recipes = JsonSerializer.Deserialize<List<RecipeView>>((await api.Send("GET", "/api/admin/recipes", headers: admin)).Json.ToJsonString(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var edited = recipes.Single(x => x.MaCauHinhMon == copied[0].MaCauHinhMon);
        var materials = JsonSerializer.Deserialize<IngredientSnapshot>((await api.Send("GET", "/api/admin/ingredients", headers: admin)).Content, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var lines = edited.ThanhPhan.Select(x => new RecipeLineInput(x.MaNguyenLieu, materials.NguyenLieu.Single(n => n.MaNguyenLieu == x.MaNguyenLieu).MaDonViCoSo,
            x.DinhMucCoSo + 1, x.LoaiDinhMuc, x.TyLeHaoHut)).ToList();
        (await api.Send("PUT", $"/api/admin/recipes/{edited.MaCongThuc}", Json(new RecipeInput(edited.MaCauHinhMon, null, edited.TenCongThuc, null, lines, edited.PhienBanDuLieu, Guid.NewGuid())), admin)).Expect(200, "Tinh chỉnh bản riêng");
        Assert.Equal(sourceLines, await api.Sql($"SELECT l.* FROM dbo.ChiTietCongThuc l JOIN dbo.CongThuc c ON l.MaCongThuc=c.MaCongThuc JOIN dbo.CauHinhMon v ON c.MaCauHinhMon=v.MaCauHinhMon WHERE v.MaMon={source.MaMon} ORDER BY l.MaCongThuc,l.MaNguyenLieu,l.LoaiDinhMuc FOR JSON PATH"));
        Assert.Contains((await api.Send("GET", "/api/menu")).Json.AsArray(), x => x!.Number("maMon") == id);
        Assert.Equal("1", await api.Sql($"SELECT COUNT(*) FROM dbo.SuKienDongBo WHERE MaThaoTac='{input.MaThaoTac}'"));
    }

    /// <summary>Mẫu sai hoặc lỗi ghi lịch sử phải hoàn tác cả món/cấu hình/công thức, không để món dở dang.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-02")]
    public async Task MonMauSai_AuditLoi_RollbackToanBo()
    {
        await using var api = await TestApi.Start("ADM02-TemplateRollback"); var admin = await api.AdminLogin();
        var source = (await Snapshot(api, admin)).Mon.First();
        var before = await api.Sql("SELECT (SELECT COUNT(*) FROM dbo.Mon),(SELECT COUNT(*) FROM dbo.CauHinhMon),(SELECT COUNT(*) FROM dbo.CongThuc),(SELECT COUNT(*) FROM dbo.ChiTietCongThuc)");
        var input = new MenuDishInput(1, "Món mẫu rollback", null, null, 0, false, true, null, null, Guid.NewGuid(), 99999999);
        (await api.Send("POST", "/api/admin/menu/dishes", Json(input), admin)).Expect(400, "Mẫu không tồn tại");
        await api.Sql("DENY INSERT ON dbo.NhatKyHeThong TO fourchang_app");
        try { (await api.Send("POST", "/api/admin/menu/dishes", Json(input with { MaMonMau = source.MaMon, MaThaoTac = Guid.NewGuid() }), admin)).Expect(409, "Lỗi audit"); }
        finally { await api.Sql("REVOKE INSERT ON dbo.NhatKyHeThong FROM fourchang_app"); }
        Assert.Equal(before, await api.Sql("SELECT (SELECT COUNT(*) FROM dbo.Mon),(SELECT COUNT(*) FROM dbo.CauHinhMon),(SELECT COUNT(*) FROM dbo.CongThuc),(SELECT COUNT(*) FROM dbo.ChiTietCongThuc)"));
    }

}
