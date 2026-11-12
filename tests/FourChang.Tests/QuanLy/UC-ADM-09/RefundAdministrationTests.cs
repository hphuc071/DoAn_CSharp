using System.Text.Json;
using FourChang.Tests.Shared;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Tests.Admin;

/// <summary>UC-ADM-09 kiểm tra hạn mức và đối soát thủ công đã chốt, không giả API ngân hàng đã xác thực.</summary>
[Collection("AdminFeatures"), Trait("UseCase", "UC-ADM-09"), Trait("Actor", "QuanLy")]
public sealed class RefundAdministrationTests(AdminFeatureFixture fixture)
{
    /// <summary>Nhập thông tin chi chưa tăng R; cần đối chiếu thật, lệnh lặp không hoàn hai lần và không hồi kho.</summary>
    [Fact]
    public async Task Duyet_DangChi_DoiChieuThuCong_MoiTinhDaHoan()
    {
        var (scenario, refund) = await Prepare();
        var stock = await fixture.Api.Sql("SELECT SUM(TonThucTe) FROM dbo.TonKho");
        await Approve(refund);
        refund = await Refund(refund.MaHoanTien);
        var begin = new RefundAttemptInput(refund.YeuCau, "OUT-" + Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddMinutes(-1), "Phiếu giao dịch TEST", true, null, refund.PhienBanDuLieu, Guid.NewGuid());
        await fixture.Send("POST", $"/api/admin/refunds/{refund.MaHoanTien}/attempts", begin);
        refund = await Refund(refund.MaHoanTien); Assert.Equal("PROCESSING", refund.TrangThai);
        var detail = await Detail(refund.MaHoanTien); Assert.Equal(0, detail.TaiChinh.DaHoan); Assert.Equal(refund.YeuCau, detail.TaiChinh.DangGiuHanMuc);
        var attempt = Assert.Single(refund.LanChi);
        var verify = new RefundVerificationInput("SUCCEEDED", "Sao kê TEST đã khớp người nhận, chiều OUT và số tiền", false, "QUAN-TEST", null, attempt.PhienBanDuLieu, Guid.NewGuid());
        await fixture.Send("POST", $"/api/admin/refunds/attempts/{attempt.MaLanChi}/verify", verify, 400);
        verify = verify with { QuanLyDaDoiChieu = true, MaThaoTac = Guid.NewGuid() };
        await fixture.Send("POST", $"/api/admin/refunds/attempts/{attempt.MaLanChi}/verify", verify);
        await fixture.Send("POST", $"/api/admin/refunds/attempts/{attempt.MaLanChi}/verify", verify);
        detail = await Detail(refund.MaHoanTien); Assert.Equal("REFUNDED", detail.HoSo.TrangThai); Assert.Equal(scenario.Total / 2, detail.TaiChinh.DaHoan);
        Assert.Equal(0, detail.TaiChinh.DangGiuHanMuc); Assert.Equal(stock, await fixture.Api.Sql("SELECT SUM(TonThucTe) FROM dbo.TonKho"));
        Assert.Equal("1", await fixture.Api.Sql($"SELECT COUNT(*) FROM dbo.GiaoDichNganHang WHERE MaLanChiHoan={attempt.MaLanChi} AND ChieuGiaoDich='OUT'"));
        Assert.Equal("0", await fixture.Api.Sql("SELECT COUNT(*) FROM dbo.NhatKyHeThong WHERE DuLieuSauJson LIKE '%111122223333%' OR DuLieuTruocJson LIKE '%111122223333%'"));
    }
    /// <summary>Hạn mức đang duyệt ngăn hồ sơ thứ hai vượt thu; kết quả chưa rõ giữ hạn mức và chặn chi lại.</summary>
    [Fact]
    public async Task HaiHoSo_KhongDuyetVuotThu_ChuaRoKhongChiLai()
    {
        var (scenario, refund) = await Prepare(); await Approve(refund); refund = await Refund(refund.MaHoanTien);
        var duplicate = Request(scenario, refund.MaHuyMon!.Value);
        var created = await fixture.Send("POST", "/api/admin/refunds", duplicate);
        var other = await Refund(created.Number("ma"));
        await fixture.Send("POST", $"/api/admin/refunds/{other.MaHoanTien}/decision", new RefundDecisionInput(true, other.YeuCau, "Không vượt khoản thu", true, other.PhienBanDuLieu, Guid.NewGuid()), 409);
        await fixture.Send("POST", $"/api/admin/refunds/{refund.MaHoanTien}/attempts", new RefundAttemptInput(refund.YeuCau, "OUT-" + Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddMinutes(-1), "Giao dịch chưa rõ", true, null, refund.PhienBanDuLieu, Guid.NewGuid()));
        refund = await Refund(refund.MaHoanTien); var attempt = Assert.Single(refund.LanChi);
        await fixture.Send("POST", $"/api/admin/refunds/attempts/{attempt.MaLanChi}/verify", new RefundVerificationInput("RECONCILING", "Đang yêu cầu ngân hàng đối chiếu", false, null, null, attempt.PhienBanDuLieu, Guid.NewGuid()));
        refund = await Refund(refund.MaHoanTien);
        await fixture.Send("POST", $"/api/admin/refunds/{refund.MaHoanTien}/attempts", new RefundAttemptInput(refund.YeuCau, "NEW-" + Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddMinutes(-1), "Không chi lại khi chưa rõ", true, null, refund.PhienBanDuLieu, Guid.NewGuid()), 409);
        Assert.Equal(0, (await Detail(refund.MaHoanTien)).TaiChinh.DaHoan);
    }
    /// <summary>Chuẩn bị khoản thu thật trong fixture và hủy hợp lệ để có nghĩa vụ hoàn có nguồn.</summary>
    private async Task<(OperationsScenario, RefundView)> Prepare()
    {
        var scenario = await OperationsScenarioSeed.Create(fixture, "ACCEPTED");
        var order = (await fixture.Send("GET", $"/api/admin/orders/{scenario.Order}")).Deserialize<AdminOrderDetail>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        await fixture.Send("POST", $"/api/admin/orders/{scenario.Order}/actions", new OrderActionInput("CANCEL", "Khách đổi ý trước pha", order.Don.PhienBanDuLieu, Guid.NewGuid(), scenario.Line, 1));
        var record = (await All()).Single(x => x.MaDonHang == scenario.Order);
        await fixture.Send("PUT", $"/api/admin/refunds/{record.MaHoanTien}", Request(scenario, record.MaHuyMon!.Value) with { PhienBanDuLieu = record.PhienBanDuLieu });
        return (scenario, await Refund(record.MaHoanTien));
    }
    /// <summary>Tài khoản giả lập để kiểm tra mã hóa/che; không phải dữ liệu ngân hàng thực.</summary>
    private static RefundRequestInput Request(OperationsScenario s, long cancellation) => new(s.Order, s.Payment, cancellation, "CANCELLATION", s.Total / 2,
        "BANK_TRANSFER", "Nguyễn Tuấn Kiệt", "Ngân hàng kiểm thử", "111122223333", "Khách yêu cầu hoàn dòng hủy", "Biên bản hủy TEST", null, Guid.NewGuid());
    /// <summary>Duyệt bằng phiên Admin thật và xác nhận đã đối chiếu người nhận.</summary>
    private Task<System.Text.Json.Nodes.JsonNode> Approve(RefundView r) => fixture.Send("POST", $"/api/admin/refunds/{r.MaHoanTien}/decision", new RefundDecisionInput(true, r.YeuCau, "Đủ chứng cứ", true, r.PhienBanDuLieu, Guid.NewGuid()));
    /// <summary>Đọc danh sách typed như WinForms để kiểm tra trạng thái sau commit.</summary>
    private async Task<List<RefundView>> All() => (await fixture.Send("GET", "/api/admin/refunds")).Deserialize<List<RefundView>>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    /// <summary>Tìm hồ sơ theo khóa, không theo thứ tự dòng trong danh sách.</summary>
    private async Task<RefundView> Refund(long id) => (await All()).Single(x => x.MaHoanTien == id);
    /// <summary>Đọc số dư và hồ sơ cùng hợp đồng với màn hình chi tiết.</summary>
    private async Task<RefundDetail> Detail(long id) => (await fixture.Send("GET", $"/api/admin/refunds/{id}")).Deserialize<RefundDetail>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
}
