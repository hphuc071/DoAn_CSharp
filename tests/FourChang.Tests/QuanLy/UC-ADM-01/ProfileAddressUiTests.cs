using System.Diagnostics;
using FourChang.Desktop.Admin.Customers.Views;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Tests.Shared;

namespace FourChang.Tests.Admin;

/// <summary>Kiểm thử giao diện hồ sơ và lưu địa chỉ thật bằng worker WinForms, không gọi tắt thao tác lưu của form.</summary>
public sealed class ProfileAddressUiTests
{
    /// <summary>Chuẩn bị customer/employee riêng, thao tác địa chỉ Admin và lưu bằng chứng của các cửa sổ mới.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-01"), Trait("UseCase", "UC-ADM-12"), Trait("Layer", "WinForms")]
    public async Task HoSoMoi_LuuDiaChiVaHienThiDung()
    {
        await using var api = await TestApi.Start("ProfileAddress-WinForms");
        var admin = await api.AdminLogin(); var customer = await api.NewCustomer(); var employee = await api.Employee(admin, "Nguyễn Hoàng Minh");
        await DesktopWorker.Run(api, "ProfileAddresses", new()
        {
            ["FOURCHANG_API_URL"] = api.Url,
            ["FOURCHANG_TEST_ADMIN_PASSWORD"] = api.AdminPassword,
            ["FOURCHANG_TEST_CUSTOMER_ID"] = customer.Id.ToString(),
            ["FOURCHANG_TEST_EMPLOYEE_ID"] = employee.Number("maNhanVien").ToString(),
            ["FOURCHANG_POS_CREDENTIALS_PATH"] = Path.Combine(api.Artifacts, "device.dpapi")
        });
        Assert.Equal("1", await api.Sql($"SELECT COUNT(*) FROM dbo.DiaChiKhachHang WHERE MaKhachHang={customer.Id} AND DiaChiChiTiet=N'123 Đường Hoa, Phường An Bình, TP Hồ Chí Minh' AND LaDiaChiMacDinh=1"));
    }

