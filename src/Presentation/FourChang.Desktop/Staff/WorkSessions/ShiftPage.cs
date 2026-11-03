using System.Data;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;

namespace FourChang.Desktop.Staff.WorkSessions;
public static class ShiftPage
{
    public static Control Create()
    {
        var session = LiveSession.Current;
        var amount = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 1_000_000_000,
            ThousandsSeparator = true,
            Width = 300,
            Font = CafeTheme.Body()
        };
        var reason = Ui.Input("Lý do nếu chênh lệch", "", 400);
        var error = Ui.Label("", 9, false, CafeTheme.Danger);
        error.Visible = false;
        var close = Ui.Button("Kết thúc ca", () =>
        {
        }, true);
        var panel = Ui.Panel("Ca làm việc", Ui.Label($"Nhân viên: {session?.Name}"), Ui.Label($"Mã ca: {session?.ShiftId}"), Ui.Field("Tiền mặt thực đếm", amount), Ui.Field("Lý do chênh lệch", reason), error, close);
        close.Click += async (_, _) =>
        {
            close.Enabled = false;
            try
            {
                await LiveSession.Api.CloseShiftAsync(new(amount.Value, reason.Text));
                LiveSession.Api.Token = null;
                LiveSession.Current = null;
                panel.FindForm()?.Close();
            }
            catch (CafeApiException ex)
            {
                error.Text = ex.Message;
                error.Visible = true;
                close.Enabled = true;
            }
        };
        return panel;
    }
}
