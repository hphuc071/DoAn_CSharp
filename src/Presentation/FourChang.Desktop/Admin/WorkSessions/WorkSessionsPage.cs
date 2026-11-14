using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Admin.WorkSessions;

/// <summary>ADM-12: lọc/phân trang ca, giữ mã nhân viên khi mở lịch sử và không có quản trị thiết bị.</summary>
public sealed class WorkSessionsPage : UserControl
{
    private readonly TextBox employee = Ui.Input("Mã nhân viên, để trống: tất cả", "", 235);
    private readonly TextBox machine = Ui.Input("Mã máy lịch sử", "", 180);
    private readonly ComboBox name = Ui.Select("Tất cả ca", "Sáng", "Chiều", "Tối");
    private readonly ComboBox state = Ui.Select("Tất cả trạng thái", "Đang mở", "Đang khóa", "Đã chốt", "Đã bàn giao");
    private readonly DateTimePicker from = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 160, Value = DateTime.Today.AddMonths(-1) };
    private readonly DateTimePicker to = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 160, Value = DateTime.Today };
    private readonly DataGridView grid = Ui.Grid(new([], []), 400);
    private readonly Label message = Ui.Label("", 9, false, CafeTheme.Muted);
    private readonly Label counter = Ui.Label("", 9, false, CafeTheme.Muted);
    private readonly List<Button> buttons = [];
    private readonly AdminListLayout layout;
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
        employee.Name = "WorkSessionEmployeeFilter"; machine.Name = "WorkSessionMachineFilter"; name.Name = "WorkSessionNameFilter";
        state.Name = "WorkSessionStateFilter"; from.Name = "WorkSessionFrom"; to.Name = "WorkSessionTo"; grid.Name = "WorkSessionsGrid";
        foreach (var (property, caption, weight) in new[] { ("MaCaLamViec", "Mã ca", 55f), ("MaNhanVien", "Mã NV", 55f), ("HoTen", "Nhân viên", 160f),
            ("TenCa", "Ca", 65f), ("MaMay", "Máy (lịch sử)", 110f), ("BatDauLuc", "Bắt đầu", 120f), ("KetThucLuc", "Kết thúc", 120f), ("TrangThai", "Trạng thái", 105f) })
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = property, DataPropertyName = property, HeaderText = caption, FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable });
        grid.AutoGenerateColumns = false;
        grid.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex < 0) return;
            if (e.Value is DateTime utc) e.Value = utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            if (grid.Columns[e.ColumnIndex].Name == "TrangThai" && e.Value is string status) e.Value = status switch { "OPEN" => "Đang mở", "LOCKED" => "Đang khóa", "CLOSED" => "Đã chốt", "HANDED_OVER" => "Đã bàn giao", _ => status };
        };
        var filterActions = Ui.Row(Action("Tìm kiếm", Find, true), Action("Tải lại", Reload));
        filterActions.Dock = DockStyle.None; filterActions.Margin = new Padding(0, 30, 0, 8);
        var machineField = Ui.Field("Máy", machine);
        var filters = Ui.Row(Ui.Field("Nhân viên", employee), Ui.Field("Tên ca", name), Ui.Field("Trạng thái", state), machineField,
            Ui.Field("Từ ngày", from), Ui.Field("Đến hết ngày", to), filterActions);
        // Một FlowLayoutPanel đo theo chiều rộng thật, không lồng các hàng AutoSize gây feedback chiều cao.
        filters.SetFlowBreak(machineField, true);
        var actions = Ui.Row(Action("Xem đối chiếu", Details, true));
        var footer = Ui.Row(Action("Trang trước", Previous), Action("Trang sau", Next), counter);
        layout = new AdminListLayout("Giám sát ca làm việc", "Tra cứu nguồn tiền · Đối chiếu thực đếm · Chốt / bàn giao có lịch sử", filters, actions, message, grid, footer, "NHÂN SỰ / CA LÀM VIỆC");
        Controls.Add(layout);
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
        employee.Enabled = machine.Enabled = name.Enabled = state.Enabled = from.Enabled = to.Enabled = !value;
        layout.Busy = value;
    }
    /// <summary>Kiểm tra ngày/mã trước khi gửi; giữ bảng cũ nếu mạng lỗi, không tạo ca mẫu.</summary>
    private async Task LoadPage()
    {
        long? id = null;
        if (employee.Text.Trim().Length > 0)
        {
            if (!long.TryParse(employee.Text.Trim(), out var parsed) || parsed <= 0) { message.Text = "Mã nhân viên phải là số nguyên dương; để trống nếu xem tất cả."; return; }
            id = parsed;
        }
        if (from.Value.Date > to.Value.Date) { message.Text = "Từ ngày không được sau đến ngày."; return; }
        try
        {
            var data = await LiveSession.Api.GetWorkSessionsAsync(id, from.Value.Date, to.Value.Date.AddDays(1), name.SelectedIndex == 0 ? "" : name.Text,
                state.SelectedIndex switch { 1 => "OPEN", 2 => "LOCKED", 3 => "CLOSED", 4 => "HANDED_OVER", _ => "" }, machine.Text.Trim(), page);
            if (IsDisposed) return;
            grid.DataSource = data.Items.ToList(); total = data.Total; loaded = DateTime.Now;
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
