using System.Text.RegularExpressions;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Staff;

namespace FourChang.Desktop.Admin.Employees;

/// <summary>Thêm/sửa hồ sơ nhân viên; không có username, mật khẩu hoặc mã hiển thị riêng.</summary>
public sealed class EmployeeEditorForm : Form
{
    private readonly Employee? original;
    private readonly TextBox name = Ui.Input("Họ và tên đầy đủ", "", 400);
    private readonly TextBox phone = Ui.Input("Số điện thoại liên hệ", "", 300);
    private readonly TextBox address = Ui.Input("Địa chỉ liên hệ", "", 600);
    private readonly DateTimePicker date = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 260 };
    private readonly Label error = Ui.Label("", 9, false, CafeTheme.Danger);
    private readonly Button save;
    private readonly Button cancel;
    private bool saving;
    private EmployeeInput? pending;
    public Employee? SavedEmployee { get; private set; }

    /// <summary>Điền bản mới nhất và giữ dữ liệu trên form khi sai trường, mất mạng hoặc version xung đột.</summary>
    public EmployeeEditorForm(Employee? employee)
    {
        original = employee;
        Text = employee is null ? "Thêm nhân viên" : "Sửa hồ sơ nhân viên";
        Name = "EmployeeEditor";
        ClientSize = new Size(820, 540);
        MinimumSize = new Size(700, 510);
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = CafeTheme.Cream;
        Font = CafeTheme.Body();
        name.Name = "EmployeeName"; phone.Name = "EmployeePhone"; address.Name = "EmployeeAddress"; date.Name = "EmployeeStartDate";
        name.AccessibleName = "Họ tên"; phone.AccessibleName = "Điện thoại"; address.AccessibleName = "Địa chỉ liên hệ";
        name.MaxLength = 120; phone.MaxLength = 20; address.MaxLength = 500;
        name.Text = employee?.HoTen ?? ""; phone.Text = employee?.SoDienThoai ?? ""; address.Text = employee?.DiaChiLienHe ?? "";
        date.Value = employee?.NgayVaoLam ?? DateTime.Today;
        error.Name = "EmployeeFeedback";
        save = Ui.Button("Lưu hồ sơ", () => { }, true); save.Name = "EmployeeSave";
        cancel = Ui.Button("Hủy", () => Close());
        var body = Ui.Panel(employee is null ? "Hồ sơ nhân viên mới" : $"Hồ sơ nhân viên #{employee.MaNhanVien}",
            Ui.Label(employee is null ? "Hồ sơ mới ở trạng thái Đang làm. Nhân viên dùng mã nội bộ và họ tên để mở ca." : "Thông tin đã chốt trong lịch sử ca/giao dịch được giữ nguyên.", 9, false, CafeTheme.Muted),
            AdminListLayout.Fields(AdminListLayout.Field("Họ tên *", name), AdminListLayout.Field("Điện thoại (nếu có)", phone)),
            AdminListLayout.Field("Địa chỉ liên hệ (nếu có)", address),
            AdminListLayout.Fields(AdminListLayout.Field("Ngày vào làm *", date), Ui.Label(employee?.TrangThai == "INACTIVE" ? "Trạng thái: Ngừng làm" : "Trạng thái: Đang làm", 10, true)), error);
        AdminUi.Compose(this, body, save, cancel);
        foreach (var field in new[] { name, phone, address }) field.TextChanged += (_, _) => error.Text = "";
        date.ValueChanged += (_, _) => error.Text = "";
        save.Click += async (_, _) => await Save();
        FormClosing += ConfirmClose;
    }

    /// <summary>Kiểm tra sớm các trường hồ sơ; máy chủ vẫn kiểm tra lại toàn bộ dữ liệu và quyền.</summary>
    private bool ValidateFields()
    {
        if (string.IsNullOrWhiteSpace(name.Text)) { error.Text = "Họ tên không được để trống."; name.Focus(); return false; }
        if (!string.IsNullOrWhiteSpace(phone.Text) && !Regex.IsMatch(phone.Text.Trim(), @"^\+?[0-9]{8,15}$"))
        { error.Text = "Số liên hệ gồm 8–15 chữ số, có thể bắt đầu bằng +."; phone.Focus(); return false; }
        return true;
    }

    /// <summary>Giữ nguyên request khi timeout; chỉ tạo mã mới sau lỗi xác định đã rollback.</summary>
    private async Task Save()
    {
        if (saving || pending is null && !ValidateFields()) return;
        pending ??= new(name.Text, phone.Text, date.Value.Date, original?.TrangThai ?? "ACTIVE", original?.PhienBanDuLieu,
            Guid.NewGuid(), null, address.Text);
        SetBusy(true);
        try
        {
            SavedEmployee = await AdminUi.Recover(() => LiveSession.Api.SaveEmployeeAsync(pending, original?.MaNhanVien),
                () => LiveSession.Api.EmployeeReceiptAsync(pending.MaThaoTac));
            SetBusy(false);
            DialogResult = DialogResult.OK;
        }
        catch (CafeApiException ex)
        {
            error.Text = ex.Message;
            if (ex.StatusCode is not null && (int)ex.StatusCode < 500) pending = null;
            save.Text = pending is null ? "Lưu hồ sơ" : "Tra kết quả / thử lại";
        }
        finally { SetBusy(false); }
        if (DialogResult == DialogResult.OK) Close();
    }

    /// <summary>Khóa ô trong lúc gửi hoặc khi kết quả chưa rõ để không trộn nội dung vào mã thao tác cũ.</summary>
    private void SetBusy(bool value)
    {
        saving = value;
        if (IsDisposed) return;
        save.Enabled = cancel.Enabled = !value;
        name.Enabled = phone.Enabled = address.Enabled = date.Enabled = !value && pending is null;
        UseWaitCursor = value;
    }

    /// <summary>Không bỏ thay đổi chưa lưu hoặc đóng form khi yêu cầu đang gửi mà không thông báo.</summary>
    private void ConfirmClose(object? sender, FormClosingEventArgs e)
    {
        if (saving) { e.Cancel = true; return; }
        if (DialogResult == DialogResult.OK) return;
        var changed = name.Text != (original?.HoTen ?? "") || phone.Text != (original?.SoDienThoai ?? "") ||
            address.Text != (original?.DiaChiLienHe ?? "") || date.Value.Date != (original?.NgayVaoLam.Date ?? DateTime.Today);
        if (changed || pending is not null)
            e.Cancel = MessageBox.Show(this, pending is null ? "Bỏ những thay đổi chưa lưu?" : "Kết quả lưu chưa rõ. Đóng form sẽ bỏ việc theo dõi mã yêu cầu. Bạn vẫn muốn đóng?",
                "Xác nhận đóng", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes;
    }
}
