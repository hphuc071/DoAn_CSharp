using System.Text.Json;
using FourChang.Tests.Shared;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Tests.Admin;

/// <summary>UC-ADM-06 chạy HTTP/SQL thật trong database fixture; không chạm database quán.</summary>
[Collection("AdminFeatures"), Trait("UseCase", "UC-ADM-06"), Trait("Actor", "QuanLy")]
public sealed class StockDocumentAdministrationTests(AdminFeatureFixture fixture)
{
    /// <summary>Nháp không đổi tồn; ghi sổ lặp không cộng hai lần; đảo tạo phiếu mới và giữ gốc bất biến.</summary>
    [Fact]
    public async Task NhapKho_NhapKhongDoiTon_GhiSoMotLan_DaoCoLienKet()
    {
        var stock = (await Stock()).First(x => x.TonThucTe > 100);
        var before = stock.TonThucTe;
        var created = await fixture.Send("POST", "/api/admin/stock-documents", Input("RECEIPT", [new(stock.MaNguyenLieu, 12.5m)]));
        var id = created.Number("ma");
        Assert.Equal(before, (await Stock()).Single(x => x.MaNguyenLieu == stock.MaNguyenLieu).TonThucTe);
        var draft = (await Documents()).Single(x => x.MaPhieuKho == id);
        var post = new StockDocumentAction("Nhập theo phiếu giao", draft.PhienBanDuLieu, Guid.NewGuid());
        await fixture.Send("POST", $"/api/admin/stock-documents/{id}/post", post);
        await fixture.Send("POST", $"/api/admin/stock-documents/{id}/post", post);
        Assert.Equal(before + 12.5m, (await Stock()).Single(x => x.MaNguyenLieu == stock.MaNguyenLieu).TonThucTe);
        var posted = (await Documents()).Single(x => x.MaPhieuKho == id);
        await fixture.Send("PUT", $"/api/admin/stock-documents/{id}", Input("RECEIPT", [new(stock.MaNguyenLieu, 99)]) with { PhienBanDuLieu = posted.PhienBanDuLieu }, 409);
        var reverse = new StockDocumentAction("Sai số lượng cần đảo", posted.PhienBanDuLieu, Guid.NewGuid());
        var receipt = await fixture.Send("POST", $"/api/admin/stock-documents/{id}/reverse", reverse);
        await fixture.Send("POST", $"/api/admin/stock-documents/{id}/reverse", reverse);
        Assert.Equal(before, (await Stock()).Single(x => x.MaNguyenLieu == stock.MaNguyenLieu).TonThucTe);
        Assert.Equal(id, (await Documents()).Single(x => x.MaPhieuKho == receipt.Number("ma")).MaPhieuBiDao);
        Assert.Equal("POSTED", (await Documents()).Single(x => x.MaPhieuKho == id).TrangThai);
        await fixture.Send("POST", $"/api/admin/stock-documents/{id}/reverse", reverse with { MaThaoTac = Guid.NewGuid() }, 409);
    }

    /// <summary>Một dòng vượt tồn làm toàn phiếu thất bại, không trừ dòng hợp lệ trước nó.</summary>
    [Fact]
    public async Task XuatVuotTon_ChanToanPhieu_VaDoiNoiDungMaCuBiChan()
    {
        var stocks = (await Stock()).Where(x => x.TonThucTe > 100).Take(2).ToArray();
        var input = Input("ISSUE", [new(stocks[0].MaNguyenLieu, 1), new(stocks[1].MaNguyenLieu, stocks[1].TonThucTe!.Value + 1)]);
        var saved = await fixture.Send("POST", "/api/admin/stock-documents", input);
        await fixture.Send("POST", "/api/admin/stock-documents", input with { LyDo = "Nội dung khác" }, 409);
        var id = saved.Number("ma"); var draft = (await Documents()).Single(x => x.MaPhieuKho == id);
        await fixture.Send("POST", $"/api/admin/stock-documents/{id}/post", new StockDocumentAction("Xuất hỏng", draft.PhienBanDuLieu, Guid.NewGuid()), 409);
        var after = await Stock();
        foreach (var stock in stocks) Assert.Equal(stock.TonThucTe, after.Single(x => x.MaNguyenLieu == stock.MaNguyenLieu).TonThucTe);
        Assert.Equal("DRAFT", (await Documents()).Single(x => x.MaPhieuKho == id).TrangThai);
    }

