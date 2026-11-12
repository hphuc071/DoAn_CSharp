using FourChang.Desktop.Shared.Theme;

namespace FourChang.Desktop.Shared.Controls.Admin;

/// <summary>Hộp nhập lý do dùng chung cho nhân viên/ca; không tự gửi lệnh hoặc nhập tiền thay nhân viên.</summary>
internal static class WorkforceDialogs
{
    /// <summary>Trả null khi người dùng hủy, giữ lý do tối đa theo cột SQL đã chốt.</summary>
    internal static string? Reason(Control owner, string title, string description)
    {
        using var form = AdminUi.Dialog(title, 700, 330);
        form.Name = "WorkforceReasonDialog";
        var input = Ui.Input("Nhập lý do thực hiện", "", 580);
        input.Name = "WorkforceReason";
        input.MaxLength = 500;
        var error = Ui.Label("", 9, false, CafeTheme.Danger);
        var confirm = Ui.Button("Xác nhận", () =>
        {
            if (string.IsNullOrWhiteSpace(input.Text)) { error.Text = "Vui lòng nhập lý do."; input.Focus(); return; }
            form.DialogResult = DialogResult.OK;
        }, true);
        confirm.Name = "WorkforceConfirm";
        var help = Ui.Label(description, 9, false, CafeTheme.Muted);
        help.MaximumSize = new Size(580, 0);
        var body = Ui.Panel(title, help, AdminListLayout.Field("Lý do *", input), error);
        body.SizeChanged += (_, _) => help.MaximumSize = new Size(Math.Max(200, body.ClientSize.Width - body.Padding.Horizontal), 0);
        AdminUi.Compose(form, body,
            confirm, Ui.Button("Hủy", () => form.Close()));
        return form.ShowDialog(owner.FindForm()) == DialogResult.OK ? input.Text.Trim() : null;
    }
}
