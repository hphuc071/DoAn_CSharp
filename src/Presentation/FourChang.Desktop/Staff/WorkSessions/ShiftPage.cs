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

/// <summary>POS-01 tích hợp ADM-12: xem quỹ, chốt trực tiếp khi đủ điều kiện hoặc gửi đối chiếu khóa ca.</summary>
public sealed class ShiftPage : UserControl
{
    private readonly Label summary = Ui.Label("Đang tải ca hiện tại…", 11, true);
    private readonly Label balances = Ui.Label("", 11);
    private readonly Label message = Ui.Label("", 9, false, CafeTheme.Muted);
    private readonly NumericUpDown amount = new() { Minimum = 0, Maximum = 999999999999999999m, ThousandsSeparator = true, Width = 320 };
    private readonly TextBox reason = Ui.Input("Lý do khi thực đếm khác dự kiến", "", 620);
    private readonly Button submit;
    private readonly Button close;
    private readonly Button temporaryLock;
    private readonly Button requestResume;
    private readonly Button reload;
    private readonly Button newShift;
    private WorkSessionDetails? data;
    private bool busy;
    private bool connected;
    private SubmitReconciliation? pendingSubmit;
    private CloseShift? pendingClose;
    private ResumeWorkSessionInput? pendingLock;
    private RequestShiftResumeInput? pendingRequest;

    /// <summary>Không hỏi tài khoản nhân viên; tiền thực đếm do chính nhân viên nhập tại ca của token.</summary>
    public ShiftPage()
    {
        Name = "StaffShiftPage"; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; Dock = DockStyle.Top; BackColor = CafeTheme.Cream;
        amount.Name = "ShiftCountedCash"; reason.Name = "ShiftCashReason"; reason.MaxLength = 500;
        submit = Action("Gửi đối chiếu", Submit, true); submit.Name = "SubmitShiftReconciliation";
        close = Action("Chốt ca trực tiếp", CloseShift); close.Name = "CloseStaffShift";
        temporaryLock = Action("Tạm khóa ca", Lock); requestResume = Action("Đề nghị tiếp tục ca", RequestResume);
        reload = Action("Tải lại", LoadCurrent);
        newShift = Ui.Button("Mở ca mới", () => { ShiftSessionFile.Clear(); LiveSession.Current = null; LiveSession.Api.Token = null; (FindForm() as MainForm)?.RequestReauthentication(); });
        Controls.Add(Ui.Panel("Ca làm việc của tôi", summary, balances, Ui.Label("Chuyển khoản không cộng vào quỹ tiền mặt. Gửi đối chiếu sẽ khóa bán hàng, chờ Quản lý chốt/bàn giao.", 9, false, CafeTheme.Muted),
            Ui.Field("Tiền mặt thực đếm (đồng)", amount), Ui.Field("Lý do chênh lệch", reason), message,
            Ui.Row(submit, close, temporaryLock, requestResume, reload, newShift)));
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
            summary.Text = $"{current.Ca.HoTen} · Mã NV {current.Ca.MaNhanVien} · Ca #{current.Ca.MaCaLamViec} · {current.Ca.TenCa}";
            balances.Text = $"Đầu ca: {current.Ca.TienMatDauCa:N0}đ  |  Thu CASH: {current.ThuTienMat:N0}đ  |  Hoàn CASH: {current.HoanTienMat:N0}đ\nDự kiến: {current.TienMatDuKienHienTai:N0}đ · {current.DonDangXuLy.Count} đơn dở · {current.KhoanChuaRo.Count} khoản chưa rõ";
            message.ForeColor = CafeTheme.Muted;
            message.Text = current.Ca.KetThucLuc is not null ? "Ca đã kết thúc; không nhận lệnh mới. Có thể mở ca mới."
                : current.DangChoDoiChieu ? "Đã gửi thực đếm. Ca đang khóa chờ Quản lý đối chiếu; không tự mở bán lại."
                : context.ChiDoiChieu ? "Phiên phục hồi: chỉ nhập và gửi thực đếm, không được bán hàng hoặc tự chốt."
                : current.Ca.TrangThai == "LOCKED" && current.SuKienTrangThai == "SHIFT_TEMP_LOCKED" ? "Ca đang khóa tạm. Dùng Đề nghị tiếp tục ca để Quản lý kiểm tra phiên."
                : current.Ca.TrangThai == "LOCKED" ? "Ca đang khóa để xử lý sự cố. Liên hệ Quản lý; không tự mở bán hoặc coi chưa đếm là 0."
                : "Ca đang mở. Kiểm đếm thực tế trước khi gửi đối chiếu hoặc chốt.";
            if (current.Ca.TrangThai != "OPEN" && current.Ca.TienMatThucDem is { } counted && pendingSubmit is null)
            { amount.Value = counted; reason.Text = current.Ca.LyDoChenhLech ?? ""; }
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
        amount.Enabled = reason.Enabled = !busy && (open || recovery) && pendingSubmit is null && pendingClose is null;
        submit.Enabled = !busy && (pendingSubmit is not null || open || recovery);
        close.Enabled = !busy && (pendingClose is not null || open && !restricted);
        temporaryLock.Enabled = !busy && (pendingLock is not null || open && !restricted);
        requestResume.Enabled = !busy && (pendingRequest is not null || connected && !restricted && data?.Ca.TrangThai == "LOCKED" && !data.DangChoDoiChieu && data.SuKienTrangThai == "SHIFT_TEMP_LOCKED" && !data.CoYeuCauKhoiPhuc);
        reload.Enabled = !busy;
        newShift.Visible = data?.Ca.KetThucLuc is not null;
        newShift.Enabled = !busy;
    }

