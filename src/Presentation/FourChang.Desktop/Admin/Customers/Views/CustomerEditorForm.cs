using System.Net;
using FourChang.Desktop.Shared.Controls.Admin;
using System.Security.Cryptography;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;
namespace FourChang.Desktop.Admin.Customers.Views;

// Một biểu mẫu dùng các chế độ của UC-ADM-01; không chứa thao tác nhân viên hoặc nghiệp vụ tiền.
public sealed class CustomerEditorForm : Form
{
    private CustomerAccount? account;
    private readonly string action;
    private readonly TextBox username;
    private readonly TextBox name;
    private readonly TextBox email;
    private readonly TextBox phone;
    private readonly TextBox password;
    private readonly TextBox confirmation;
    private readonly TextBox reason;
    private readonly ComboBox status = Ui.Select("Hoạt động", "Đã khóa", "Đã ngừng");
    private readonly Label feedback = Ui.Label("", 9, false, CafeTheme.Danger);
    private readonly Button save;
    private readonly Button reload;
    private readonly Button cancel;
    private readonly List<Control> inputs = [];
    private Guid operation;
    private Func<Task<CustomerMutation>>? pending;
    private bool busy;
    private bool succeeded;
    private bool uncertain;
    private readonly CustomerAddressBookPanel? addresses;

