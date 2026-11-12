using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Tests.Shared;

namespace FourChang.Tests.Admin;

/// <summary>ADM-05: số dư/giữ chỗ/an toàn, cảnh báo, drill-down và ranh giới quyền.</summary>
[Collection("AdminFeatures"), Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-05")]
public sealed class InventoryAdministrationTests(AdminFeatureFixture fixture)
{
    /// <summary>Tất cả API mới chặn ẩn danh và Customer; không dùng việc ẩn nút làm phân quyền.</summary>
    [Theory]
    [InlineData("/api/admin/ingredients")]
    [InlineData("/api/admin/recipes")]
    [InlineData("/api/admin/inventory")]
    public async Task PhanQuyen_KhachHangKhongDuocQuanTri(string path)
    {
        (await fixture.Api.Send("GET", path)).Expect(401, "Ẩn danh");
        var customer = await fixture.Api.NewCustomer(); var headers = await fixture.Api.Login(customer);
        (await fixture.Api.Send("GET", path, headers: headers)).Expect(403, "Customer không có quyền Admin");
    }

    /// <summary>Cảnh báo dựa khả dụng, truy vấn không tạo biến động; drill-down trả đúng nguyên liệu.</summary>
    [Fact]
    public async Task KhaDung_CanhBao_TruyVet_KhongSuaTon()
    {
        var snapshot = await fixture.Send("GET", "/api/admin/inventory");
        var row = snapshot["nguyenLieu"]!.AsArray().First(x => x!["tonThucTe"] is not null)!; var id = row.Number("maNguyenLieu");
        var onHand = row["tonThucTe"]!.GetValue<decimal>(); var held = row["giuCho"]!.GetValue<decimal>(); var safety = row["tonAnToan"]!.GetValue<decimal>();
        Assert.Equal(onHand - held - safety, row["khaDung"]!.GetValue<decimal>());
        var before = await fixture.Api.Sql("SELECT COUNT(*) FROM dbo.BienDongKho");
        var detail = await fixture.Send("GET", $"/api/admin/inventory/{id}");
        Assert.Equal(id, detail["nguyenLieu"]!.Number("maNguyenLieu"));
        Assert.Equal(before, await fixture.Api.Sql("SELECT COUNT(*) FROM dbo.BienDongKho"));
        await fixture.Send("GET", "/api/admin/inventory?status=INVALID", expected: 400);
        // Chỉ điều chỉnh dữ liệu fixture riêng để kiểm tra phép tính khóa/mở menu.
        await fixture.Api.Sql($"UPDATE dbo.TonKho SET TonThucTe=TongGiuCho WHERE MaNguyenLieu={id}");
        var low = await fixture.Send("GET", $"/api/admin/inventory/{id}"); Assert.Equal("OUT", low["nguyenLieu"]!.Text("canhBao"));
        await fixture.Api.Sql($"UPDATE dbo.TonKho SET TonThucTe={onHand.ToString(System.Globalization.CultureInfo.InvariantCulture)} WHERE MaNguyenLieu={id}");
        Assert.Equal(before, await fixture.Api.Sql("SELECT COUNT(*) FROM dbo.BienDongKho"));
    }

    /// <summary>Nguyên liệu mới chưa có số dư được báo UNKNOWN, không mặc định là hết/0.</summary>
    [Fact]
    public async Task ThieuSoDu_BaoChuaXacDinh_KhongGanKhong()
    {
        var unit = (await fixture.Ingredients()).DonVi.First();
        var saved = await fixture.Send("POST", "/api/admin/ingredients", new IngredientInput("Chưa có số dư " + Guid.NewGuid().ToString("N")[..8], unit.MaDonVi, 0, 0, null, true, [], null, Guid.NewGuid()));
        var id = saved.Number("ma"); var result = await fixture.Send("GET", $"/api/admin/inventory/{id}");
        Assert.Equal("UNKNOWN", result["nguyenLieu"]!.Text("canhBao")); Assert.Null(result["nguyenLieu"]!["tonThucTe"]);
    }
}
