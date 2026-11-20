using System.Text.Json;
using FourChang.Tests.Shared;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Tests.Admin;

/// <summary>UC-ADM-04 chạy HTTP/SQL thật trong database fixture; không chạm database quán.</summary>
[Collection("AdminFeatures"), Trait("UseCase", "UC-ADM-04"), Trait("Actor", "QuanLy")]
public sealed class StockDocumentAdministrationTests(AdminFeatureFixture fixture)
{
    /// <summary>Thành công khóa sửa/xóa/hủy ở cả endpoint hủy cũ và hủy lịch; không sinh phiếu ngược hoặc đổi tồn.</summary>
    [Theory]
    [InlineData("RECEIPT")]
    [InlineData("ISSUE")]
    public async Task DaThanhCong_KhongDuocHuySuaXoa(string type)
    {
        var stock = (await Stock()).First(x => x.KhaDung > 100);
        var before = stock.TonThucTe;
        var id = (await fixture.Send("POST", "/api/admin/stock-documents", Input(type, [new(stock.MaNguyenLieu, 12.5m)]))).Number("ma");
        var draft = (await Documents()).Single(x => x.MaPhieuKho == id);
        var post = new StockDocumentAction("Xác nhận phiếu", draft.PhienBanDuLieu, Guid.NewGuid());
        await fixture.Send("POST", $"/api/admin/stock-documents/{id}/post", post);
        await fixture.Send("POST", $"/api/admin/stock-documents/{id}/post", post);
        var posted = (await Documents()).Single(x => x.MaPhieuKho == id);
        await fixture.Send("PUT", $"/api/admin/stock-documents/{id}", Input(type, [new(stock.MaNguyenLieu, 99)]) with { PhienBanDuLieu = posted.PhienBanDuLieu }, 409);
        await fixture.Send("POST", $"/api/admin/stock-documents/{id}/delete", new AdminDeleteInput(posted.PhienBanDuLieu, Guid.NewGuid()), 409);
        foreach (var action in new[] { "reverse", "cancel" })
            await fixture.Send("POST", $"/api/admin/stock-documents/{id}/{action}", new StockDocumentAction("Không được hủy phiếu thành công", posted.PhienBanDuLieu, Guid.NewGuid()), 409);
        Assert.Equal(before + (type == "RECEIPT" ? 12.5m : -12.5m), (await Stock()).Single(x => x.MaNguyenLieu == stock.MaNguyenLieu).TonThucTe);
        var unchanged = (await Documents()).Single(x => x.MaPhieuKho == id);
        Assert.Equal("POSTED", unchanged.TrangThai); Assert.Equal(posted.PhienBanDuLieu, unchanged.PhienBanDuLieu);
        Assert.Equal("0", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.PhieuKho WHERE MaPhieuBiDao={id}"));
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

    /// <summary>Xác nhận ngày tương lai khóa phiếu, chưa đổi tồn; worker đến hạn ghi đúng một lần cho cả nhập và xuất.</summary>
    [Theory]
    [InlineData("RECEIPT")]
    [InlineData("ISSUE")]
    public async Task HenNgay_XacNhanTruoc_DenHanTuGhiSo(string type)
    {
        var stock = (await Stock()).First(x => x.KhaDung > 100);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var input = Input(type, [new(stock.MaNguyenLieu, 2)]) with { NgayThucHien = today.AddDays(1) };
        var id = (await fixture.Send("POST", "/api/admin/stock-documents", input)).Number("ma");
        var draft = (await Documents()).Single(x => x.MaPhieuKho == id);
        var approval = new StockDocumentAction("Duyệt phiếu ngày mai", draft.PhienBanDuLieu, Guid.NewGuid());
        await fixture.Send("POST", $"/api/admin/stock-documents/{id}/post", approval);
        await fixture.Send("POST", $"/api/admin/stock-documents/{id}/post", approval);
        var scheduled = (await Documents()).Single(x => x.MaPhieuKho == id);
        Assert.Equal("SCHEDULED", scheduled.TrangThai); Assert.Null(scheduled.GhiSoLuc);
        Assert.NotNull(scheduled.NguoiGhiSo);
        Assert.Equal(stock.TonThucTe, (await Stock()).Single(x => x.MaNguyenLieu == stock.MaNguyenLieu).TonThucTe);
        await fixture.Send("PUT", $"/api/admin/stock-documents/{id}", input with { PhienBanDuLieu = scheduled.PhienBanDuLieu, MaThaoTac = Guid.NewGuid() }, 409);
        await fixture.Send("POST", $"/api/admin/stock-documents/{id}/delete", new AdminDeleteInput(scheduled.PhienBanDuLieu, Guid.NewGuid()), 409);
        // Chỉ database GUID fixture: đưa ngày về hôm nay để mô phỏng lịch đã đến hạn, không sửa đồng hồ máy.
        await fixture.Api.Sql($"UPDATE dbo.PhieuKho SET NgayThucHien=CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME())) WHERE MaPhieuKho={id}");
        var posted = await WaitForState(id, "POSTED");
        Assert.NotNull(posted.GhiSoLuc);
        Assert.Equal(stock.TonThucTe + (type == "RECEIPT" ? 2 : -2), (await Stock()).Single(x => x.MaNguyenLieu == stock.MaNguyenLieu).TonThucTe);
        await Task.Delay(5500); // Qua thêm một vòng worker để kiểm không ghi sổ lặp.
        Assert.Equal("1", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.BienDongKho b JOIN dbo.ChiTietPhieuKho l ON l.MaDongPhieuKho=b.MaDongPhieuKho WHERE l.MaPhieuKho={id}"));
        Assert.Equal("1", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.NhatKyHeThong WHERE LoaiTacNhan='SYSTEM' AND HanhDong='STOCK_SCHEDULE_POSTED' AND MaDoiTuong='{id}'"));
    }

    /// <summary>Thiếu một nguyên liệu không trừ dòng nào; FAILED không tự thử lại khi nhập thêm hàng, sửa và xác nhận lại cùng ID.</summary>
    [Fact]
    public async Task HenXuatThieuTon_CanKiemTra_KhongTruMotPhan()
    {
        var stocks = (await Stock()).Where(x => x.KhaDung > 100).Take(2).ToArray();
        var input = Input("ISSUE", [new(stocks[0].MaNguyenLieu, 1), new(stocks[1].MaNguyenLieu, stocks[1].TonThucTe!.Value + 1)])
            with { NgayThucHien = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(1) };
        var id = (await fixture.Send("POST", "/api/admin/stock-documents", input)).Number("ma");
        var draft = (await Documents()).Single(x => x.MaPhieuKho == id);
        await fixture.Send("POST", $"/api/admin/stock-documents/{id}/post", new StockDocumentAction("Xuất theo lịch", draft.PhienBanDuLieu, Guid.NewGuid()));
        await fixture.Api.Sql($"UPDATE dbo.PhieuKho SET NgayThucHien=CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME())) WHERE MaPhieuKho={id}");
        var failed = await WaitForState(id, "FAILED");
        Assert.NotEmpty(failed.LoiXuLy!); Assert.Null(failed.GhiSoLuc);
        foreach (var stock in stocks) Assert.Equal(stock.TonThucTe, (await Stock()).Single(x => x.MaNguyenLieu == stock.MaNguyenLieu).TonThucTe);
        Assert.Equal("0", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.BienDongKho b JOIN dbo.ChiTietPhieuKho l ON l.MaDongPhieuKho=b.MaDongPhieuKho WHERE l.MaPhieuKho={id}"));
        await fixture.Api.Sql($"UPDATE dbo.TonKho SET TonThucTe=TonThucTe+1000 WHERE MaNguyenLieu={stocks[1].MaNguyenLieu}");
        await Task.Delay(5500);
        Assert.Equal("FAILED", (await Documents()).Single(x => x.MaPhieuKho == id).TrangThai);
        await fixture.Send("PUT", $"/api/admin/stock-documents/{id}", input with { NgayThucHien = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)), ThanhPhan = [new(stocks[0].MaNguyenLieu, 1), new(stocks[1].MaNguyenLieu, 1)], PhienBanDuLieu = failed.PhienBanDuLieu, MaThaoTac = Guid.NewGuid() });
        draft = (await Documents()).Single(x => x.MaPhieuKho == id);
        Assert.Equal("DRAFT", draft.TrangThai); Assert.Null(draft.LoiXuLy);
        await fixture.Send("POST", $"/api/admin/stock-documents/{id}/post", new StockDocumentAction("Xác nhận lại", draft.PhienBanDuLieu, Guid.NewGuid()));
        Assert.Equal("POSTED", (await Documents()).Single(x => x.MaPhieuKho == id).TrangThai);
    }

    /// <summary>Hủy lịch giữ lịch sử và không cập nhật kho dù sau đó đến hạn; replay hủy không tạo hành động mới.</summary>
    [Fact]
    public async Task HuyLich_KhongGhiSoKhiDenNgay()
    {
        var stock = (await Stock()).First(x => x.KhaDung > 100);
        var id = (await fixture.Send("POST", "/api/admin/stock-documents", Input("RECEIPT", [new(stock.MaNguyenLieu, 5)]) with { NgayThucHien = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(1) })).Number("ma");
        var row = (await Documents()).Single(x => x.MaPhieuKho == id);
        await fixture.Send("POST", $"/api/admin/stock-documents/{id}/post", new StockDocumentAction("Hẹn nhập", row.PhienBanDuLieu, Guid.NewGuid()));
        row = (await Documents()).Single(x => x.MaPhieuKho == id);
        var cancel = new StockDocumentAction("Không còn nhận lô này", row.PhienBanDuLieu, Guid.NewGuid());
        await fixture.Send("POST", $"/api/admin/stock-documents/{id}/cancel", cancel);
        await fixture.Send("POST", $"/api/admin/stock-documents/{id}/cancel", cancel);
        await fixture.Api.Sql($"UPDATE dbo.PhieuKho SET NgayThucHien=CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME())) WHERE MaPhieuKho={id}");
        await Task.Delay(5500);
        Assert.Equal("CANCELLED", (await Documents()).Single(x => x.MaPhieuKho == id).TrangThai);
        Assert.Equal(stock.TonThucTe, (await Stock()).Single(x => x.MaNguyenLieu == stock.MaNguyenLieu).TonThucTe);
    }

    /// <summary>Worker lỗi ghi audit rollback cả kho và trạng thái; khôi phục quyền rồi chỉ ghi sổ đúng một lần.</summary>
    [Fact]
    public async Task HenNgay_LoiAudit_RollbackVaPhucHoi()
    {
        var stock = (await Stock()).First(x => x.KhaDung > 100);
        var id = (await fixture.Send("POST", "/api/admin/stock-documents", Input("RECEIPT", [new(stock.MaNguyenLieu, 3)]) with { NgayThucHien = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(1) })).Number("ma");
        var draft = (await Documents()).Single(x => x.MaPhieuKho == id);
        await fixture.Send("POST", $"/api/admin/stock-documents/{id}/post", new StockDocumentAction("Hẹn nhập", draft.PhienBanDuLieu, Guid.NewGuid()));
        await fixture.Api.Sql("DENY INSERT ON OBJECT::dbo.NhatKyHeThong TO fourchang_app");
        try
        {
            await fixture.Api.Sql($"UPDATE dbo.PhieuKho SET NgayThucHien=CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME())) WHERE MaPhieuKho={id}");
            await Task.Delay(6500);
            Assert.Equal("SCHEDULED", (await Documents()).Single(x => x.MaPhieuKho == id).TrangThai);
            Assert.Equal(stock.TonThucTe, (await Stock()).Single(x => x.MaNguyenLieu == stock.MaNguyenLieu).TonThucTe);
            Assert.Equal("0", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.BienDongKho b JOIN dbo.ChiTietPhieuKho l ON l.MaDongPhieuKho=b.MaDongPhieuKho WHERE l.MaPhieuKho={id}"));
        }
        finally { await fixture.Api.Sql("REVOKE INSERT ON OBJECT::dbo.NhatKyHeThong FROM fourchang_app"); }
        await WaitForState(id, "POSTED");
        Assert.Equal(stock.TonThucTe + 3, (await Stock()).Single(x => x.MaNguyenLieu == stock.MaNguyenLieu).TonThucTe);
    }

    private async Task<StockDocumentView> WaitForState(long id, string state)
    {
        var until = DateTime.UtcNow.AddSeconds(25);
        while (DateTime.UtcNow < until)
        {
            var row = (await Documents()).Single(x => x.MaPhieuKho == id);
            if (row.TrangThai == state) return row;
            await Task.Delay(150);
        }
        throw new TimeoutException($"Phiếu {id} chưa chuyển sang {state}.");
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

