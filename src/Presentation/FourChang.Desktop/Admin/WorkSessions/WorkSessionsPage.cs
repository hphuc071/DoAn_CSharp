// UC: UC-ADM-08
// Chức năng: Tìm và mở chi tiết ca cần quản lý đối chiếu.
// Cần nhớ: Đọc hàm từ bước kiểm tra dữ liệu đến bước trả kết quả; các quyết định nghiệp vụ do Backend kiểm tra.

using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Admin.WorkSessions;

/// <summary>UC-ADM-08: lọc/phân trang ca, giữ mã nhân viên khi mở lịch sử và không có quản trị thiết bị.</summary>
public sealed class WorkSessionsPage : UserControl
{
    private readonly TextBox employee = Ui.Input("Tên hoặc mã nhân viên", "", 235);

    private readonly ComboBox name = Ui.Select("Tất cả", "Sáng", "Chiều", "Tối");
    private readonly ComboBox state = Ui.Select("Tất cả", "Đang làm", "Chờ đối chiếu", "Đã kết thúc", "Đã bàn giao");
    private readonly DateTimePicker from = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 160, Value = DateTime.Today.AddDays(-30) };
    private readonly DateTimePicker to = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 160, Value = DateTime.Today };
    private readonly DataGridView grid = Ui.Grid(new([], []), 400);
    private readonly Label message = Ui.Label("", 9, false, CafeTheme.Muted);
    private readonly Label counter = Ui.Label("", 9, false, CafeTheme.Muted);
    private readonly List<Button> buttons = [];
    private readonly AdminListLayout layout;
    private string? boundSnapshot;
    private int page = 1;
    private int total;
    private bool busy;
    private DateTime? loaded;

    /// <summary>Ngày nhập trực tiếp không có checkbox; lịch sử hồ sơ dùng sẵn khóa nhân viên.</summary>
    public WorkSessionsPage(long? employeeId = null)
    {
        message.Visible = false; message.TextChanged += (_, _) => message.Visible = !string.IsNullOrWhiteSpace(message.Text);
        Name = "WorkSessionsPage"; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; Dock = DockStyle.Top; BackColor = CafeTheme.Cream; Padding = new Padding(16);
        employee.Text = employeeId?.ToString() ?? "";
        if (employeeId is not null) from.Value = DateTime.Today.AddYears(-1);
        state.Width = 210;
        employee.Name = "WorkSessionEmployeeFilter"; name.Name = "WorkSessionNameFilter";
        state.Name = "WorkSessionStateFilter"; from.Name = "WorkSessionFrom"; to.Name = "WorkSessionTo"; grid.Name = "WorkSessionsGrid";
        foreach (var (property, caption, weight) in new[] { ("MaCaHienThi", "Mã ca", 75f), ("HoTen", "Nhân viên", 160f), ("TenCa", "Ca", 75f),
            ("BatDauLuc", "Bắt đầu", 130f), ("KetThucLuc", "Kết thúc", 130f), ("TienMatThucDem", "Tiền kiểm đếm", 120f), ("TrangThai", "Trạng thái", 145f) })
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = property, DataPropertyName = property, HeaderText = caption, FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable });        grid.AutoGenerateColumns = false;
        grid.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex < 0) return;
            if (e.Value is DateTime utc) { e.Value = utc.ToLocalTime().ToString("dd/MM/yyyy\nHH:mm"); e.FormattingApplied = true; }
            if (grid.Columns[e.ColumnIndex].Name == "TienMatThucDem") { e.Value = e.Value is decimal amount ? amount.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + "đ" : "Chưa gửi"; e.FormattingApplied = true; }
            if (grid.Columns[e.ColumnIndex].Name == "TrangThai" && e.Value is string status) e.Value = status switch { "OPEN" => "Đang mở", "LOCKED" => "Chờ đối chiếu", "CLOSED" => "Đã chốt", "HANDED_OVER" => "Đã bàn giao", _ => status };
        };
        var filterActions = Ui.Row(Action("Tìm kiếm", Find, true), Action("Tải lại", Reload));
        filterActions.Dock = DockStyle.None; filterActions.Margin = new Padding(0, 30, 0, 8);
        var filters = Ui.Row();
        CommerceWorkspaceVisuals.AdminFilters(filters, employee, from, to, ("Tên ca", name), ("Trạng thái", state));
        filters.Controls.Add(filterActions);
        var actions = Ui.Row(Action("Xem chi tiết ca", Details, true));
        var footer = Ui.Row(Action("Trang trước", Previous), Action("Trang sau", Next), counter);
        layout = new AdminListLayout("Lịch sử ca làm việc", "Đơn hoàn thành • Kết quả kiểm đếm • Bàn giao đơn còn dở", filters, actions, message, grid, footer, "NHÂN SỰ / CA LÀM VIỆC", fitTableToRows: false);        Controls.Add(layout);
        CommerceWorkspaceVisuals.AdminStyle(filters, actions, grid);
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        foreach (DataGridViewColumn column in grid.Columns)
        {
            column.MinimumWidth = column.DataPropertyName switch { "MaCaHienThi" => 70, "HoTen" => 150, "TenCa" => 75, "TrangThai" => 140, _ => 120 };
            if (column.DataPropertyName is "BatDauLuc" or "KetThucLuc" or "HoTen") column.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            if (column.DataPropertyName == "TienMatThucDem") column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.CellPainting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex].Name != "TrangThai" || grid.Rows[e.RowIndex].DataBoundItem is not WorkSessionInfo row) return;
            CommerceWorkspaceVisuals.StatusBadge(grid, e, row.TrangThai switch { "OPEN" => "Đang làm", "CLOSED" => "Đã kết thúc", "HANDED_OVER" => "Đã bàn giao", _ => "Chờ đối chiếu" }, row.TrangThai is "CLOSED" or "HANDED_OVER");
        };
        employee.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter && !busy) { e.SuppressKeyPress = true; SetBusy(true); try { await Find(); } finally { SetBusy(false); } } };
        grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0 && !busy) await Details(); };
        HandleCreated += async (_, _) => { SetBusy(true); try { await LoadPage(); } finally { SetBusy(false); } };
    }

    /// <summary>Gắn các hành động UI; khóa nút trong lúc tải và xử lý lỗi quyền tập trung.</summary>
    private Button Action(string title, Func<Task> action, bool primary = false)
    {
        var button = Ui.Button(title, () => { }, primary); buttons.Add(button);
        button.Click += async (_, _) => { if (busy) return; SetBusy(true); try { await action(); } catch (CafeApiException ex) { if (!IsDisposed) AdminUi.Error(this, ex); } finally { SetBusy(false); } };
        return button;
    }
    /// <summary>Không cho nhiều request trả về đè bộ lọc của nhau.</summary>
    private void SetBusy(bool value)
    {
        busy = value; if (IsDisposed) return;
        foreach (var button in buttons) button.Enabled = !value;
        employee.Enabled = name.Enabled = state.Enabled = from.Enabled = to.Enabled = !value;
        layout.Busy = value;
    }
    /// <summary>Kiểm tra ngày/mã trước khi gửi; giữ bảng cũ nếu mạng lỗi, không tạo ca mẫu.</summary>
    private async Task LoadPage()
    {
        long? id = null; string? term = null;
        if (employee.Text.Trim().Length > 0)
        {
            if (long.TryParse(employee.Text.Trim(), out var parsed) && parsed > 0) id = parsed; else term = employee.Text.Trim();
        }
        if (from.Value.Date > to.Value.Date) { message.Text = "Từ ngày không được sau đến ngày."; return; }
        try
        {
            var data = await LiveSession.Api.GetWorkSessionsAsync(id, from.Value.Date, to.Value.Date.AddDays(1), name.SelectedIndex == 0 ? "" : name.Text,
                state.SelectedIndex switch { 1 => "OPEN", 2 => "LOCKED", 3 => "CLOSED", 4 => "HANDED_OVER", _ => "" }, "", page, term);
            if (IsDisposed) return;
            var snapshot = System.Text.Json.JsonSerializer.Serialize(data.Items);
            if (snapshot != boundSnapshot)
            {
                var selected = (grid.CurrentRow?.DataBoundItem as WorkSessionInfo)?.MaCaLamViec;
                grid.DataSource = data.Items.ToList(); boundSnapshot = snapshot;
                var index = data.Items.Select((x, i) => (x, i)).FirstOrDefault(x => x.x.MaCaLamViec == selected).i;
                if (grid.RowCount > 0) grid.CurrentCell = grid.Rows[index].Cells[0];
            }
            total = data.Total; loaded = DateTime.Now;
            counter.Text = $"Trang {page}/{Math.Max(1, (total + 19) / 20)} · {total} ca";
            message.Text = "";
        }
        catch (CafeApiException ex) { message.Text = ex.Message + (loaded is null ? " Chưa tải được ca." : " · Đang hiển thị dữ liệu đã tải trước đó."); }
    }
    /// <summary>Thay bộ lọc đưa về trang đầu.</summary>
    private async Task Find() { page = 1; await LoadPage(); }
    /// <summary>Tải lại giữ bộ lọc đang xem và đưa về trang đầu.</summary>
    private async Task Reload() { page = 1; await LoadPage(); }
    /// <summary>Không gửi chỉ số trang nhỏ hơn 1.</summary>
    private async Task Previous() { if (page > 1) { page--; await LoadPage(); } }
    /// <summary>Chỉ mở trang còn nằm trong tổng do API trả.</summary>
    private async Task Next() { if (page * 20 < total) { page++; await LoadPage(); } }
    /// <summary>Đọc chi tiết mới nhất; đóng dialog vẫn giữ bộ lọc danh sách.</summary>
    private async Task Details()
    {
        if (grid.CurrentRow?.DataBoundItem is not WorkSessionInfo shift) return;
        using var form = new WorkSessionDetailsForm(await LiveSession.Api.GetWorkSessionAsync(shift.MaCaLamViec));
        form.ShowDialog(FindForm());
        await LoadPage();
    }
}
