using FourChang.Desktop.Admin.WorkSessions;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Staff;

namespace FourChang.Desktop.Admin.Employees;

/// <summary>ADM-11: danh sách phân trang, hồ sơ, trạng thái/xóa có điều kiện và nối lịch sử ca ADM-12.</summary>
public sealed class EmployeesPage : UserControl
{
    private readonly TextBox search = Ui.Input("Tên hoặc số điện thoại", "", 340);
    private readonly ComboBox status = Ui.Select("Tất cả", "Đang làm", "Ngừng làm");
    private readonly DataGridView grid = Ui.Grid(new([], []), 400);
    private readonly Label message = Ui.Label("", 9, false, CafeTheme.Muted);
    private readonly Label counter = Ui.Label("", 9, false, CafeTheme.Muted);
    private readonly List<Button> buttons = [];
    private readonly AdminListLayout layout;
    private int page = 1;
    private int total;
    private bool busy;
    private DateTime? loaded;
    private readonly Dictionary<long, EmployeeStatusInput> pendingStatuses = [];
    private readonly Dictionary<long, DeleteEmployeeInput> pendingDeletions = [];

    /// <summary>Dựng khung đồng bộ các trang quản lý đã tinh chỉnh, giữ mã nội bộ làm khóa chọn dòng.</summary>
    public EmployeesPage()
    {
        message.Visible = false; message.TextChanged += (_, _) => message.Visible = !string.IsNullOrWhiteSpace(message.Text);
        Name = "EmployeesPage"; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; Dock = DockStyle.Top; Padding = new Padding(16); BackColor = CafeTheme.Cream;
        search.Name = "EmployeeSearch"; status.Name = "EmployeeStatusFilter"; grid.Name = "EmployeesGrid";
        foreach (var (property, caption, weight) in new[] { ("MaNhanVien", "Mã", 45f), ("HoTen", "Họ tên", 150f), ("SoDienThoai", "Điện thoại", 100f),
            ("DiaChiLienHe", "Địa chỉ liên hệ", 180f), ("NgayVaoLam", "Ngày vào làm", 100f), ("TrangThai", "Trạng thái", 100f) })
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = property, DataPropertyName = property, HeaderText = caption, FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable });
        grid.AutoGenerateColumns = false;
        grid.Columns["NgayVaoLam"].DefaultCellStyle.Format = "dd/MM/yyyy";
        grid.CellFormatting += (_, e) => { if (e.ColumnIndex >= 0 && grid.Columns[e.ColumnIndex].Name == "TrangThai") e.Value = e.Value?.ToString() == "ACTIVE" ? "Đang làm" : "Ngừng làm"; };
        var filters = Ui.Row(Ui.Field("Tìm kiếm", search), Ui.Field("Trạng thái", status));
        var filterActions = Ui.Row(Action("Tìm kiếm", Find, true), Action("Tải lại", Reset));
        filterActions.Dock = DockStyle.None; filterActions.Margin = new Padding(0, 30, 0, 8); filters.Controls.Add(filterActions);
        var actions = Ui.Row(Action("Thêm nhân viên", () => Edit(null), true), Action("Xem hồ sơ", View), Action("Sửa hồ sơ", EditSelected),
            Action("Ngừng / kích hoạt lại", ChangeStatus), Action("Xóa có điều kiện", Delete), Action("Lịch sử ca", History));
        var footer = Ui.Row(Action("Trang trước", Previous), Action("Trang sau", Next), counter);
        layout = new AdminListLayout("Danh sách nhân viên", "Hồ sơ làm việc · Trạng thái · Lịch sử ca; không tạo tài khoản nhân viên", filters, actions, message, grid, footer, "NHÂN SỰ / NHÂN VIÊN");
        Controls.Add(layout);
        HandleCreated += async (_, _) => { SetBusy(true); try { await LoadPage(); } finally { SetBusy(false); } };
        search.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; if (!busy) await Find(); } };
    }

    /// <summary>Giữ điểm gọi router cũ để tích hợp không phải đổi nơi tạo trang.</summary>
    public static Control Create() => new EmployeesPage();

    /// <summary>Gắn lệnh bất đồng bộ với busy/error tập trung, không thay nghiệp vụ trong nút UI.</summary>
    private Button Action(string text, Func<Task> action, bool primary = false)
    {
        var button = Ui.Button(text, () => { }, primary, text.StartsWith("Xóa"));
        buttons.Add(button);
        button.Click += async (_, _) =>
        {
            if (busy) return;
            SetBusy(true);
            try { await action(); }
            catch (CafeApiException ex) { if (!IsDisposed) AdminUi.Error(this, ex); }
            finally { SetBusy(false); }
        };
        return button;
    }

    /// <summary>Khóa truy vấn chồng nhau và chỉ chạy loading trong lúc có tác vụ.</summary>
    private void SetBusy(bool value)
    {
        busy = value;
        if (IsDisposed) return;
        foreach (var button in buttons) button.Enabled = !value;
        search.Enabled = status.Enabled = !value;
        layout.Busy = value;
    }

    /// <summary>Tải trang từ server; mất mạng giữ bảng cũ kèm thời điểm, không thay bằng số liệu giả.</summary>
    private async Task LoadPage()
    {
        try
        {
            var data = await LiveSession.Api.SearchEmployeesAsync(search.Text.Trim(), status.SelectedIndex switch { 1 => "ACTIVE", 2 => "INACTIVE", _ => "" }, page);
            if (IsDisposed) return;
            total = data.Total;
            if (data.Items.Count == 0 && total > 0 && page > 1) { page = Math.Max(1, (total + 19) / 20); await LoadPage(); return; }
            grid.DataSource = data.Items.ToList();
            loaded = DateTime.Now;
            counter.Text = $"Trang {page}/{Math.Max(1, (total + 19) / 20)} · {total} nhân viên";
            message.Text = "";
        }
        catch (CafeApiException ex) { message.Text = ex.Message + (loaded is null ? " Chưa tải được hồ sơ." : " · Đang hiển thị dữ liệu đã tải trước đó."); }
    }

    /// <summary>Bộ lọc mới luôn bắt đầu trang đầu.</summary>
    private async Task Find() { page = 1; await LoadPage(); }
    /// <summary>Bỏ từ khóa/trạng thái trước tải lại, trả tổng ban đầu.</summary>
    private async Task Reset() { search.Clear(); status.SelectedIndex = 0; page = 1; await LoadPage(); }
    /// <summary>Không gửi chỉ số trang âm.</summary>
    private async Task Previous() { if (page > 1) { page--; await LoadPage(); } }
    /// <summary>Không đi quá tổng do máy chủ trả.</summary>
    private async Task Next() { if (page * 20 < total) { page++; await LoadPage(); } }
    /// <summary>Lấy bản mới nhất trước khi xem/sửa, không suy nhân viên từ tên hoặc vị trí dòng.</summary>
    private async Task<Employee?> Selected() => grid.CurrentRow?.DataBoundItem is Employee employee ? await LiveSession.Api.GetEmployeeAsync(employee.MaNhanVien) : null;
    /// <summary>Mở editor với rowversion mới nhất.</summary>
    private async Task EditSelected() { var item = await Selected(); if (item is not null) await Edit(item); }
    /// <summary>Lưu thành công mới tải lại danh sách; hủy không tạo hồ sơ nháp trên server.</summary>
    private async Task Edit(Employee? item) { using var form = new EmployeeEditorForm(item); if (form.ShowDialog(FindForm()) == DialogResult.OK) await LoadPage(); }

    /// <summary>Xem địa chỉ/điện thoại/trạng thái và liên kết lịch sử, không sửa snapshot ca đã chốt.</summary>
    private async Task View()
    {
        var item = await Selected(); if (item is null) return;
        using var form = AdminUi.Dialog("Hồ sơ nhân viên", 800, 510);
        form.Name = "EmployeeDetails";
        var history = Ui.Button("Lịch sử ca", () => ShowHistory(item));
        AdminUi.Compose(form, Ui.Panel(item.HoTen, Ui.Label($"Mã nhân viên: {item.MaNhanVien}", 10, true),
            Ui.Label("Điện thoại: " + (item.SoDienThoai ?? "Chưa có")), Ui.Label("Địa chỉ liên hệ: " + (item.DiaChiLienHe ?? "Chưa có")),
            Ui.Label($"Ngày vào làm: {item.NgayVaoLam:dd/MM/yyyy}"), Ui.Label(item.TrangThai == "ACTIVE" ? "Đang làm" : "Ngừng làm")), history, Ui.Button("Đóng hồ sơ", () => form.Close()));
        form.ShowDialog(FindForm());
    }

    /// <summary>Giữ bộ lọc MaNhanVien để phân biệt tên trùng, đóng lịch sử quay lại đúng hồ sơ/danh sách.</summary>
    private void ShowHistory(Employee item)
    {
        using var form = AdminUi.Dialog($"Lịch sử ca · {item.HoTen} · #{item.MaNhanVien}", 1180, 800);
        form.Name = "EmployeeShiftHistory";
        form.Controls.Add(new WorkSessionsPage(item.MaNhanVien));
        form.ShowDialog(FindForm());
    }
    /// <summary>Mở lịch sử nhân viên được chọn, không tự tạo hoặc kết thúc ca.</summary>
    private async Task History() { var item = await Selected(); if (item is not null) ShowHistory(item); }

    /// <summary>Ngừng/kích hoạt có lý do; server kiểm tra ca và biên bản bàn giao trước khi lưu.</summary>
    private async Task ChangeStatus()
    {
        var item = await Selected(); if (item is null) return;
        if (!pendingStatuses.TryGetValue(item.MaNhanVien, out var input))
        {
            var reason = WorkforceDialogs.Reason(this, item.TrangThai == "ACTIVE" ? "Ngừng làm" : "Kích hoạt lại", "Giữ nguyên lịch sử ca/giao dịch. Ca chưa kết thúc hoặc chờ nhận bàn giao sẽ chặn ngừng làm.");
            if (reason is null) return;
            input = new(item.TrangThai == "ACTIVE" ? "INACTIVE" : "ACTIVE", reason, item.PhienBanDuLieu!, Guid.NewGuid());
            pendingStatuses[item.MaNhanVien] = input;
        }
        try
        {
            await AdminUi.Recover(() => LiveSession.Api.SetEmployeeStatusAsync(item.MaNhanVien, input), () => LiveSession.Api.EmployeeReceiptAsync(input.MaThaoTac));
            pendingStatuses.Remove(item.MaNhanVien);
        }
        catch (CafeApiException ex) { if (ex.StatusCode is not null && (int)ex.StatusCode < 500) pendingStatuses.Remove(item.MaNhanVien); throw; }
        await LoadPage();
    }

    /// <summary>Máy chủ kiểm tra tham chiếu/version sau hộp xác nhận; không xóa người đã có lịch sử ca.</summary>
    private async Task Delete()
    {
        if (grid.CurrentRow?.DataBoundItem is not Employee selected) return;
        var id = selected.MaNhanVien;
        if (!pendingDeletions.TryGetValue(id, out var input))
        {
            var item = await LiveSession.Api.GetEmployeeAsync(id);
            if (MessageBox.Show(FindForm(), $"Xóa hồ sơ #{id} · {item.HoTen}? Chỉ xóa được khi chưa có ca hoặc tham chiếu.", "Xác nhận xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            input = new(item.PhienBanDuLieu!, Guid.NewGuid());
            pendingDeletions[id] = input;
        }
        try
        {
            await AdminUi.Recover(() => LiveSession.Api.DeleteEmployeeAsync(id, input), () => LiveSession.Api.EmployeeOperationReceiptAsync(input.MaThaoTac));
            pendingDeletions.Remove(id);
        }
        catch (CafeApiException ex) { if (ex.StatusCode is not null && (int)ex.StatusCode < 500) pendingDeletions.Remove(id); throw; }
        await LoadPage();
    }
}
