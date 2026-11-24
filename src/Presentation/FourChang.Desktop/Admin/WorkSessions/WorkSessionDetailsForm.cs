using System.Globalization;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Orders;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Admin.WorkSessions;

/// <summary>Đối chiếu ca chỉ đọc nguồn; lệnh chốt/bàn giao có version, mốc nguồn và mã chống trùng.</summary>
public sealed class WorkSessionDetailsForm : Form
{
    private WorkSessionDetails data;
    private readonly Panel snapshot = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top };
    private readonly Label message = Ui.Label("", 9, false, CafeTheme.Muted);
    private readonly Button closeShift;
    private readonly Button handover;
    private readonly Button recovery;
    private readonly Button reload;
    private readonly Button dismiss;
    private bool busy;
    private int activeTab;
    private CloseWorkSessionInput? pendingClose;
    private HandoverWorkSessionInput? pendingHandover;
    private IssueShiftRecoveryInput? pendingRecovery;

    /// <summary>Hiển thị thực đếm chưa nhập bằng chữ, không đổi NULL thành 0; nút nằm ngoài vùng cuộn.</summary>
    public WorkSessionDetailsForm(WorkSessionDetails details)
    {
        data = details; Name = "WorkSessionDetails"; Text = $"Đối chiếu ca {data.Ca.MaCaHienThi}";
        ClientSize = new Size(1220, 850); MinimumSize = new Size(940, 650); StartPosition = FormStartPosition.CenterParent;
        BackColor = CafeTheme.Cream; Font = CafeTheme.Body(); AutoScaleMode = AutoScaleMode.Dpi;
        closeShift = Button("Chốt ca chờ đối chiếu", CloseShift, true); closeShift.Name = "AdminConfirmShift";
        handover = Button("Bàn giao", Handover); handover.Name = "AdminHandoverShift";
        recovery = Button("Hỗ trợ mất phiên", IssueRecovery); recovery.Name = "AdminIssueShiftRecovery";
        reload = Button("Tải lại đối chiếu", Reload);
        dismiss = Ui.Button("Đóng", () => Close());
        var body = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(16) };
        body.Controls.Add(AdminListLayout.Column(snapshot, message));
        AdminUi.Compose(this, body, closeShift, handover, recovery, reload, dismiss);
        ShowSnapshot();
        Shown += (_, _) => ResetViewport();
        FormClosing += (_, e) => { if (busy) e.Cancel = true; };
    }

    /// <summary>Chạy thao tác UI một lần; lỗi không làm mất dữ liệu đang xem hoặc tạo request khác.</summary>
    private Button Button(string text, Func<Task> action, bool primary = false)
    {
        var button = Ui.Button(text, () => { }, primary);
        button.Click += async (_, _) =>
        {
            if (busy) return; busy = true; ApplyState(); UseWaitCursor = true;
            try { await action(); }
            catch (CafeApiException ex) { message.Text = ex.Message; if (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden) AdminUi.Error(this, ex); }
            finally { busy = false; if (!IsDisposed) { UseWaitCursor = false; ApplyState(); } }
        };
        return button;
    }

    /// <summary>Ngăn đổi sang lệnh khác khi lần ghi trước chưa rõ kết quả; vẫn cho thử lại đúng lệnh và tải lại.</summary>
    private bool CanStart(string action)
    {
        var waiting = pendingClose is not null ? "chốt" : pendingHandover is not null ? "bàn giao" : pendingRecovery is not null ? "phục hồi" : null;
        if (waiting is null || waiting == action) return true;
        message.Text = $"Lệnh {waiting} chưa rõ kết quả. Hãy thử lại đúng lệnh để tra receipt trước khi làm việc khác.";
        return false;
    }

    /// <summary>Định dạng tiền theo tiếng Việt; không tính lại tiền thu/hoàn ở giao diện.</summary>
    private static string Money(decimal value) => value.ToString("#,##0", CultureInfo.GetCultureInfo("vi-VN")) + "đ";

    /// <summary>Tạo bảng chỉ đọc có cột tiếng Việt và khóa nguồn để truy vết giao dịch.</summary>
    private static DataGridView Grid(string name, object rows, params (string Property, string Caption)[] columns)
    {
        var grid = Ui.Grid(new([], []), 280); grid.Name = name; grid.AutoGenerateColumns = false;
        foreach (var (property, caption) in columns) grid.Columns.Add(new DataGridViewTextBoxColumn { Name = property, DataPropertyName = property, HeaderText = caption, SortMode = DataGridViewColumnSortMode.NotSortable });
        grid.DataSource = rows;
        OperationsPresentation.Grid(grid); CommerceWorkspaceVisuals.FormatGrid(grid);
        grid.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex < 0) return;
            if (e.Value is decimal money) { e.Value = Money(money); e.FormattingApplied = true; }
            if (e.Value is DateTime at) { e.Value = at.ToLocalTime().ToString("dd/MM/yyyy HH:mm"); e.FormattingApplied = true; }
        };
        return grid;
    }

    /// <summary>Chỉ đọc hồ sơ ca, ba chỉ tiêu và ba bảng; giao dịch tiền vẫn thuộc ca thực sự thu/chi.</summary>
    private void ShowSnapshot()
    {
        snapshot.SuspendLayout();
        foreach (Control control in snapshot.Controls) control.Dispose(); snapshot.Controls.Clear();
        var ca = data.Ca;
        var status = ca.TrangThai switch { "OPEN" => "Đang làm", "CLOSED" => "Đã chốt", "HANDED_OVER" => "Đã bàn giao", _ => "Chờ đối chiếu / hỗ trợ" };
        var header = OrderDetailsForm.Section("Thông tin ca làm việc", OrderDetailsForm.Value("Mã ca", ca.MaCaHienThi),
            OrderDetailsForm.Value("Nhân viên", $"{ca.HoTen} · NV {ca.MaNhanVien}", 370), OrderDetailsForm.Value("Ca", ca.TenCa), OrderDetailsForm.Value("Trạng thái", status, 220),
            OrderDetailsForm.Value("Bắt đầu", ca.BatDauLuc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), 220),
            OrderDetailsForm.Value("Kết thúc", ca.KetThucLuc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "Chưa kết thúc", 220));
        var completed = OrderDetailsForm.Section("Kết quả phục vụ", OrderDetailsForm.Value("Số đơn hoàn thành", data.SoDonHoanThanh.ToString()), OrderDetailsForm.Value("Tổng tiền đơn hoàn thành", Money(data.TongTienDonHoanThanh), 300));
        var cash = OrderDetailsForm.Section("Kết quả kiểm đếm", OrderDetailsForm.Value("Tiền đầu ca", Money(ca.TienMatDauCa), 200),
            OrderDetailsForm.Value("Tiền mặt hệ thống", Money(data.TienMatDuKienHienTai), 200), OrderDetailsForm.Value("Tiền nhân viên kiểm đếm", ca.TienMatThucDem is { } counted ? Money(counted) : "Chưa gửi", 220),
            OrderDetailsForm.Value("Chênh lệch", data.ChenhLech is { } delta ? Money(delta) : "Chưa gửi", 180));
        var done = Grid("ShiftCompletedOrders", (data.DonHoanThanh ?? []).ToList(), ("MaDon", "Mã đơn"), ("SoTien", "Tổng tiền"), ("HoanTatLuc", "Hoàn thành lúc"));
        var waiting = Grid("ShiftPendingOrders", data.DonDangXuLy.ToList(), ("MaDonHienThi", "Mã đơn"), ("TrangThai", "Trạng thái"), ("MaCaNguon", "Ca nguồn"));
        var transactions = Grid("ShiftTransactions", data.GiaoDich.ToList(), ("MaDon", "Mã đơn"), ("Loai", "Thu / hoàn"), ("PhuongThuc", "Phương thức"), ("SoTien", "Số tiền"), ("TrangThai", "Kết quả"));
        var tabs = Ui.Tabs(($"Đơn hoàn thành ({data.SoDonHoanThanh})", done), ($"Đơn cần bàn giao ({data.DonDangXuLy.Count})", waiting), ("Thu / hoàn tiền", transactions));
        tabs.Name = "WorkSessionTables"; tabs.Height = 290;
        foreach (TabPage tab in tabs.TabPages) { tab.AutoScroll = false; foreach (var table in tab.Controls.OfType<DataGridView>()) { table.Dock = DockStyle.Fill; table.MinimumSize = Size.Empty; } }
        tabs.SelectedIndex = Math.Clamp(activeTab, 0, tabs.TabCount - 1); tabs.SelectedIndexChanged += (_, _) => activeTab = tabs.SelectedIndex;
        var handoverText = data.BanGiao is { } link ? $"Đã bàn giao cho nhân viên #{link.MaNhanVienNhan}. Nhân viên nhận mở ca tại POS để tiếp tục xử lý đơn."
            : ca.MaCaBanGiaoTruoc is { } source ? $"Đã tiếp nhận đơn từ ca #{source}. Tiền thu/hoàn của ca cũ không chuyển sang ca này." : "Chưa có liên kết bàn giao.";
        var note = data.KhoanChuaRo.Count > 0 ? $"Có {data.KhoanChuaRo.Count} khoản chưa xác minh; xử lý ở Đơn hàng/Hoàn tiền hoặc bàn giao có liên kết." : "Tiền mặt hệ thống = tiền đầu ca + thu tiền mặt đã xác minh − hoàn tiền mặt thành công. Chuyển khoản không cộng vào két.";
        snapshot.Controls.Add(AdminListLayout.Column(header, completed, cash, Ui.Panel("Đơn và giao dịch của ca", Ui.Label(note, 9, false, CafeTheme.Muted), tabs, Ui.Label(handoverText, 9, false, CafeTheme.Muted))));
        snapshot.ResumeLayout(true);
        if (IsHandleCreated) BeginInvoke(new Action(ResetViewport));
        ApplyState();
    }
    /// <summary>Không để TabControl đầu tiên tự kéo vùng cuộn xuống khi mở form chỉ đọc, làm mất phần quỹ phía trên.</summary>
    private void ResetViewport()
    {
        if (IsDisposed) return;
        dismiss.Focus();
        for (Control? parent = snapshot.Parent; parent is not null && parent != this; parent = parent.Parent)
            if (parent is ScrollableControl { AutoScroll: true } scroll) scroll.AutoScrollPosition = Point.Empty;
    }

    /// <summary>Nút dựa trạng thái thật; pending giữ quyền tra lại dù ca đã kết thúc sau commit.</summary>
    private void ApplyState()
    {
        if (IsDisposed) return;
        var active = data.Ca.KetThucLuc is null;
        // Ca đã kết thúc hợp lệ chỉ đọc. Nút chốt chỉ hỗ trợ ca còn khóa chờ thực đếm, không phê duyệt lại ca đã xong.
        closeShift.Visible = pendingClose is not null || active && data.DangChoDoiChieu && data.DonDangXuLy.Count == 0 && data.KhoanChuaRo.Count == 0;
        handover.Visible = pendingHandover is not null || active && data.DangChoDoiChieu && (data.DonDangXuLy.Count > 0 || data.KhoanChuaRo.Count > 0);
        recovery.Visible = pendingRecovery is not null || active;
        closeShift.Enabled = !busy && (pendingClose is not null || active && data.DangChoDoiChieu && data.DonDangXuLy.Count == 0 && data.KhoanChuaRo.Count == 0);
        handover.Enabled = !busy && (pendingHandover is not null || active && data.DangChoDoiChieu);
        recovery.Enabled = !busy && (pendingRecovery is not null || active);
        reload.Enabled = dismiss.Enabled = !busy;
    }

    /// <summary>Tải mới nguồn và version; không xóa pending sau lỗi mất phản hồi.</summary>
    private async Task Reload() { data = await LiveSession.Api.GetWorkSessionAsync(data.Ca.MaCaLamViec); if (!IsDisposed) ShowSnapshot(); }

    /// <summary>Chốt chỉ từ thực đếm đã gửi; cùng request được giữ để hồi phục sau timeout.</summary>
    private async Task CloseShift()
    {
        if (!CanStart("chốt")) return;
        if (pendingClose is null && MessageBox.Show(this, "Xác nhận chốt ca theo số thực đếm và nguồn đang xem? Ca đã chốt không nhận giao dịch mới.", "Chốt ca", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        pendingClose ??= new(data.Ca.PhienBanDuLieu, Guid.NewGuid(), data.MaDoiChieu);
        try
        {
            await AdminUi.Recover(() => LiveSession.Api.ConfirmWorkSessionAsync(data.Ca.MaCaLamViec, pendingClose), () => LiveSession.Api.ReadWorkSessionReceiptAsync<WorkSessionInfo>(pendingClose.MaThaoTac));
            pendingClose = null; await Reload(); message.Text = "Đã chốt ca. Nguồn thu/hoàn được giữ nguyên.";
        }
        catch (CafeApiException ex) { if (ex.StatusCode is not null && (int)ex.StatusCode < 500) pendingClose = null; throw; }
    }

    /// <summary>Xác định người nhận cùng toàn bộ nguồn còn dở; server kiểm tra lại chống bàn giao thiếu hoặc xung đột.</summary>
    private async Task Handover()
    {
        if (!CanStart("bàn giao")) return;
        pendingHandover ??= await ReadHandover(); if (pendingHandover is null) return;
        try
        {
            await AdminUi.Recover(() => LiveSession.Api.HandoverWorkSessionAsync(data.Ca.MaCaLamViec, pendingHandover), () => LiveSession.Api.ReadWorkSessionReceiptAsync<HandoverInfo>(pendingHandover.MaThaoTac));
            pendingHandover = null; await Reload(); message.Text = "Đã bàn giao. Người nhận mở ca tại POS; tiền nguồn không chuyển sang ca mới.";
        }
        catch (CafeApiException ex) { if (ex.StatusCode is not null && (int)ex.StatusCode < 500) pendingHandover = null; throw; }
    }

    /// <summary>Form bàn giao hiển thị mã để phân biệt tên trùng; không cần cấp/thu hồi máy.</summary>
    private async Task<HandoverWorkSessionInput?> ReadHandover()
    {
        var employees = (await LiveSession.Api.GetEmployeesAsync(true)).Where(x => x.TrangThai == "ACTIVE" && x.MaNhanVien != data.Ca.MaNhanVien).Select(x => new { Employee = x.MaNhanVien, Label = $"#{x.MaNhanVien} · {x.HoTen}" }).ToList();
        using var form = AdminUi.Dialog("Xác nhận bàn giao", 820, 540);
        var receiver = new ComboBox { DataSource = employees, DisplayMember = "Label", ValueMember = "Employee", DropDownStyle = ComboBoxStyle.DropDownList, Width = 640, Name = "HandoverReceiver" };
        receiver.SelectedIndex = -1;

        var reason = Ui.Input("Lý do bàn giao", "", 640); reason.MaxLength = 500;
        var error = Ui.Label("", 9, false, CafeTheme.Danger);
        var confirm = Ui.Button("Xác nhận bàn giao", () => { if (receiver.SelectedValue is not long || string.IsNullOrWhiteSpace(reason.Text)) { error.Text = "Chọn nhân viên nhận và nhập lý do."; return; } form.DialogResult = DialogResult.OK; }, true);
        AdminUi.Compose(form, Ui.Panel("Người nhận và nguồn bàn giao", Ui.Field("Nhân viên nhận *", receiver), Ui.Field("Lý do *", reason),
            Ui.Label($"Bàn giao toàn bộ {data.DonDangXuLy.Count} đơn đang xử lý và {data.KhoanChuaRo.Count} khoản chưa rõ trong đối chiếu hiện tại.", 9, false, CafeTheme.Muted), error), confirm, Ui.Button("Hủy", () => form.Close()));
        return form.ShowDialog(this) == DialogResult.OK ? new((long)receiver.SelectedValue!, data.Ca.MaMay, reason.Text,
            data.DonDangXuLy.Select(x => x.MaDonHang).ToArray(), data.KhoanChuaRo.ToArray(), data.Ca.PhienBanDuLieu, Guid.NewGuid(), data.MaDoiChieu) : null;
    }

    /// <summary>Mã chỉ hiển thị lần đầu, không lưu vào audit hoặc receipt. Sau timeout không đoán lại mã cũ.</summary>
    private async Task IssueRecovery()
    {
        if (!CanStart("phục hồi")) return;
        if (pendingRecovery is null)
        {
            var reason = WorkforceDialogs.Reason(this, "Cấp mã phục hồi đối chiếu", "Khóa ca và cấp mã một lần cho đúng nhân viên; chỉ nhập/gửi số đếm, không bán hàng hay tự chốt.");
            if (reason is null) return; pendingRecovery = new(reason, data.Ca.PhienBanDuLieu, Guid.NewGuid());
        }
        try
        {
            var receipt = await AdminUi.Recover(() => LiveSession.Api.IssueShiftRecoveryAsync(data.Ca.MaCaLamViec, pendingRecovery), () => LiveSession.Api.ReadWorkSessionReceiptAsync<ShiftRecoveryReceipt>(pendingRecovery.MaThaoTac));
            pendingRecovery = null; await Reload();
            if (receipt.MaPhucHoi is null) { message.Text = "Đã cấp mã nhưng không lấy lại mã bí mật từ receipt. Nếu chưa nhận được mã, cấp mã mới; mã cũ sẽ mất hiệu lực."; return; }
            using var form = AdminUi.Dialog("Mã phục hồi một lần", 820, 380);
            var code = Ui.Input("", receipt.MaPhucHoi, 700); code.ReadOnly = true; code.Name = "ShiftRecoveryCode";
            var copy = Ui.Button("Sao chép mã", () => { try { Clipboard.SetText(receipt.MaPhucHoi); } catch (System.Runtime.InteropServices.ExternalException) { code.SelectAll(); code.Focus(); } });
            AdminUi.Compose(form, Ui.Panel($"Ca #{receipt.MaCaLamViec} · Nhân viên #{receipt.MaNhanVien}",
                Ui.Label($"Mã hết hạn lúc {receipt.HetHanLuc.ToLocalTime():HH:mm:ss}, chỉ sử dụng một lần.", 10), Ui.Field("Mã chỉ hiển thị tại đây", code),
                Ui.Label("Tại màn hình nhân viên: Phục hồi đối chiếu bằng mã → nhập mã ca, mã/họ tên và mã này.", 9, false, CafeTheme.Muted)), copy, Ui.Button("Đóng", () => form.Close()));
            form.ShowDialog(this);
        }
        catch (CafeApiException ex) { if (ex.StatusCode is not null && (int)ex.StatusCode < 500) pendingRecovery = null; throw; }
    }
}