    /// <summary>Tạo biểu mẫu theo thao tác Admin, giữ các handler bảo vệ phiên bản và nháp khi lỗi mạng.</summary>
    public CustomerEditorForm(CustomerDetails? detail, string action)
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        account = detail?.Account; this.action = action;
        Text = action switch { "create" => "Thêm khách hàng", "update" => "Sửa hồ sơ khách hàng", "status" => "Khóa / mở / ngừng tài khoản", "reset-password" => "Đặt lại mật khẩu khách hàng", _ => "Xóa khách hàng có điều kiện" };
        Name = "CustomerEditorForm"; ClientSize = new Size(940, 840); MinimumSize = new Size(840, 700);
        BackColor = CafeTheme.Cream; Font = CafeTheme.Body(); StartPosition = FormStartPosition.CenterParent; AutoScroll = true;
        username = Input("CustomerUsername", "Tên đăng nhập", account?.TenDangNhap ?? "");
        name = Input("CustomerName", "Họ tên", account?.HoTen ?? "");
        email = Input("CustomerEmail", "Email", account?.Email ?? "");
        phone = Input("CustomerPhone", "Số điện thoại (10 chữ số)", account?.SoDienThoai ?? "");
        phone.MaxLength = 10;
        phone.KeyPress += (_, e) => e.Handled = !char.IsControl(e.KeyChar) && (e.KeyChar < '0' || e.KeyChar > '9');
        password = Input("CustomerTemporaryPassword", "Mật khẩu tạm", action == "create" ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(18)) : "");
        password.UseSystemPasswordChar = true; password.MaxLength = 128;
        confirmation = Input("CustomerPasswordConfirmation", "Nhập lại mật khẩu mới", "");
        confirmation.UseSystemPasswordChar = true; confirmation.MaxLength = 128;
        if (action == "reset-password")
        {
            Text = "Cấp lại mật khẩu theo yêu cầu khách hàng";
            ClientSize = new Size(760, 760); MinimumSize = new Size(720, 680);
        }
        reason = Input("CustomerReason", "Lý do", ""); reason.MaxLength = 500; reason.Multiline = true; reason.Height = 70;
        username.MaxLength = 100; name.MaxLength = 120; email.MaxLength = 254; phone.MaxLength = 10;
        status.SelectedIndex = account?.TrangThai switch { "LOCKED" => 1, "INACTIVE" => 2, _ => 0 };
        status.Name = "CustomerStatus";
        feedback.Name = "CustomerFeedback"; feedback.MaximumSize = new Size(560, 0); feedback.Visible = false;
        save = Ui.Button("Xác nhận lưu", () => { }, true); save.Name = "CustomerSave";
        if (action == "reset-password") save.Text = "Cấp lại mật khẩu";
        reload = Ui.Button("Đối chiếu hồ sơ mới", () => { }); reload.Visible = account is not null;
        cancel = Ui.Button("Hủy", () => Close());
        var content = new List<Control>();
        if (action is "create" or "update")
        {
            content.Add(AdminProfileLayout.Card("Thông tin tài khoản",
                AdminListLayout.Fields(AdminListLayout.Field("Tên đăng nhập", username), AdminListLayout.Field("Họ tên", name)),
                AdminListLayout.Fields(AdminListLayout.Field("Email", email), AdminListLayout.Field("Điện thoại · bắt buộc 10 số", phone))));
            inputs.AddRange([name, email, phone]); username.ReadOnly = account is not null; if (account is null) inputs.Add(username);
        }
        else content.Add(Ui.Label($"{account?.HoTen} · {account?.TenDangNhap}", 11, true));
        if (action == "status") { content.Add(Ui.Field("Trạng thái", status)); inputs.Add(status); }
        if (action is "create" or "reset-password")
        {
            var show = new CheckBox { Text = "Hiện mật khẩu tạm để bàn giao cho khách", AutoSize = true, Font = CafeTheme.Body(9) };
            show.CheckedChanged += (_, _) => { password.UseSystemPasswordChar = !show.Checked; confirmation.UseSystemPasswordChar = !show.Checked; };
            content.Add(Ui.Field(action == "reset-password" ? "Mật khẩu mới cấp cho khách · 5–128 ký tự" : "Mật khẩu tạm (khách phải đổi khi đăng nhập)", password));
            if (action == "reset-password")
            {
                content.Add(Ui.Field("Nhập lại mật khẩu mới", confirmation)); inputs.Add(confirmation);
                content.Add(Ui.Label("Nhập mật khẩu mới theo yêu cầu của khách.\nKhách phải đổi mật khẩu tạm khi đăng nhập Website.", 9, false, CafeTheme.Muted));
            }
            content.Add(show); inputs.Add(password);
        }
        if (action is "status" or "reset-password" or "delete")
        {
            var reasonFrame = new SurfacePanel { Width = 490, Height = 94, Padding = new Padding(12), BackColor = CafeTheme.Surface };
            reason.Dock = DockStyle.Fill; reason.BorderStyle = BorderStyle.None; reason.BackColor = CafeTheme.Surface;
            reasonFrame.Controls.Add(reason);
            content.Add(Ui.Field("Lý do bắt buộc", reasonFrame)); inputs.Add(reason);
        }
        if (action == "delete") content.Add(Ui.Label(detail?.CoTheXoa == true
            ? "Chỉ xóa khi chưa có tham chiếu. Hệ thống kiểm tra lại lúc xác nhận."
            : "Khách đã có tham chiếu. Không xóa cứng; hãy dùng chức năng khóa/ngừng.", 9));
        if (action == "update" && account is not null)
        {
            addresses = new CustomerAddressBookPanel(account, true); inputs.Add(addresses);
            addresses.Changed += updated =>
            {
                // Chỉ nhận version tự tăng do sửa địa chỉ khi các trường hồ sơ chưa bị người khác đổi.
                if (account is not null && account.HoTen == updated.HoTen && account.Email == updated.Email && account.SoDienThoai == updated.SoDienThoai && account.TrangThai == updated.TrangThai)
                    account = updated;
                else ShowFeedback("Hồ sơ đã thay đổi ở nơi khác. Chọn Đối chiếu hồ sơ mới trước khi lưu thông tin cá nhân.");
            };
            content.Add(AdminProfileLayout.Card("Địa chỉ giao hàng", addresses));
        }
        foreach (var field in inputs.OfType<TextBox>())
        {
            // Khi người dùng sửa lại đầu vào thì bỏ thông báo validation cũ; lỗi chưa rõ kết quả vẫn giữ.
            field.TextChanged += (_, _) => { if (!busy && !uncertain && !succeeded) feedback.Visible = false; };
        }
        content.Add(feedback);
        AdminProfileLayout.Mount(this, AdminProfileLayout.Header(account?.HoTen ?? "Khách hàng mới", Text, account is null ? "Tạo mới" : CustomerDetailsForm.Status(account.TrangThai)),
            AdminListLayout.Column(content.ToArray()), cancel, reload, save);
        save.Click += async (_, _) => await Submit(); reload.Click += async (_, _) => await RefreshVersion();
        FormClosing += (_, e) =>
        {
            if (succeeded) DialogResult = DialogResult.OK;
            else if (busy || uncertain || addresses?.HasUnresolvedMutation == true) { e.Cancel = true; ShowFeedback("Kết quả lưu chưa rõ. Chọn Kiểm tra / thử lại trước khi đóng để không gửi trùng yêu cầu."); }
        };
    }

    /// <summary>Tạo trường có Name ổn định cho bàn phím và kiểm thử giao diện.</summary>
    private static TextBox Input(string key, string hint, string value)
    { var input = Ui.Input(hint, value, 490); input.Name = key; return input; }

    /// <summary>Chụp yêu cầu bất biến, lưu hoặc tra receipt sau timeout; không phát lại bằng mã mới.</summary>
    private async Task Submit()
    {
        if (busy || succeeded || addresses?.HasUnresolvedMutation == true) return;
        if (pending is null)
        {
            // Kiểm tra ngay tại form để người dùng sửa trường lỗi, API/SQL vẫn xác thực lại độc lập.
            if (action is "create" or "update" && (phone.Text.Length != 10 || phone.Text.Any(c => c < '0' || c > '9')))
            { ShowFeedback("Số điện thoại bắt buộc gồm đúng 10 chữ số."); phone.Focus(); return; }
            if (action == "reset-password")
            {
                if (password.Text.Length is < 5 or > 128) { ShowFeedback("Nhập mật khẩu mới dài 5–128 ký tự."); password.Focus(); return; }
                if (password.Text != confirmation.Text) { ShowFeedback("Xác nhận mật khẩu chưa khớp. Vui lòng nhập lại."); confirmation.Focus(); return; }
                if (string.IsNullOrWhiteSpace(reason.Text)) { ShowFeedback("Nhập lý do cấp lại, ví dụ khách yêu cầu đổi mật khẩu."); reason.Focus(); return; }
            }
            operation = Guid.NewGuid();
            // Chụp bất biến dữ liệu và rowversion. Thử lại không đọc các ô có thể đã đổi trên form.
            if (action is "create" or "update")
            {
                var id = account?.MaKhachHang;
                var input = new CustomerSaveInput(operation, username.Text, name.Text, email.Text, phone.Text,
                    action == "create" ? password.Text : null, account?.PhienBanDuLieu);
                pending = () => LiveSession.Api.SaveCustomerAsync(id, input);
            }
            else
            {
                var id = account!.MaKhachHang;
                var input = new CustomerActionInput(operation, account.PhienBanDuLieu, reason.Text,
                    action == "status" ? new[] { "ACTIVE", "LOCKED", "INACTIVE" }[status.SelectedIndex] : null,
                    action == "reset-password" ? password.Text : null);
                pending = () => LiveSession.Api.ActOnCustomerAsync(id, action, input);
            }
        }
        busy = true; LockInputs(true); save.Enabled = false; reload.Enabled = false; cancel.Enabled = false;
        try
        {
            // Nếu lần trước mất phản hồi, tra audit trước. Chưa thấy mới gửi lại cùng mã và cùng nội dung.
            var result = uncertain ? await LiveSession.Api.GetCustomerOperationAsync(operation) : null;
            result ??= await pending();
            account = result.Account; succeeded = true; uncertain = false; pending = null;
            ShowFeedback(action is "create" or "reset-password"
                ? "Đã lưu. Bàn giao mật khẩu tạm cho khách; khách phải đổi trước khi sử dụng tài khoản."
                : result.DaXoa ? "Đã xóa khách hàng chưa có tham chiếu." : "Đã lưu thay đổi.", true);
            save.Visible = false; reload.Visible = false; cancel.Text = "Đóng";
            if (action is "create" or "reset-password") { password.Enabled = true; password.ReadOnly = true; }
            // Giữ mật khẩu tạm trong form hiện tại để bàn giao; không ghi ra file hoặc lưu vào hồ sơ trả về.
            if (action is not ("create" or "reset-password")) { DialogResult = DialogResult.OK; Close(); }
        }
        catch (CafeApiException ex)
        {
            uncertain = ex.StatusCode is null || (int)ex.StatusCode >= 500;
            ShowFeedback(ex.Message + (uncertain ? " Chưa xác nhận kết quả; giữ nguyên dữ liệu và chọn Kiểm tra / thử lại." : " Dữ liệu nhập vẫn được giữ để đối chiếu hoặc sửa."));
            if (!uncertain) { pending = null; LockInputs(false); }
            save.Text = uncertain ? "Kiểm tra / thử lại" : "Xác nhận lưu";
        }
        finally
        {
            busy = false; save.Enabled = !succeeded; reload.Enabled = !uncertain && !succeeded; cancel.Enabled = !uncertain;
        }
    }

    /// <summary>Đối chiếu dữ liệu hiện hành và chỉ dùng version mới sau khi người dùng đồng ý giữ nháp.</summary>
    private async Task RefreshVersion()
    {
        if (account is null || uncertain || busy) return;
        busy = true; reload.Enabled = false;
        try
        {
            var latest = await LiveSession.Api.GetCustomerAsync(account.MaKhachHang);
            var current = latest.Account;
            var message = $"Hồ sơ hiện tại: {current.HoTen}\nEmail: {current.Email}\nĐiện thoại: {current.SoDienThoai}\nTrạng thái: {current.TrangThai}\n\nGiữ dữ liệu bạn đang nhập và dùng phiên bản mới để xác nhận lại?";
            if (MessageBox.Show(this, message, "Đối chiếu thay đổi", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            { account = current; pending = null; ShowFeedback("Đã lấy phiên bản mới. Kiểm tra dữ liệu đang nhập trước khi xác nhận."); }
        }
        catch (CafeApiException ex) { ShowFeedback(ex.Message); }
        finally { busy = false; reload.Enabled = true; }
    }

    /// <summary>Khóa trường nhập trong lúc yêu cầu đang chạy để nội dung không thay đổi giữa các lần thử.</summary>
    private void LockInputs(bool locked) { foreach (var input in inputs) input.Enabled = !locked; }
    /// <summary>Hiển thị kết quả hoặc lỗi trong form, bảo toàn dữ liệu đang nhập.</summary>
    private void ShowFeedback(string message, bool success = false)
    { feedback.Text = message; feedback.ForeColor = success ? Color.DarkGreen : CafeTheme.Danger; feedback.Visible = true; }
}
