using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Tests.Shared;

namespace FourChang.Tests.Admin;

/// <summary>UC-ADM-03 tinh gọn: một ngưỡng, đơn vị cố định, xác nhận ngừng, đồng thời và rollback trên SQL riêng.</summary>
[Collection("AdminFeatures"), Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-03")]
public sealed class IngredientAdministrationTests(AdminFeatureFixture fixture)
{
    /// <summary>Thêm/sửa với một ngưỡng; replay không tạo trùng, bản cũ bị chặn, ngừng/kích hoạt không sinh kho.</summary>
    [Fact]
    public async Task ThemSua_MotNguong_Version_ChongTrung_VaNgungKichHoat()
    {
        var snap = await fixture.Ingredients(); var unit = snap.DonVi.First(x => x.DangSuDung);
        var input = new IngredientInput("Nguyên liệu " + Guid.NewGuid().ToString("N")[..8], unit.MaDonVi, 1, 5, "xUnit", true, [], null, Guid.NewGuid());
        var receipt = await fixture.Send("POST", "/api/admin/ingredients", input); var id = receipt.Number("ma");
        Assert.Equal(id, (await fixture.Send("POST", "/api/admin/ingredients", input)).Number("ma"));
        await fixture.Send("POST", "/api/admin/ingredients", input with { TenNguyenLieu = "Nội dung khác" }, 409);
        var item = (await fixture.Ingredients()).NguyenLieu.Single(x => x.MaNguyenLieu == id);
        Assert.Empty(item.QuyDoi); Assert.Equal(unit.MaDonVi, item.MaDonViCoSo); Assert.Equal(0, item.TonAnToan);
        var edit = input with { NguongCanhBao = 6, PhienBanDuLieu = item.PhienBanDuLieu, MaThaoTac = Guid.NewGuid() };
        await fixture.Send("PUT", $"/api/admin/ingredients/{id}", edit);
        await fixture.Send("PUT", $"/api/admin/ingredients/{id}", edit with { MaThaoTac = Guid.NewGuid() }, 409);
        item = (await fixture.Ingredients()).NguyenLieu.Single(x => x.MaNguyenLieu == id);
        Assert.Equal("0", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.TonKho WHERE MaNguyenLieu={id}"));
        await fixture.Send("POST", $"/api/admin/ingredients/{id}/delete", new AdminDeleteInput(item.PhienBanDuLieu, Guid.NewGuid()), 409);
        var impact = await fixture.Send("GET", $"/api/admin/ingredients/{id}/impact");
        var stop = edit with { DangSuDung = false, PhienBanDuLieu = item.PhienBanDuLieu, MaThaoTac = Guid.NewGuid(),
            PhienBanAnhHuong = impact["phienBanAnhHuong"]!.GetValue<string>() };
        await fixture.Send("PUT", $"/api/admin/ingredients/{id}", stop);
        item = (await fixture.Ingredients()).NguyenLieu.Single(x => x.MaNguyenLieu == id); Assert.False(item.DangSuDung);
        await fixture.Send("PUT", $"/api/admin/ingredients/{id}", stop with { DangSuDung = true, PhienBanDuLieu = item.PhienBanDuLieu, MaThaoTac = Guid.NewGuid(), PhienBanAnhHuong = null });
        Assert.True((await fixture.Ingredients()).NguyenLieu.Single(x => x.MaNguyenLieu == id).DangSuDung);
        Assert.Equal("0", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.TonKho WHERE MaNguyenLieu={id}"));
    }

    /// <summary>Thiếu xác nhận ảnh hưởng thì không ngừng; không xóa và không đổi đơn vị sau khi có dữ liệu tham chiếu.</summary>
    [Fact]
    public async Task DuLieuThamChieu_KhongXoa_KhongDoiDonVi_NgungCanXacNhan()
    {
        var snap = await fixture.Ingredients(); var item = snap.NguyenLieu.First(x => x.SoThamChieu > 0);
        var input = new IngredientInput(item.TenNguyenLieu, item.MaDonViCoSo, item.TonAnToan, item.NguongCanhBao, item.MoTa, false, item.QuyDoi, item.PhienBanDuLieu, Guid.NewGuid());
        await fixture.Send("PUT", $"/api/admin/ingredients/{item.MaNguyenLieu}", input, 409);
        await fixture.Send("POST", $"/api/admin/ingredients/{item.MaNguyenLieu}/delete", new AdminDeleteInput(item.PhienBanDuLieu, Guid.NewGuid()), 409);
        await fixture.Send("PUT", $"/api/admin/ingredients/{item.MaNguyenLieu}", input with { DangSuDung = true, MaDonViCoSo = snap.DonVi.First(x => x.MaDonVi != item.MaDonViCoSo).MaDonVi, MaThaoTac = Guid.NewGuid() }, 409);
    }

    /// <summary>Ngưỡng sai hoặc audit lỗi không tạo hồ sơ hay sự kiện dở dang.</summary>
    [Fact]
    public async Task Validation_VaLoiAudit_RollbackHoSoVaSuKien()
    {
        var snap = await fixture.Ingredients(); var unit = snap.DonVi.First();
        var input = new IngredientInput("Rollback " + Guid.NewGuid().ToString("N")[..8], unit.MaDonVi, 0, -1, null, true, [], null, Guid.NewGuid());
        await fixture.Send("POST", "/api/admin/ingredients", input, 400);
        input = input with { NguongCanhBao = 10, QuyDoi = [new(unit.MaDonVi, 0)] };
        await fixture.Send("POST", "/api/admin/ingredients", input, 400);
        input = input with { QuyDoi = [] };
        await fixture.Api.Sql("DENY INSERT ON OBJECT::dbo.NhatKyHeThong TO fourchang_app");
        try
        {
            var result = await fixture.Api.Send("POST", "/api/admin/ingredients", System.Text.Json.JsonSerializer.SerializeToNode(input), fixture.Admin);
            Assert.True(result.Status >= 400);
        }
        finally { await fixture.Api.Sql("REVOKE INSERT ON OBJECT::dbo.NhatKyHeThong FROM fourchang_app"); }
        Assert.Equal("0", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.NguyenLieu WHERE TenNguyenLieu=N'{input.TenNguyenLieu}'"));
        Assert.Equal("0", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.SuKienDongBo WHERE MaThaoTac='{input.MaThaoTac}'"));
    }

    /// <summary>Gam và Mililit là cố định; client cũ cũng không được tạo/sửa/xóa danh mục đơn vị.</summary>
    [Fact]
    public async Task DonViMacDinh_ChiGamVaMililit_KhongQuanLyDonVi()
    {
        var before = await fixture.Ingredients();
        Assert.Equal(new[] { "g", "ml" }, before.DonVi.Where(x => IngredientUnits.IsSupported(x.KyHieu)).Select(x => x.KyHieu).Order().ToArray());
        var unit = before.DonVi.First();
        var input = new UnitInput("kg", "Kilôgam", true, null, Guid.NewGuid());
        await fixture.Send("POST", "/api/admin/ingredients/units", input, 409);
        await fixture.Send("PUT", $"/api/admin/ingredients/units/{unit.MaDonVi}", input with { PhienBanDuLieu = unit.PhienBanDuLieu, MaThaoTac = Guid.NewGuid() }, 409);
        await fixture.Send("POST", $"/api/admin/ingredients/units/{unit.MaDonVi}/delete", new AdminDeleteInput(unit.PhienBanDuLieu, Guid.NewGuid()), 409);
        Assert.Equal(before.DonVi.Select(x => (x.MaDonVi, x.KyHieu, x.TenDonVi, x.DangSuDung, Convert.ToBase64String(x.PhienBanDuLieu))).ToArray(),
            (await fixture.Ingredients()).DonVi.Select(x => (x.MaDonVi, x.KyHieu, x.TenDonVi, x.DangSuDung, Convert.ToBase64String(x.PhienBanDuLieu))).ToArray());
    }

    /// <summary>Chặn đơn vị ngoài danh mục cố định và hệ số/cặp quy đổi tự khai báo trước khi phát sinh dữ liệu.</summary>
    [Fact]
    public async Task KhongNhanDonViKhac_HoacHeSoQuyDoi()
    {
        var snapshot = await fixture.Ingredients(); var gram = snapshot.DonVi.Single(x => x.KyHieu == "g");
        var ml = snapshot.DonVi.Single(x => x.KyHieu == "ml");
        var input = new IngredientInput("Chặn quy đổi " + Guid.NewGuid().ToString("N")[..8], gram.MaDonVi, 0, 1, null, true, [new(gram.MaDonVi, 2)], null, Guid.NewGuid());
        await fixture.Send("POST", "/api/admin/ingredients", input, 400);
        await fixture.Send("POST", "/api/admin/ingredients", input with { QuyDoi = [new(ml.MaDonVi, 1)], MaThaoTac = Guid.NewGuid() }, 400);
        await fixture.Send("POST", "/api/admin/ingredients", input with { MaDonViCoSo = 999, QuyDoi = [], MaThaoTac = Guid.NewGuid() }, 400);
        Assert.Equal("0", await fixture.Api.Sql("SELECT COUNT(*) FROM dbo.NguyenLieu WHERE DonViCoSo NOT IN ('g','ml')"));
    }

    /// <summary>Ngưỡng duy nhất có thể bằng 0; dữ liệu tương thích không được cản hạ ngưỡng và không làm thay số dư.</summary>
    [Fact]
    public async Task NguongDocLapTonAnToan_KhongAm_KhongLamTronNgam()
    {
        var name = "Một ngưỡng " + Guid.NewGuid().ToString("N")[..8];
        var create = new IngredientInput(name, 1, 100, 12.5m, "Ghi chú", true, [], null, Guid.NewGuid());
        var id = (await fixture.Send("POST", "/api/admin/ingredients", create)).Number("ma");
        var item = (await fixture.Ingredients()).NguyenLieu.Single(n => n.MaNguyenLieu == id);
        Assert.Equal(0, item.TonAnToan); Assert.Equal(12.5m, item.NguongCanhBao);
        await fixture.Api.Sql($"UPDATE dbo.NguyenLieu SET TonAnToan=10 WHERE MaNguyenLieu={id}");
        item = (await fixture.Ingredients()).NguyenLieu.Single(n => n.MaNguyenLieu == id);
        var edit = create with { NguongCanhBao = 0, PhienBanDuLieu = item.PhienBanDuLieu, MaThaoTac = Guid.NewGuid() };
        await fixture.Send("PUT", $"/api/admin/ingredients/{id}", edit);
        item = (await fixture.Ingredients()).NguyenLieu.Single(n => n.MaNguyenLieu == id);
        Assert.Equal(0, item.NguongCanhBao); Assert.Equal(0, item.TonAnToan);
        foreach (var bad in new[] { -0.01m, 0.00001m })
            await fixture.Send("PUT", $"/api/admin/ingredients/{id}", edit with { NguongCanhBao = bad, PhienBanDuLieu = item.PhienBanDuLieu, MaThaoTac = Guid.NewGuid() }, 400);
        await fixture.Send("POST", "/api/admin/ingredients", create with { MaThaoTac = Guid.NewGuid() }, 409);
        Assert.Equal("0", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.TonKho WHERE MaNguyenLieu={id}"));
    }

    /// <summary>Xác nhận có phiên bản; ngừng/khôi phục chỉ đổi khả dụng, không thay BOM, số dư hoặc ghi trùng khi retry.</summary>
    [Fact]
    public async Task NgungSauXacNhan_ChanXacNhanCu_TamHetMenu_GiuKhoVaCongThuc()
    {
        var snapshot = await fixture.Ingredients();
        var milk = snapshot.NguyenLieu.Single(n => n.TenNguyenLieu == "Sữa tươi");
        var beforeStock = await fixture.Api.Sql($"SELECT * FROM dbo.TonKho WHERE MaNguyenLieu={milk.MaNguyenLieu} FOR JSON PATH");
        var impact = await fixture.Send("GET", $"/api/admin/ingredients/{milk.MaNguyenLieu}/impact");
        Assert.NotEmpty(impact["monPhuThuoc"]!.AsArray());
        // Chỉ thay metadata trong DB fixture để chứng minh dấu xác nhận cũ không còn hợp lệ.
        await fixture.Api.Sql($"UPDATE dbo.CongThuc SET GhiChu=N'Kiểm xác nhận cũ' WHERE MaCongThuc=(SELECT TOP(1) c.MaCongThuc FROM dbo.CongThuc c JOIN dbo.ChiTietCongThuc l ON l.MaCongThuc=c.MaCongThuc WHERE l.MaNguyenLieu={milk.MaNguyenLieu} AND c.TrangThai='ACTIVE' ORDER BY c.MaCongThuc)");
        var input = new IngredientInput(milk.TenNguyenLieu, milk.MaDonViCoSo, 0, milk.NguongCanhBao, milk.MoTa, false, [], milk.PhienBanDuLieu, Guid.NewGuid(), impact["phienBanAnhHuong"]!.GetValue<string>());
        await fixture.Send("PUT", $"/api/admin/ingredients/{milk.MaNguyenLieu}", input, 409);
        Assert.True((await fixture.Ingredients()).NguyenLieu.Single(n => n.MaNguyenLieu == milk.MaNguyenLieu).DangSuDung);
        impact = await fixture.Send("GET", $"/api/admin/ingredients/{milk.MaNguyenLieu}/impact");
        input = input with { MaThaoTac = Guid.NewGuid(), PhienBanAnhHuong = impact["phienBanAnhHuong"]!.GetValue<string>() };
        var beforeRecipes = await fixture.Api.Sql($"SELECT c.* FROM dbo.CongThuc c WHERE EXISTS(SELECT 1 FROM dbo.ChiTietCongThuc l WHERE l.MaCongThuc=c.MaCongThuc AND l.MaNguyenLieu={milk.MaNguyenLieu}) ORDER BY c.MaCongThuc FOR JSON PATH");
        var beforeCatalog = await fixture.Send("GET", "/api/catalog");
        try
        {
            await fixture.Send("PUT", $"/api/admin/ingredients/{milk.MaNguyenLieu}", input);
            var stopped = (await fixture.Ingredients()).NguyenLieu.Single(n => n.MaNguyenLieu == milk.MaNguyenLieu);
            await fixture.Send("PUT", $"/api/admin/ingredients/{milk.MaNguyenLieu}", input);
            Assert.Equal(stopped.PhienBanDuLieu, (await fixture.Ingredients()).NguyenLieu.Single(n => n.MaNguyenLieu == milk.MaNguyenLieu).PhienBanDuLieu);
            var afterCatalog = await fixture.Send("GET", "/api/catalog");
            Assert.NotEqual(beforeCatalog.ToJsonString(), afterCatalog.ToJsonString());
            var dishes = afterCatalog["mon"]!.AsArray();
            Assert.All(dishes.Where(d => d!["tenMon"]!.GetValue<string>() is "Bạc xỉu" or "Latte"), d =>
                Assert.All(d!["cauHinh"]!.AsArray(), v => Assert.False(v!["conHang"]!.GetValue<bool>())));
            Assert.Equal(beforeStock, await fixture.Api.Sql($"SELECT * FROM dbo.TonKho WHERE MaNguyenLieu={milk.MaNguyenLieu} FOR JSON PATH"));
            Assert.Equal(beforeRecipes, await fixture.Api.Sql($"SELECT c.* FROM dbo.CongThuc c WHERE EXISTS(SELECT 1 FROM dbo.ChiTietCongThuc l WHERE l.MaCongThuc=c.MaCongThuc AND l.MaNguyenLieu={milk.MaNguyenLieu}) ORDER BY c.MaCongThuc FOR JSON PATH"));
        }
        finally
        {
            var current = (await fixture.Ingredients()).NguyenLieu.Single(n => n.MaNguyenLieu == milk.MaNguyenLieu);
            await fixture.Send("PUT", $"/api/admin/ingredients/{milk.MaNguyenLieu}", input with { DangSuDung = true, PhienBanDuLieu = current.PhienBanDuLieu, PhienBanAnhHuong = null, MaThaoTac = Guid.NewGuid() });
        }
        Assert.Equal(beforeCatalog.ToJsonString(), (await fixture.Send("GET", "/api/catalog")).ToJsonString());
    }

}
