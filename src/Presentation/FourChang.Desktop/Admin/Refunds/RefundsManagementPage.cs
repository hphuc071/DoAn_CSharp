using FourChang.Desktop.Admin.Orders;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Admin.Refunds;

/// <summary>UC-ADM-09: duyệt, đang chi và xác minh là ba thao tác độc lập; UI không tự coi nhập chứng cứ là đã hoàn.</summary>
public sealed class RefundsManagementPage : AdminWorkspacePage<RefundView>
{
    private readonly Dictionary<string, object> drafts = [];
    /// <summary>Danh sách có nhãn nguồn và trạng thái rõ, tài khoản luôn đã che.</summary>
    public RefundsManagementPage() : base("Đối soát và hoàn tiền", "Yêu cầu → Duyệt hạn mức → Ghi lần chi → Đối chiếu sao kê → Kết quả", true, "TÀI CHÍNH / HOÀN TIỀN")
    {
        Column("MaHoan", "Mã hồ sơ", 140); Column("MaDonHang", "Đơn", 50); Column("LoaiNguon", "Nguồn", 100); Column("YeuCau", "Yêu cầu", 85);
        Column("DuocDuyet", "Đã duyệt", 85); Column("TrangThai", "Trạng thái", 110); Column("PhuongThuc", "Phương thức", 100); Column("NguoiNhan", "Người nhận", 120); Column("TaiKhoanDaChe", "Tài khoản", 80);
        AddAction(Actions, "Lập hồ sơ", () => Edit(null), true); AddAction(Actions, "Bổ sung hồ sơ", () => Edit(Selected())); AddAction(Actions, "Chi tiết / lịch sử", Details);
        AddAction(Actions, "Duyệt hạn mức", () => Decide(true)); AddAction(Actions, "Từ chối / bổ sung", () => Decide(false));
        AddAction(Actions, "Ghi lần chi", Begin); AddAction(Actions, "Đối chiếu sao kê", Verify);
        AddAction(Actions, "Đối soát tiền vào", Incoming);
        OperationsPresentation.FitRows(Filters); OperationsPresentation.FitRows(Actions); OperationsPresentation.Grid(Grid);
    }
    /// <summary>Đọc DTO có dữ liệu ngân hàng đã che; không sử dụng entity chứa ciphertext.</summary>
    protected override Task<IReadOnlyList<RefundView>> Fetch() => LiveSession.Api.GetAdminRefundsAsync();
    /// <summary>Tìm theo mã hồ sơ/đơn, nguồn, người nhận và trạng thái.</summary>
    protected override bool Match(RefundView item, string query) => query.Length == 0 || $"{item.MaHoan} {item.MaDonHang} {item.LoaiNguon} {item.NguoiNhan} {item.TrangThai}".Contains(query, StringComparison.CurrentCultureIgnoreCase);
    /// <summary>Mỗi hồ sơ/hành động giữ request riêng để không gửi payload cũ cho một hồ sơ khác.</summary>
    private CommandDraft<T> Draft<T>(string key) where T : class
    { if (!drafts.TryGetValue(key, out var value)) drafts[key] = value = new CommandDraft<T>(); return (CommandDraft<T>)value; }

