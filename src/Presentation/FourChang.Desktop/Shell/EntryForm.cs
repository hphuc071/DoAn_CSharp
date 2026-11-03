using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Common;
using FourChang.Presentation.Shared.Contracts.Staff;
namespace FourChang.Desktop.Shell;

public sealed class RoleApplicationContext : ApplicationContext
{
    public RoleApplicationContext(string role) => ShowEntry(role);
    private void ShowEntry(string role)
    {
        var previous = MainForm;
        var entry = new EntryForm(role, name =>
        {
            var old = MainForm; var shell = new MainForm(role, name); MainForm = shell;
            shell.ReauthenticationRequested += (_, _) => ShowEntry(role);
            shell.Show(); old?.Close();
        });
        MainForm = entry; entry.Show(); previous?.Close();
    }
}
// Màn hình vào ứng dụng: Admin đăng nhập; nhân viên nhập mã và họ tên, được kiểm tra với hồ sơ hiện có.
public sealed class EntryForm : Form
{
    public EntryForm(string role, Action<string> onContinue)
    {
        var staff = role == "Staff";
        var windowIcon = BrandMark.CreateWindowIcon(); Icon = windowIcon; Disposed += (_, _) => windowIcon.Dispose();
        Text = $"{CafeTheme.Brand} · {(staff ? "Nhân viên — Mở ca" : "Quản lý — Đăng nhập")}";
        ClientSize = new Size(1100, 800); MinimumSize = new Size(1020, 760); AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen; BackColor = CafeTheme.Cream; Font = CafeTheme.Body();
        var visual = new FourChang.Desktop.Shared.Controls.Entry.EntryBrandPanel { Dock = DockStyle.Left, Width = 346, Padding = new Padding(30, 36, 30, 26) };
        var logo = new BrandMark { Dock = DockStyle.Top, Height = 136, BackColor = CafeTheme.Dark, Name = "EntryBrandMark" };
        var brand = Ui.Label("4CHANG", 24, true, Color.White); brand.Dock = DockStyle.Top; brand.AutoSize = false; brand.Height = 52; brand.TextAlign = ContentAlignment.MiddleCenter;
        var sub = Ui.Label("LINH NGU LAM", 12, true, Color.FromArgb(238, 213, 183)); sub.Dock = DockStyle.Top; sub.AutoSize = false; sub.Height = 44; sub.TextAlign = ContentAlignment.TopCenter;
        var caption = Ui.Label(staff ? "Bắt đầu ca làm việc\ncủa bạn tại đây." : "Quản lý quán\nmỗi ngày.", 14, true, Color.FromArgb(249, 233, 211)); caption.AutoSize = false; caption.Height = 100; caption.Dock = DockStyle.Top; caption.TextAlign = ContentAlignment.MiddleCenter;
        var foot = Ui.Label("Cà phê ngon · Mỗi ngày an nhiên", 9, false, Color.FromArgb(216, 193, 167)); foot.Dock = DockStyle.Bottom; foot.AutoSize = false; foot.Height = 35; foot.TextAlign = ContentAlignment.MiddleCenter; foot.BackColor = Color.Transparent;
        visual.Controls.Add(caption); visual.Controls.Add(sub); visual.Controls.Add(brand); visual.Controls.Add(logo); visual.Controls.Add(foot);
        foreach (var label in new[] { brand, sub, caption }) label.BackColor = Color.Transparent;
        var main = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = CafeTheme.Cream };
        var name = Ui.Input("Tên đăng nhập", "quanly", 410); name.Name = "AdminLoginName";
        var password = Ui.Input("Nhập mật khẩu", "", 410); password.UseSystemPasswordChar = true; password.Name = "AdminLoginPassword";
        var employeeId = Ui.Input("Ví dụ: 1", "", 410); employeeId.Name = "StaffEmployeeId"; employeeId.MaxLength = 19;
        var employeeName = Ui.Input("Nhập đầy đủ họ tên", "", 410); employeeName.Name = "StaffEmployeeName"; employeeName.MaxLength = 120;
        var shift = Ui.Select("Sáng", "Chiều", "Tối"); shift.Name = "StaffShift";
        var opening = new NumericUpDown { Maximum = 1_000_000_000, Minimum = 0, Increment = 10000, ThousandsSeparator = true, Value = 500000, Font = CafeTheme.Body(), Name = "StaffOpeningCash" };
        var feedback = Ui.Label("", 9, false, CafeTheme.Danger); feedback.Name = "EntryFeedback"; feedback.MaximumSize = new Size(442, 0); feedback.Visible = false;
        var verified = Ui.Label("", 9, false, CafeTheme.Muted); verified.Visible = false; verified.Name = "StaffVerifyResult"; verified.MaximumSize = new Size(442, 0);
        var button = Ui.Button(staff ? "Mở ca làm việc" : "Đăng nhập", () => { }, true); button.Name = "EntrySubmit"; button.AutoSize = false; button.Size = new Size(442, 50); button.Font = CafeTheme.Strong(11);
        var fields = new List<Control> { Ui.Label(staff ? "CA LÀM VIỆC" : "TÀI KHOẢN QUẢN LÝ", 9, true, CafeTheme.Coffee), Ui.Label(staff ? "Sẵn sàng cho ca mới" : "Chào mừng trở lại", 23, true), Ui.Label(staff ? "Nhập thông tin của bạn để bắt đầu ca." : "Đăng nhập để quản lý hoạt động của quán.", 10, false, CafeTheme.Muted) };
        Control Field(string label, Control input, int width = 442)
        { var frame = new FourChang.Desktop.Shared.Controls.Entry.EntryInputFrame(input) { Width = width, Margin = Padding.Empty }; return Ui.Field(label, frame); }
        if (staff)
        {
            fields.Add(Field("Mã nhân viên", employeeId)); fields.Add(Field("Họ và tên", employeeName)); fields.Add(verified);
            fields.Add(Ui.Row(Field("Ca làm việc", shift, 207), Field("Tiền đầu ca", opening, 207)));
        }
        else { fields.Add(Field("Tài khoản quản lý", name)); fields.Add(Field("Mật khẩu", password)); }
        fields.Add(feedback); fields.Add(button);
        var card = new SurfacePanel { Width = 506, AutoSize = false, MinimumSize = new Size(506, 0), MaximumSize = new Size(506, 0), Padding = new Padding(28) };
        var layout = Ui.Stack(fields.ToArray()); foreach (Control field in layout.Controls) field.Margin = new Padding(0, 0, 0, 12);
        card.Controls.Add(layout); main.Controls.Add(card); Controls.Add(main); Controls.Add(visual); AcceptButton = button;
        // Tính chiều cao theo nội dung thực, tránh cắt nút mở ca khi thông báo hoặc chữ tiếng Việt xuống dòng.
        bool arranging = false;
        void Position()
        {
            if (arranging || IsDisposed) return; arranging = true;
            try
            {
                layout.PerformLayout(); card.Height = layout.PreferredSize.Height + card.Padding.Vertical;
                main.AutoScrollMinSize = new Size(0, card.Height + 48);
                card.Location = new Point(Math.Max(22, (main.ClientSize.Width - card.Width) / 2), Math.Max(24, (main.ClientSize.Height - card.Height) / 2));
            }
            finally { arranging = false; }
        }
        main.Resize += (_, _) => Position(); layout.SizeChanged += (_, _) => Position(); Shown += (_, _) => Position();
        bool busy = false; var identityRevision = 0; CancellationTokenSource? pendingIdentity = null;
        var debounce = new System.Windows.Forms.Timer { Interval = 500 };
        void Error(string text) { feedback.ForeColor = CafeTheme.Danger; feedback.Text = text; feedback.Visible = true; }
        string Friendly(CafeApiException ex) => staff && ex.StatusCode == System.Net.HttpStatusCode.Unauthorized ? "Máy POS chưa được cấp quyền hoặc cấu hình đã bị thu hồi. Vui lòng nhờ Quản lý kiểm tra thiết bị." : ex.Message;
        void ShowIdentity(Employee employee) { verified.ForeColor = Color.FromArgb(67, 110, 65); verified.Text = $"{employee.HoTen} · Mã {employee.MaNhanVien}"; verified.Visible = true; }
        async Task<Employee> CheckEmployee()
        {
            if (!long.TryParse(employeeId.Text.Trim(), out var id) || id <= 0 || string.IsNullOrWhiteSpace(employeeName.Text)) throw new CafeApiException("Nhập mã nhân viên hợp lệ và đầy đủ họ tên.");
            var employee = await LiveSession.Api.VerifyEmployeeAsync(new(id, employeeName.Text)); if (!IsDisposed) ShowIdentity(employee); return employee;
        }
        void SetBusy(bool value) { busy = value; button.Enabled = !value; employeeId.Enabled = employeeName.Enabled = shift.Enabled = opening.Enabled = !value; name.Enabled = password.Enabled = !value; UseWaitCursor = value; }
        // Chờ người dùng ngừng gõ 500ms. Hủy lần kiểm tra cũ và bỏ phản hồi cũ nếu mã/tên đã đổi.
        foreach (var input in new[] { employeeId, employeeName }) input.TextChanged += (_, _) =>
        {
            identityRevision++; debounce.Stop(); pendingIdentity?.Cancel(); verified.Visible = false; feedback.Visible = false;
            if (staff && !busy && long.TryParse(employeeId.Text.Trim(), out var id) && id > 0 && !string.IsNullOrWhiteSpace(employeeName.Text)) debounce.Start();
        };
        debounce.Tick += async (_, _) =>
        {
            debounce.Stop(); if (busy || IsDisposed) return;
            var revision = identityRevision; var text = employeeName.Text; var id = long.Parse(employeeId.Text.Trim());
            using var cancellation = new CancellationTokenSource(); pendingIdentity = cancellation;
            verified.ForeColor = CafeTheme.Muted; verified.Text = "Đang xác nhận thông tin…"; verified.Visible = true;
            try
            {
                var employee = await LiveSession.Api.VerifyEmployeeAsync(new(id, text), cancellation.Token);
                if (!IsDisposed && revision == identityRevision && !busy) ShowIdentity(employee);
            }
            catch (OperationCanceledException) { }
            catch (CafeApiException ex) { if (!IsDisposed && revision == identityRevision && !busy) { verified.ForeColor = CafeTheme.Danger; verified.Text = Friendly(ex); verified.Visible = true; } }
            finally { if (ReferenceEquals(pendingIdentity, cancellation)) pendingIdentity = null; }
        };
        FormClosed += (_, _) => { debounce.Stop(); debounce.Dispose(); pendingIdentity?.Cancel(); };
        button.Click += async (_, _) =>
        {
            if (busy) return; debounce.Stop(); pendingIdentity?.Cancel(); identityRevision++; SetBusy(true); feedback.Visible = false;
            try
            {
                if (LiveSession.Preview) { onContinue(staff ? "Nhân viên" : "Quản lý"); return; }
                SessionResult session;
                if (staff)
                {
                    // Luôn kiểm tra lại khi bấm mở ca; API cũng kiểm tra cặp mã/tên trong transaction.
                    var employee = await CheckEmployee(); session = await LiveSession.Api.OpenShiftAsync(new(employee.MaNhanVien, shift.Text, Environment.MachineName, opening.Value, employeeName.Text));
                }
                else session = await LiveSession.Api.LoginAdminAsync(new(name.Text, password.Text));
                LiveSession.Current = session; LiveSession.Api.Token = session.Token;
                if (!staff && session.MustChangePassword)
                {
                    var context = await LiveSession.Api.GetAdminPasswordContextAsync(); using var change = new FourChang.Desktop.Admin.Access.Views.AdminPasswordForm(context, password.Text);
                    var changed = change.ShowDialog(this) == DialogResult.OK; if (!changed) { try { await LiveSession.Api.LogoutAsync(); } catch (CafeApiException) { } }
                    LiveSession.Api.Token = null; LiveSession.Current = null; password.Clear(); Error(changed ? "Đã đổi mật khẩu. Đăng nhập lại bằng mật khẩu mới." : "Cần đổi mật khẩu tạm trước khi sử dụng Quản lý."); return;
                }
                password.Clear(); onContinue(session.Name);
            }
            catch (CafeApiException ex) { Error(Friendly(ex)); }
            finally { if (!IsDisposed) SetBusy(false); }
        };
    }
}
