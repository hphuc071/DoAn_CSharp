using FourChang.Desktop.Admin.Employees;
using FourChang.Desktop.Admin.WorkSessions;
using FourChang.Desktop.Shared.Security;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shell;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Staff;

namespace FourChang.Tests.Admin;

/// <summary>WinForms thật cho ADM-11/12 và điểm nối POS; chỉ sử dụng API/SQL fixture, không dùng Reflection bypass sự kiện.</summary>
internal static class WorkforceUiChecks
{
    /// <summary>Thêm/sửa bằng form, kiểm tra địa chỉ, tìm/bỏ lọc và lịch sử quay lại đúng nhân viên.</summary>
    internal static async Task Employees(Form shell, string output)
    {
        var materialName = "Nguyễn Anh Tuấn UI " + Guid.NewGuid().ToString("N")[..6];
        using var editor = new EmployeeEditorForm(null) { StartPosition = FormStartPosition.Manual, Location = new Point(-12000, -12000) };
        editor.Show();
        var name = Find<TextBox>(editor, "EmployeeName");
        var phone = Find<TextBox>(editor, "EmployeePhone");
        var address = Find<TextBox>(editor, "EmployeeAddress");
        var save = Find<Button>(editor, "EmployeeSave");
        name.Text = materialName; phone.Text = "abc"; address.Text = "25 Đường Hoa Mai, Thành phố Hồ Chí Minh";
        save.PerformClick();
        Assert.Contains("chữ số", Find<Label>(editor, "EmployeeFeedback").Text);
        phone.Text = "0912345678";
        Assert.DoesNotContain(Descendants(editor).OfType<TextBox>(), x => x.Name.Contains("Password") || x.Name.Contains("Username"));
        Capture(editor, Path.Combine(output, "UC-ADM-11-editor.png"));
        editor.Size = editor.MinimumSize; await Task.Delay(100);
        AssertFooter(editor, save);
        Capture(editor, Path.Combine(output, "UC-ADM-11-editor-compact.png"));
        save.PerformClick(); await WaitUntil(() => !editor.Visible);
        Assert.Equal(DialogResult.OK, editor.DialogResult);
        Assert.NotNull(editor.SavedEmployee);
        var saved = await LiveSession.Api.GetEmployeeAsync(editor.SavedEmployee!.MaNhanVien);
        Assert.Equal("25 Đường Hoa Mai, Thành phố Hồ Chí Minh", saved.DiaChiLienHe);
        using var edit = new EmployeeEditorForm(saved) { StartPosition = FormStartPosition.Manual, Location = new Point(-12000, -12000) };
        edit.Show();
        Find<TextBox>(edit, "EmployeeAddress").Text = "36 Đường Hoa Sữa, Hà Nội";
        Find<Button>(edit, "EmployeeSave").PerformClick(); await WaitUntil(() => !edit.Visible);
        Assert.Equal("36 Đường Hoa Sữa, Hà Nội", (await LiveSession.Api.GetEmployeeAsync(saved.MaNhanVien)).DiaChiLienHe);

        var search = Find<TextBox>(shell, "EmployeeSearch");
        var grid = Find<DataGridView>(shell, "EmployeesGrid");
        search.Text = materialName;
        Descendants(shell).OfType<Button>().Single(x => x.Text == "Tìm kiếm").PerformClick();
        await WaitUntil(() => grid.Rows.Count == 1 && grid.Rows[0].DataBoundItem is Employee e && e.MaNhanVien == saved.MaNhanVien);
        Capture(shell, Path.Combine(output, "UC-ADM-11-search.png"));
        var completed = false;
        Exception? timerError = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 70 };
        timer.Tick += (_, _) =>
        {
            var form = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.Name == "EmployeeShiftHistory");
            if (form is null) return;
            try
            {
                var filter = Find<TextBox>(form, "WorkSessionEmployeeFilter");
                if (!Descendants(form).OfType<Label>().Any(x => x.Text.StartsWith("Trang "))) return;
                Assert.Equal(saved.MaNhanVien.ToString(), filter.Text);
                Capture(form, Path.Combine(output, "UC-ADM-11-history.png"));
                timer.Stop(); completed = true; form.Close();
            }
            catch (Exception ex) { timerError = ex; timer.Stop(); completed = true; form.Close(); }
        };
        timer.Start();
        Descendants(shell).OfType<Button>().Single(x => x.Text == "Lịch sử ca").PerformClick();
        await WaitUntil(() => completed);
        Assert.Null(timerError);
        Assert.Equal(materialName, search.Text);
        Descendants(shell).OfType<Button>().Single(x => x.Text == "Tải lại").PerformClick();
        await WaitUntil(() => search.Text.Length == 0 && grid.Rows.Count > 1);
    }

    /// <summary>Đi qua UI cấp mã, đổi mã, gửi thực đếm; xác minh phiên phục hồi không mở màn hình bán.</summary>
    internal static async Task WorkSessions(Form shell, string output)
    {
        var original = LiveSession.Current;
        var token = LiveSession.Api.Token;
        var locked = LiveSession.ShiftLocked;
        var employee = await LiveSession.Api.SaveEmployeeAsync(new("Trần Hoàng Phúc UI", "0912345678", DateTime.Today, "ACTIVE", MaThaoTac: Guid.NewGuid(), DiaChiLienHe: "18 Đường Nguyễn Văn Cừ"));
        var session = await LiveSession.Api.OpenShiftAsync(new(employee.MaNhanVien, "Chiều", "UI-" + Guid.NewGuid().ToString("N")[..10], 500000, employee.HoTen));
        var id = session.ShiftId!.Value;
        try
        {
            using var detail = new WorkSessionDetailsForm(await LiveSession.Api.GetWorkSessionAsync(id)) { StartPosition = FormStartPosition.Manual, Location = new Point(-12000, -12000) };
            detail.Show(); await Task.Delay(150);
            var countedLabel = Descendants(detail).OfType<Label>().Single(x => x.Text == "Chưa gửi");
            var countedBounds = detail.RectangleToClient(countedLabel.RectangleToScreen(countedLabel.ClientRectangle));
            Assert.True(countedBounds.Top >= 0 && countedBounds.Bottom <= detail.ClientSize.Height - 60, $"Phần thực đếm bị che khi mở đối chiếu: {countedBounds}");
            // Chặn tái xuất hiện lỗi AutoSize làm thẻ tiền giãn thành khoảng trống quá lớn.
            var cashSurface = countedLabel.Parent;
            while (cashSurface is not null && cashSurface.GetType().Name != "SurfacePanel") cashSurface = cashSurface.Parent;
            Assert.NotNull(cashSurface);
            Assert.InRange(cashSurface!.Height, 100, 300);
            Assert.Empty(Descendants(detail).OfType<NumericUpDown>());
            Assert.False(Find<Button>(detail, "AdminConfirmShift").Enabled);
            File.WriteAllLines(Path.Combine(output, "UC-ADM-12-detail-layout.txt"), Descendants(detail).Select(c => $"{c.GetType().Name} {c.Name} bounds={c.Bounds} parent={c.Parent?.GetType().Name}"));
            Capture(detail, Path.Combine(output, "UC-ADM-12-detail.png"));
            detail.Size = detail.MinimumSize; await Task.Delay(100);
            AssertFooter(detail, Find<Button>(detail, "AdminIssueShiftRecovery"));
            Capture(detail, Path.Combine(output, "UC-ADM-12-detail-compact.png"));
            detail.ClientSize = new Size(1220, 850);
            string? code = null;
            Exception? dialogError = null;
            using var timer = new System.Windows.Forms.Timer { Interval = 60 };
            timer.Tick += (_, _) =>
            {
                try
                {
                    var reasonDialog = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.Name == "WorkforceReasonDialog");
                    if (reasonDialog is not null)
                    {
                        Find<TextBox>(reasonDialog, "WorkforceReason").Text = "Mất phiên khi máy khởi động lại";
                        Find<Button>(reasonDialog, "WorkforceConfirm").PerformClick();
                        return;
                    }
                    var codeDialog = Application.OpenForms.Cast<Form>().FirstOrDefault(x => Descendants(x).OfType<TextBox>().Any(t => t.Name == "ShiftRecoveryCode"));
                    if (codeDialog is null) return;
                    code = Find<TextBox>(codeDialog, "ShiftRecoveryCode").Text;
                    Assert.StartsWith("PH-", code);
                    timer.Stop(); codeDialog.Close();
                }
                catch (Exception ex) { dialogError = ex; timer.Stop(); }
            };
            timer.Start();
            Find<Button>(detail, "AdminIssueShiftRecovery").PerformClick();
            await WaitUntil(() => code is not null || dialogError is not null);
            Assert.Null(dialogError);
            await WaitUntil(() => Descendants(detail).OfType<Button>().Single(x => x.Text == "Đóng").Enabled);
            detail.Close();

            using var recovery = new FourChang.Desktop.Staff.WorkSessions.ShiftRecoveryForm { StartPosition = FormStartPosition.Manual, Location = new Point(-12000, -12000) };
            recovery.Show();
            Find<TextBox>(recovery, "RecoveryShiftId").Text = id.ToString();
            Find<TextBox>(recovery, "RecoveryEmployeeId").Text = employee.MaNhanVien.ToString();
            Find<TextBox>(recovery, "RecoveryEmployeeName").Text = employee.HoTen;
            Find<TextBox>(recovery, "RecoveryOneTimeCode").Text = code;
            Find<Button>(recovery, "RedeemShiftRecovery").PerformClick();
            await WaitUntil(() => !recovery.Visible);
            Assert.Equal("STAFF_RECONCILIATION", recovery.Session?.Role);
            LiveSession.Current = recovery.Session; LiveSession.Api.Token = recovery.Session!.Token; LiveSession.Api.PosKey = null; LiveSession.ShiftLocked = true;
            Assert.True(ShiftSessionFile.Save(recovery.Session));
            Assert.True(ShiftSessionFile.Load()?.Token == recovery.Session.Token, "DPAPI đọc lại đúng phiên, không in token ra báo lỗi.");
            var cache = Environment.GetEnvironmentVariable("FOURCHANG_SHIFT_SESSION_PATH")!;
            Assert.False(System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(cache)).Contains(recovery.Session.Token, StringComparison.Ordinal));
            using var staff = new MainForm("Staff", employee.HoTen) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-14000, -14000) };
            var returnedToEntry = false;
            staff.ReauthenticationRequested += (_, _) => returnedToEntry = true;
            staff.Show();
            await WaitUntil(() => Find<Button>(staff, "SubmitShiftReconciliation").Enabled);
            Assert.False(Find<Button>(staff, "CloseStaffShift").Enabled);
            staff.Navigate("sale");
            await WaitUntil(() => Find<Button>(staff, "SubmitShiftReconciliation").Enabled);
            Assert.Contains(Descendants(staff), x => x.Name == "StaffShiftPage");
            Find<NumericUpDown>(staff, "ShiftCountedCash").Value = 500000;
            Capture(staff, Path.Combine(output, "UC-ADM-12-pos-reconciliation.png"));
            Find<Button>(staff, "SubmitShiftReconciliation").PerformClick();
            await WaitUntil(() => returnedToEntry);
            Assert.Null(LiveSession.Current);
            Assert.Null(LiveSession.Api.Token);
            staff.Close();
            LiveSession.Current = original; LiveSession.Api.Token = token; LiveSession.ShiftLocked = locked;
            var sent = await LiveSession.Api.GetWorkSessionAsync(id);
            Assert.True(sent.DangChoDoiChieu);
            using var confirm = new WorkSessionDetailsForm(sent) { StartPosition = FormStartPosition.Manual, Location = new Point(-12000, -12000) };
            confirm.Show();
            Assert.True(Find<Button>(confirm, "AdminConfirmShift").Enabled);
            await LiveSession.Api.ConfirmWorkSessionAsync(id, new(sent.Ca.PhienBanDuLieu, Guid.NewGuid(), sent.MaDoiChieu));
            Descendants(confirm).OfType<Button>().Single(x => x.Text == "Tải lại đối chiếu").PerformClick();
            await WaitUntil(() => !Find<Button>(confirm, "AdminConfirmShift").Enabled && Descendants(confirm).OfType<Label>().Any(x => x.Text.Contains("Đã chốt")));
            Capture(confirm, Path.Combine(output, "UC-ADM-12-closed.png"));
            confirm.Close();
        }
        finally { LiveSession.Current = original; LiveSession.Api.Token = token; LiveSession.ShiftLocked = locked; ShiftSessionFile.Clear(); }
    }

    /// <summary>Lấy control theo Name ổn định, không dựa vị trí tọa độ hoặc tên người trùng nhau.</summary>
    private static T Find<T>(Control root, string name) where T : Control => Descendants(root).OfType<T>().Single(x => x.Name == name);
    /// <summary>Duyệt cây control đã render.</summary>
    private static IEnumerable<Control> Descendants(Control root) { foreach (Control c in root.Controls) { yield return c; foreach (var child in Descendants(c)) yield return child; } }
    /// <summary>Chờ event/UI/API trong deadline, không bỏ qua trạng thái chưa thành công.</summary>
    private static async Task WaitUntil(Func<bool> condition) { var until = DateTime.UtcNow.AddSeconds(20); while (!condition() && DateTime.UtcNow < until) await Task.Delay(70); Assert.True(condition(), "Giao diện không đạt trạng thái mong đợi trong deadline."); }
    /// <summary>Footer luôn nằm trong vùng nhìn thấy ở cửa sổ nhỏ.</summary>
    private static void AssertFooter(Form form, Button button) { var rect = form.RectangleToClient(button.RectangleToScreen(button.ClientRectangle)); Assert.True(rect.Top >= 0 && rect.Bottom <= form.ClientSize.Height); }
    /// <summary>Chụp ảnh thực tế; không chụp hộp hiển thị mã phục hồi hoặc file chứa token.</summary>
    private static void Capture(Form form, string path) { using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(path); }
}
