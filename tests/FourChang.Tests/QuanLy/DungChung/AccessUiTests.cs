using FourChang.Tests.Shared;
using Xunit;
using System.Diagnostics;
using System.Security.Cryptography;
using FourChang.Desktop.Shared.Preview;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Desktop.Admin.Access.Views;
using FourChang.Desktop.Shared.Security;
using FourChang.Desktop.Shell;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Common;
using FourChang.Presentation.Shared.Contracts.Customer;
using FourChang.Presentation.Shared.Contracts.Staff;

public sealed class AccessUiTests
{

    /// <summary>
    /// Chuẩn bị thiết bị fixture rồi chạy giao diện tài khoản, cấp hoặc thu hồi máy và đổi mật khẩu Admin trong worker riêng.
    /// </summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "COMMON-ADMIN-AUTH"), Trait("Layer", "WinForms")]
    public async Task GiaoDienVaHanhVi_UC_ADM_02()
    {
        await using var api = await TestApi.Start("UC-ADM-02-WinForms");
        var values = new Dictionary<string, string?>
        {
            ["FOURCHANG_API_URL"] = api.Url,
            ["FOURCHANG_TEST_API_URL"] = api.Url,
            ["FOURCHANG_TEST_PROXY_URL"] = TestApi.FreeUrl() + "/",
            ["FOURCHANG_TEST_ADMIN_PASSWORD"] = api.AdminPassword,
            ["FOURCHANG_POS_CREDENTIALS_PATH"] = Path.Combine(api.Artifacts, "device.dpapi")
        };
        var admin = await api.AdminLogin();
        await api.NewDevice(admin);
        await DesktopWorker.Run(api, "UC-ADM-02", values);
    }

    private static string output = "";
    private static int passed;

    /// <summary>
    /// Khởi tạo giao diện WinForms và message loop thật trên STA, lưu bằng chứng rồi trả kết quả về worker.
    /// </summary>
    internal static int RunHarness(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetDefaultFont(CafeTheme.Body());
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        output = args[0];
        Directory.CreateDirectory(output);
        var result = 1;
        // Message loop thật giúp async continuation chạy trên WinForms, giống ứng dụng người dùng mở.
        using var host = new Form
        {
            ShowInTaskbar = false,
            Location = new Point(-22000, -22000),
            StartPosition = FormStartPosition.Manual
        };
        host.Shown += (_, _) => host.BeginInvoke(new Action(() =>
        {
            result = Run();
            host.Close();
        }));
        Application.Run(host);
        return result;
    }

