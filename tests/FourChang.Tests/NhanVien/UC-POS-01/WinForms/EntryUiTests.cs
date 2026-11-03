using FourChang.Tests.Shared;
using Xunit;
using System.Diagnostics;
using FourChang.Desktop.Shared.Preview;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Desktop.Shell;

public sealed class EntryUiTests
{

    /// <summary>
    /// Chuẩn bị hai nhân viên trùng tên và thiết bị riêng, rồi kiểm tra mở ca cùng giao diện Staff trong worker STA.
    /// </summary>
    [Fact, Trait("Actor", "NhanVien"), Trait("UseCase", "UC-POS-01"), Trait("Layer", "WinForms")]
    public async Task GiaoDienVaHanhVi_UC_POS_01()
    {
        await using var api = await TestApi.Start("UC-POS-01-WinForms");
        var values = new Dictionary<string, string?>
        {
            ["FOURCHANG_API_URL"] = api.Url,
            ["FOURCHANG_TEST_API_URL"] = api.Url,
            ["FOURCHANG_TEST_PROXY_URL"] = TestApi.FreeUrl() + "/",
            ["FOURCHANG_TEST_ADMIN_PASSWORD"] = api.AdminPassword,
            ["FOURCHANG_POS_CREDENTIALS_PATH"] = Path.Combine(api.Artifacts, "device.dpapi")
        };
        var admin = await api.AdminLogin();
        var name = "Nhân viên trùng tên kiểm thử";
        var first = await api.Employee(admin, name);
        var second = await api.Employee(admin, name);
        var device = await api.NewDevice(admin, Environment.MachineName.ToUpperInvariant());
        values["FOURCHANG_TEST_POS_KEY"] = device.Input.Text("KhoaThietBi");
        values["FOURCHANG_TEST_EMPLOYEE_ID"] = first.Number("maNhanVien").ToString();
        values["FOURCHANG_TEST_SECOND_EMPLOYEE_ID"] = second.Number("maNhanVien").ToString();
        values["FOURCHANG_TEST_EMPLOYEE_NAME"] = name;
        await DesktopWorker.Run(api, "UC-POS-01", values);
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
        var code = 1;
        using var host = new Form
        {
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-22000, -22000)
        };
        host.Shown += (_, _) => host.BeginInvoke(new Action(() =>
        {
            code = Run();
            host.Close();
        }));
        Application.Run(host);
        return code;
    }

    /// <summary>
    /// Thực hiện chuỗi thao tác của kịch bản giao diện và trả trạng thái thành công hoặc lỗi cho bộ kiểm thử.
    /// </summary>
    private static int Run()
    {
        try
        {
            LiveSession.Preview = false;
            LiveSession.Api.PosKey = Environment.GetEnvironmentVariable("FOURCHANG_TEST_POS_KEY");
            LiveSession.Api.PosMachine = Environment.MachineName.ToUpperInvariant();
            LiveSession.Api.Token = null;
            var name = Environment.GetEnvironmentVariable("FOURCHANG_TEST_EMPLOYEE_NAME")!;
            var first = Environment.GetEnvironmentVariable("FOURCHANG_TEST_EMPLOYEE_ID")!;
            var second = Environment.GetEnvironmentVariable("FOURCHANG_TEST_SECOND_EMPLOYEE_ID")!;
            bool opened = false;
            using (var entry = new EntryForm("Staff", _ => opened = true))
            {
                Show(entry);
                Capture(entry, "staff-entry");
                Check(!All(entry).OfType<ComboBox>().Any(x => x.Name != "StaffShift"), "Tên nhân viên được nhập bằng TextBox, không chọn danh sách");
                var id = Field<TextBox>(entry, "StaffEmployeeId");
                var fullName = Field<TextBox>(entry, "StaffEmployeeName");
                var submit = Field<Button>(entry, "EntrySubmit");
                Check(!All(entry).OfType<Button>().Any(x => x.Text.Contains("Kiểm tra nhân viên")), "Không còn nút kiểm tra nhân viên thủ công");
                Check(submit.GetType().Name == "CafeButton", "Nút mở ca chỉ có chữ, không vẽ biểu tượng khóa");
                Check(!Field<Label>(entry, "StaffVerifyResult").Visible, "Chưa nhập thông tin thì ẩn trạng thái xác minh");
                Check(FullyVisible(submit), "Nút mở ca hiển thị đầy đủ, không bị card cắt mất");
                submit.PerformClick();
                PumpUntil(() => submit.Enabled);
                Check(!opened && Field<Label>(entry, "EntryFeedback").Visible, "Thiếu mã/tên không mở ca");
                id.Text = first;
                fullName.Text = "Sai họ tên";
                PumpUntil(() => Field<Label>(entry, "StaffVerifyResult").Text.Contains("Không tìm thấy"));
                Check(Field<Label>(entry, "StaffVerifyResult").Text.Contains("Không tìm thấy"), "Sai tên hiện lỗi rõ ràng");
                Capture(entry, "staff-invalid");
                fullName.Text = name;
                PumpUntil(() => Field<Label>(entry, "StaffVerifyResult").Text.Contains("Mã " + first));
                Check(Field<Label>(entry, "StaffVerifyResult").Text.Contains("Mã " + first), "Xác minh đúng mã thứ nhất");
                id.Text = second;
                Check(!Field<Label>(entry, "StaffVerifyResult").Visible, "Đổi mã làm mất trạng thái xác minh cũ");
                PumpUntil(() => Field<Label>(entry, "StaffVerifyResult").Text.Contains("Mã " + second));
                Check(Field<Label>(entry, "StaffVerifyResult").Text.Contains("Mã " + second), "Tên trùng vẫn xác minh đúng mã thứ hai");
                Capture(entry, "staff-verified");
                fullName.Text = "Sai họ tên";
                Pump(100);
                fullName.Text = name;
                PumpUntil(() => Field<Label>(entry, "StaffVerifyResult").Visible && Field<Label>(entry, "StaffVerifyResult").Text.Contains("Mã " + second));
                Check(!Field<Label>(entry, "StaffVerifyResult").Text.Contains("Không tìm thấy"), "Nhập nhanh rồi sửa tên không giữ phản hồi cũ");
                submit.PerformClick();
                PumpUntil(() => opened);
                Check(LiveSession.Current?.Id == long.Parse(second), "Mở ca từ giao diện lưu đúng nhân viên");
                entry.Close();
            }

            using (var shell = new MainForm("Staff", name))
            {
                Show(shell);
                Pump(650);
                Capture(shell, "staff-sale");
                shell.Navigate("shift");
                Pump(250);
                Capture(shell, "staff-shift");
                shell.Close();
            }

            Wait(LiveSession.Api.CloseShiftAsync(new(500000, null)));
            LiveSession.Api.Token = null;
            LiveSession.Current = null;
            bool adminOpened = false;
            using (var adminEntry = new EntryForm("Admin", _ => adminOpened = true))
            {
                Show(adminEntry);
                Check(Field<Button>(adminEntry, "EntrySubmit").GetType().Name == "CafeButton", "Nút đăng nhập Admin không có biểu tượng khóa");
                Check(All(adminEntry).Any(x => x.Name == "EntryBrandMark"), "Admin dùng logo vector mới");
                Check(Field<TextBox>(adminEntry, "AdminLoginPassword").UseSystemPasswordChar, "Đăng nhập Admin giữ mật khẩu được che");
                Capture(adminEntry, "admin-entry");
                Field<TextBox>(adminEntry, "AdminLoginPassword").Text = Environment.GetEnvironmentVariable("FOURCHANG_TEST_ADMIN_PASSWORD")!;
                Field<Button>(adminEntry, "EntrySubmit").PerformClick();
                PumpUntil(() => adminOpened || Field<Label>(adminEntry, "EntryFeedback").Visible);
                Check(adminOpened && LiveSession.Current?.Role == "ADMIN", "Đăng nhập Admin từ giao diện mới thành công");
                adminEntry.Close();
            }

            var session = LiveSession.Current ?? throw new InvalidOperationException("Thiếu phiên Admin sau đăng nhập giao diện.");
            LiveSession.Current = session;
            LiveSession.Api.Token = session.Token;
            using (var shell = new MainForm("Admin", "Quản lý"))
            {
                Show(shell);
                shell.Navigate("customers");
                Pump(600);
                Capture(shell, "admin-customers");
                shell.Navigate("access");
                Pump(600);
                Capture(shell, "admin-access");
                shell.Close();
            }

            Wait(LiveSession.Api.LogoutAsync());
            Console.WriteLine($"STAFF ENTRY UI PASSED: {passed} checks.");
            File.WriteAllText(Path.Combine(output, "result.json"), System.Text.Json.JsonSerializer.Serialize(new { passed, screenshots = 8 }));
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }

    /// <summary>
    /// Kiểm tra control nằm trong vùng hiển thị của mọi control cha để phát hiện nút bị cắt hoặc che khuất.
    /// </summary>
    private static bool FullyVisible(Control control)
    {
        var rect = control.RectangleToScreen(control.ClientRectangle);
        for (Control? current = control; current is not null; current = current.Parent)
        {
            if (!current.Visible || !current.RectangleToScreen(current.ClientRectangle).Contains(rect))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Đưa điều kiện kiểm tra về assertion xUnit cùng thông báo rõ mục đích; sai điều kiện làm test thất bại.
    /// </summary>
    private static void Check(bool value, string text)
    {
        Assert.True(value, text);
        passed++;
        Console.WriteLine("PASS: " + text);
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
    private static void Show(Form f)
    {
        f.StartPosition = FormStartPosition.Manual;
        f.Location = new Point(-16000, -16000);
        f.Show();
        Pump(180);
    }

    /// <summary>
    /// Vẽ biểu mẫu vào ảnh và lưu bằng chứng của kịch bản sau khi hoàn tất bố cục.
    /// </summary>
    private static void Capture(Form f, string name)
    {
        Pump(120);
        using var b = new Bitmap(f.Width, f.Height);
        f.DrawToBitmap(b, new Rectangle(Point.Empty, f.Size));
        b.Save(Path.Combine(output, name + ".png"));
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
    private static void PumpUntil(Func<bool> test)
    {
        var watch = Stopwatch.StartNew();
        while (!test())
        {
            if (watch.ElapsedMilliseconds > 30000)
            {
                throw new TimeoutException("Giao diện chưa hoàn tất trong 30 giây.");
            }

            Application.DoEvents();
            Thread.Sleep(10);
        }

        Pump(100);
    }
}
