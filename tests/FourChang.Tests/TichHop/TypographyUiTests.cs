using System.Text.Json;
using FourChang.Desktop;
using FourChang.Desktop.Shell;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.Contracts.Common;
using FourChang.Tests.Shared;

namespace FourChang.Tests;

/// <summary>Kiểm tra font/layout thật của hai Desktop với API và database fixture riêng, không ghi vào dữ liệu quán.</summary>
public sealed class TypographyUiTests
{
    /// <summary>Mở từng route của Admin/POS bằng phiên hợp lệ; lưu ảnh để kiểm tra dấu và căn chỉnh.</summary>
    [Theory, Trait("Layer", "WinForms"), Trait("Feature", "Typography")]
    [InlineData("Admin")]
    [InlineData("Staff")]
    public async Task CacTrang_DungFontTiengViet_VaKhongCatNut(string role)
    {
        await using var api = await TestApi.Start("Typography-" + role);
        var admin = await api.AdminLogin();
        var session = (await api.Send("POST", "/api/auth/admin/login", TestData.Body(("TenDangNhap", "quanly"), ("MatKhau", api.AdminPassword)))).Expect(200, "Phiên kiểm tra UI").Json;
        var device = await api.NewDevice(admin);
        if (role == "Staff")
            session = (await api.Send("POST", "/api/pos/shifts", api.OpenBody(await api.Employee(admin, "Nguyễn Hoàng Minh"), device), device.Headers)).Expect(200, "Mở ca fixture").Json;
        await DesktopWorker.Run(api, "TYPOGRAPHY", new()
        {
            ["FOURCHANG_API_URL"] = api.Url,
            ["FOURCHANG_TYPOGRAPHY_SESSION"] = session.ToJsonString(),
            ["FOURCHANG_TYPOGRAPHY_ROLE"] = role,
            ["FOURCHANG_TYPOGRAPHY_MACHINE"] = device.Input.Text("MaMay"),
            ["FOURCHANG_TYPOGRAPHY_KEY"] = device.Input.Text("KhoaThietBi"),
            ["FOURCHANG_POS_CREDENTIALS_PATH"] = Path.Combine(api.Artifacts, "typography-device.dpapi")
        });
    }

    /// <summary>Worker STA sử dụng chính bootstrap của ứng dụng để phát hiện sai lệch font giữa bản chạy và test.</summary>
    internal static int RunHarness(string output)
    {
        DesktopLauncher.ConfigureUi();
        var role = Environment.GetEnvironmentVariable("FOURCHANG_TYPOGRAPHY_ROLE")!;
        LiveSession.Current = JsonSerializer.Deserialize<SessionResult>(Environment.GetEnvironmentVariable("FOURCHANG_TYPOGRAPHY_SESSION")!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        LiveSession.Api.Token = LiveSession.Current.Token;
        LiveSession.Api.PosKey = Environment.GetEnvironmentVariable("FOURCHANG_TYPOGRAPHY_KEY");
        LiveSession.Api.PosMachine = Environment.GetEnvironmentVariable("FOURCHANG_TYPOGRAPHY_MACHINE");
        LiveSession.Preview = false;
        var exit = 1;
        using var loop = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) };
        loop.Shown += async (_, _) =>
        {
            try
            {
                using (var entry = new EntryForm(role, _ => { }))
                {
                    entry.StartPosition = FormStartPosition.Manual; entry.Location = new(-18000, -18000); entry.Show();
                    await Task.Delay(500); Capture(entry, output, role + "-entry"); entry.Close();
                }
                using var shell = new MainForm(role, role == "Admin" ? "Quản lý" : "Nguyễn Hoàng Minh")
                { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-16000, -16000) };
                shell.Show();
                var routes = ScreenCatalog.All.Where(x => x.Role == role).Select(x => x.Route).Distinct().ToList();
                if (role == "Admin") routes.Insert(0, "dashboard");
                foreach (var route in routes)
                {
                    shell.Navigate(route); await Task.Delay(700); shell.PerformLayout();
                    Capture(shell, output, role + "-" + route);
                    foreach (var button in Descendants(shell).OfType<Button>().Where(x => x.Visible && x.Height >= 32 && !string.IsNullOrWhiteSpace(x.Text)))
                    {
                        var height = TextRenderer.MeasureText(button.Text, button.Font, Size.Empty, TextFormatFlags.SingleLine).Height;
                        Assert.True(height <= button.Height, "Nút cắt chiều cao chữ: " + button.Text);
                    }
                }
                shell.ClientSize = new(1240, 820); await Task.Delay(100); Capture(shell, output, role + "-compact");
                shell.Close(); exit = 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); }
            finally { loop.Close(); }
        };
        Application.Run(loop); return exit;
    }

    /// <summary>Chụp toàn cửa sổ vào artifact, giữ nguyên dữ liệu và vị trí cửa sổ người dùng đang làm việc.</summary>
    private static void Capture(Form form, string directory, string name)
    {
        using var image = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
        image.Save(Path.Combine(directory, name + ".png"));
    }

    /// <summary>Duyệt control con để kiểm tra các nút ở mọi vùng chức năng, không dựa tên control riêng từng trang.</summary>
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
