using System.Net;
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

    public CustomerEditorForm(CustomerDetails? detail, string action)
    {
        account = detail?.Account; this.action = action;
        Text = action switch { "create" => "Thêm khách hàng", "update" => "Sửa hồ sơ khách hàng", "status" => "Khóa / mở / ngừng tài khoản", "reset-password" => "Đặt lại mật khẩu khách hàng", _ => "Xóa khách hàng có điều kiện" };
        Name = "CustomerEditorForm"; ClientSize = new Size(580, 760); MinimumSize = new Size(580, 680);
        BackColor = CafeTheme.Cream; Font = CafeTheme.Body(); StartPosition = FormStartPosition.CenterParent; AutoScroll = true;
        username = Input("CustomerUsername", "Tên đăng nhập", account?.TenDangNhap ?? "");
        name = Input("CustomerName", "Họ tên", account?.HoTen ?? "");
        email = Input("CustomerEmail", "Email", account?.Email ?? "");
        phone = Input("CustomerPhone", "Số điện thoại", account?.SoDienThoai ?? "");
        password = Input("CustomerTemporaryPassword", "Mật khẩu tạm", action is "create" or "reset-password" ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(18)) : "");
        password.UseSystemPasswordChar = true; password.MaxLength = 128;
        reason = Input("CustomerReason", "Lý do", ""); reason.MaxLength = 500; reason.Multiline = true; reason.Height = 70;
        username.MaxLength = 100; name.MaxLength = 120; email.MaxLength = 254; phone.MaxLength = 20;
        status.SelectedIndex = account?.TrangThai switch { "LOCKED" => 1, "INACTIVE" => 2, _ => 0 };
        status.Name = "CustomerStatus";
        feedback.MaximumSize = new Size(490, 0); feedback.Visible = false;
        save = Ui.Button("Xác nhận lưu", () => { }, true); save.Name = "CustomerSave";
        reload = Ui.Button("Đối chiếu hồ sơ mới", () => { }); reload.Visible = account is not null;
        cancel = Ui.Button("Hủy", () => Close());
        var content = new List<Control> { Ui.Label(Text, 15, true) };
        if (action is "create" or "update")
        {
            content.AddRange([Ui.Field("Tên đăng nhập", username), Ui.Field("Họ tên", name), Ui.Field("Email", email), Ui.Field("Điện thoại", phone)]);
            inputs.AddRange([name, email, phone]); username.ReadOnly = account is not null; if (account is null) inputs.Add(username);
        }
        else content.Add(Ui.Label($"{account?.HoTen} · {account?.TenDangNhap}", 11, true));
        if (action == "status") { content.Add(Ui.Field("Trạng thái", status)); inputs.Add(status); }
        if (action is "create" or "reset-password")
        {
            var show = new CheckBox { Text = "Hiện mật khẩu tạm để bàn giao cho khách", AutoSize = true, Font = CafeTheme.Body(9) };
            show.CheckedChanged += (_, _) => password.UseSystemPasswordChar = !show.Checked;
            content.Add(Ui.Field("Mật khẩu tạm (khách phải đổi khi đăng nhập)", password)); content.Add(show); inputs.Add(password);
        }
        if (action is "status" or "reset-password" or "delete") { content.Add(Ui.Field("Lý do bắt buộc", reason)); inputs.Add(reason); }
        if (action == "delete") content.Add(Ui.Label(detail?.CoTheXoa == true
            ? "Chỉ xóa khi chưa có tham chiếu. Hệ thống kiểm tra lại lúc xác nhận."
            : "Khách đã có tham chiếu. Không xóa cứng; hãy dùng chức năng khóa/ngừng.", 9));
        content.Add(feedback); content.Add(Ui.Row(save, reload, cancel));
        var stack = Ui.Stack(content.ToArray()); stack.Padding = new Padding(24); Controls.Add(stack);
        save.Click += async (_, _) => await Submit(); reload.Click += async (_, _) => await RefreshVersion();
        FormClosing += (_, e) =>
        {
            if (succeeded) DialogResult = DialogResult.OK;
            else if (busy || uncertain) { e.Cancel = true; ShowFeedback("Kết quả lưu chưa rõ. Chọn Kiểm tra / thử lại trước khi đóng để không gửi trùng yêu cầu."); }
        };
    }

    private static TextBox Input(string key, string hint, string value)
    { var input = Ui.Input(hint, value, 490); input.Name = key; return input; }

    private async Task Submit()
    {
        if (busy || succeeded) return;
        if (pending is null)
        {
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

    private void LockInputs(bool locked) { foreach (var input in inputs) input.Enabled = !locked; }
    private void ShowFeedback(string message, bool success = false)
    { feedback.Text = message; feedback.ForeColor = success ? Color.DarkGreen : CafeTheme.Danger; feedback.Visible = true; }
}
