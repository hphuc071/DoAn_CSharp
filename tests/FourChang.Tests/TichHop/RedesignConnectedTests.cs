using System.Diagnostics;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Desktop.Shell;
using FourChang.Presentation.Shared.Contracts.Common;
using FourChang.Tests.Shared;

namespace FourChang.Tests.Integration;

/// <summary>QA giao diện chạy đúng route thật với API/SQL riêng; không bật Preview và không dùng dữ liệu dự án chính.</summary>
public sealed class RedesignConnectedTests
{
    /// <summary>Kiểm tra toàn bộ route Admin/POS, bảng/tab thật, hai theme và giữ nháp khi điều hướng.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("Actor", "NhanVien"), Trait("Layer", "WinForms")]
    public async Task CacManHinhThat_GiuDuLieuVaThuongHieu()
    {
        await using var api = await TestApi.Start("Redesign-Connected");
        var admin = await api.AdminLogin();
        await api.NewCustomer("redesign");
        var employee = await api.Employee(admin, "Nhân viên kiểm tra giao diện");
        var device = await api.NewDevice(admin);
        await DesktopWorker.Run(api, "RedesignConnected", new()
        {
            ["FOURCHANG_API_URL"] = api.Url,
            ["FOURCHANG_TEST_ADMIN_PASSWORD"] = api.AdminPassword,
            ["FOURCHANG_TEST_EMPLOYEE_ID"] = employee.Number("maNhanVien").ToString(),
            ["FOURCHANG_TEST_EMPLOYEE_NAME"] = employee.Text("hoTen"),
            ["FOURCHANG_TEST_MACHINE"] = device.Input.Text("MaMay"),
            ["FOURCHANG_TEST_DEVICE_KEY"] = device.Input.Text("KhoaThietBi"),
            ["FOURCHANG_POS_CREDENTIALS_PATH"] = Path.Combine(api.Artifacts, "isolated-device.dpapi"),
            ["FOURCHANG_APPEARANCE_PATH"] = Path.Combine(api.Artifacts, "appearance.json")
        });
    }