    /// <summary>Không đổi loại lệnh khi lệnh trước chưa rõ kết quả; bảo toàn payload và mã chống trùng.</summary>
    private bool CanStart(string action)
    {
        var pending = pendingSubmit is not null ? "gửi" : pendingClose is not null ? "chốt" : pendingLock is not null ? "khóa" : pendingRequest is not null ? "tiếp tục" : null;
        if (pending is null || pending == action) return true;
        message.Text = $"Lệnh {pending} chưa rõ kết quả. Thử lại đúng lệnh trước khi làm việc khác."; return false;
    }

    /// <summary>Gửi số đếm với rowversion, lưu LOCKED trên server; không coi thao tác này là đã chốt.</summary>
    private async Task Submit()
    {
        if (!CanStart("gửi") || data is null) return;
        pendingSubmit ??= new(amount.Value, reason.Text, data.Ca.PhienBanDuLieu, Guid.NewGuid());
        try
        {
            await AdminUi.Recover(() => LiveSession.Api.SubmitReconciliationAsync(pendingSubmit), () => LiveSession.Api.ReconciliationReceiptAsync(pendingSubmit.MaThaoTac));
            pendingSubmit = null; ReturnToEntry();
        }
        catch (CafeApiException ex) { if (ex.StatusCode is not null && (int)ex.StatusCode < 500) pendingSubmit = null; throw; }
    }

    /// <summary>Giữ đường chốt trực tiếp khi đã xử lý hết; máy chủ kiểm tra lại tiền, lý do và các nguồn còn dở.</summary>
    private async Task CloseShift()
    {
        if (!CanStart("chốt") || data is null) return;
        if (pendingClose is null && MessageBox.Show(FindForm(), $"Chốt ca với thực đếm {amount.Value:N0}đ? Ca đã chốt không được mở lại để ghi tiền.", "Chốt ca", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        pendingClose ??= new(amount.Value, reason.Text, data.Ca.PhienBanDuLieu, Guid.NewGuid());
        try
        {
            await AdminUi.Recover(() => LiveSession.Api.CloseShiftAsync(pendingClose), () => LiveSession.Api.ReconciliationReceiptAsync(pendingClose.MaThaoTac));
            pendingClose = null; ReturnToEntry();
        }
        catch (CafeApiException ex) { if (ex.StatusCode is not null && (int)ex.StatusCode < 500) pendingClose = null; throw; }
    }

    /// <summary>Gửi/chốt thành công quay về mở ca. Chỉ xóa ngữ cảnh local, không tự chốt ca đang chờ Admin.</summary>
    private void ReturnToEntry()
    {
        ShiftSessionFile.Clear();
        LiveSession.Current = null;
        LiveSession.Api.Token = null;
        LiveSession.ShiftLocked = false;
        (FindForm() as MainForm)?.RequestReauthentication();
    }

    /// <summary>Khóa tạm không ghi thực đếm hoặc kết thúc ca.</summary>
    private async Task Lock()
    {
        if (!CanStart("khóa") || data is null) return;
        if (pendingLock is null)
        {
            var text = WorkforceDialogs.Reason(this, "Tạm khóa ca", "Tạm khóa ngăn ghi mới. Sau đó cần phiên đã lưu và Quản lý cho tiếp tục.");
            if (text is null) return; pendingLock = new(text, data.Ca.PhienBanDuLieu, Guid.NewGuid());
        }
        try
        {
            await AdminUi.Recover(() => LiveSession.Api.LockCurrentShiftAsync(pendingLock), () => LiveSession.Api.ReconciliationReceiptAsync(pendingLock.MaThaoTac));
            pendingLock = null; LiveSession.ShiftLocked = true; await LoadCurrent();
        }
        catch (CafeApiException ex) { if (ex.StatusCode is not null && (int)ex.StatusCode < 500) pendingLock = null; throw; }
    }

    /// <summary>Chứng minh còn phiên ca và đúng hồ sơ; request không tự mở lại quyền bán.</summary>
    private async Task RequestResume()
    {
        if (!CanStart("tiếp tục") || LiveSession.Current is not { } current) return;
        pendingRequest ??= new(current.Id, current.Name, Guid.NewGuid());
        try
        {
            await AdminUi.Recover(() => LiveSession.Api.RequestShiftResumeAsync(pendingRequest), () => LiveSession.Api.ReconciliationReceiptAsync(pendingRequest.MaThaoTac));
            pendingRequest = null; await LoadCurrent(); message.Text = "Đã đề nghị. Chờ Quản lý cho tiếp tục, sau đó bấm Tải lại.";
        }
        catch (CafeApiException ex) { if (ex.StatusCode is not null && (int)ex.StatusCode < 500) pendingRequest = null; throw; }
    }
}
