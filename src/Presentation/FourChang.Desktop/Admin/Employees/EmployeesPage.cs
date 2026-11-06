using FourChang.Desktop.Admin.WorkSessions;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Database;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Staff;
using FourChang.Presentation.Shared.Preview;

namespace FourChang.Desktop.Admin.Employees;

/// <summary>Giao diện UC-ADM-12: tìm, phân trang, xem, thêm/sửa, trạng thái và lịch sử ca; không có xóa/tài khoản nhân viên.</summary>
public sealed class EmployeesPage : UserControl
{
    private readonly TextBox search = Ui.Input("Tên hoặc số điện thoại", "", 330);
    private readonly ComboBox status = Ui.Select("Tất cả", "Đang làm", "Ngừng làm");
    private readonly DataGridView grid = Ui.Grid(new PreviewTable(["Mã nội bộ", "Họ tên", "Điện thoại", "Ngày vào làm", "Trạng thái"], []), 390);
    private readonly Label summary = Ui.Label("Đang tải…", 10);
    private readonly Label error = Ui.Label("", 9, false, CafeTheme.Danger);
    private readonly List<Control> actions = [];
    private List<Employee> records = [];
    private readonly AdminListLayout workspace;
    private int page = 1;
    private int total;
    private bool busy;

    /// <summary>Tạo trang nghiệp vụ với bảng/toolbar đồng nhất ADM-01; tải API sau khi control đã có handle.</summary>
    public EmployeesPage()
    {
        AutoSize = true; Dock = DockStyle.Top;
        search.Name = "EmployeeSearch"; status.Width = 150; grid.Name = "EmployeesGrid";
        var find = Button("Tìm kiếm", async () => { page = 1; await LoadPage(); }, true);
        var reload = Button("Tải lại", async () => { page = 1; await LoadPage(); });
        var previous = Button("Trang trước", async () => { if (page > 1) { page--; await LoadPage(); } });
        var next = Button("Trang sau", async () => { if (page * 20 < total) { page++; await LoadPage(); } });
        var filters = AdminListLayout.Fields(AdminListLayout.Field("Tìm nhân viên", search), AdminListLayout.Field("Trạng thái làm việc", status),
            Ui.Stack(Ui.Label("Thao tác", 9, true), Ui.Row(find, reload)));
        var toolbar = Ui.Row(Button("Thêm nhân viên", async () => await Edit(null), true), Button("Xem hồ sơ", async () => await Selected(View)),
            Button("Sửa hồ sơ", async () => await Selected(x => Edit(x))), Button("Ngừng / kích hoạt lại", async () => await Selected(ChangeStatus)),
            Button("Lịch sử ca", async () => await Selected(x => { History(x); return Task.CompletedTask; })));
        workspace = new AdminListLayout("Đội ngũ nhân viên", "Quản lý hồ sơ làm việc, thông tin liên hệ và lịch sử ca của từng nhân viên.", filters,
            toolbar, error, grid, Ui.Row(previous, summary, next));
        Controls.Add(workspace); AdminListLayout.StyleGrid(grid, 4);
        Load += async (_, _) => { SetBusy(true); try { await LoadPage(); } finally { if (!IsDisposed) SetBusy(false); } };
        search.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter && !busy) { e.SuppressKeyPress = true; page = 1; await LoadPage(); } };
    }

    /// <summary>Điểm tạo tương thích router Desktop hiện có.</summary>
    public static Control Create() => new EmployeesPage();

    /// <summary>Gắn hành động bất đồng bộ, khóa thao tác trong lúc chạy và xử lý lỗi xác thực tập trung.</summary>
    private Control Button(string text, Func<Task> action, bool primary = false)
    {
        var button = AdminListLayout.Action(text, primary);
        actions.Add(button);
        button.Click += async (_, _) =>
        {
            if (busy) return;
            SetBusy(true);
            try { await action(); }
            catch (CafeApiException ex) { if (!IsDisposed) WorkforceUi.Error(this, ex); }
            finally { if (!IsDisposed) SetBusy(false); }
        };
        return button;
    }

    /// <summary>Không cho hai truy vấn chồng nhau hoặc trả kết quả cũ đè bộ lọc mới.</summary>
    private void SetBusy(bool value)
    {
        busy = value; workspace.Busy = value;
        foreach (var item in actions) item.Enabled = !value && (item.Text != "Trang trước" || page > 1) && (item.Text != "Trang sau" || page * 20 < total);
        search.Enabled = status.Enabled = !value;
    }

    /// <summary>Dùng từ khóa hiện tại khi tải lại; bỏ từ khóa sẽ trả tổng ban đầu, không giữ truy vấn cũ.</summary>
    private async Task LoadPage()
    {
        try
        {
            var data = await LiveSession.Api.SearchEmployeesAsync(search.Text.Trim(), status.SelectedIndex switch { 1 => "ACTIVE", 2 => "INACTIVE", _ => "" }, page);
            if (IsDisposed) return;
            records = data.Items.ToList(); total = data.Total;
            if (records.Count == 0 && total > 0 && page > 1) { page = Math.Max(1, (total + 19) / 20); await LoadPage(); return; }
            DatabaseViews.SetGrid(grid, new(["Mã nội bộ", "Họ tên", "Điện thoại", "Ngày vào làm", "Trạng thái"],
                records.Select(x => new[] { x.MaNhanVien.ToString(), x.HoTen, x.SoDienThoai ?? "", x.NgayVaoLam.ToString("dd/MM/yyyy"), WorkforceUi.Status(x.TrangThai) }).ToArray()));
            grid.Columns[0].FillWeight = 10; grid.Columns[1].FillWeight = 30; grid.Columns[2].FillWeight = 23;
            grid.Columns[3].FillWeight = 19; grid.Columns[4].FillWeight = 18;
            summary.Text = $"Trang {page}/{Math.Max(1, (total + 19) / 20)} · {total} nhân viên";
            error.Text = "";
        }
        catch (CafeApiException ex) { if (!IsDisposed) { error.Text = "Chưa tải được dữ liệu mới. " + ex.Message; WorkforceUi.Error(this, ex); } }
    }

    /// <summary>Lấy đúng khóa và phiên bản từ API, không suy ra nhân viên từ tên trùng.</summary>
    private async Task Selected(Func<Employee, Task> action)
    {
        if (grid.CurrentRow is null) { MessageBox.Show(FindForm(), "Chọn một nhân viên."); return; }
        var id = Convert.ToInt64(grid.CurrentRow.Cells[0].Value);
        await action(await LiveSession.Api.GetEmployeeAsync(id));
    }

    /// <summary>Xem hồ sơ chỉ đọc và cho mở lịch sử; đóng hộp thoại không làm thay đổi dữ liệu.</summary>
    private Task View(Employee item)
    {
        using var form = new EmployeeDetailsForm(item, () => History(item));
        form.ShowDialog(FindForm());
        return Task.CompletedTask;
    }

    /// <summary>Mở ADM-13 có sẵn khóa nhân viên; đóng lịch sử quay lại hồ sơ/danh sách hiện tại.</summary>
    private void History(Employee item)
    {
        using var form = WorkforceUi.Dialog($"Lịch sử ca · {item.HoTen} · #{item.MaNhanVien}", 1100, 730);
        form.Controls.Add(new WorkSessionsPage(item.MaNhanVien));
        form.ShowDialog(FindForm());
    }

    /// <summary>Form thêm/sửa giữ nháp khi lỗi; mã yêu cầu giữ nguyên nếu timeout và chỉ sinh mới khi dữ liệu được sửa hợp lệ.</summary>
    private async Task Edit(Employee? item)
    {
        using var form = WorkforceUi.Dialog(item is null ? "Thêm nhân viên" : "Sửa hồ sơ nhân viên", 820, 700);
        var name = Ui.Input("Họ tên", item?.HoTen ?? "", 440); name.MaxLength = 120;
        var phone = Ui.Input("Số điện thoại", item?.SoDienThoai ?? "", 440); phone.MaxLength = 20;
        var address = Ui.Input("Địa chỉ liên hệ", item?.DiaChiLienHe ?? "", 680); address.MaxLength = 500; address.Name = "EmployeeContactAddress";
        var date = new DateTimePicker { Width = 440, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Value = item?.NgayVaoLam ?? DateTime.Today };
        var message = Ui.Label("", 9, false, CafeTheme.Danger);
        var save = Ui.Button("Lưu hồ sơ", () => { }, true);
        var cancel = Ui.Button("Hủy", () => form.Close());
        var content = AdminProfileLayout.Card("Thông tin nhân viên",
            AdminListLayout.Fields(AdminListLayout.Field("Họ tên", name), AdminListLayout.Field("Số điện thoại", phone)),
            AdminListLayout.Fields(AdminListLayout.Field("Ngày vào làm", date)), AdminListLayout.Field("Địa chỉ liên hệ", address), message);
        AdminProfileLayout.Mount(form, AdminProfileLayout.Header(item?.HoTen ?? "Nhân viên mới", item is null ? "Thêm hồ sơ làm việc" : $"Chỉnh sửa hồ sơ #{item.MaNhanVien:0000}", WorkforceUi.Status(item?.TrangThai ?? "ACTIVE")), content, cancel, save);
        EmployeeInput? pending = null;
        save.Click += async (_, _) =>
        {
            save.Enabled = cancel.Enabled = false;
            pending ??= new(name.Text, phone.Text, date.Value.Date, item?.TrangThai ?? "ACTIVE", item?.PhienBanDuLieu, Guid.NewGuid(), DiaChiLienHe: address.Text);
            name.Enabled = phone.Enabled = date.Enabled = address.Enabled = false;
            try
            {
                await WorkforceUi.Recover(() => LiveSession.Api.SaveEmployeeAsync(pending, item?.MaNhanVien), () => LiveSession.Api.EmployeeReceiptAsync(pending.MaThaoTac));
                save.Enabled = true; form.DialogResult = DialogResult.OK; form.Close();
            }
            catch (CafeApiException ex)
            {
                message.Text = ex.Message;
                if (ex.StatusCode is not null) { pending = null; name.Enabled = phone.Enabled = date.Enabled = address.Enabled = true; }
                save.Text = pending is null ? "Lưu hồ sơ" : "Thử lại yêu cầu";
            }
            finally { if (!save.IsDisposed) save.Enabled = cancel.Enabled = true; }
        };
        form.FormClosing += (_, e) => { if (!save.Enabled) e.Cancel = true; };
        if (form.ShowDialog(FindForm()) == DialogResult.OK) await LoadPage();
    }

    /// <summary>Đổi trạng thái riêng có lý do; API chặn ca chưa xử lý thay vì tự đóng hoặc đổi chủ ca.</summary>
    private async Task ChangeStatus(Employee item)
    {
        using var form = WorkforceUi.Dialog("Cập nhật trạng thái làm việc", height: 380);
        var reason = Ui.Input("Nhập lý do", "", 440); reason.MaxLength = 500;
        var message = Ui.Label("", 9, false, CafeTheme.Danger);
        var save = Ui.Button(item.TrangThai == "ACTIVE" ? "Xác nhận ngừng làm" : "Kích hoạt lại", () => { }, true);
        var newStatus = item.TrangThai == "ACTIVE" ? "INACTIVE" : "ACTIVE";
        form.Controls.Add(Ui.Panel(item.HoTen, Ui.Label($"{WorkforceUi.Status(item.TrangThai)} → {WorkforceUi.Status(newStatus)}"),
            Ui.Field("Lý do", reason), message, Ui.Row(save, Ui.Button("Lịch sử ca", () => History(item)))));
        EmployeeStatusInput? pending = null;
        save.Click += async (_, _) =>
        {
            save.Enabled = reason.Enabled = false;
            pending ??= new(newStatus, reason.Text, item.PhienBanDuLieu!, Guid.NewGuid());
            try
            {
                await WorkforceUi.Recover(() => LiveSession.Api.SetEmployeeStatusAsync(item.MaNhanVien, pending), () => LiveSession.Api.EmployeeReceiptAsync(pending.MaThaoTac));
                save.Enabled = true; form.DialogResult = DialogResult.OK; form.Close();
            }
            catch (CafeApiException ex) { message.Text = ex.Message; if (ex.StatusCode is not null) { pending = null; reason.Enabled = true; } }
            finally { if (!save.IsDisposed) save.Enabled = true; }
        };
        if (form.ShowDialog(FindForm()) == DialogResult.OK) await LoadPage();
    }
}
