using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Security;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Desktop.Shell;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Staff;

namespace FourChang.Desktop.Staff.WorkSessions;

/// <summary>POS-01: gửi báo cáo kiểm đếm và đóng ca; ca có ngoại lệ/phục hồi giữ nhánh đối chiếu với Admin.</summary>
public sealed class ShiftPage : UserControl
{
    private readonly Label summary = Ui.Label("Đang tải ca hiện tại…", 11, true);
    private readonly Label balances = Ui.Label("", 11);
    private readonly Label completedTotal = Ui.Label("Đang tải…", 18, true);
    private readonly Label completedCount = Ui.Label("", 14, true);
    private readonly Label expected = Ui.Label("", 14, true);
    private readonly Label difference = Ui.Label("", 12, true);
    private readonly Label message = Ui.Label("", 9, false, CafeTheme.Muted);
    private readonly NumericUpDown amount = new() { Minimum = 0, Maximum = 999999999999999999m, ThousandsSeparator = true, Width = 320 };
    private readonly Button submit;
    private readonly Button close;
    private readonly Button reload;
    private readonly Button newShift;
    private WorkSessionDetails? data;
    private bool busy;
    private bool connected;
    private SubmitReconciliation? pendingSubmit;
    private CloseShift? pendingClose;

    /// <summary>Không hỏi tài khoản nhân viên; tiền thực đếm do chính nhân viên nhập tại ca của token.</summary>
    public ShiftPage()
    {
        Name = "StaffShiftPage"; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; Dock = DockStyle.Top; BackColor = CafeTheme.Cream;
        completedTotal.Name="ShiftCompletedOrdersTotal"; completedCount.Name="ShiftCompletedOrdersCount"; amount.Name = "ShiftCountedCash"; expected.Name="ShiftExpectedCash"; difference.Name="ShiftCashDifference"; amount.ValueChanged += (_, _) => ShowReceived();
        submit = Action("Gửi đối chiếu / bàn giao", Submit); submit.Name = "SubmitShiftReconciliation";
        close = Action("Gửi kết quả & kết thúc ca", CloseShift, true); close.Name = "CloseStaffShift";
        reload = Action("Tải lại", LoadCurrent);
        newShift = Ui.Button("Mở ca mới", () => { ShiftSessionFile.Clear(); LiveSession.Current = null; LiveSession.Api.Token = null; (FindForm() as MainForm)?.RequestReauthentication(); });
        Controls.Add(Ui.Panel("Ca làm việc của tôi", summary, balances, Ui.Label("Tổng dưới đây chỉ tính các đơn đã hoàn thành. Kiểm đếm tiền cuối ca theo số hệ thống trước khi kết ca.", 9, false, CafeTheme.Muted),
            Ui.Row(Ui.Field("Tổng tiền đơn đã hoàn thành trong ca", completedTotal), Ui.Field("Số đơn hoàn thành", completedCount)),
            Ui.Field("Tiền mặt cuối ca cần kiểm đếm", expected),
            Ui.Row(Ui.Field("Tiền mặt nhân viên kiểm đếm (đồng)", amount), Ui.Field("Chênh lệch", difference)), message,
            Ui.Row(close, submit, reload, newShift)));
        HandleCreated += async (_, _) => { busy = true; ApplyState(); try { await LoadCurrent(); } finally { busy = false; ApplyState(); } };
    }
    /// <summary>Giữ điểm gọi của router hiện tại.</summary>
    public static Control Create() => new ShiftPage();

    /// <summary>Khóa thao tác trong khi gửi và giữ lỗi trên trang, không tự điều chỉnh tiền.</summary>
    private Button Action(string text, Func<Task> action, bool primary = false)
    {
        var button = Ui.Button(text, () => { }, primary);
        button.Click += async (_, _) =>
        {
            if (busy) return; busy = true; ApplyState();
            try { await action(); }
            catch (CafeApiException ex) { message.ForeColor = CafeTheme.Danger; message.Text = ex.Message; }
            finally { busy = false; if (!IsDisposed) ApplyState(); }
        };
        return button;
    }

