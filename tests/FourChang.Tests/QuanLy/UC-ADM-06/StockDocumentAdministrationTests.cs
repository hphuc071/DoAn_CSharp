using System.Text.Json;
using FourChang.Tests.Shared;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Tests.Admin;

/// <summary>UC-ADM-04 chạy HTTP/SQL thật trong database fixture; không chạm database quán.</summary>
[Collection("AdminFeatures"), Trait("UseCase", "UC-ADM-04"), Trait("Actor", "QuanLy")]
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

    /// <summary>Giữ phiếu kiểm kê lịch sử để xem; không cho sửa hoặc xác nhận thành biến động mới.</summary>
    [Fact]
    public async Task KiemKeCu_ChiXem_KhongSuaHoacGhiSo()
    {
        var code = "KK-CU-" + Guid.NewGuid().ToString("N")[..12];
        await fixture.Api.Sql($"INSERT dbo.PhieuKho(SoPhieuKho,LoaiPhieu,TrangThai,MaQuanLyLap,MaThaoTac,LyDo) SELECT '{code}','COUNT','DRAFT',MaQuanLy,NEWID(),N'Kiểm kê lịch sử' FROM dbo.TaiKhoanQuanLy WHERE TenDangNhapChuanHoa='quanly'");
        var old = (await Documents()).Single(x => x.SoPhieuKho == code);
        var stock = (await Stock()).First(x => x.TonThucTe > 100);
        await fixture.Send("POST", $"/api/admin/stock-documents/{old.MaPhieuKho}/post", new StockDocumentAction("Kiểm kê cũ", old.PhienBanDuLieu, Guid.NewGuid()), 409);
        await fixture.Send("PUT", $"/api/admin/stock-documents/{old.MaPhieuKho}", Input("RECEIPT", [new(stock.MaNguyenLieu, 1)]) with { PhienBanDuLieu = old.PhienBanDuLieu }, 409);
        await fixture.Send("POST", $"/api/admin/stock-documents/{old.MaPhieuKho}/delete", new AdminDeleteInput(old.PhienBanDuLieu, Guid.NewGuid()), 409);
        var unchanged = (await Documents()).Single(x => x.MaPhieuKho == old.MaPhieuKho);
        Assert.Equal(old.PhienBanDuLieu, unchanged.PhienBanDuLieu);
        Assert.Equal("COUNT", unchanged.LoaiPhieu); Assert.Equal("DRAFT", unchanged.TrangThai);
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

    /// <summary>Ngày dự kiến không tự cộng kho; trước ngày không được xác nhận, sửa nháp đến hôm nay mới ghi sổ.</summary>
    [Theory]
    [InlineData("RECEIPT")]
    [InlineData("ISSUE")]
    public async Task NgayTuongLai_ChoXacNhan_KhongTuGhiSo(string type)
    {
        var stock = (await Stock()).First(x => x.TonThucTe > 100);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var input = Input(type, [new(stock.MaNguyenLieu, 2)]) with { NgayThucHien = today.AddDays(1) };
        var saved = await fixture.Send("POST", "/api/admin/stock-documents", input);
        var id = saved.Number("ma"); var draft = (await Documents()).Single(x => x.MaPhieuKho == id);
        Assert.Equal(today.AddDays(1), draft.NgayThucHien);
        await fixture.Send("POST", $"/api/admin/stock-documents/{id}/post", new StockDocumentAction("", draft.PhienBanDuLieu, Guid.NewGuid()), 409);
        Assert.Equal(stock.TonThucTe, (await Stock()).Single(x => x.MaNguyenLieu == stock.MaNguyenLieu).TonThucTe);
        await fixture.Send("PUT", $"/api/admin/stock-documents/{id}", input with { NgayThucHien = today.AddDays(-1), PhienBanDuLieu = draft.PhienBanDuLieu, MaThaoTac = Guid.NewGuid() });
        draft = (await Documents()).Single(x => x.MaPhieuKho == id);
        // Đã đến ngày vẫn chỉ là nháp. Chỉ lệnh xác nhận bên dưới mới làm thay đổi tồn.
        Assert.Equal("DRAFT", draft.TrangThai);
        Assert.Equal(stock.TonThucTe, (await Stock()).Single(x => x.MaNguyenLieu == stock.MaNguyenLieu).TonThucTe);
        await fixture.Send("POST", $"/api/admin/stock-documents/{id}/post", new StockDocumentAction("", draft.PhienBanDuLieu, Guid.NewGuid()));
        var posted = (await Documents()).Single(x => x.MaPhieuKho == id);
        Assert.Equal(today.AddDays(-1), posted.NgayThucHien);
        Assert.NotNull(posted.GhiSoLuc);
        Assert.Equal(stock.TonThucTe + (type == "RECEIPT" ? 2 : -2), (await Stock()).Single(x => x.MaNguyenLieu == stock.MaNguyenLieu).TonThucTe);
        await fixture.Send("PUT", $"/api/admin/stock-documents/{id}", input with { PhienBanDuLieu = posted.PhienBanDuLieu, MaThaoTac = Guid.NewGuid() }, 409);
        await fixture.Send("POST", $"/api/admin/stock-documents/{id}/delete", new AdminDeleteInput(posted.PhienBanDuLieu, Guid.NewGuid()), 409);
    }

    /// <summary>Nguyên liệu mới chưa có số dư vẫn nhập được; lưu nháp không tự tạo tồn, xác nhận mới tạo số dư.</summary>
    [Fact]
    public async Task NguyenLieuMoi_NhapLanDau_VaKhoaPhieuDaXacNhan()
    {
        var unit = (await fixture.Ingredients()).DonVi.First(x => x.KyHieu == "g");
        var material = await fixture.Send("POST", "/api/admin/ingredients", new IngredientInput("Nguyên liệu nhập mới " + Guid.NewGuid().ToString("N")[..8], unit.MaDonVi, 0, 5, null, true, [], null, Guid.NewGuid()));
        var materialId = material.Number("ma");
        var saved = await fixture.Send("POST", "/api/admin/stock-documents", Input("RECEIPT", [new(materialId, 2500, 100)]) with { LyDo = "" });
        var id = saved.Number("ma");
        Assert.Equal("0", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.TonKho WHERE MaNguyenLieu={materialId}"));
        var draft = (await Documents()).Single(x => x.MaPhieuKho == id);
        var action = new StockDocumentAction("", draft.PhienBanDuLieu, Guid.NewGuid());
        await fixture.Send("POST", $"/api/admin/stock-documents/{id}/post", action);
        await fixture.Send("POST", $"/api/admin/stock-documents/{id}/post", action);
        Assert.Equal(2500m, (await Stock()).Single(x => x.MaNguyenLieu == materialId).TonThucTe);
        Assert.Equal("1", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.BienDongKho b JOIN dbo.ChiTietPhieuKho c ON c.MaDongPhieuKho=b.MaDongPhieuKho WHERE c.MaPhieuKho={id}"));
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => fixture.Api.Sql($"UPDATE dbo.ChiTietPhieuKho SET SoLuongTheoDonViNhap=1 WHERE MaPhieuKho={id}"));
    }

    /// <summary>Không thể gọi API cũ để lập kiểm kê/điều chỉnh sau khi bỏ chức năng khỏi phạm vi.</summary>
    [Theory]
    [InlineData("COUNT")]
    [InlineData("ADJUSTMENT")]
    public async Task ChiChoLapPhieuNhapXuat(string type)
    {
        var stock = (await Stock()).First(x => x.TonThucTe > 100);
        await fixture.Send("POST", "/api/admin/stock-documents", Input(type, [new(stock.MaNguyenLieu, 1)]), 400);
    }

    /// <summary>Tạo yêu cầu có lý do/mục đích; không gắn đơn tiêu hao vào xuất thủ công.</summary>
    private static StockDocumentInput Input(string type, IReadOnlyList<StockDocumentLineInput> lines) => new(type, "Nhà cung cấp kiểm thử", type == "ISSUE" ? "DAMAGED" : null, "Kiểm thử chứng từ kho", lines, null, Guid.NewGuid());
    /// <summary>Đọc mốc tồn và phiên bản từ cùng API mà WinForms dùng.</summary>
    private async Task<IReadOnlyList<StockView>> Stock() => (await fixture.Send("GET", "/api/admin/inventory")).Deserialize<StockSnapshot>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!.NguyenLieu;
    /// <summary>Đọc chi tiết phiếu để lấy rowversion thật, không hardcode byte kiểm thử.</summary>
    private async Task<IReadOnlyList<StockDocumentView>> Documents() => (await fixture.Send("GET", "/api/admin/stock-documents")).Deserialize<List<StockDocumentView>>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
}

