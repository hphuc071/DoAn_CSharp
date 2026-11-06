using System.Reflection;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Desktop.Shell;
using FourChang.Tests.Shared;
using FourChang.Presentation.Shared.Contracts.Common;

namespace FourChang.Tests.Integration;

/// <summary>Kiểm tra giao diện cố định, nhóm nút, cửa sổ co giãn và cấu hình theme cũ bị bỏ qua.</summary>
public sealed class AppearanceDesktopTests
{
    /// <summary>Chạy tất cả route trong worker STA; không sửa database hoặc cấu hình của người dùng.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("Actor", "NhanVien"), Trait("Layer", "WinForms")]
    public async Task GiaoDienCoDinh_GiuLayoutVaNhapLieu()
    {
        var output = Path.Combine(TestApi.Root, "artifacts", "tests", "ui-refinement-desktop", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        await using var child = new ChildProcess("dotnet", [Assembly.GetExecutingAssembly().Location, "--desktop-scenario", "Appearance", output], TestApi.Root,
            new() { ["FOURCHANG_APPEARANCE_PATH"] = Path.Combine(output, "legacy-appearance.json") }, Path.Combine(output, "worker"));
        await child.Wait();
        var error = await File.ReadAllTextAsync(Path.Combine(output, "worker.stderr.log"));
        Assert.True(child.ExitCode == 0, error.Length > 4000 ? error[..4000] : error);
    }

    /// <summary>Mở form và render GDI thật; theme cũ chỉ là dữ liệu test, ứng dụng không đọc hoặc ghi file này.</summary>
    internal static int RunHarness(string output)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false); Application.SetDefaultFont(CafeTheme.Body());
        const string legacy = "{\"Dark\":true,\"OtherSetting\":\"keep\"}";
        var path = Environment.GetEnvironmentVariable("FOURCHANG_APPEARANCE_PATH")!;
        File.WriteAllText(path, legacy);
        CafeVisuals.Initialize(); LiveSession.Preview = true;
        try
        {
            foreach (var role in new[] { "Admin", "Staff" })
            {
                using (var entry = new EntryForm(role, _ => { }))
                {
                    Show(entry); CafeVisuals.Apply(entry);
                    var text = All(entry).OfType<TextBox>().First(); text.Text = "Nội dung đang nhập";
                    entry.ClientSize = new Size(1100, 800); Pump();
                    Assert.Equal("Nội dung đang nhập", text.Text);
                    Assert.Equal(CafeTheme.Surface.ToArgb(), text.BackColor.ToArgb());
                    AssertNoToggle(entry); ValidateButtonRows(entry);
                    Capture(entry, Path.Combine(output, role + "-entry.png")); entry.Close();
                }
                using var shell = new MainForm(role, "Nguyễn Hoàng Minh"); Show(shell);
                var routes = ScreenCatalog.All.Where(x => x.Role == role).Select(x => x.Route).ToList();
                if (role == "Admin") routes.Insert(0, "dashboard");
                foreach (var route in routes)
                {
                    shell.Navigate(route); Pump();
                    var inputs = All(shell).OfType<TextBox>().ToArray(); var before = inputs.Select(x => x.Text).ToArray();
                    foreach (var size in new[] { new Size(1440, 960), new Size(1100, 740) })
                    {
                        shell.ClientSize = size; Pump(); CafeVisuals.Apply(shell);
                        Assert.Equal(before, inputs.Select(x => x.Text).ToArray());
                        Assert.Equal(CafeTheme.Cream.ToArgb(), shell.BackColor.ToArgb());
                        AssertNoToggle(shell); ValidateButtonRows(shell);
                        var menu = All(shell).OfType<FlowLayoutPanel>().Single(x => x.Name == "MainNavigation");
                        Assert.False(menu.HorizontalScroll.Visible, "Sidebar tràn ngang: client=" + menu.ClientSize + ", display=" + menu.DisplayRectangle + ", scroll=" + menu.VerticalScroll.Visible + ", children=" + string.Join(";", menu.Controls.Cast<Control>().Select(x => x.GetType().Name + ":" + x.Bounds + "/margin=" + x.Margin)));
                        Capture(shell, Path.Combine(output, $"{role}-{route}-{size.Width}.png"));
                    }
                }
                shell.Close();
            }
            Assert.Equal(legacy, File.ReadAllText(path));
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    /// <summary>Không còn nút chuyển theme trên shell, entry hoặc trang con.</summary>
    private static void AssertNoToggle(Control root) => Assert.DoesNotContain(All(root).OfType<Button>(), x => x.Name == "AppearanceToggle" || x.Text is "Giao diện sáng" or "Giao diện tối");

    /// <summary>So sánh chiều cao nút trên cùng hàng thực, không yêu cầu các caption khác nhau cùng chiều rộng.</summary>
    private static void ValidateButtonRows(Control root)
    {
        foreach (var panel in All(root).OfType<FlowLayoutPanel>().Where(x => x.Visible))
        {
            foreach (var row in panel.Controls.OfType<Button>().Where(x => x.Visible).GroupBy(x => x.Top))
                Assert.True(row.Select(x => x.Height).Distinct().Count() <= 1, "Nút cùng hàng lệch chiều cao: " + string.Join(", ", row.Select(x => x.Text + "=" + x.Height)));
        }
    }
    /// <summary>Hiện form ngoài vùng làm việc người dùng, vẫn giữ layout và HWND thật.</summary>
    private static void Show(Form form) { form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-16000, -16000); form.Show(); Pump(); }
    /// <summary>Cho phép WinForms hoàn tất layout; không thêm animation hay thời gian chờ nghiệp vụ.</summary>
    private static void Pump() { for (var i = 0; i < 4; i++) Application.DoEvents(); }
    /// <summary>Duyệt control để kiểm tra binding/instance và tên control ổn định.</summary>
    private static IEnumerable<Control> All(Control root) { foreach (Control child in root.Controls) { yield return child; foreach (var nested in All(child)) yield return nested; } }
    /// <summary>Lưu ảnh từng màn hình và kích thước để kiểm tra trực quan sau assertion.</summary>
    private static void Capture(Form form, string path) { using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size)); image.Save(path); }
}
