using System.Diagnostics;
using FourChang.Desktop.Admin.Employees;
using FourChang.Desktop.Admin.WorkSessions;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Tests.Shared;

namespace FourChang.Tests.Admin;

/// <summary>Kiểm thử hai trang Admin thật trên STA riêng; ảnh và log được lưu trong artifacts, không dùng dữ liệu chính.</summary>
public sealed class WorkforceUiTests
{
    /// <summary>Mở hai trang, tìm/lọc, xóa từ khóa và tải lại để kiểm tra tổng số trở về; lưu ảnh bố cục.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-12"), Trait("UseCase", "UC-ADM-13"), Trait("Layer", "WinForms")]
    public async Task DanhSachNhanVien_VaCaLamViec_TimTaiLai()
    {
        await using var api = await TestApi.Start("Workforce-WinForms");
        var admin = await api.AdminLogin();
        var employee = await api.Employee(admin, "Nhân viên giao diện Workforce");
        var device = await api.NewDevice(admin);
        (await api.Send("POST", "/api/pos/shifts", api.OpenBody(employee, device), device.Headers)).Expect(200, "Dữ liệu ca cho UI");
        await DesktopWorker.Run(api, "Workforce", new()
        {
            ["FOURCHANG_API_URL"] = api.Url,
            ["FOURCHANG_TEST_ADMIN_PASSWORD"] = api.AdminPassword,
            ["FOURCHANG_TEST_EMPLOYEE_ID"] = employee.Number("maNhanVien").ToString(),
            ["FOURCHANG_POS_CREDENTIALS_PATH"] = Path.Combine(api.Artifacts, "device.dpapi")
        });
    }

    /// <summary>Điểm vào chỉ dành cho worker xUnit; WinForms được chạy với message loop và theme giống ứng dụng.</summary>
    internal static int RunHarness(string output)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false); Application.SetDefaultFont(CafeTheme.Body());
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        var exit = 1;
        using var host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
        host.Shown += async (_, _) =>
        {
            try
            {
                var session = await LiveSession.Api.LoginAdminAsync(new("quanly", Environment.GetEnvironmentVariable("FOURCHANG_TEST_ADMIN_PASSWORD")!));
                LiveSession.Api.Token = session.Token; LiveSession.Current = session; LiveSession.Preview = false;
                using var form = new Form { Text = "4CHANG · Quản lý", ClientSize = new Size(1280, 760), BackColor = CafeTheme.Cream, StartPosition = FormStartPosition.Manual, Location = new Point(-16000, -16000) };
                var page = new EmployeesPage(); form.Controls.Add(page); form.Show();
                await Until(() => All(page).OfType<DataGridView>().Single().Rows.Count > 0);
                var grid = All(page).OfType<DataGridView>().Single(); var original = grid.Rows.Count;
                var search = All(page).OfType<TextBox>().Single(x => x.Name == "EmployeeSearch");
                search.Text = "KhongTimThayWorkforce"; Click(page, "Tìm kiếm");
                await Until(() => grid.Rows.Count == 0);
                search.Text = ""; Click(page, "Tải lại");
                await Until(() => grid.Rows.Count == original);
                Capture(form, Path.Combine(output, "admin-employees.png"));
                form.Controls.Remove(page); page.Dispose();
                var shifts = new WorkSessionsPage(long.Parse(Environment.GetEnvironmentVariable("FOURCHANG_TEST_EMPLOYEE_ID")!));
                form.Controls.Add(shifts);
                await Until(() => All(shifts).OfType<DataGridView>().Single().Rows.Count == 1);
                Assert.Contains(All(shifts).OfType<Button>(), x => x.Text == "Xem đối chiếu");
                Capture(form, Path.Combine(output, "admin-work-sessions.png"));
                Click(shifts, "Xem đối chiếu");
                await Until(() => Application.OpenForms.Cast<Form>().Any(x => x.Text.StartsWith("Đối chiếu ca ·")));
                var details = Application.OpenForms.Cast<Form>().Single(x => x.Text.StartsWith("Đối chiếu ca ·"));
                Assert.Contains(All(details).OfType<Button>(), x => x.Text == "Xác nhận chốt ca");
                Assert.Contains(All(details).OfType<Button>(), x => x.Text == "Xác nhận bàn giao");
                Capture(details, Path.Combine(output, "admin-work-session-details.png"));
                details.Close();
                form.Close();
                using var shell = new FourChang.Desktop.Shell.MainForm("Admin", session.Name);
                shell.ClientSize = new Size(1440, 960); shell.StartPosition = FormStartPosition.Manual; shell.Location = new Point(-16000, -16000);
                shell.Show(); shell.Navigate("work-sessions");
                await Until(() => All(shell).OfType<DataGridView>().Any(x => x.Name == "WorkSessionsGrid" && x.Rows.Count > 0));
                var shellGrid = All(shell).OfType<DataGridView>().Single(x => x.Name == "WorkSessionsGrid");
                Assert.True(shellGrid.Columns.Cast<DataGridViewColumn>().Where(x => x.Visible).Sum(x => x.Width) <= shellGrid.ClientSize.Width,
                    "Các cột phải vừa bảng ở cửa sổ desktop, không bị đẩy ra ngoài bởi mã dài.");
                Capture(shell, Path.Combine(output, "admin-shell-work-sessions.png"));
                shell.Navigate("employees");
                await Until(() => All(shell).OfType<DataGridView>().Any(x => x.Name == "EmployeesGrid" && x.Rows.Count > 0));
                Capture(shell, Path.Combine(output, "admin-shell-employees.png"));
                shell.Close(); exit = 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); }
            finally { host.Close(); }
        };
        Application.Run(host);
        return exit;
    }

    /// <summary>Duyệt control con để xác định đúng ô/bảng/nút trong layout thật.</summary>
    private static IEnumerable<Control> All(Control parent)
    {
        foreach (Control item in parent.Controls) { yield return item; foreach (var child in All(item)) yield return child; }
    }

    /// <summary>Bấm nút thật; không gọi tắt service thay hành vi UI.</summary>
    private static void Click(Control parent, string text) => All(parent).OfType<Button>().Single(x => x.Text == text).PerformClick();

    /// <summary>Đợi dữ liệu hoàn tất với timeout hữu hạn; Task.Delay nhường message loop để async UI tiếp tục.</summary>
    private static async Task Until(Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (!condition()) { if (timer.Elapsed > TimeSpan.FromSeconds(20)) throw new TimeoutException("Giao diện không đạt trạng thái chờ."); await Task.Delay(50); }
        await Task.Delay(150);
    }

    /// <summary>Chụp bố cục để kiểm tra trực quan bảng và toolbar sau khi đã tải xong.</summary>
    private static void Capture(Form form, string path)
    {
        using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(path);
    }
}
