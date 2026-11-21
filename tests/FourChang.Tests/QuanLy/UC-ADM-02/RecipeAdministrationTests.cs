using System.Text.Json;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Admin;

/// <summary>Tái sử dụng test BOM cũ cho UC-ADM-02 mới: lưu áp dụng ngay và bảo toàn lịch sử.</summary>
[Collection("AdminFeatures"), Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-02")]
public sealed class RecipeAdministrationTests(AdminFeatureFixture fixture)
{
    /// <summary>Bảng6 cấu hình lưu cùng lúc, trừ đá ở nóng, retry không nhân bản; lỗi cột cuối rollback cả món.</summary>
    [Fact]
    public async Task BangSize_NongLanh_LuuNguyenTu_Replay_LoiKhongLuuDoDang()
    {
        var dish = (await fixture.Send("POST", "/api/admin/menu/dishes", new MenuDishInput(1, "Món bảng size " + Guid.NewGuid().ToString("N")[..8], null, null, 0, false, false, null, null, Guid.NewGuid()))).Number("ma");
        var ids = new List<(long Id, string Temperature, int Size)>();
        var index = 0;
        foreach (var size in new[] { "S", "M", "L" })
        {
            index++;
            foreach (var temperature in new[] { "ICED", "HOT" })
            {
                var v = await fixture.Send("POST", "/api/admin/menu/variants", new MenuVariantInput(dish, size, temperature, 30000, false, ids.Count == 0, [], [], null, Guid.NewGuid()));
                ids.Add((v.Number("ma"), temperature, index));
            }
        }
        var ingredients = (await fixture.Ingredients()).NguyenLieu;
        var milk = ingredients.First(n => n.TenNguyenLieu == "Sữa tươi");
        var ice = ingredients.First(n => n.TenNguyenLieu == "Đá");
        var entries = ids.Select(v => new RecipeDishVariantInput(null, new(v.Id, null, "Bảng size " + v.Id, null,
            new[] { new RecipeLineInput(milk.MaNguyenLieu, milk.MaDonViCoSo, 80 * v.Size, "FIXED", 0) }
                .Concat(v.Temperature == "ICED" ? [new RecipeLineInput(ice.MaNguyenLieu, ice.MaDonViCoSo, 100 * v.Size, "ICE", 0)] : Array.Empty<RecipeLineInput>()).ToList(), null, Guid.Empty, 75))).ToList();
        var batch = new RecipeDishInput(dish, entries, Guid.NewGuid());
        var replies = await Task.WhenAll(Enumerable.Range(0,2).Select(_ => fixture.Api.Send("POST", "/api/admin/recipes/dish", JsonSerializer.SerializeToNode(batch), fixture.Admin)));
        foreach (var reply in replies) reply.Expect(200, "Lưu cùng bảng công thức");
        Assert.Equal(replies[0].Json.Number("ma"), replies[1].Json.Number("ma"));
        var saved = (await fixture.Recipes()).Where(r => ids.Any(v => v.Id == r.MaCauHinhMon)).OrderBy(r => r.MaCauHinhMon).ToList();
        Assert.Equal(6, saved.Count);
        foreach (var r in saved)
        {
            var v = ids.Single(v => v.Id == r.MaCauHinhMon);
            Assert.Equal(80 * v.Size, r.ThanhPhan.Single(l => l.MaNguyenLieu == milk.MaNguyenLieu).DinhMucCoSo);
            Assert.Equal(75, r.PhanTramDuongMacDinh);
            if (v.Temperature == "HOT") Assert.DoesNotContain(r.ThanhPhan, l => l.LoaiDinhMuc == "ICE");
        }
        var materialSnapshot = await fixture.Ingredients();
        var update = saved.Select(r => new RecipeDishVariantInput(r.MaCongThuc, Input(r, materialSnapshot, 1))).ToList();
        update[^1] = update[^1] with { CongThuc = update[^1].CongThuc with { ThanhPhan = [] } };
        await fixture.Send("POST", "/api/admin/recipes/dish", new RecipeDishInput(dish, update, Guid.NewGuid()), 400);
        var after = (await fixture.Recipes()).Where(r => ids.Any(v => v.Id == r.MaCauHinhMon)).OrderBy(r => r.MaCauHinhMon).ToList();
        Assert.Equal(saved.Select(r => r.MaCongThuc), after.Select(r => r.MaCongThuc));
        Assert.Equal(JsonSerializer.Serialize(saved.Select(r => r.ThanhPhan)), JsonSerializer.Serialize(after.Select(r => r.ThanhPhan)));
        await fixture.Send("POST", "/api/admin/recipes/dish", batch with { CauHinh = entries.Take(5).ToList(), MaThaoTac = Guid.NewGuid() }, 409);
        await fixture.Send("POST", "/api/admin/recipes/dish", batch with { MaThaoTac = Guid.NewGuid() }, 409);
        var hotIndex = ids.FindIndex(v => v.Temperature == "HOT");
        var hotEntries = saved.Select(r => new RecipeDishVariantInput(r.MaCongThuc, Input(r, materialSnapshot, 1))).ToList();
        var hotEntry = hotEntries.FindIndex(e => e.CongThuc.MaCauHinhMon == ids[hotIndex].Id);
        hotEntries[hotEntry] = hotEntries[hotEntry] with { CongThuc = hotEntries[hotEntry].CongThuc with { ThanhPhan = [new(ice.MaNguyenLieu, ice.MaDonViCoSo, 1, "ICE", 0)] } };
        await fixture.Send("POST", "/api/admin/recipes/dish", new RecipeDishInput(dish, hotEntries, Guid.NewGuid()), 400);
    }


    /// <summary>Sửa trực tiếp tự thay công thức; replay/concurrent chỉ tạo một bản mới, version cũ bị chặn.</summary>
    [Fact]
    public async Task LuuApDungNgay_Replay_DongThoi_BaoToanDinhMucCu()
    {
        var source = (await fixture.Recipes()).First(x => x.TrangThai == "ACTIVE" && x.MaCauHinhMon is not null);
        var ingredients = await fixture.Ingredients();
        var oldLines = await fixture.Api.Sql($"SELECT * FROM dbo.ChiTietCongThuc WHERE MaCongThuc={source.MaCongThuc} ORDER BY MaNguyenLieu,LoaiDinhMuc FOR JSON PATH");
        var input = Input(source, ingredients, 2);
        var path = $"/api/admin/recipes/{source.MaCongThuc}";
        var requests = await Task.WhenAll(fixture.Api.Send("PUT", path, JsonSerializer.SerializeToNode(input), fixture.Admin), fixture.Api.Send("PUT", path, JsonSerializer.SerializeToNode(input), fixture.Admin));
        foreach (var response in requests) response.Expect(200, "Lưu công thức cùng mã");
        var id = requests[0].Json.Number("ma"); Assert.Equal(id, requests[1].Json.Number("ma"));
        Assert.NotEqual(source.MaCongThuc, id);
        var current = (await fixture.Recipes()).Single(x => x.MaCongThuc == id);
        Assert.Equal("ACTIVE", current.TrangThai); Assert.Single(current.ThanhPhan);
        Assert.Equal(2m, current.ThanhPhan[0].DinhMucCoSo);
        Assert.Equal("RETIRED", await fixture.Api.Sql($"SELECT TrangThai FROM dbo.CongThuc WHERE MaCongThuc={source.MaCongThuc}"));
        Assert.Equal(oldLines, await fixture.Api.Sql($"SELECT * FROM dbo.ChiTietCongThuc WHERE MaCongThuc={source.MaCongThuc} ORDER BY MaNguyenLieu,LoaiDinhMuc FOR JSON PATH"));
        Assert.DoesNotContain(await fixture.Recipes(), x => x.MaCongThuc == source.MaCongThuc);
        await fixture.Send("PUT", path, input with { MaThaoTac = Guid.NewGuid() }, 409);
        await fixture.Send("PUT", path, input with { TenCongThuc = "Nội dung khác" }, 409);
        Assert.Equal("1", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.SuKienDongBo WHERE MaThaoTac='{input.MaThaoTac}'"));
        await fixture.Send("GET", $"/api/admin/recipes/operations/{input.MaThaoTac}");
    }

    /// <summary>Không lưu công thức rỗng, sai lượng/đơn vị, trùng dòng hoặc nguyên liệu đã ngừng.</summary>
    [Fact]
    public async Task DinhMucSai_ThieuThanhPhan_NguyenLieuNgung_KhongThayBanCu()
    {
        var source = (await fixture.Recipes()).First(x => x.TrangThai == "ACTIVE");
        var ingredients = await fixture.Ingredients(); var input = Input(source, ingredients, 1);
        foreach (var lines in new IReadOnlyList<RecipeLineInput>[] {
            [], [input.ThanhPhan[0] with { DinhMuc = 0 }], [input.ThanhPhan[0] with { DinhMuc = -1 }],
            [input.ThanhPhan[0] with { DinhMuc = 1.00001m }], [input.ThanhPhan[0] with { MaDonVi = 999999 }],
            [input.ThanhPhan[0] with { TyLeHaoHut = 1 }], [input.ThanhPhan[0], input.ThanhPhan[0]] })
            await fixture.Send("PUT", $"/api/admin/recipes/{source.MaCongThuc}", input with { ThanhPhan = lines, MaThaoTac = Guid.NewGuid() }, 400);
        var ingredient = input.ThanhPhan[0].MaNguyenLieu;
        await fixture.Api.Sql($"UPDATE dbo.NguyenLieu SET DangSuDung=0 WHERE MaNguyenLieu={ingredient}");
        try { await fixture.Send("PUT", $"/api/admin/recipes/{source.MaCongThuc}", input, 400); }
        finally { await fixture.Api.Sql($"UPDATE dbo.NguyenLieu SET DangSuDung=1 WHERE MaNguyenLieu={ingredient}"); }
        Assert.Equal("ACTIVE", await fixture.Api.Sql($"SELECT TrangThai FROM dbo.CongThuc WHERE MaCongThuc={source.MaCongThuc}"));
        Assert.Equal(Convert.ToBase64String(source.PhienBanDuLieu), Convert.ToBase64String((await fixture.Recipes()).Single(x => x.MaCongThuc == source.MaCongThuc).PhienBanDuLieu));
    }

    /// <summary>Đơn checkout trước sửa giữ snapshot cũ; đơn checkout sau sửa nhận công thức mới ngay.</summary>
    [Fact]
    public async Task DonCuGiuSnapshot_DonMoiNhanCongThucMoi()
    {
        // BR-03 mới không trừ tồn an toàn; đơn mới vẫn đặt được khi tồn thực tế đủ.
        await fixture.Api.Sql("UPDATE dbo.NguyenLieu SET NguongCanhBao=1000000");
        var catalog = (await fixture.Api.Send("GET", "/api/catalog")).Json;
        var available = catalog["mon"]!.AsArray().SelectMany(x => x!["cauHinh"]!.AsArray()).Where(x => x!.Flag("conHang")).Select(x => x!.Number("maCauHinhMon")).ToHashSet();
        var source = (await fixture.Recipes()).First(x => x.TrangThai == "ACTIVE" && x.MaCauHinhMon is { } id && available.Contains(id));
        var menu = await MenuAdministrationTests.Snapshot(fixture.Api, fixture.Admin);
        var variant = menu.CauHinh.Single(x => x.MaCauHinhMon == source.MaCauHinhMon);
        var employee = await fixture.Api.Employee(fixture.Admin); var device = await fixture.Api.NewPosClient(fixture.Admin);
        var staff = TestApi.Bearer((await fixture.Api.Send("POST", "/api/pos/shifts", fixture.Api.OpenBody(employee, device), device.Headers)).Expect(200, "Mở ca checkout").Json);
        var oldOrder = await Checkout(variant, staff);
        var before = await SnapshotOrder(oldOrder);
        var input = Input(source, await fixture.Ingredients(), 2);
        var result = await fixture.Send("PUT", $"/api/admin/recipes/{source.MaCongThuc}", input);
        var newOrder = await Checkout(variant, staff);
        Assert.Equal(before, await SnapshotOrder(oldOrder));
        Assert.Equal(result.Number("ma").ToString(), await fixture.Api.Sql($"SELECT MaCongThuc FROM dbo.ChiTietDonHang WHERE MaDonHang={newOrder}"));
        Assert.Equal("2.0000", await fixture.Api.Sql($"SELECT n.DinhMucMoiLy FROM dbo.NguyenLieuChiTietDonHang n JOIN dbo.ChiTietDonHang d ON n.MaChiTietDonHang=d.MaChiTietDonHang WHERE d.MaDonHang={newOrder} AND n.MaNguyenLieu={input.ThanhPhan[0].MaNguyenLieu}"));
    }

    /// <summary>Lỗi audit phải rollback bản mới, trạng thái bản cũ và sự kiện; chức năng đã bỏ trả 410.</summary>
    [Fact]
    public async Task AuditLoi_RollbackVaChanCacChucNangDaBo()
    {
        var source = (await fixture.Recipes()).First(x => x.TrangThai == "ACTIVE");
        var input = Input(source, await fixture.Ingredients(), 1);
        var before = await fixture.Api.Sql("SELECT COUNT(*) FROM dbo.CongThuc");
        await fixture.Api.Sql("DENY INSERT ON dbo.NhatKyHeThong TO fourchang_app");
        try { await fixture.Send("PUT", $"/api/admin/recipes/{source.MaCongThuc}", input, 409); }
        finally { await fixture.Api.Sql("REVOKE INSERT ON dbo.NhatKyHeThong FROM fourchang_app"); }
        Assert.Equal(before, await fixture.Api.Sql("SELECT COUNT(*) FROM dbo.CongThuc"));
        Assert.Equal("ACTIVE", await fixture.Api.Sql($"SELECT TrangThai FROM dbo.CongThuc WHERE MaCongThuc={source.MaCongThuc}"));
        Assert.Equal("0", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.SuKienDongBo WHERE MaThaoTac='{input.MaThaoTac}'"));
        foreach (var action in new[] { "clone", "publish", "stop", "delete" })
            await fixture.Send("POST", $"/api/admin/recipes/{source.MaCongThuc}/{action}", new { }, 410);
        await fixture.Send("POST", "/api/admin/recipes/simulate", new { }, 410);
    }

    /// <summary>Tạo công thức đầu tiên cho topping ngừng, sau đó kích hoạt dùng chung mọi cấu hình.</summary>
    [Fact]
    public async Task TaoCongThucTopping_KichHoatDungChung()
    {
        var created = await fixture.Send("POST", "/api/admin/menu/toppings", new MenuToppingInput("Topping " + Guid.NewGuid().ToString("N")[..8], null, 10000, 2, null, false, null, Guid.NewGuid()));
        var ingredient = (await fixture.Ingredients()).NguyenLieu.First(x => x.DangSuDung);
        var input = new RecipeInput(null, created.Number("ma"), "Công thức topping", null,
            [new(ingredient.MaNguyenLieu, ingredient.MaDonViCoSo, 3, "FIXED", 0)], null, Guid.NewGuid());
        var saved = await fixture.Send("POST", "/api/admin/recipes", input);
        Assert.Equal("ACTIVE", (await fixture.Recipes()).Single(x => x.MaCongThuc == saved.Number("ma")).TrangThai);
        await fixture.Send("POST", "/api/admin/recipes", input with { MaThaoTac = Guid.NewGuid() }, 409);
        var top = (await MenuAdministrationTests.Snapshot(fixture.Api, fixture.Admin)).MonThem.Single(x => x.MaMonThem == input.MaMonThem);
        await fixture.Send("PUT", $"/api/admin/menu/toppings/{top.MaMonThem}", new MenuToppingInput(top.TenMonThem, top.MoTa, top.GiaMoiPhan, top.GioiHanMacDinhMoiLy, top.DuongDanAnh, true, top.PhienBanDuLieu, Guid.NewGuid()));
        var catalog = (await fixture.Api.Send("GET", "/api/catalog")).Json;
        var variants = catalog["mon"]!.AsArray().SelectMany(x => x!["cauHinh"]!.AsArray()).ToList();
        Assert.NotEmpty(variants);
        Assert.All(variants, v => Assert.Contains(v!["monThem"]!.AsArray(), t => t!.Number("maMonThem") == top.MaMonThem));
    }

    /// <summary>Luồng đầy đủ món mới: lưu ẩn, cấu hình riêng size/nhiệt độ, công thức rồi bật bán.</summary>
    [Fact]
    public async Task MonMoi_CauHinh_CongThuc_HienThiBan_DungGia()
    {
        var name = "Món mới " + Guid.NewGuid().ToString("N")[..8];
        var dishInput = new MenuDishInput(1, name, "Mô tả món", "sprite:0", 20, false, true, null, null, Guid.NewGuid());
        var dish = await fixture.Send("POST", "/api/admin/menu/dishes", dishInput);
        var dishId = dish.Number("ma");
        var variantInput = new MenuVariantInput(dishId, "M", "HOT", 42000, false, true, [], [], null, Guid.NewGuid());
        var variant = await fixture.Send("POST", "/api/admin/menu/variants", variantInput);
        var variantId = variant.Number("ma");
        await fixture.Send("POST", "/api/admin/menu/variants", variantInput with { MaThaoTac = Guid.NewGuid() }, 400);
        // Chưa có công thức thì bật bán/hiển thị bị từ chối, giao dịch không lưu dở.
        var hidden = (await MenuAdministrationTests.Snapshot(fixture.Api, fixture.Admin)).Mon.Single(x => x.MaMon == dishId);
        await fixture.Send("PUT", $"/api/admin/menu/dishes/{dishId}", dishInput with { DaCongBo = true, PhienBanDuLieu = hidden.PhienBanDuLieu, MaThaoTac = Guid.NewGuid() }, 400);
        var ingredient = (await fixture.Ingredients()).NguyenLieu.First(x => x.DangSuDung);
        await fixture.Send("POST", "/api/admin/recipes", new RecipeInput(variantId, null, name + " M nóng", null,
            [new(ingredient.MaNguyenLieu, ingredient.MaDonViCoSo, 5, "FIXED", 0)], null, Guid.NewGuid()));
        var savedVariant = (await MenuAdministrationTests.Snapshot(fixture.Api, fixture.Admin)).CauHinh.Single(x => x.MaCauHinhMon == variantId);
        await fixture.Send("PUT", $"/api/admin/menu/variants/{variantId}", MenuAdministrationTests.Variant(savedVariant) with { DangBan = true });
        hidden = (await MenuAdministrationTests.Snapshot(fixture.Api, fixture.Admin)).Mon.Single(x => x.MaMon == dishId);
        await fixture.Send("PUT", $"/api/admin/menu/dishes/{dishId}", dishInput with { DaCongBo = true, PhienBanDuLieu = hidden.PhienBanDuLieu, MaThaoTac = Guid.NewGuid() });
        var card = (await fixture.Api.Send("GET", "/api/menu")).Json.AsArray().Single(x => x!.Number("maMon") == dishId)!;
        Assert.Equal(42000, card.Number("price")); Assert.Equal(variantId, card.Number("id")); Assert.True(card.Flag("available"));
        var publicVariant = (await fixture.Api.Send("GET", "/api/catalog")).Json["mon"]!.AsArray().Single(x => x!.Number("maMon") == dishId)!["cauHinh"]!.AsArray().Single()!;
        Assert.Equal("HOT", publicVariant.Text("nhietDo"));
        Assert.All(publicVariant["tuyChon"]!.AsArray(), x => Assert.Equal(0, x!.Number("phanTramDa")));
    }

    /// <summary>Mặc định đường theo công thức; giữ/trừ đúng nhiều nguyên liệu, START ghi một lần và FINISH không trừ thêm.</summary>
    [Fact]
    public async Task NhieuNguyenLieu_DuongMacDinh_TruDungLuong_KhongHaoHut()
    {
        var menu = await MenuAdministrationTests.Snapshot(fixture.Api, fixture.Admin);
        var variant = menu.CauHinh.First(v => v.LaMacDinh);
        var recipe = (await fixture.Recipes()).Single(r => r.MaCauHinhMon == variant.MaCauHinhMon);
        var ingredients = (await fixture.Ingredients()).NguyenLieu;
        var coffee = ingredients.First(n => n.TenNguyenLieu == "Hạt cà phê");
        var milk = ingredients.First(n => n.TenNguyenLieu == "Sữa tươi");
        var sugar = ingredients.First(n => n.TenNguyenLieu == "Đường");
        var lines = new RecipeLineInput[] { new(coffee.MaNguyenLieu, coffee.MaDonViCoSo, 50, "FIXED", 0),
            new(milk.MaNguyenLieu, milk.MaDonViCoSo, 100, "FIXED", 0), new(sugar.MaNguyenLieu, sugar.MaDonViCoSo, 20, "SUGAR", 0) };
        var input = new RecipeInput(variant.MaCauHinhMon, null, "Công thức nhiều nguyên liệu", null, lines, recipe.PhienBanDuLieu, Guid.NewGuid(), 75);
        await fixture.Send("PUT", $"/api/admin/recipes/{recipe.MaCongThuc}", input with { PhanTramDuongMacDinh = 30 }, 400);
        await fixture.Send("PUT", $"/api/admin/recipes/{recipe.MaCongThuc}", input with { ThanhPhan = [lines[0] with { TyLeHaoHut = .05m }], MaThaoTac = Guid.NewGuid() }, 400);
        await fixture.Send("PUT", $"/api/admin/recipes/{recipe.MaCongThuc}", input);
        var variants = (await fixture.Api.Send("GET", $"/api/menu/{variant.MaMon}/variants")).Json.AsArray();
        Assert.Equal(75, variants.Single(v => v!.Number("maCauHinhMon") == variant.MaCauHinhMon)!.Number("phanTramDuongMacDinh"));
        var before = new Dictionary<long,decimal>();
        foreach (var n in new[] { coffee, milk, sugar })
            before[n.MaNguyenLieu] = decimal.Parse(await fixture.Api.Sql($"SELECT TonThucTe FROM dbo.TonKho WHERE MaNguyenLieu={n.MaNguyenLieu}"), System.Globalization.CultureInfo.InvariantCulture);
        var employee = await fixture.Api.Employee(fixture.Admin); var device = await fixture.Api.NewPosClient(fixture.Admin);
        var staff = TestApi.Bearer((await fixture.Api.Send("POST", "/api/pos/shifts", fixture.Api.OpenBody(employee, device), device.Headers)).Expect(200, "Mở ca kiểm kho").Json);
        var checkout = Body(("MaThaoTac", NewId()), ("HinhThuc", "TAKEAWAY"), ("PhuongThuc", "CASH"), ("TongTienDaChapNhan", variant.GiaBan * 2), ("TienKhachDua", variant.GiaBan * 2),
            ("Items", new[] { Body(("MaCauHinhMon", variant.MaCauHinhMon), ("SoLuong", 2), ("PhanTramDuong", 75), ("PhanTramDa", variant.NhietDo == "HOT" ? 0 : 50), ("Toppings", Array.Empty<object>())) }));
        var order = (await fixture.Api.Send("POST", "/api/pos/checkout", checkout, staff)).Expect(200, "Giữ đúng nguyên liệu cho hai ly").Json.Number("maDonHang");
        var orderLine = long.Parse(await fixture.Api.Sql($"SELECT MaChiTietDonHang FROM dbo.ChiTietDonHang WHERE MaDonHang={order}"));
        var expected = new Dictionary<long, decimal> { [coffee.MaNguyenLieu] = 100, [milk.MaNguyenLieu] = 200, [sugar.MaNguyenLieu] = 30 };
        foreach (var (id, amount) in expected)
        {
            Assert.Equal(amount, decimal.Parse(await fixture.Api.Sql($"SELECT SoLuongGiuCho FROM dbo.NguyenLieuChiTietDonHang WHERE MaChiTietDonHang={orderLine} AND MaNguyenLieu={id}"), System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(before[id], decimal.Parse(await fixture.Api.Sql($"SELECT TonThucTe FROM dbo.TonKho WHERE MaNguyenLieu={id}"), System.Globalization.CultureInfo.InvariantCulture));
        }
        foreach (var action in new[] { "ACCEPT", "START", "FINISH" })
        {
            var detail = System.Text.Json.JsonSerializer.Deserialize<AdminOrderDetail>((await fixture.Api.Send("GET", $"/api/pos/orders/{order}", headers: staff)).Content, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            if (action == "ACCEPT" && detail.Don.TrangThai == "ACCEPTED") continue;
            var request = new OrderActionInput(action, "", detail.Don.PhienBanDuLieu, Guid.NewGuid(), orderLine, 2);
            (await fixture.Api.Send("POST", $"/api/pos/orders/{order}/actions", JsonSerializer.SerializeToNode(request), staff)).Expect(200, action);
            (await fixture.Api.Send("POST", $"/api/pos/orders/{order}/actions", JsonSerializer.SerializeToNode(request), staff)).Expect(200, "Gửi lại " + action);
            if (action == "ACCEPT") continue;
            foreach (var (id, amount) in expected)
                Assert.Equal(before[id] - amount, decimal.Parse(await fixture.Api.Sql($"SELECT TonThucTe FROM dbo.TonKho WHERE MaNguyenLieu={id}"), System.Globalization.CultureInfo.InvariantCulture));
        }
        Assert.Equal("3", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.BienDongKho WHERE MaChiTietDonHang={orderLine} AND LoaiBienDong='ORDER_CONSUMPTION'"));
        var current = (await fixture.Recipes()).Single(r => r.MaCauHinhMon == variant.MaCauHinhMon);
        await fixture.Send("PUT", $"/api/admin/recipes/{current.MaCongThuc}", input with { PhanTramDuongMacDinh = 0, PhienBanDuLieu = current.PhienBanDuLieu, MaThaoTac = Guid.NewGuid() });
        Assert.Equal(0, (await fixture.Recipes()).Single(r => r.MaCauHinhMon == variant.MaCauHinhMon).PhanTramDuongMacDinh);
    }

    /// <summary>Dựng yêu cầu sửa đơn giản theo đơn vị nguyên liệu đang dùng.</summary>
    private static RecipeInput Input(RecipeView source, IngredientSnapshot ingredients, decimal amount)
    {
        var n = ingredients.NguyenLieu.First(x => x.DangSuDung);
        return new(source.MaCauHinhMon, source.MaMonThem, source.TenCongThuc, source.GhiChu,
            [new(n.MaNguyenLieu, n.MaDonViCoSo, amount, "FIXED", 0)], source.PhienBanDuLieu, Guid.NewGuid(), source.PhanTramDuongMacDinh);
    }
    /// <summary>Checkout một ly thật để kiểm snapshot, hóa đơn và giữ chỗ cùng một nguồn giá.</summary>
    private async Task<long> Checkout(MenuVariant v, Dictionary<string,string> staff)
    {
        var input = Body(("MaThaoTac", NewId()), ("HinhThuc", "TAKEAWAY"), ("PhuongThuc", "CASH"), ("TongTienDaChapNhan", v.GiaBan), ("TienKhachDua", v.GiaBan),
            ("Items", new[] { Body(("MaCauHinhMon", v.MaCauHinhMon), ("SoLuong", 1), ("PhanTramDuong", 50), ("PhanTramDa", v.NhietDo == "HOT" ? 0 : 50), ("Toppings", Array.Empty<object>())) }));
        return (await fixture.Api.Send("POST", "/api/pos/checkout", input, staff)).Expect(200, "Checkout snapshot").Json.Number("maDonHang");
    }
    /// <summary>Đọc snapshot dòng món/nguyên liệu đã chốt, không lấy dữ liệu menu hiện tại.</summary>
    private Task<string> SnapshotOrder(long id) => fixture.Api.Sql($"SELECT d.*,JSON_QUERY((SELECT n.* FROM dbo.NguyenLieuChiTietDonHang n WHERE n.MaChiTietDonHang=d.MaChiTietDonHang ORDER BY n.MaNguyenLieu FOR JSON PATH)) AS NguyenLieu FROM dbo.ChiTietDonHang d WHERE d.MaDonHang={id} ORDER BY d.MaChiTietDonHang FOR JSON PATH");
}
