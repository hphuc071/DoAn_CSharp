using System.Text.Json;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Tests.Shared;

namespace FourChang.Tests.Admin;

/// <summary>ADM-04 kiểm thử công thức có phiên bản, mô phỏng và khả dụng, không sửa snapshot đơn cũ.</summary>
[Collection("AdminFeatures"), Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-04")]
public sealed class RecipeAdministrationTests(AdminFeatureFixture fixture)
{
    /// <summary>Mô phỏng gom FIXED/SUGAR dùng chung nguyên liệu, hao hụt và làm tròn một lần.</summary>
    [Fact]
    public async Task MoPhong_GomNguyenLieu_DuongDa_HaoHut_KhongTruKho()
    {
        var snap = await fixture.Ingredients(); var n = snap.NguyenLieu.First(x => x.DangSuDung);
        var seed = (await fixture.Recipes()).First(x => x.MaCauHinhMon is not null);
        var input = new RecipeInput(seed.MaCauHinhMon, null, "Mô phỏng " + Guid.NewGuid().ToString("N")[..8], null,
            [new(n.MaNguyenLieu, n.MaDonViCoSo, .0001m, "FIXED", .1m), new(n.MaNguyenLieu, n.MaDonViCoSo, .0001m, "SUGAR", .1m)], null, Guid.NewGuid());
        var saved = await fixture.Send("POST", "/api/admin/recipes", input); var id = saved.Number("ma");
        var before = await fixture.Api.Sql($"SELECT TonThucTe FROM dbo.TonKho WHERE MaNguyenLieu={n.MaNguyenLieu}");
        var result = await fixture.Send("POST", "/api/admin/recipes/simulate", new RecipeSimulationInput(id, 3, 50, 0, []));
        Assert.Single(result.AsArray()); Assert.Equal(.0005m, result[0]!["soLuongCan"]!.GetValue<decimal>());
        Assert.Equal(before, await fixture.Api.Sql($"SELECT TonThucTe FROM dbo.TonKho WHERE MaNguyenLieu={n.MaNguyenLieu}"));
        await fixture.Send("POST", $"/api/admin/recipes/{id}/delete", new AdminDeleteInput(Convert.FromBase64String(saved.Text("phienBanDuLieu")), Guid.NewGuid()));
    }

    /// <summary>Công bố phiên bản thay thế, chặn sửa bản hiệu lực và giữ định mức cũ nguyên vẹn.</summary>
    [Fact]
    public async Task PhienBan_CongBo_KhongSuaBanCu_Clone_LapLenh()
    {
        var old = (await fixture.Recipes()).First(x => x.MaCauHinhMon is not null && x.TrangThai == "ACTIVE");
        var snap = await fixture.Ingredients(); var line = old.ThanhPhan.First(); var n = snap.NguyenLieu.Single(x => x.MaNguyenLieu == line.MaNguyenLieu);
        var input = new RecipeInput(old.MaCauHinhMon, null, "Phiên bản mới " + Guid.NewGuid().ToString("N")[..8], null,
            [new(n.MaNguyenLieu, n.MaDonViCoSo, 1, "FIXED", 0)], null, Guid.NewGuid());
        var saved = await fixture.Send("POST", "/api/admin/recipes", input); var id = saved.Number("ma");
        var publish = new RecipePublishInput(DateTime.UtcNow, null, Convert.FromBase64String(saved.Text("phienBanDuLieu")), Guid.NewGuid());
        await fixture.Send("POST", $"/api/admin/recipes/{id}/publish", publish);
        await fixture.Send("POST", $"/api/admin/recipes/{id}/publish", publish);
        var current = (await fixture.Recipes()).Single(x => x.MaCongThuc == id);
        Assert.Equal("ACTIVE", current.TrangThai);
        await fixture.Send("PUT", $"/api/admin/recipes/{id}", input with { PhienBanDuLieu = current.PhienBanDuLieu, MaThaoTac = Guid.NewGuid() }, 409);
        var original = (await fixture.Recipes()).Single(x => x.MaCongThuc == old.MaCongThuc);
        Assert.Equal("RETIRED", original.TrangThai); Assert.Equal(old.ThanhPhan.ToArray(), original.ThanhPhan.ToArray());
        var clone = await fixture.Send("POST", $"/api/admin/recipes/{id}/clone", new AdminDeleteInput(current.PhienBanDuLieu, Guid.NewGuid()));
        Assert.Equal("DRAFT", (await fixture.Recipes()).Single(x => x.MaCongThuc == clone.Number("ma")).TrangThai);
        await fixture.Send("POST", $"/api/admin/recipes/{id}/delete", new AdminDeleteInput(current.PhienBanDuLieu, Guid.NewGuid()), 409);
    }

    /// <summary>Không công bố thiếu thành phần; định mức hoặc quy đổi sai bị chặn ở API.</summary>
    [Fact]
    public async Task ThieuThanhPhan_QuyDoiSai_KhongCongBo()
    {
        var old = (await fixture.Recipes()).First();
        var empty = new RecipeInput(old.MaCauHinhMon, old.MaMonThem, "Nháp thiếu thành phần", null, [], null, Guid.NewGuid());
        var saved = await fixture.Send("POST", "/api/admin/recipes", empty); var id = saved.Number("ma");
        await fixture.Send("POST", $"/api/admin/recipes/{id}/publish", new RecipePublishInput(DateTime.UtcNow, null, Convert.FromBase64String(saved.Text("phienBanDuLieu")), Guid.NewGuid()), 409);
        var n = (await fixture.Ingredients()).NguyenLieu.First();
        await fixture.Send("POST", "/api/admin/recipes", empty with { ThanhPhan = [new(n.MaNguyenLieu, n.MaDonViCoSo, -1, "FIXED", 0)], MaThaoTac = Guid.NewGuid() }, 400);
        await fixture.Send("POST", "/api/admin/recipes", empty with { ThanhPhan = [new(n.MaNguyenLieu, 9999999, 1, "FIXED", 0)], MaThaoTac = Guid.NewGuid() }, 400);
    }

    /// <summary>Lịch mới chỉ kích hoạt khi đến hạn, chặn lịch chồng và giữ đúng một phiên bản ACTIVE.</summary>
    [Fact]
    public async Task LichCongBo_ChanChongLan_KichHoatDungHan()
    {
        var source = (await fixture.Recipes()).First(x => x.MaCauHinhMon is not null && x.TrangThai == "ACTIVE");
        var clone = await fixture.Send("POST", $"/api/admin/recipes/{source.MaCongThuc}/clone", new AdminDeleteInput(source.PhienBanDuLieu, Guid.NewGuid()));
        var id = clone.Number("ma");
        var start = DateTime.UtcNow.AddSeconds(8);
        await fixture.Send("POST", $"/api/admin/recipes/{id}/publish", new RecipePublishInput(start, null, Convert.FromBase64String(clone.Text("phienBanDuLieu")), Guid.NewGuid()));
        Assert.Equal("SCHEDULED", (await fixture.Recipes()).Single(x => x.MaCongThuc == id).TrangThai);
        var other = await fixture.Send("POST", $"/api/admin/recipes/{source.MaCongThuc}/clone", new AdminDeleteInput((await fixture.Recipes()).Single(x => x.MaCongThuc == source.MaCongThuc).PhienBanDuLieu, Guid.NewGuid()));
        await fixture.Send("POST", $"/api/admin/recipes/{other.Number("ma")}/publish", new RecipePublishInput(start.AddSeconds(2), null, Convert.FromBase64String(other.Text("phienBanDuLieu")), Guid.NewGuid()), 409);
        var deadline = DateTime.UtcNow.AddSeconds(25);
        RecipeView current = source;
        do
        {
            await Task.Delay(250);
            // Worker đổi lịch có thể cạnh tranh khóa SQL với lượt đọc. Mô phỏng Tải lại của UI khi 503 tạm thời,
            // không retry lệnh publish/ghi và không dùng bản cũ để kết luận worker đã kích hoạt thành công.
            var response = await fixture.Api.Send("GET", "/api/admin/recipes", headers:fixture.Admin);
            if (response.Status == 503 && DateTime.UtcNow < deadline) continue;
            current = response.Expect(200, "Đọc lại BOM theo lịch").Json.Deserialize<List<RecipeView>>(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!.Single(x => x.MaCongThuc == id);
        } while (current.TrangThai != "ACTIVE" && DateTime.UtcNow < deadline);
        Assert.Equal("ACTIVE", current.TrangThai);
        var versions = (await fixture.Recipes()).Where(x => x.MaCauHinhMon == source.MaCauHinhMon).ToList();
        Assert.Single(versions, x => x.TrangThai == "ACTIVE");
        Assert.Equal("RETIRED", versions.Single(x => x.MaCongThuc == source.MaCongThuc).TrangThai);
        Assert.Equal(source.ThanhPhan.ToArray(), versions.Single(x => x.MaCongThuc == source.MaCongThuc).ThanhPhan.ToArray());
    }

}