    /// <summary>Đọc quyền/trạng thái từ server; cache không đủ để tự cho tiếp tục ca sau sự cố.</summary>
    private async Task LoadCurrent()
    {
        try
        {
            var context = await LiveSession.Api.CurrentShiftSessionAsync();
            var current = await LiveSession.Api.CurrentReconciliationAsync();
            if (IsDisposed) return;
            LiveSession.Current = context.Phien; LiveSession.ShiftLocked = context.TrangThai != "OPEN" || context.ChiDoiChieu;
            if (current.Ca.KetThucLuc is null) ShiftSessionFile.Save(context.Phien); else ShiftSessionFile.Clear();
            data = current; connected = true;
            completedTotal.Text=$"{current.TongTienDonHoanThanh:N0}đ"; completedCount.Text=current.SoDonHoanThanh.ToString();
            summary.Text = $"{current.Ca.HoTen} · Mã NV {current.Ca.MaNhanVien} · Ca #{current.Ca.MaCaLamViec} · {current.Ca.TenCa}";
            balances.Text = $"{current.DonDangXuLy.Count} đơn đang xử lý · {current.KhoanChuaRo.Count} khoản cần đối chiếu";
            message.ForeColor = CafeTheme.Muted;
            message.Text = current.Ca.KetThucLuc is not null ? "Ca đã kết thúc; không nhận lệnh mới. Có thể mở ca mới."
                : current.DangChoDoiChieu ? "Đã gửi thực đếm. Ca đang khóa chờ Quản lý đối chiếu; không tự mở bán lại."
                : context.ChiDoiChieu ? "Phiên phục hồi: chỉ nhập và gửi thực đếm, không được bán hàng hoặc tự chốt."
                : current.Ca.TrangThai == "LOCKED" ? "Ca đang khóa để xử lý sự cố. Liên hệ Quản lý; không tự mở bán hoặc coi chưa đếm là 0."
                : "Kiểm đếm và nhập tiền cuối ca, sau đó gửi kết quả để kết thúc ca.";
            if (current.Ca.TrangThai != "OPEN" && current.Ca.TienMatThucDem is { } counted && pendingSubmit is null)
            { amount.Value = counted; }
            ShowReceived();
        }
        catch (CafeApiException ex)
        {
            connected = false; LiveSession.ShiftLocked = true; message.ForeColor = CafeTheme.Danger;
            message.Text = ex.Message + " Không xác nhận offline; nếu mất phiên, nhờ Quản lý cấp mã phục hồi chỉ đối chiếu.";
        }
    }

    /// <summary>Phiên chỉ đối chiếu hoặc ca LOCKED không mở các lệnh bán/chốt; pending vẫn có thể tra lại.</summary>
    private void ApplyState()
    {
        if (IsDisposed) return;
        var restricted = LiveSession.Current?.Role == "STAFF_RECONCILIATION";
        var open = connected && data?.Ca.TrangThai == "OPEN";
        var recovery = connected && restricted && data?.Ca.TrangThai == "LOCKED" && !data.DangChoDoiChieu;
        amount.Enabled = !busy && (open || recovery) && pendingSubmit is null && pendingClose is null;
        // Ca bình thường chỉ có một hành động chính: gửi báo cáo và đóng. Nhánh đối chiếu riêng dành cho phục hồi hoặc còn việc cần bàn giao.
        submit.Visible = restricted || pendingSubmit is not null || open && data is not null && (data.DonDangXuLy.Count > 0 || data.KhoanChuaRo.Count > 0);
        submit.Text = restricted ? "Gửi kết quả đối chiếu" : "Gửi đối chiếu / bàn giao";
        close.Visible = !restricted;
        submit.Enabled = !busy && (pendingSubmit is not null || (open || recovery) && amount.Value == data?.TienMatDuKienHienTai);
        close.Enabled = !busy && (pendingClose is not null || open && !restricted && amount.Value == data?.TienMatDuKienHienTai);
        reload.Enabled = !busy;
        newShift.Visible = data?.Ca.KetThucLuc is not null;
        newShift.Enabled = !busy;
    }

