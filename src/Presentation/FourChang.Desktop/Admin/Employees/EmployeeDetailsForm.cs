using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.Contracts.Staff;

namespace FourChang.Desktop.Admin.Employees;

/// <summary>Hồ sơ nhân viên chỉ đọc, tách giao diện khỏi điều phối tìm kiếm/lưu; lịch sử mở đúng mã nhân viên.</summary>
internal sealed class EmployeeDetailsForm : Form
{
    /// <summary>Thể hiện đủ hồ sơ và địa chỉ liên hệ đã được bổ sung, không tạo tài khoản nhân viên.</summary>
    internal EmployeeDetailsForm(Employee employee, Action history)
    {
        Name = "EmployeeDetailsForm"; Text = "Hồ sơ nhân viên"; ClientSize = new Size(820, 640); MinimumSize = new Size(720, 580); StartPosition = FormStartPosition.CenterParent;
        var close = Ui.Button("Đóng hồ sơ", Close);
        var shifts = Ui.Button("Lịch sử ca", history, true);
        var address = Ui.Label(employee.DiaChiLienHe ?? "Chưa cập nhật địa chỉ liên hệ.", 10); address.AutoSize = false; address.Height = 65; address.Dock = DockStyle.Top;
        var body = AdminListLayout.Column(AdminProfileLayout.Card("Thông tin làm việc", AdminProfileLayout.Info(
            ("Mã nhân viên", $"#{employee.MaNhanVien:0000}"), ("Điện thoại", employee.SoDienThoai),
            ("Ngày vào làm", employee.NgayVaoLam.ToString("dd/MM/yyyy")), ("Trạng thái", WorkforceUi.Status(employee.TrangThai)))),
            AdminProfileLayout.Card("Địa chỉ liên hệ", address));
        AdminProfileLayout.Mount(this, AdminProfileLayout.Header(employee.HoTen, $"Nhân viên · Mã #{employee.MaNhanVien:0000}", WorkforceUi.Status(employee.TrangThai)), body, close, shifts);
        Shown += (_, _) => ActiveControl = close;
    }
}
