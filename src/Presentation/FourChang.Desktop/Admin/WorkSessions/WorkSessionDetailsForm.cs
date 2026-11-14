using System.Globalization;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
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
        data = details; Name = "WorkSessionDetails"; Text = $"Đối chiếu ca #{data.Ca.MaCaLamViec}";
        ClientSize = new Size(1220, 850); MinimumSize = new Size(940, 650); StartPosition = FormStartPosition.CenterParent;
        BackColor = CafeTheme.Cream; Font = CafeTheme.Body(); AutoScaleMode = AutoScaleMode.Dpi;
        closeShift = Button("Xác nhận chốt", CloseShift, true); closeShift.Name = "AdminConfirmShift";
        handover = Button("Bàn giao", Handover); handover.Name = "AdminHandoverShift";
        recovery = Button("Cấp mã phục hồi", IssueRecovery); recovery.Name = "AdminIssueShiftRecovery";
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
        OperationsPresentation.Grid(grid);
        return grid;
    }

    /// <summary>Vẽ lại snapshot từ API: tách CASH/CK, nguồn chưa rõ và lịch sử; không sửa trạng thái khi xem.</summary>
    private void ShowSnapshot()
    {
        snapshot.SuspendLayout();
        foreach (Control control in snapshot.Controls) control.Dispose(); snapshot.Controls.Clear();
        var ca = data.Ca;
        var status = ca.TrangThai switch { "OPEN" => "Đang mở", "CLOSED" => "Đã chốt", "HANDED_OVER" => "Đã bàn giao", _ => data.DangChoDoiChieu ? "Khóa chờ đối chiếu" : data.SuKienTrangThai == "SHIFT_TEMP_LOCKED" ? "Khóa tạm" : data.SuKienTrangThai == "SHIFT_RECOVERY_ISSUED" ? "Khóa phục hồi · chờ nhân viên gửi đối chiếu" : "Đang khóa · cần kiểm tra lịch sử" };
        var header = Ui.Panel($"{ca.HoTen} · Ca #{ca.MaCaLamViec}",
            Ui.Label($"Mã NV {ca.MaNhanVien} · Ca {ca.TenCa} · Máy (lịch sử): {ca.MaMay} · Bắt đầu {ca.BatDauLuc.ToLocalTime():dd/MM/yyyy HH:mm}", 10),
            Ui.Label(status, 10, true),
            Ui.Label($"Đã hoàn thành: {data.SoDonHoanThanh} đơn · Tổng tiền: {data.TongTienDonHoanThanh:N0}đ", 12, true));
        var cash = Ui.Panel("Đối chiếu tiền mặt", MoneyGrid(("Tiền đầu ca", Money(ca.TienMatDauCa)), ("Thu đã xác minh", Money(data.ThuTienMat)),
            ("Hoàn thành công", Money(data.HoanTienMat)), ("Dự kiến", Money(data.TienMatDuKienHienTai)),
            ("Tiền cuối ca", ca.TienMatThucDem is { } amount ? Money(amount) : "Chưa gửi"),
            ("Tiền mặt thu được", data.TienThuDuoc is { } received ? Money(received) : "Chưa gửi")),
            Ui.Label($"Chênh lệch: {(data.ChenhLech is { } delta ? Money(delta) : "Chưa có thực đếm")}", 10, false, CafeTheme.Muted));
        WrapCardLabels(header); WrapCardLabels(cash);
        var columns = new (string, string)[] { ("Loai", "Thu / hoàn"), ("Ma", "Mã nguồn"), ("MaDonHang", "Mã đơn"), ("ThamChieu", "Tham chiếu"), ("PhuongThuc", "Phương thức"), ("SoTien", "Số tiền"), ("TrangThai", "Trạng thái") };
        var cashGrid = Grid("ShiftCashTransactions", data.GiaoDich.Where(x => x.PhuongThuc == "CASH").ToList(), columns);
        var bankGrid = Grid("ShiftBankTransactions", data.GiaoDich.Where(x => x.PhuongThuc == "BANK_TRANSFER").ToList(), columns);
        var orders = Grid("ShiftPendingOrders", data.DonDangXuLy.ToList(), ("MaDonHang", "Mã đơn"), ("MaDonHienThi", "Số đơn"), ("TrangThai", "Trạng thái"), ("MaCaNguon", "Ca nguồn"));
        var unresolved = Grid("ShiftUnresolved", data.KhoanChuaRo.Select(x => new { Nguon = x }).ToList(), ("Nguon", "Nguồn cần đối soát"));
        var audits = Grid("ShiftAudit", data.NhatKy.ToList(), ("ThoiDiem", "Thời điểm UTC"), ("NguoiThucHien", "Người thực hiện"), ("HanhDong", "Hành động"), ("LyDo", "Lý do"));
        var tabs = Ui.Tabs(("Tiền mặt", cashGrid), ("Chuyển khoản", bankGrid), ($"Đơn đang xử lý ({data.DonDangXuLy.Count})", orders),
            ($"Khoản chưa rõ ({data.KhoanChuaRo.Count})", unresolved), ("Nhật ký", audits)); tabs.Height = 340;
        foreach (TabPage tab in tabs.TabPages)
        {
            tab.AutoScroll = false;
            foreach (var table in tab.Controls.OfType<DataGridView>()) { table.Dock = DockStyle.Fill; table.MinimumSize = Size.Empty; }
        }
        tabs.SelectedIndex = Math.Clamp(activeTab, 0, tabs.TabCount - 1);
        tabs.SelectedIndexChanged += (_, _) => activeTab = tabs.SelectedIndex;
        var handoverText = data.BanGiao is { } link ? $"Đã bàn giao cho nhân viên #{link.MaNhanVienNhan} · Máy lịch sử {link.MaMayNhan}. Ca nhận được mở tại POS."
            : ca.MaCaBanGiaoTruoc is { } source ? $"Ca này tiếp nhận từ ca #{source}. Giao dịch cũ vẫn thuộc ca nguồn." : "Chưa có liên kết bàn giao.";
        snapshot.Controls.Add(AdminListLayout.Column(header, cash, Ui.Panel("Giao dịch và truy vết", Ui.Label("Dự kiến tiền mặt = đầu ca + thu CASH đã xác minh − hoàn CASH thành công. Chuyển khoản hiển thị riêng.", 9, false, CafeTheme.Muted), tabs, Ui.Label(handoverText, 9, false, CafeTheme.Muted))));
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

    /// <summary>Các cột chia theo tỷ lệ: tránh đo Flow lồng nhau làm card tiền mặt cao rỗng hàng trăm px.</summary>
    private static Control MoneyGrid(params (string Caption, string Value)[] values)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Top, Height = 110, ColumnCount = values.Length, RowCount = 1, Margin = Padding.Empty };
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (var i = 0; i < values.Length; i++)
        {
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / values.Length));
            var cell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 0, 12, 0) };
            cell.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); cell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var caption = Ui.Label(values[i].Caption, 9, false, CafeTheme.Muted);
            var value = Ui.Label(values[i].Value, 16, true);
            caption.Dock = value.Dock = DockStyle.Fill; caption.AutoSize = value.AutoSize = false;
            caption.Margin = value.Margin = Padding.Empty;
            value.TextAlign = ContentAlignment.TopLeft;
            cell.Controls.Add(caption, 0, 0); cell.Controls.Add(value, 0, 1);
            row.Controls.Add(cell, i, 0);
        }
        return row;
    }

    /// <summary>Nhãn dài được xuống hàng trong thẻ, không kéo ngang dialog khi tên/địa chỉ dài.</summary>
    private static void WrapCardLabels(SurfacePanel card)
    {
        card.SizeChanged += (_, _) =>
        {
            var width = Math.Max(200, card.ClientSize.Width - card.Padding.Horizontal);
            foreach (var label in card.Controls.OfType<TableLayoutPanel>().SelectMany(x => x.Controls.OfType<Label>()))
                label.MaximumSize = new Size(width, 0);
        };
    }

    /// <summary>Nút dựa trạng thái thật; pending giữ quyền tra lại dù ca đã kết thúc sau commit.</summary>
    private void ApplyState()
    {
        if (IsDisposed) return;
        var active = data.Ca.KetThucLuc is null;
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
        var employees = (await LiveSession.Api.GetEmployeesAsync(true)).Where(x => x.TrangThai == "ACTIVE").Select(x => new { Employee = x.MaNhanVien, Label = $"#{x.MaNhanVien} · {x.HoTen}" }).ToList();
        using var form = AdminUi.Dialog("Xác nhận bàn giao", 820, 540);
        var receiver = new ComboBox { DataSource = employees, DisplayMember = "Label", ValueMember = "Employee", DropDownStyle = ComboBoxStyle.DropDownList, Width = 640, Name = "HandoverReceiver" };
        receiver.SelectedIndex = -1;
        var machine = Ui.Input("Máy nhận (thông tin lịch sử)", data.Ca.MaMay, 640);
        var reason = Ui.Input("Lý do bàn giao", "", 640); reason.MaxLength = 500;
        var error = Ui.Label("", 9, false, CafeTheme.Danger);
        var confirm = Ui.Button("Xác nhận bàn giao", () => { if (receiver.SelectedValue is not long || string.IsNullOrWhiteSpace(reason.Text)) { error.Text = "Chọn nhân viên nhận và nhập lý do."; return; } form.DialogResult = DialogResult.OK; }, true);
        AdminUi.Compose(form, Ui.Panel("Người nhận và nguồn bàn giao", Ui.Field("Nhân viên nhận *", receiver), Ui.Field("Máy nhận (lịch sử)", machine), Ui.Field("Lý do *", reason),
            Ui.Label($"Bàn giao toàn bộ {data.DonDangXuLy.Count} đơn đang xử lý và {data.KhoanChuaRo.Count} khoản chưa rõ trong đối chiếu hiện tại.", 9, false, CafeTheme.Muted), error), confirm, Ui.Button("Hủy", () => form.Close()));
        return form.ShowDialog(this) == DialogResult.OK ? new((long)receiver.SelectedValue!, machine.Text, reason.Text,
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