    /// <summary>Kiểm kê khớp không tạo sổ giả; thay đổi tồn sau mốc chặn chốt và giữ nguyên nháp.</summary>
    [Fact]
    public async Task KiemKe_MocDaDoiBiChan_ChenhLechKhongKhongTaoBienDong()
    {
        var stock = (await Stock()).First(x => x.TonThucTe > 100);
        var count = Input("COUNT", [new(stock.MaNguyenLieu, stock.TonThucTe!.Value, null, stock.TonThucTe, stock.PhienBanDuLieu)]);
        var saved = await fixture.Send("POST", "/api/admin/stock-documents", count); var id = saved.Number("ma");
        var draft = (await Documents()).Single(x => x.MaPhieuKho == id);
        await fixture.Send("POST", $"/api/admin/stock-documents/{id}/post", new StockDocumentAction("Kiểm kê khớp", draft.PhienBanDuLieu, Guid.NewGuid()));
        Assert.Equal("0", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.BienDongKho b JOIN dbo.ChiTietPhieuKho c ON c.MaDongPhieuKho=b.MaDongPhieuKho WHERE c.MaPhieuKho={id}"));
        var next = await fixture.Send("POST", "/api/admin/stock-documents", count with { MaThaoTac = Guid.NewGuid() }); var nextId = next.Number("ma");
        await fixture.Api.Sql($"UPDATE dbo.TonKho SET TonThucTe=TonThucTe+1 WHERE MaNguyenLieu={stock.MaNguyenLieu}");
        var stale = (await Documents()).Single(x => x.MaPhieuKho == nextId);
        await fixture.Send("POST", $"/api/admin/stock-documents/{nextId}/post", new StockDocumentAction("Chốt kiểm kê", stale.PhienBanDuLieu, Guid.NewGuid()), 409);
        Assert.Equal("DRAFT", (await Documents()).Single(x => x.MaPhieuKho == nextId).TrangThai);
    }

    /// <summary>Không mở quyền kho cho khách/Staff hoặc truy cập ẩn danh.</summary>
    [Fact]
    public async Task Kho_YeuCauPhienAdmin()
    {
        (await fixture.Api.Send("GET", "/api/admin/stock-documents")).Expect(401, "Chưa đăng nhập");
        var customer = await fixture.Api.NewCustomer("stock");
        var headers = await fixture.Api.Login(customer);
        (await fixture.Api.Send("GET", "/api/admin/stock-documents", headers: headers)).Expect(403, "Khách không có quyền kho");
    }

    /// <summary>Nháp sửa/xóa được với rowversion đúng; xóa nháp không để dòng mồ côi và không đổi tồn.</summary>
    [Fact]
    public async Task Nhap_SuaVaXoa_KhongDoiTon()
    {
        var stock = (await Stock()).First(x => x.TonThucTe > 100);
        var input = Input("RECEIPT", [new(stock.MaNguyenLieu, 5)]);
        var saved = await fixture.Send("POST", "/api/admin/stock-documents", input);
        var id = saved.Number("ma");
        var row = (await Documents()).Single(x => x.MaPhieuKho == id);
        await fixture.Send("PUT", $"/api/admin/stock-documents/{id}", input with { ThanhPhan=[new(stock.MaNguyenLieu, 7)], PhienBanDuLieu=row.PhienBanDuLieu, MaThaoTac=Guid.NewGuid() });
        row = (await Documents()).Single(x => x.MaPhieuKho == id);
        await fixture.Send("POST", $"/api/admin/stock-documents/{id}/delete", new AdminDeleteInput(row.PhienBanDuLieu, Guid.NewGuid()));
        Assert.DoesNotContain(await Documents(), x => x.MaPhieuKho == id);
        Assert.Equal(stock.TonThucTe, (await Stock()).Single(x => x.MaNguyenLieu == stock.MaNguyenLieu).TonThucTe);
        Assert.Equal("0", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.ChiTietPhieuKho WHERE MaPhieuKho={id}"));
    }

    /// <summary>Audit bắt buộc lỗi phải rollback cả tồn, sổ kho và trạng thái POSTED, không chỉ báo lỗi trên UI.</summary>
    [Fact]
    public async Task LoiAudit_KhongGhiSoMotPhan()
    {
        var stock = (await Stock()).First(x => x.TonThucTe > 100);
        var saved = await fixture.Send("POST", "/api/admin/stock-documents", Input("RECEIPT", [new(stock.MaNguyenLieu, 8)]));
        var id = saved.Number("ma"); var row = (await Documents()).Single(x => x.MaPhieuKho == id);
        var operation = Guid.NewGuid();
        await fixture.Api.Sql("DENY INSERT ON OBJECT::dbo.NhatKyHeThong TO fourchang_app");
        try
        {
            var response = await fixture.Api.Send("POST", $"/api/admin/stock-documents/{id}/post", System.Text.Json.JsonSerializer.SerializeToNode(new StockDocumentAction("Ghi sổ kiểm tra atomic", row.PhienBanDuLieu, operation)), fixture.Admin);
            Assert.True(response.Status >= 400);
        }
        finally { await fixture.Api.Sql("REVOKE INSERT ON OBJECT::dbo.NhatKyHeThong FROM fourchang_app"); }
        Assert.Equal(stock.TonThucTe, (await Stock()).Single(x => x.MaNguyenLieu == stock.MaNguyenLieu).TonThucTe);
        Assert.Equal("DRAFT", (await Documents()).Single(x => x.MaPhieuKho == id).TrangThai);
        Assert.Equal("0", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.SuKienDongBo WHERE MaThaoTac='{operation}'"));
        Assert.Equal("0", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.BienDongKho b JOIN dbo.ChiTietPhieuKho l ON b.MaDongPhieuKho=l.MaDongPhieuKho WHERE l.MaPhieuKho={id}"));
    }

    /// <summary>Tạo yêu cầu có lý do/mục đích; không gắn đơn tiêu hao vào xuất thủ công.</summary>
    private static StockDocumentInput Input(string type, IReadOnlyList<StockDocumentLineInput> lines) => new(type, "Nhà cung cấp kiểm thử", type == "ISSUE" ? "DAMAGED" : null, "Kiểm thử chứng từ kho", lines, null, Guid.NewGuid());
    /// <summary>Đọc mốc tồn và phiên bản từ cùng API mà WinForms dùng.</summary>
    private async Task<IReadOnlyList<StockView>> Stock() => (await fixture.Send("GET", "/api/admin/inventory")).Deserialize<StockSnapshot>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!.NguyenLieu;
    /// <summary>Đọc chi tiết phiếu để lấy rowversion thật, không hardcode byte kiểm thử.</summary>
    private async Task<IReadOnlyList<StockDocumentView>> Documents() => (await fixture.Send("GET", "/api/admin/stock-documents")).Deserialize<List<StockDocumentView>>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
}