    /// <summary>Worker có message loop riêng để tự thao tác các modal mà không chặn testhost.</summary>
    internal static int RunHarness(string output)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Application.SetDefaultFont(CafeTheme.Body()); Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        var code = 1; using var host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
        host.Shown += async (_, _) =>
        {
            try
            {
                var session = await LiveSession.Api.LoginAdminAsync(new("quanly", Environment.GetEnvironmentVariable("FOURCHANG_TEST_ADMIN_PASSWORD")!));
                LiveSession.Api.Token = session.Token; LiveSession.Current = session; LiveSession.Preview = false;
                var id = long.Parse(Environment.GetEnvironmentVariable("FOURCHANG_TEST_CUSTOMER_ID")!);
                var detail = await LiveSession.Api.GetCustomerAsync(id);
                using var editor = new CustomerEditorForm(detail, "update"); Show(editor);
                await Until(() => All(editor).OfType<Label>().Any(x => x.Name == "AddressBookCount" && x.Text == "0 địa chỉ đang sử dụng"));
                All(editor).OfType<Button>().Single(x => x.Name == "CustomerAddressAdd").PerformClick();
                await Until(() => Application.OpenForms.Cast<Form>().Any(x => x.Text == "Thêm địa chỉ giao hàng"));
                var address = Application.OpenForms.Cast<Form>().Single(x => x.Text == "Thêm địa chỉ giao hàng");
                All(address).OfType<TextBox>().Single(x => x.Name == "AddressRecipient").Text = "Nguyễn Minh Anh";
                All(address).OfType<TextBox>().Single(x => x.Name == "AddressPhone").Text = "0912345678";
                All(address).OfType<TextBox>().Single(x => x.Name == "AddressText").Text = "123 Đường Hoa, Phường An Bình, TP Hồ Chí Minh";
                All(address).OfType<CheckBox>().Single(x => x.Name == "AddressDefault").Checked = true;
                Capture(address, Path.Combine(output, "address-editor.png"));
                All(address).OfType<Button>().Single(x => x.Name == "AddressSave").PerformClick();
                await Until(() => !address.Visible);
                Capture(editor, Path.Combine(output, "customer-editor-address.png")); editor.Close();
                detail = await LiveSession.Api.GetCustomerAsync(id);
                using var view = new CustomerDetailsForm(detail); Show(view); await Task.Delay(200);
                Capture(view, Path.Combine(output, "customer-profile.png"));
                All(view).OfType<TabControl>().Single(x => x.Name == "CustomerProfileTabs").SelectedIndex = 1;
                await Until(() => All(view).OfType<Label>().Any(x => x.Name == "AddressBookCount" && x.Text == "1 địa chỉ đang sử dụng"));
                Capture(view, Path.Combine(output, "customer-profile-address.png")); view.Close();
                using var shell = new FourChang.Desktop.Shell.MainForm("Admin", session.Name);
                Show(shell); shell.Navigate("employees");
                await Until(() => All(shell).OfType<DataGridView>().Any(x => x.Name == "EmployeesGrid" && x.Rows.Count > 0));
                var grid = All(shell).OfType<DataGridView>().Single(x => x.Name == "EmployeesGrid");
                var employeeId = long.Parse(Environment.GetEnvironmentVariable("FOURCHANG_TEST_EMPLOYEE_ID")!);
                var row = grid.Rows.Cast<DataGridViewRow>().Single(x => Convert.ToInt64(x.Cells[0].Value) == employeeId); grid.CurrentCell = row.Cells[1];
                All(shell).OfType<Button>().Single(x => x.Text == "Xem hồ sơ").PerformClick();
                await Until(() => Application.OpenForms.Cast<Form>().Any(x => x.Name == "EmployeeDetailsForm"));
                var employee = Application.OpenForms.Cast<Form>().Single(x => x.Name == "EmployeeDetailsForm");
                Capture(employee, Path.Combine(output, "employee-profile.png")); employee.Close();
                await Until(() => All(shell).OfType<Button>().Single(x => x.Text == "Sửa hồ sơ").Enabled);
                All(shell).OfType<Button>().Single(x => x.Text == "Sửa hồ sơ").PerformClick();
                await Until(() => Application.OpenForms.Cast<Form>().Any(x => x.Text == "Sửa hồ sơ nhân viên"));
                var employeeEdit = Application.OpenForms.Cast<Form>().Single(x => x.Text == "Sửa hồ sơ nhân viên");
                All(employeeEdit).OfType<TextBox>().Single(x => x.Name == "EmployeeContactAddress").Text = "45 Đường Trà, Phường An Bình, TP Hồ Chí Minh";
                Capture(employeeEdit, Path.Combine(output, "employee-editor-address.png"));
                All(employeeEdit).OfType<Button>().Single(x => x.Text == "Lưu hồ sơ").PerformClick();
                await Until(() => !employeeEdit.Visible);
                Assert.Equal("45 Đường Trà, Phường An Bình, TP Hồ Chí Minh", (await LiveSession.Api.GetEmployeeAsync(employeeId)).DiaChiLienHe);
                shell.Navigate("work-sessions"); await Task.Delay(250);
                Assert.All(All(shell).OfType<DateTimePicker>(), x => Assert.False(x.ShowCheckBox));
                Assert.Empty(All(shell).OfType<CheckBox>());
                Capture(shell, Path.Combine(output, "work-sessions-no-checkbox.png")); shell.Close();
                code = 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); }
            finally { host.Close(); }
        };
        Application.Run(host); return code;
    }

    /// <summary>Hiện form ngoài màn hình thật để không làm gián đoạn người dùng.</summary>
    private static void Show(Form form) { form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-16000, -16000); form.Show(); }
    /// <summary>Duyệt control thật, dùng Name ổn định cho trường nhập thay vì tọa độ màn hình.</summary>
    private static IEnumerable<Control> All(Control root) { foreach (Control child in root.Controls) { yield return child; foreach (var nested in All(child)) yield return nested; } }
    /// <summary>Chờ async continuation và modal trong giới hạn thời gian hữu hạn.</summary>
    private static async Task Until(Func<bool> condition) { var timer = Stopwatch.StartNew(); while (!condition()) { if (timer.Elapsed > TimeSpan.FromSeconds(20)) throw new TimeoutException("UI không đạt trạng thái chờ."); await Task.Delay(50); } await Task.Delay(100); }
    /// <summary>Lưu ảnh cửa sổ để kiểm tra bố cục, cắt chữ và vị trí hành động.</summary>
    private static void Capture(Form form, string path) { using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size)); image.Save(path); }
}
