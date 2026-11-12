using System.Text;
using System.Text.Json;
using FourChang.Tests.Shared;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Tests.Admin;

/// <summary>UC-ADM-08: snapshot bất biến, điều chỉnh duy nhất và PDF đọc được tiếng Việt.</summary>
[Collection("AdminFeatures"), Trait("UseCase", "UC-ADM-08"), Trait("Actor", "QuanLy")]
public sealed class InvoiceAdministrationTests(AdminFeatureFixture fixture)
{
    /// <summary>Giá menu đổi không đổi hóa đơn; xem PDF không phát hành thêm hoặc tăng thu.</summary>
    [Fact]
    public async Task SnapshotBatBien_PdfKhongPhatHanhLai()
    {
        var scenario = await OperationsScenarioSeed.Create(fixture, "ACCEPTED", invoice: true); var id = scenario.Invoice!.Value;
        var before = await Detail(id);
        var raw = (await fixture.Api.Send("GET", $"/api/admin/invoices/{id}/pdf", headers: fixture.Admin)).Expect(200, "Xuất PDF snapshot");
        Assert.StartsWith("%PDF-", raw.Content);
        Assert.Equal(before.SnapshotJson, (await Detail(id)).SnapshotJson);
        Assert.Equal("1", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.HoaDon WHERE MaDonHang={scenario.Order}"));
        Assert.Equal(scenario.Total, (await Detail(id)).TaiChinh.DaThu);
    }
    /// <summary>Hủy tạo điều chỉnh âm đúng nguồn, hai mã thao tác cho cùng nguồn vẫn chỉ có một chứng từ.</summary>
    [Fact]
    public async Task DieuChinhCoNguon_KhongTrung_KhongSuaBanGoc()
    {
        var scenario = await OperationsScenarioSeed.Create(fixture, "ACCEPTED", invoice: true); var id = scenario.Invoice!.Value;
        var original = await Detail(id);
        var order = (await fixture.Send("GET", $"/api/admin/orders/{scenario.Order}")).Deserialize<AdminOrderDetail>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        await fixture.Send("POST", $"/api/admin/orders/{scenario.Order}/actions", new OrderActionInput("CANCEL", "Khách đổi ý một ly", order.Don.PhienBanDuLieu, Guid.NewGuid(), scenario.Line, 1));
        var source = long.Parse(await fixture.Api.Sql($"SELECT TOP(1) MaHuyMon FROM dbo.HuyMon WHERE MaChiTietDonHang={scenario.Line}"));
        var input = new InvoiceAdjustmentInput(source, "Điều chỉnh dòng đã hủy", original.HoaDon.PhienBanDuLieu, Guid.NewGuid());
        var receipt = await fixture.Send("POST", $"/api/admin/invoices/{id}/adjustments", input);
        var repeated = await fixture.Send("POST", $"/api/admin/invoices/{id}/adjustments", input with { MaThaoTac = Guid.NewGuid() });
        Assert.Equal(receipt.Number("ma"), repeated.Number("ma"));
        var current = await Detail(id); Assert.Equal(original.SnapshotJson, current.SnapshotJson); Assert.Equal(original.HoaDon.SoTien, current.HoaDon.SoTien);
        Assert.Single(current.DieuChinh); Assert.Equal(-scenario.Total / 2, current.DieuChinh[0].SoTien);
        Assert.Equal(scenario.Total / 2, current.TaiChinh.NghiaVu);
    }
    /// <summary>Khiếu nại đã duyệt mới được điều chỉnh; giảm nghĩa vụ một lần, chưa chi không tăng số đã hoàn.</summary>
    [Fact]
    public async Task KhieuNai_DuyetMoiDieuChinh_KhongGiamNghiaVuHaiLan()
    {
        var s = await OperationsScenarioSeed.Create(fixture, "COMPLETED", invoice: true);
        var request = new RefundRequestInput(s.Order, s.Payment, null, "COMPLAINT", 5000, "BANK_TRANSFER", "Nguyễn Tuấn Kiệt", "Ngân hàng TEST", "111122223333", "Khách phản ánh chất lượng", "Biên bản khách đồng ý giảm giá", null, Guid.NewGuid());
        var saved = await fixture.Send("POST", "/api/admin/refunds", request);
        var id = saved.Number("ma");
        var detail = (await fixture.Send("GET", $"/api/admin/refunds/{id}")).Deserialize<RefundDetail>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var invoice = await Detail(s.Invoice!.Value);
        var adjust = new InvoiceAdjustmentInput(null, "Điều chỉnh theo khiếu nại", invoice.HoaDon.PhienBanDuLieu, Guid.NewGuid(), id);
        await fixture.Send("POST", $"/api/admin/invoices/{s.Invoice}/adjustments", adjust, 409);
        await fixture.Send("POST", $"/api/admin/refunds/{id}/decision", new RefundDecisionInput(true, 5000, "Đã kiểm tra nguồn và người nhận", true, detail.HoSo.PhienBanDuLieu, Guid.NewGuid()));
        await fixture.Send("POST", $"/api/admin/invoices/{s.Invoice}/adjustments", adjust with { MaThaoTac=Guid.NewGuid() });
        invoice = await Detail(s.Invoice.Value);
        Assert.Equal(s.Total-5000, invoice.TaiChinh.NghiaVu);
        Assert.Equal(0, invoice.TaiChinh.DaHoan);
        Assert.Equal(id, Assert.Single(invoice.DieuChinh).MaHoanTien);
        detail=(await fixture.Send("GET", $"/api/admin/refunds/{id}")).Deserialize<RefundDetail>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        await fixture.Send("POST", $"/api/admin/refunds/{id}/attempts", new RefundAttemptInput(5000, "OUT-"+Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddMinutes(-1), "Lần chi bị ngân hàng từ chối", true, null, detail.HoSo.PhienBanDuLieu, Guid.NewGuid()));
        detail=(await fixture.Send("GET", $"/api/admin/refunds/{id}")).Deserialize<RefundDetail>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var attempt=Assert.Single(detail.HoSo.LanChi);
        await fixture.Send("POST", $"/api/admin/refunds/attempts/{attempt.MaLanChi}/verify", new RefundVerificationInput("FAILED", "Sao kê xác nhận không chi thành công", true, null, "Tài khoản nhận chưa hợp lệ", attempt.PhienBanDuLieu, Guid.NewGuid()));
        invoice=await Detail(s.Invoice.Value);
        Assert.Equal(s.Total-5000, invoice.TaiChinh.NghiaVu);
        Assert.Equal(0, invoice.TaiChinh.DaHoan);
        Assert.Equal(0, invoice.TaiChinh.DangGiuHanMuc);
        detail=(await fixture.Send("GET", $"/api/admin/refunds/{id}")).Deserialize<RefundDetail>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        await fixture.Send("POST", $"/api/admin/refunds/{id}/decision", new RefundDecisionInput(true, 5000, "Duyệt lại khoản phải hoàn còn nguyên", true, detail.HoSo.PhienBanDuLieu, Guid.NewGuid()));
        Assert.Equal(s.Total-5000, (await Detail(s.Invoice.Value)).TaiChinh.NghiaVu);

    }

    /// <summary>Đọc chi tiết qua API bảo vệ để kiểm tra cùng hợp đồng của UI.</summary>
    private async Task<AdminInvoiceDetail> Detail(long id) => (await fixture.Send("GET", $"/api/admin/invoices/{id}")).Deserialize<AdminInvoiceDetail>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
}