    /// <summary>Không đổi loại lệnh khi lệnh trước chưa rõ kết quả; bảo toàn payload và mã chống trùng.</summary>
    private bool CanStart(string action)
    {
        var pending = pendingSubmit is not null ? "gửi" : pendingClose is not null ? "chốt" : null;
        if (pending is null || pending == action) return true;
        message.Text = $"Lệnh {pending} chưa rõ kết quả. Thử lại đúng lệnh trước khi làm việc khác."; return false;
    }

    /// <summary>Gửi số đếm với rowversion, lưu LOCKED trên server; không coi thao tác này là đã chốt.</summary>
    private async Task Submit()
    {
        if (!CanStart("gửi") || data is null) return;
        pendingSubmit ??= new(amount.Value, null, data.Ca.PhienBanDuLieu, Guid.NewGuid());
        try
        {
            await AdminUi.Recover(() => LiveSession.Api.SubmitReconciliationAsync(pendingSubmit), () => LiveSession.Api.ReconciliationReceiptAsync(pendingSubmit.MaThaoTac));
            pendingSubmit = null; ReturnToEntry();
        }
        catch (CafeApiException ex) { if (ex.StatusCode is not null && (int)ex.StatusCode < 500) pendingSubmit = null; throw; }
    }

    /// <summary>Giữ đường chốt trực tiếp khi đã xử lý hết; máy chủ kiểm tra lại số tiền hợp lệ và các nguồn còn dở.</summary>
    private async Task CloseShift()
    {
        if (!CanStart("chốt") || data is null) return;
        if (pendingClose is null && MessageBox.Show(FindForm(), $"Tiền cuối ca: {amount.Value:N0}đ\nTiền đầu ca: {data.Ca.TienMatDauCa:N0}đ\nTiền mặt hệ thống: {data.TienMatDuKienHienTai:N0}đ\n\nGửi kết quả và kết thúc ca?", "Gửi kết quả và kết thúc ca", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        pendingClose ??= new(amount.Value, null, data.Ca.PhienBanDuLieu, Guid.NewGuid());
        try
        {
            await AdminUi.Recover(() => LiveSession.Api.CloseShiftAsync(pendingClose), () => LiveSession.Api.ReconciliationReceiptAsync(pendingClose.MaThaoTac));
            pendingClose = null; ReturnToEntry();
        }
        catch (CafeApiException ex) { if (ex.StatusCode is not null && (int)ex.StatusCode < 500) pendingClose = null; throw; }
    }

    /// <summary>Đóng ca thành công quay về mở ca; nhánh phục hồi chỉ gửi đối chiếu vẫn giữ quy trình Admin xử lý.</summary>
    private void ReturnToEntry()
    {
        ShiftSessionFile.Clear();
        LiveSession.Current = null;
        LiveSession.Api.Token = null;
        LiveSession.ShiftLocked = false;
        (FindForm() as MainForm)?.RequestReauthentication();
    }

    /// <summary>Đối chiếu số đếm với tổng tiền thực của ca; sai lệch không được dùng để tự thay doanh thu.</summary>
    private void ShowReceived()
    {
        expected.Text = data is null ? "Đang tải…" : $"{data.TienMatDuKienHienTai:N0}đ";
        var delta = data is null ? 0 : amount.Value-data.TienMatDuKienHienTai;
        difference.Text = data is null ? "Đang tải…" : delta == 0 ? "Đã khớp" : $"{delta:N0}đ · Chưa khớp";
        difference.ForeColor = delta == 0 ? Color.FromArgb(48,105,66) : CafeTheme.Danger;
        ApplyState();
    }
}