    /// <summary>
    /// Thực hiện chuỗi thao tác của kịch bản giao diện và trả trạng thái thành công hoặc lỗi cho bộ kiểm thử.
    /// </summary>
    private static int Run()
    {
        try
        {
            var original = Environment.GetEnvironmentVariable("FOURCHANG_TEST_ADMIN_PASSWORD")!;
            Login(original);
            var stamp = Guid.NewGuid().ToString("N")[..10];
            using (var entry = new EntryForm("Admin", _ =>
            {
            }))
            {
                Show(entry);
                Capture(entry, "admin-login");
                entry.Close();
            }

            using (var shell = new MainForm("Admin", "Quản lý"))
            {
                Show(shell);
                shell.Navigate("access");
                PumpUntil(() => All(shell).OfType<Label>().Any(x => x.Name == "AdminAccountInfo" && x.Text.Contains("quanly")));
                var tabs = Field<TabControl>(shell, "AccessTabs");
                Check(tabs.TabPages.Count == 3, "Đúng ba vùng tài khoản, thiết bị, phạm vi quyền");
                Check(!All(shell).OfType<Button>().Any(x => x.Text.Contains("Thêm quản lý")), "Không có tạo Quản lý thứ hai");
                Capture(shell, "access-account");
                tabs.SelectedIndex = 1;
                Pump(250);
                Capture(shell, "access-devices");
                Check(Field<DataGridView>(shell, "PosDevicesGrid").Rows.Count > 0, "Thiết bị thực được tải từ API");
                tabs.SelectedIndex = 2;
                Pump(250);
                Capture(shell, "access-permissions");
                Check(All(tabs.TabPages[2]).OfType<DataGridView>().All(x => x.ReadOnly), "Ma trận quyền chỉ đọc");
                shell.Close();
            }

            var machine = "UI-" + stamp.ToUpperInvariant();
            using (var form = new PosDeviceForm())
            {
                Show(form);
                Field<TextBox>(form, "PosDeviceMachine").Text = machine;
                Field<TextBox>(form, "PosDeviceName").Text = "Máy kiểm tra giao diện";
                Check(Field<TextBox>(form, "PosDeviceKey").UseSystemPasswordChar, "Khóa thiết bị được che");
                Check(!All(form).OfType<CheckBox>().Single(x => x.Text.StartsWith("Cấu hình POS")).Enabled, "Không lưu cấu hình máy khác lên Windows hiện tại");
                Field<TextBox>(form, "PosDeviceReason").Text = "Kiểm tra cấp cấu hình từ WinForms";
                Capture(form, "device-register");
                Field<Button>(form, "PosDeviceSave").PerformClick();
                PumpUntil(() => !Field<Button>(form, "PosDeviceSave").Visible);
                Check(Field<TextBox>(form, "PosDeviceKey").ReadOnly, "Sau cấp giữ khóa để bàn giao, không sửa yêu cầu đã chốt");
                form.Close();
                Check(form.DialogResult == DialogResult.OK, "Đóng form sau cấp báo cần tải lại danh sách");
            }

            var device = Wait(LiveSession.Api.GetPosDevicesAsync()).Single(x => x.MaMay == machine);
            Check(device.TrangThai == "ACTIVE", "Cấp máy từ giao diện lưu vào database");
            using (var revoke = new PosDeviceForm(device, true))
            {
                Show(revoke);
                Field<TextBox>(revoke, "PosDeviceReason").Text = "Kết thúc kiểm tra giao diện";
                Capture(revoke, "device-revoke");
                Field<Button>(revoke, "PosDeviceSave").PerformClick();
                PumpUntil(() => !revoke.Visible);
                Check(revoke.DialogResult == DialogResult.OK, "Thu hồi từ giao diện hoàn tất");
            }

            Check(Wait(LiveSession.Api.GetPosDevicesAsync()).Single(x => x.MaMay == machine).TrangThai == "REVOKED", "Trạng thái máy thu hồi được cập nhật trên API");
            var protectedPath = Path.Combine(output, "credential-test.json");
            var before = Environment.GetEnvironmentVariable("FOURCHANG_POS_CREDENTIALS_PATH");
            try
            {
                Environment.SetEnvironmentVariable("FOURCHANG_POS_CREDENTIALS_PATH", protectedPath);
                var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                PosDeviceCredentials.Save(Environment.MachineName, key);
                Check(PosDeviceCredentials.Load()?.KhoaThietBi == key, "DPAPI đọc lại được cấu hình của đúng người dùng Windows");
                Check(!File.ReadAllText(protectedPath).Contains(key), "File cấu hình không chứa khóa dạng rõ");
                File.Delete(protectedPath);
            }
            finally
            {
                Environment.SetEnvironmentVariable("FOURCHANG_POS_CREDENTIALS_PATH", before);
            }

            var next = "Ui-" + Guid.NewGuid().ToString("N") + "!";
            using (var form = new AdminPasswordForm(Wait(LiveSession.Api.GetAdminPasswordContextAsync())))
            {
                Show(form);
                Field<TextBox>(form, "AdminCurrentPassword").Text = original;
                Field<TextBox>(form, "AdminNewPassword").Text = next;
                Field<TextBox>(form, "AdminConfirmPassword").Text = "khong-khop";
                Check(new[] { "AdminCurrentPassword", "AdminNewPassword", "AdminConfirmPassword" }.All(n => Field<TextBox>(form, n).UseSystemPasswordChar), "Ba trường mật khẩu đều được che");
                Field<Button>(form, "AdminPasswordSave").PerformClick();
                PumpUntil(() => Field<Button>(form, "AdminPasswordSave").Enabled);
                Check(!form.Saved && form.Visible, "Xác nhận không khớp giữ form và không đổi mật khẩu");
                Capture(form, "admin-password-validation");
                Field<TextBox>(form, "AdminConfirmPassword").Text = next;
                Capture(form, "admin-password");
                Field<Button>(form, "AdminPasswordSave").PerformClick();
                PumpUntil(() => form.Saved);
                Check(form.DialogResult == DialogResult.OK, "Đổi mật khẩu từ giao diện yêu cầu đăng nhập lại");
            }

            Login(next);
            var profile = Wait(LiveSession.Api.GetAdminPasswordContextAsync());
            Wait(LiveSession.Api.ChangeAdminPasswordAsync(new(Guid.NewGuid(), profile.PhienBanDuLieu, next, original, original)));
            Login(original);
            Wait(LiveSession.Api.LogoutAsync());
            LiveSession.Api.Token = null;
            LiveSession.Current = null;
            Console.WriteLine($"UI QA PASSED: {passed} checks, 8 screenshots.");
            File.WriteAllText(Path.Combine(output, "result.json"), System.Text.Json.JsonSerializer.Serialize(new { passed, screenshots = 8 }));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    /// <summary>
    /// Đăng nhập bằng dữ liệu fixture và chờ phiên hoặc trang đích sẵn sàng trước khi kiểm tra bước tiếp theo.
    /// </summary>
    private static void Login(string password)
    {
        var session = Wait(LiveSession.Api.LoginAdminAsync(new("quanly", password)));
        LiveSession.Current = session;
        LiveSession.Api.Token = session.Token;
        LiveSession.Preview = false;
    }

    /// <summary>
    /// Đưa điều kiện kiểm tra về assertion xUnit cùng thông báo rõ mục đích; sai điều kiện làm test thất bại.
    /// </summary>
    private static void Check(bool value, string name)
    {
        Assert.True(value, name);
        passed++;
        Console.WriteLine("PASS: " + name);
    }

    /// <summary>
    /// Duyệt cây control đệ quy để tìm thành phần lồng trong panel, tab hoặc biểu mẫu con.
    /// </summary>
    private static IEnumerable<Control> All(Control root)
    {
        foreach (Control c in root.Controls)
        {
            yield return c;
            foreach (var child in All(c))
            {
                yield return child;
            }
        }
    }

    /// <summary>
    /// Tìm đúng ô nhập liệu theo nhãn hoặc tên control để thao tác trên giao diện thật thay vì gọi tắt nghiệp vụ.
    /// </summary>
    private static T Field<T>(Control root, string name)
        where T : Control => All(root).OfType<T>().Single(x => x.Name == name);

    /// <summary>
    /// Chờ thao tác hoàn tất trong giới hạn thời gian; để lỗi gốc được báo về test thay vì tiếp tục khi dữ liệu chưa sẵn sàng.
    /// </summary>
    private static T Wait<T>(Task<T> task)
    {
        PumpUntil(() => task.IsCompleted);
        return task.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Mở biểu mẫu tại vị trí ngoài màn hình và chờ bố cục ổn định, tránh làm gián đoạn cửa sổ người dùng đang thao tác.
    /// </summary>
    private static void Show(Form form)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-16000, -16000);
        form.Show();
        Pump(200);
    }

    /// <summary>
    /// Vẽ biểu mẫu vào ảnh và lưu bằng chứng của kịch bản sau khi hoàn tất bố cục.
    /// </summary>
    private static void Capture(Form form, string name)
    {
        Pump(150);
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        bitmap.Save(Path.Combine(output, name + ".png"));
    }

    /// <summary>
    /// Cho WinForms xử lý message trong khoảng thời gian ngắn để timer, bố cục và async continuation tiếp tục chạy.
    /// </summary>
    private static void Pump(int ms)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < ms)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    /// <summary>
    /// Xử lý message WinForms đến khi điều kiện hoàn tất; báo timeout rõ ràng nếu giao diện không phản hồi.
    /// </summary>
    private static void PumpUntil(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.ElapsedMilliseconds > 30000)
            {
                throw new TimeoutException("WinForms chưa hoàn tất trong 30 giây.");
            }

            Application.DoEvents();
            Thread.Sleep(10);
        }

        Pump(80);
    }
}
