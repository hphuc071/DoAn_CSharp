using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Common;

namespace FourChang.Desktop.Staff.WorkSessions;

/// <summary>Đổi mã một lần do Admin cấp thành phiên chỉ nhập/gửi đối chiếu; không có tài khoản hoặc mật khẩu nhân viên.</summary>
public sealed class ShiftRecoveryForm : Form
{
    private readonly TextBox shift = Ui.Input("Mã ca cần phục hồi", "", 300);
    private readonly TextBox employee = Ui.Input("Mã nhân viên", "", 300);
    private readonly TextBox name = Ui.Input("Họ tên khớp hồ sơ", "", 600);
    private readonly TextBox code = Ui.Input("Dán mã PH-... do Quản lý cấp", "", 600);
    private readonly Label message = Ui.Label("", 9, false, CafeTheme.Danger);
    private readonly Button redeem;
    private bool busy;
    public SessionResult? Session { get; private set; }

    /// <summary>Mã được giữ trong bộ nhớ biểu mẫu và xóa khi thành công; không ghi mã vào cache/audit.</summary>
    public ShiftRecoveryForm()
    {
        Text = "Phục hồi đối chiếu ca"; Name = "ShiftRecoveryForm"; ClientSize = new(800, 560); MinimumSize = new(720, 520);
        StartPosition = FormStartPosition.CenterParent; BackColor = CafeTheme.Cream; Font = CafeTheme.Body(); AutoScaleMode = AutoScaleMode.Dpi;
        shift.Name = "RecoveryShiftId"; employee.Name = "RecoveryEmployeeId"; name.Name = "RecoveryEmployeeName"; code.Name = "RecoveryOneTimeCode";
        code.MaxLength = 51; code.UseSystemPasswordChar = true; name.MaxLength = 120;
        redeem = Ui.Button("Mở phiên đối chiếu", () => { }, true); redeem.Name = "RedeemShiftRecovery";
        redeem.Click += async (_, _) => await Redeem();
        AdminUi.Compose(this, Ui.Panel("Phục hồi để gửi số thực đếm", Ui.Label("Mã có hạn 15 phút, dùng một lần. Phiên nhận được không được bán hàng hoặc tự chốt ca.", 9, false, CafeTheme.Muted),
            AdminListLayout.Fields(AdminListLayout.Field("Mã ca *", shift), AdminListLayout.Field("Mã nhân viên *", employee)),
            AdminListLayout.Field("Họ và tên *", name), AdminListLayout.Field("Mã phục hồi *", code), message), redeem, Ui.Button("Hủy", () => Close()));
        FormClosing += (_, e) => { if (busy) e.Cancel = true; };
    }

    /// <summary>Máy chủ kiểm tra ca/hồ sơ/mã một lần; sai dữ liệu không tự tạo hoặc mở lại ca.</summary>
    private async Task Redeem()
    {
        if (busy) return;
        if (!long.TryParse(shift.Text.Trim(), out var shiftId) || shiftId <= 0 || !long.TryParse(employee.Text.Trim(), out var employeeId) || employeeId <= 0 || string.IsNullOrWhiteSpace(name.Text))
        { message.Text = "Nhập mã ca, mã nhân viên và đầy đủ họ tên."; return; }
        busy = true; redeem.Enabled = false;
        try
        {
            Session = await LiveSession.Api.RedeemShiftRecoveryAsync(new(shiftId, employeeId, name.Text, code.Text.Trim()));
            code.Clear();
        }
        catch (CafeApiException ex) { message.Text = ex.Message; }
        finally { busy = false; if (!IsDisposed) redeem.Enabled = true; }
        if (Session is not null) { DialogResult = DialogResult.OK; Close(); }
    }
}