    /// <summary>Lập/bổ sung nguồn hoàn, tài khoản và chứng cứ; nhập lại tài khoản khi bổ sung để đối chiếu đúng người nhận.</summary>
    private async Task Edit(RefundView? selected)
    {
        using var form = new AdminCommandDialog<RefundRequestInput>(selected is null ? "Lập hồ sơ hoàn" : "Bổ sung " + selected.MaHoan, Draft<RefundRequestInput>("edit:" + selected?.MaHoanTien));
        var order = form.NumberField("Mã đơn", selected?.MaDonHang ?? 0); var payment = form.NumberField("Mã khoản thu gốc", selected?.MaThanhToan ?? 0);
        var source = form.ChoiceField("Nguồn hoàn", "CANCELLATION", "OVERPAYMENT", "LATE_PAYMENT", "COMPLAINT");
        if (selected is not null) source.SelectedIndex = Math.Max(0, Array.IndexOf(new[] { "CANCELLATION", "OVERPAYMENT", "LATE_PAYMENT", "COMPLAINT" }, selected.LoaiNguon));
        var cancel = form.NumberField("Mã hủy món (0 nếu không có)", selected?.MaHuyMon ?? 0); var amount = form.NumberField("Số tiền yêu cầu (đ)", selected?.YeuCau ?? 0);
        var method = form.ChoiceField("Phương thức hoàn", "BANK_TRANSFER", "CASH"); method.SelectedIndex = selected?.PhuongThuc == "CASH" ? 1 : 0;
        var recipient = form.TextField("Họ tên người nhận", selected?.NguoiNhan, 120); var bank = form.TextField("Ngân hàng nhận", selected?.NganHang, 100);
        var account = form.TextField("Số tài khoản nhận — không ghi vào nhật ký", max: 50);
        var reason = form.TextField("Lý do", selected?.LyDo); var evidence = form.TextField("Tham chiếu chứng cứ", selected?.ChungCu);
        form.Submit(op => new((long)order.Value, (long)payment.Value, cancel.Value == 0 ? null : (long)cancel.Value, source.Text, amount.Value, method.Text,
            recipient.Text, bank.Text, account.Text, reason.Text, evidence.Text, selected?.PhienBanDuLieu, op),
            async input => { await LiveSession.Api.SaveRefundAsync(selected?.MaHoanTien, input); }, async op => { await LiveSession.Api.RefundReceiptAsync(op); });
        if (form.ShowDialog(FindForm()) == DialogResult.OK) await Reload();
    }
    /// <summary>Cho người duyệt nhìn rõ tiền gốc, tiền đã chi và hạn mức trước khi quyết định.</summary>
    private async Task Details()
    {
        var selected = Selected(); if (selected is null) return;
        var data = await LiveSession.Api.GetAdminRefundAsync(selected.MaHoanTien);
        using var form = AdminUi.Dialog("Hồ sơ " + selected.MaHoan, 1100, 760);
        form.Controls.Add(Ui.Panel(selected.MaHoan, Ui.Label($"Khoản thu gốc: {data.ThuGoc:N0}đ · Đã chi từ khoản thu: {data.DaChiTuKhoanThu:N0}đ · Đang giữ: {data.TaiChinh.DangGiuHanMuc:N0}đ"),
            Ui.Label($"{selected.NguoiNhan} · {selected.NganHang} · {selected.TaiKhoanDaChe}"), Ui.Label(selected.LyDo),
            Ui.Tabs(("Từng lần chi", OrdersManagementPage.DataGrid(selected.LanChi)), ("Lịch sử duyệt/chi", OrdersManagementPage.DataGrid(data.LichSu)))));
        form.ShowDialog(FindForm());
    }
    /// <summary>Duyệt giữ hạn mức hoặc từ chối có lý do; không thực hiện chuyển khoản ở thao tác này.</summary>
    private async Task Decide(bool approve)
    {
        var selected = Selected(); if (selected is null) return;
        using var form = new AdminCommandDialog<RefundDecisionInput>((approve ? "Duyệt " : "Từ chối ") + selected.MaHoan, Draft<RefundDecisionInput>("decision:" + selected.MaHoanTien + ":" + approve));
        var amount = form.NumberField("Số tiền duyệt", selected.YeuCau); var checkedRecipient = form.CheckField("Đã đối chiếu nguồn hoàn và đúng người nhận/tài khoản");
        var reason = form.TextField("Lý do quyết định");
        form.Submit(op => new(approve, amount.Value, reason.Text, checkedRecipient.Checked, selected.PhienBanDuLieu, op),
            async input => { await LiveSession.Api.DecideRefundAsync(selected.MaHoanTien, input); }, async op => { await LiveSession.Api.RefundReceiptAsync(op); });
        if (form.ShowDialog(FindForm()) == DialogResult.OK) await Reload();
    }
    /// <summary>Ghi thông tin chi sau khi Admin chuyển khoản/chi tiền, trạng thái vẫn đang xử lý chờ đối chiếu.</summary>
    private async Task Begin()
    {
        var selected = Selected(); if (selected is null) return;
        using var form = new AdminCommandDialog<RefundAttemptInput>("Ghi lần chi — chưa xác minh thành công", Draft<RefundAttemptInput>("begin:" + selected.MaHoanTien));
        var amount = form.NumberField("Số tiền chi", Math.Max(0, (selected.DuocDuyet ?? 0) - selected.LanChi.Where(x => x.TrangThai == "SUCCEEDED").Sum(x => x.SoTien)));
        var reference = form.TextField("Mã giao dịch chi", max: 150); var at = form.TextField("Thời điểm chi (ISO có múi giờ)", DateTimeOffset.Now.ToString("O"));
        var evidence = form.TextField("Tham chiếu chứng từ chi"); var check = form.CheckField("Đã kiểm tra đúng người nhận, số tiền và tài khoản");
        var shift = form.NumberField("Mã ca chi tiền mặt (0 nếu chuyển khoản)");
        form.Submit(op => new(amount.Value, reference.Text, DateTimeOffset.Parse(at.Text, System.Globalization.CultureInfo.InvariantCulture).UtcDateTime, evidence.Text, check.Checked,
            shift.Value == 0 ? null : (long)shift.Value, selected.PhienBanDuLieu, op), async input => { await LiveSession.Api.BeginRefundAsync(selected.MaHoanTien, input); }, async op => { await LiveSession.Api.RefundReceiptAsync(op); });
        if (form.ShowDialog(FindForm()) == DialogResult.OK) await Reload();
    }
    /// <summary>Bước xác minh riêng yêu cầu Admin đối chiếu sao kê, mã chi, tiền và người nhận; chưa rõ không cho chi lại.</summary>
    private async Task Verify()
    {
        var selected = Selected(); if (selected is null) return;
        var attempts = selected.LanChi.Where(x => x.TrangThai is "PENDING" or "RECONCILING").ToArray();
        if (attempts.Length == 0) { Message.Text = "Không có lần chi đang chờ đối soát."; return; }
        using var form = new AdminCommandDialog<RefundVerificationInput>("Đối chiếu sao kê / phiếu chi", Draft<RefundVerificationInput>("verify:" + selected.MaHoanTien));
        var attempt = form.ChoiceField("Lần chi", attempts.Select(x => $"Lần {x.Lan} · {x.SoTien:N0}đ · {x.MaGiaoDich}").ToArray());
        var status = form.ChoiceField("Kết quả đối chiếu", "RECONCILING", "SUCCEEDED", "FAILED");
        var evidence = form.TextField("Tham chiếu sao kê/phiếu chi đã đối chiếu"); var account = form.TextField("Định danh tài khoản quán trên sao kê", max: 100);
        var reason = form.TextField("Lý do thất bại (nếu có)"); var check = form.CheckField("Tôi đã đối chiếu chứng cứ thật: đúng giao dịch, tiền, chiều chi và người nhận");
        form.Submit(op => new(status.Text, evidence.Text, check.Checked, account.Text, reason.Text, attempts[attempt.SelectedIndex].PhienBanDuLieu, op),
            async input => { await LiveSession.Api.VerifyRefundAsync(attempts[attempt.SelectedIndex].MaLanChi, input); }, async op => { await LiveSession.Api.RefundReceiptAsync(op); });
        if (form.ShowDialog(FindForm()) == DialogResult.OK) await Reload();
    }