    /// <summary>Worker STA có message loop và các phiên API được cấp thật từ fixture.</summary>
    internal static int RunHarness(string output)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false); Application.SetDefaultFont(CafeTheme.Body());
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        CafeVisuals.Initialize(); LiveSession.Preview = false;
        var result = 1;
        using var host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
        host.Shown += async (_, _) =>
        {
            try
            {
                var session = await LiveSession.Api.LoginAdminAsync(new("quanly", Environment.GetEnvironmentVariable("FOURCHANG_TEST_ADMIN_PASSWORD")!));
                LiveSession.Api.Token = session.Token; LiveSession.Current = session;
                using (var admin = new MainForm("Admin", session.Name))
                {
                    Show(admin);
                    foreach (var route in new[] { "dashboard" }.Concat(ScreenCatalog.All.Where(x => x.Role == "Admin").Select(x => x.Route)))
                    {
                        admin.Navigate(route); await Settled(admin);
                        Assert.DoesNotContain(All(admin).OfType<Button>(), x => x.Name == "AppearanceToggle");
                        Capture(admin, Path.Combine(output, $"Admin-{route}.png"));
                        // Những trang chỉ đọc dùng đúng các tab/bảng đã tồn tại; không bổ sung lệnh CRUD giả.
                        foreach (var tabs in All(admin).OfType<TabControl>().ToArray())
                        {
                            for (var index = 1; index < tabs.TabCount; index++)
                            {
                                tabs.SelectedIndex = index; await Settled(admin);
                                Capture(admin, Path.Combine(output, $"Admin-{route}-tab{index}.png"));
                            }
                        }
                    }
                    admin.Navigate("dashboard"); admin.ClientSize = new Size(1100, 740); await Settled(admin);
                    Capture(admin, Path.Combine(output, "Admin-dashboard-compact.png")); admin.Close();
                }
                LiveSession.Api.PosKey = Environment.GetEnvironmentVariable("FOURCHANG_TEST_DEVICE_KEY");
                LiveSession.Api.PosMachine = Environment.GetEnvironmentVariable("FOURCHANG_TEST_MACHINE");
                LiveSession.Api.Token = null;
                var staffSession = await LiveSession.Api.OpenShiftAsync(new(long.Parse(Environment.GetEnvironmentVariable("FOURCHANG_TEST_EMPLOYEE_ID")!), "Sáng", LiveSession.Api.PosMachine!, 500000, Environment.GetEnvironmentVariable("FOURCHANG_TEST_EMPLOYEE_NAME")));
                LiveSession.Current = staffSession; LiveSession.Api.Token = staffSession.Token;
                using (var staff = new MainForm("Staff", staffSession.Name))
                {
                    Show(staff);
                    foreach (var route in ScreenCatalog.All.Where(x => x.Role == "Staff").Select(x => x.Route))
                    {
                        staff.Navigate(route); await Settled(staff);
                        Assert.DoesNotContain(All(staff).OfType<Button>(), x => x.Name == "AppearanceToggle");
                        Capture(staff, Path.Combine(output, $"Staff-{route}.png"));
                    }
                    staff.Navigate("sale"); await Settled(staff);
                    // Nút thêm món phải vuông ở layout thật, không bị Dock kéo dãn thành bầu dục.
                    Assert.All(All(staff).OfType<Button>().Where(x => x.Visible && x.Text == "+"), button => Assert.Equal(new Size(44,44), button.Size));
                    var search = All(staff).OfType<TextBox>().First(x => x.Visible); search.Text = "Latte";
                    staff.Navigate("orders"); await Settled(staff); staff.Navigate("sale"); await Settled(staff);
                    Assert.Contains(All(staff).OfType<TextBox>(), input => ReferenceEquals(search, input) && input.Text == "Latte");
                    staff.Navigate("shift"); await Settled(staff);
                    var amount = All(staff).OfType<NumericUpDown>().Single(x => x.Visible); amount.Value = 512000;
                    staff.Navigate("sale"); staff.Navigate("shift"); await Settled(staff);
                    Assert.Same(staff, amount.FindForm());
                    Assert.Equal(512000, All(staff).OfType<NumericUpDown>().Single(x => x.Visible).Value);
                    staff.ClientSize = new Size(1100, 740); Capture(staff, Path.Combine(output, "Staff-shift-compact.png")); staff.Close();
                }
                result = 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); }
            finally { host.Close(); }
        };
        Application.Run(host); return result;
    }

    /// <summary>Đợi trạng thái đang tải thật kết thúc, có timeout và không dùng delay dài che lỗi UI.</summary>
    private static async Task Settled(Form form)
    {
        await Task.Delay(180);
        var timer = Stopwatch.StartNew();
        while (All(form).OfType<Label>().Any(x => x.Visible && x.Text.StartsWith("Đang tải")))
        {
            if (timer.Elapsed > TimeSpan.FromSeconds(20)) throw new TimeoutException("Trang vẫn đang tải.");
            await Task.Delay(40);
        }
        Assert.DoesNotContain(All(form).OfType<Label>(), label => label.Visible && label.Text.Contains("Không kết nối được backend"));
    }
    /// <summary>Hiện form ngoài vùng làm việc của người dùng nhưng vẫn chạy layout thực.</summary>
    private static void Show(Form form) { form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-16000, -16000); form.Show(); }
    /// <summary>Duyệt cây control để kiểm tra cùng instance và trạng thái tải/binding thật.</summary>
    private static IEnumerable<Control> All(Control root) { foreach (Control child in root.Controls) { yield return child; foreach (var nested in All(child)) yield return nested; } }
    /// <summary>Lưu bằng chứng GDI+ của route đang chạy với dữ liệu API thật của fixture.</summary>
    private static void Capture(Form form, string path) { using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size)); image.Save(path); }
}
