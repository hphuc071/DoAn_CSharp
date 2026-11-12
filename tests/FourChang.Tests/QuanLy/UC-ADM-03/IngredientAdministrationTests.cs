using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Tests.Shared;

namespace FourChang.Tests.Admin;

/// <summary>ADM-03: test API/SQL thật cho danh mục, quy đổi, tham chiếu, đồng thời và rollback audit.</summary>
[Collection("AdminFeatures"), Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-03")]
public sealed class IngredientAdministrationTests(AdminFeatureFixture fixture)
{
    /// <summary>CRUD nguyên liệu nháp và quy đổi, chặn bản cũ/gửi khác nội dung, giữ tồn không bị chỉnh.</summary>
    [Fact]
    public async Task Crud_QuyDoi_Version_ChongTrung_VaXoaCoDieuKien()
    {
        var snap = await fixture.Ingredients(); var unit = snap.DonVi.First(x => x.DangSuDung);
        var input = new IngredientInput("Nguyên liệu " + Guid.NewGuid().ToString("N")[..8], unit.MaDonVi, 1, 5, "xUnit", true, [], null, Guid.NewGuid());
        var receipt = await fixture.Send("POST", "/api/admin/ingredients", input); var id = receipt.Number("ma");
        Assert.Equal(id, (await fixture.Send("POST", "/api/admin/ingredients", input)).Number("ma"));
        await fixture.Send("POST", "/api/admin/ingredients", input with { TenNguyenLieu = "Nội dung khác" }, 409);
        var item = (await fixture.Ingredients()).NguyenLieu.Single(x => x.MaNguyenLieu == id);
        Assert.Single(item.QuyDoi); Assert.Equal(1, item.QuyDoi[0].HeSoVeDonViCoSo);
        var edit = input with { TonAnToan = 2, PhienBanDuLieu = item.PhienBanDuLieu, MaThaoTac = Guid.NewGuid() };
        await fixture.Send("PUT", $"/api/admin/ingredients/{id}", edit);
        await fixture.Send("PUT", $"/api/admin/ingredients/{id}", edit with { MaThaoTac = Guid.NewGuid() }, 409);
        item = (await fixture.Ingredients()).NguyenLieu.Single(x => x.MaNguyenLieu == id);
        Assert.Equal("0", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.TonKho WHERE MaNguyenLieu={id}"));
        await fixture.Send("POST", $"/api/admin/ingredients/{id}/delete", new AdminDeleteInput(item.PhienBanDuLieu, Guid.NewGuid()));
        Assert.DoesNotContain((await fixture.Ingredients()).NguyenLieu, x => x.MaNguyenLieu == id);
    }

    /// <summary>Không cho ngừng/xóa nguyên liệu còn BOM hoặc thay đơn vị cơ sở sau phát sinh.</summary>
    [Fact]
    public async Task DuLieuThamChieu_KhongXoa_KhongDoiDonVi_KhongNgung()
    {
        var snap = await fixture.Ingredients(); var item = snap.NguyenLieu.First(x => x.SoThamChieu > 0);
        var input = new IngredientInput(item.TenNguyenLieu, item.MaDonViCoSo, item.TonAnToan, item.NguongCanhBao, item.MoTa, false, item.QuyDoi, item.PhienBanDuLieu, Guid.NewGuid());
        await fixture.Send("PUT", $"/api/admin/ingredients/{item.MaNguyenLieu}", input, 409);
        await fixture.Send("POST", $"/api/admin/ingredients/{item.MaNguyenLieu}/delete", new AdminDeleteInput(item.PhienBanDuLieu, Guid.NewGuid()), 409);
        await fixture.Send("PUT", $"/api/admin/ingredients/{item.MaNguyenLieu}", input with { DangSuDung = true, MaDonViCoSo = snap.DonVi.First(x => x.MaDonVi != item.MaDonViCoSo).MaDonVi, MaThaoTac = Guid.NewGuid() }, 409);
    }

    /// <summary>Đầu vào sai hoặc audit lỗi không tạo danh mục/quy đổi dở dang.</summary>
    [Fact]
    public async Task Validation_VaLoiAudit_RollbackCaQuyDoi()
    {
        var snap = await fixture.Ingredients(); var unit = snap.DonVi.First();
        var input = new IngredientInput("Rollback " + Guid.NewGuid().ToString("N")[..8], unit.MaDonVi, 5, 3, null, true, [], null, Guid.NewGuid());
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
        // Mô phỏng metadata cũ trên database test, không chỉnh danh mục đơn vị thật.
        var kg = long.Parse(await fixture.Api.Sql("INSERT dbo.DonViTinh(KyHieu,TenDonVi,DangSuDung) VALUES('kg',N'Kilôgam kiểm thử',1); SELECT CONVERT(bigint,SCOPE_IDENTITY())"));
        try
        {
            await fixture.Send("POST", "/api/admin/ingredients", input with { MaDonViCoSo = kg, QuyDoi = [], MaThaoTac = Guid.NewGuid() }, 400);
            Assert.Equal("0", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.NguyenLieu WHERE MaDonViCoSo={kg}"));
        }
        finally { await fixture.Api.Sql($"DELETE dbo.DonViTinh WHERE MaDonVi={kg}"); }
    }
}