    /// <summary>Ghép sao kê với yêu cầu thu khi thiếu/thừa/muộn/sai tham chiếu; không mở lại đơn đã hết hạn.</summary>
    private async Task Incoming()
    {
        long id;
        using (var lookup = AdminUi.Dialog("Chọn yêu cầu thu chuyển khoản", 650, 340))
        {
            lookup.MinimumSize = new(580, 300); var input = Ui.Input("Mã khoản thu", Selected()?.MaThanhToan.ToString() ?? "", 480);
            var confirm = Ui.Button("Tra cứu", () => { if (long.TryParse(input.Text, out var value) && value > 0) lookup.DialogResult = DialogResult.OK; }, true);
            AdminUi.Compose(lookup, Ui.Panel("Khoản thu cần đối soát", Ui.Field("Mã thanh toán", input)), confirm, Ui.Button("Hủy", () => lookup.Close()));
            if (lookup.ShowDialog(FindForm()) != DialogResult.OK) return; id = long.Parse(input.Text);
        }
        var payment = await LiveSession.Api.GetIncomingPaymentAsync(id);
        using var form = new AdminCommandDialog<IncomingReconciliationInput>($"Đối soát khoản thu {id} · Đã xác minh {payment.SoTienDaXacMinh:N0}đ", Draft<IncomingReconciliationInput>("incoming:" + id));
        var provider = form.TextField("Ngân hàng / nguồn sao kê", max: 50); var account = form.TextField("Định danh tài khoản nhận của quán", max: 100);
        var reference = form.TextField("Mã giao dịch tiền vào", max: 150); var amount = form.NumberField("Số tiền thực nhận", Math.Max(0, payment.SoTienYeuCau - payment.SoTienDaXacMinh));
        var time = form.TextField("Thời điểm ngân hàng (ISO có múi giờ)", DateTimeOffset.Now.ToString("O"));
        var evidence = form.TextField("Tham chiếu sao kê"); var reason = form.TextField("Lý do ghép/đối soát");
        var check = form.CheckField("Đã đối chiếu nguồn sao kê đáng tin cậy: đúng tài khoản quán, chiều IN, tiền, người chuyển và đơn");
        form.Submit(op => new(id, provider.Text, account.Text, reference.Text, amount.Value, DateTimeOffset.Parse(time.Text, System.Globalization.CultureInfo.InvariantCulture).UtcDateTime,
            evidence.Text, reason.Text, check.Checked, payment.PhienBanDuLieu, op), async request => { await LiveSession.Api.ReconcileIncomingAsync(request); }, async op => { await LiveSession.Api.RefundReceiptAsync(op); });
        if (form.ShowDialog(FindForm()) == DialogResult.OK) await Reload();
    }
}
