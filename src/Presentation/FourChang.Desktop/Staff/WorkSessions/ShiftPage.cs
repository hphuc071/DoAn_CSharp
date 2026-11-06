using FourChang.Desktop.Admin.Employees;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Staff.WorkSessions;

/// <summary>POS-01 giữ chốt ca trực tiếp và hỗ trợ Gửi đối chiếu đã được chấp thuận cho ADM-13.</summary>
public static class ShiftPage
{
    /// <summary>Tạo form số thực đếm không gán 0 khi chưa nhập; số đếm gửi Admin phải được nhân viên xác nhận.</summary>
    public static Control Create()
    {
        var session = LiveSession.Current;
        var amount = new NumericUpDown { Minimum = 0, Maximum = 999999999999999999m, ThousandsSeparator = true, Width = 300, Font = CafeTheme.Body() };
        var entered = new CheckBox { Text = "Tôi đã kiểm đếm tiền mặt thực tế", AutoSize = true, Font = CafeTheme.Body() };
        var reason = Ui.Input("Lý do nếu chênh lệch", "", 480); reason.MaxLength = 500;
        var error = Ui.Label("", 9, false, CafeTheme.Danger);
        var summary = Ui.Label("Đang tải đối chiếu…", 11);
        var close = Ui.Button("Kết thúc ca", () => { }, true);
        var submit = Ui.Button("Gửi đối chiếu", () => { });
        var reload = Ui.Button("Tải lại", () => { });
        var panel = Ui.Panel("Ca làm việc", Ui.Label($"Nhân viên: {session?.Name}"), Ui.Label($"Mã ca: {session?.ShiftId}"), summary,
            Ui.Field("Tiền mặt thực đếm", amount), entered, Ui.Field("Lý do chênh lệch", reason), error, Ui.Row(close, submit, reload));
        WorkSessionDetails? current = null;
        SubmitReconciliation? pending = null;
        bool running = false;

        // Tải lại số tiền và trạng thái chuẩn; khi ca khóa chỉ được xem, không phát sinh giao dịch mới.
        async Task Load()
        {
            try
            {
                current = await LiveSession.Api.CurrentReconciliationAsync();
                if (panel.IsDisposed) return;
                summary.Text = $"{current.Ca.TenCa} · {WorkforceUi.Status(current.Ca.TrangThai)}\nĐầu ca: {current.Ca.TienMatDauCa:N0}đ · Thu CASH: {current.ThuTienMat:N0}đ · Hoàn CASH: {current.HoanTienMat:N0}đ\nDự kiến: {current.TienMatDuKienHienTai:N0}đ · Khoản chưa rõ: {current.KhoanChuaRo.Count}";
                var open = current.Ca.TrangThai == "OPEN";
                close.Enabled = submit.Enabled = amount.Enabled = reason.Enabled = entered.Enabled = open && !running;
                if (current.Ca.TienMatThucDem is { } counted) { amount.Value = counted; entered.Checked = true; reason.Text = current.Ca.LyDoChenhLech ?? ""; }
                if (!open) error.Text = "Ca đã khóa hoặc kết thúc. Quản lý kiểm tra tại Ca làm việc; không tiếp tục bán bằng ca này.";
            }
            catch (CafeApiException ex) { if (!error.IsDisposed) { error.Text = "Chưa tải được đối chiếu mới. " + ex.Message; close.Enabled = submit.Enabled = false; } }
        }

        // Giữ nguyên mã/nội dung sau timeout; receipt đọc bằng đúng máy/token ca kể cả khi đã khóa.
        async Task Send()
        {
            if (running || current is null) return;
            if (!entered.Checked) { error.Text = "Xác nhận đã kiểm đếm trước khi gửi."; return; }
            pending ??= new(amount.Value, reason.Text, current.Ca.PhienBanDuLieu, Guid.NewGuid());
            running = true; close.Enabled = submit.Enabled = reload.Enabled = amount.Enabled = reason.Enabled = false;
            try
            {
                await WorkforceUi.Recover(() => LiveSession.Api.SubmitReconciliationAsync(pending), () => LiveSession.Api.ReconciliationReceiptAsync(pending.MaThaoTac));
                pending = null; error.Text = "Đã gửi đối chiếu. Ca đang khóa chờ Quản lý xác nhận.";
                running = false; await Load();
            }
            catch (CafeApiException ex)
            {
                error.Text = ex.Message;
                if (ex.StatusCode is not null) pending = null;
                submit.Enabled = true; close.Enabled = pending is null; amount.Enabled = reason.Enabled = pending is null;
                submit.Text = pending is null ? "Gửi đối chiếu" : "Thử lại yêu cầu";
            }
            finally { running = false; if (!reload.IsDisposed) reload.Enabled = pending is null; }
        }

        panel.HandleCreated += async (_, _) => await Load();
        reload.Click += async (_, _) => await Load();
        submit.Click += async (_, _) => await Send();
        close.Click += async (_, _) =>
        {
            if (running) return;
            if (!entered.Checked) { error.Text = "Xác nhận đã kiểm đếm trước khi chốt."; return; }
            running = true; close.Enabled = submit.Enabled = false;
            try
            {
                await LiveSession.Api.CloseShiftAsync(new(amount.Value, reason.Text));
                LiveSession.Api.Token = null; LiveSession.Current = null; panel.FindForm()?.Close();
            }
            catch (CafeApiException ex) { error.Text = ex.Message; close.Enabled = submit.Enabled = true; }
            finally { running = false; }
        };
        return panel;
    }
}
