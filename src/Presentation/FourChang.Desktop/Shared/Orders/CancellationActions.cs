using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Presentation.Shared.Contracts.Orders;

namespace FourChang.Desktop.Shared.Orders;

public partial class OrdersWorkspacePage
{
    private readonly Dictionary<string, CommandDraft<CancellationCommand>> cancellationDrafts = [];
    private readonly Dictionary<long, CommandDraft<CounterRefundCommand>> refundDrafts = [];

    /// <summary>Hiện đúng lệnh cho vai trò: Staff chuyển, Admin quyết định; không hiển thị tiếp nhận/pha khi chờ duyệt.</summary>
    private void AddCancellationActions()
    {
        for (var index = 0; index < orderActions.Count; index++)
        {
            var action = orderActions[index];
            orderActions[index] = action with { Available = row => row.YeuCauHuy is null && action.Available(row) };
        }
        orderActions.AddRange([
            new("FORWARD", "Gửi yêu cầu duyệt", () => Cancellation("FORWARD"), row => staff && row.YeuCauHuy?.TrangThai == "REQUESTED"),
            new("APPROVE", "Duyệt hủy / hoàn tại quầy", () => Cancellation("APPROVE"), row => !staff && row.YeuCauHuy?.TrangThai == "FORWARDED"),
            new("REJECT", "Từ chối yêu cầu hủy", () => Cancellation("REJECT"), row => !staff && row.YeuCauHuy?.TrangThai == "FORWARDED"),
            new("COUNTER_REFUND", "Hoàn tiền tại quầy", CounterRefund, row => staff && counterOnly && row.TrangThai == "CANCELLED" && row.TaiChinh.PhaiHoan > 0)
        ]);
    }

    /// <summary>Hiện lý do khách gửi và xác nhận của người xử lý, giữ nguyên mã/payload nếu cần thử lại sau mất mạng.</summary>
    private async Task Cancellation(string action)
    {
        var selected = Selected();
        if (selected is null) return;
        var detail = await ReadOrder(selected.MaDonHang);
        if (detail.Don.YeuCauHuy is not { } pending) { Message.Text = "Yêu cầu đã thay đổi. Hãy tải lại."; return; }
        var key = selected.MaDonHang + ":" + pending.MaNhomHuy + ":" + action;
        if (!cancellationDrafts.TryGetValue(key, out var draft)) cancellationDrafts[key] = draft = new();
        var title = action switch { "FORWARD" => "Chuyển yêu cầu cho Quản lý", "APPROVE" => "Duyệt hủy và hoàn tiền tại quầy", _ => "Từ chối yêu cầu hủy" };
        using var form = new AdminCommandDialog<CancellationCommand>(title, draft);
        var customerReason = form.TextField("Lý do khách yêu cầu", pending.LyDo); customerReason.ReadOnly = true;
        var reason = form.TextField("Ghi chú / lý do xử lý (bắt buộc)");
        var verified = form.CheckField("Đã kiểm tra người nhận; duyệt tiền mặt để khách nhận tại quầy");
        verified.Visible = action == "APPROVE";
        form.Submit(op => new(action, pending.MaNhomHuy, reason.Text, detail.Don.PhienBanDuLieu, op, verified.Checked),
            async input => { await LiveSession.Api.CancellationActionAsync(selected.MaDonHang, input, staff); },
            async op => { await LiveSession.Api.CancellationReceiptAsync(op, staff); });
        if (form.ShowDialog(FindForm()) == DialogResult.OK) await Reload();
    }

    /// <summary>Nhân viên đối chiếu người nhận và giao tiền thật; không nhập số tiền tùy ý hoặc tự duyệt hồ sơ.</summary>
    private async Task CounterRefund()
    {
        var selected = Selected(); if (selected is null) return;
        var refunds = await LiveSession.Api.CounterRefundsAsync(selected.MaDonHang);
        if (refunds.Count == 0) { Message.Text = "Chưa có khoản tiền mặt được duyệt còn có thể chi."; return; }
        // Một đơn có thể có nhiều khoản thu. Mỗi hồ sơ có biên nhận chi riêng và hạn mức riêng.
        var record = refunds[0];
        if (!refundDrafts.TryGetValue(record.MaHoanTien, out var draft)) refundDrafts[record.MaHoanTien] = draft = new();
        using var form = new AdminCommandDialog<CounterRefundCommand>($"Hoàn tại quầy · {record.MaHoan} · {record.SoTien:N0}đ", draft);
        var expected = form.TextField("Người nhận được duyệt", record.NguoiNhan); expected.ReadOnly = true;
        var recipient = form.TextField("Họ tên khách thực nhận tiền");
        var evidence = form.TextField("Biên nhận / xác nhận nhận tiền (bắt buộc)");
        var delivered = form.CheckField($"Đã đối chiếu khách và giao đủ {record.SoTien:N0}đ tiền mặt, khách đã nhận");
        form.Submit(op => new(recipient.Text, evidence.Text, delivered.Checked, record.PhienBanDuLieu, op),
            async input => { await LiveSession.Api.PayCounterRefundAsync(record.MaHoanTien, input); },
            async op => { await LiveSession.Api.CounterRefundReceiptAsync(op); });
        if (form.ShowDialog(FindForm()) == DialogResult.OK) await Reload();
    }
}
