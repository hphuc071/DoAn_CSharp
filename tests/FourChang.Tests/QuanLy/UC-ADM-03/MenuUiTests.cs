using System.Diagnostics;
using FourChang.Desktop.Shell;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;
namespace FourChang.Tests.Admin;

/// <summary>Test WinForms thật trên STA: tab/editor Admin và cấu hình POS dùng API cùng database riêng.</summary>
public sealed class MenuUiTests
{
    /// <summary>Mở giao diện có dữ liệu, thao tác thêm/tìm rồi chụp từng form; không dùng mock UI.</summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-03"), Trait("Layer", "WinForms")]
    public async Task AdminCRUD_Layout_VaPOSCauHinh()
    {
        await using var api = await TestApi.Start("ADM03-WinForms");
        var admin = await api.AdminLogin();
        var employee = await api.Employee(admin);
        var device = await api.NewDevice(admin);
        var session = (await api.Send("POST", "/api/pos/shifts", api.OpenBody(employee, device), device.Headers)).Expect(200, "Mở ca test").Json;
        await DesktopWorker.Run(api, "Menu", new()
        {
            ["FOURCHANG_API_URL"] = api.Url,
            ["FOURCHANG_TEST_ADMIN_PASSWORD"] = api.AdminPassword,
            ["FOURCHANG_TEST_STAFF_TOKEN"] = session.Text("token"),
            ["FOURCHANG_POS_CREDENTIALS_PATH"] = Path.Combine(api.Artifacts, "pos.dpapi")
        });
    }
    /// <summary>Worker chạy message loop như ứng dụng; chỉ các luồng test mới dùng thông tin fixture.</summary>
    internal static int RunHarness(string output)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetDefaultFont(CafeTheme.Body());
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        int exit = 1;
        using var host = new Form { ShowInTaskbar = false, Location = new(-20000, -20000), StartPosition = FormStartPosition.Manual };
        host.Shown += async (_, _) =>
        {
            try
            {
                var session = await LiveSession.Api.LoginAdminAsync(new("quanly", Environment.GetEnvironmentVariable("FOURCHANG_TEST_ADMIN_PASSWORD")!));
                LiveSession.Api.Token = session.Token;
                LiveSession.Current = session;
                LiveSession.Preview = false;
                using var shell = new MainForm("Admin", session.Name) { ClientSize = new(1440, 960), StartPosition = FormStartPosition.Manual, Location = new(-16000, -16000) };
                shell.Show();
                shell.Navigate("menu");
                await Until(() => All(shell).OfType<DataGridView>().Any(g => g.Name == "MenuGrid_dishes" && g.Rows.Count == 6));
                var tabs = All(shell).OfType<TabControl>().Single(x => x.Name == "MenuTabs");
                for (int i = 0; i < tabs.TabCount; i++)
                {
                    // Bấm thanh điều hướng thật để kiểm tra màu chọn và liên kết với trang native ẩn.
                    var key = new[] { "dishes", "variants", "categories", "toppings", "banners" }[i];
                    var navigation = All(shell).OfType<Button>().Single(b => b.Name == "MenuTab_" + key);
                    navigation.PerformClick();
                    Assert.Equal(i, tabs.SelectedIndex);
                    Assert.Equal(CafeTheme.Coffee, navigation.BackColor);
                    await Task.Delay(160);
                    Capture(shell, Path.Combine(output, $"admin-menu-{i}.png"));
                    var page = tabs.SelectedTab!;
                    var grid = All(page).OfType<DataGridView>().Single();
                    var search = All(page).OfType<TextBox>().Single();
                    var count = grid.Rows.Count;
                    Assert.Equal(44, search.Parent!.Height);
                    Assert.DoesNotContain(All(page).OfType<HScrollBar>(), bar => bar.Visible);
                    Assert.All(All(page).OfType<Button>().Where(b => b.Text is "Thêm" or "Sửa" or "Xóa có điều kiện" or "Tải lại"), b => Assert.Equal(44, b.Height));
                    search.Text = "KhongCoADM03";
                    Assert.Equal(0, grid.Rows.Count);
                    search.Text = "";
                    Assert.Equal(count, grid.Rows.Count);
                    var completion = new TaskCompletionSource();
                    host.BeginInvoke(new Action(async () =>
                    {
                        try
                        {
                            await Until(() => Application.OpenForms.Cast<Form>().Any(f => f.Name.StartsWith("MenuEditor_")));
                            var dialog = Application.OpenForms.Cast<Form>().Single(f => f.Name.StartsWith("MenuEditor_"));
                            await Task.Delay(120);
                            Capture(dialog, Path.Combine(output, $"admin-editor-{i}.png"));
                            if (i == 2)
                            {
                                All(dialog).OfType<TextBox>().Single(t => t.Name == "Tên danh mục").Text = "Danh mục tạo từ UI";
                                All(dialog).OfType<Button>().Single(b => b.Name == "SaveMenu").PerformClick();
                                await Until(() => dialog.IsDisposed || !dialog.Visible);
                            }
                            else
                                All(dialog).OfType<Button>().Single(b => b.Text == "Hủy").PerformClick();
                            completion.SetResult();
                        }
                        catch (Exception e) { foreach (var f in Application.OpenForms.Cast<Form>().Where(f => f.Name.StartsWith("MenuEditor_")).ToArray()) f.Close(); completion.SetException(e); }
                    }));
                    All(page).OfType<Button>().Single(b => b.Text == "Thêm").PerformClick();
                    await completion.Task;
                }
                shell.ClientSize = new(1100, 740);
                tabs.SelectedIndex = 1;
                await Task.Delay(200);
                Capture(shell, Path.Combine(output, "admin-menu-compact.png"));
                Assert.DoesNotContain(All(tabs.SelectedTab!).OfType<HScrollBar>(), bar => bar.Visible);
                shell.Close();
                LiveSession.Api.Token = Environment.GetEnvironmentVariable("FOURCHANG_TEST_STAFF_TOKEN");
                using var pos = new MainForm("Staff", "Nhân viên test") { ClientSize = new(1440, 960), StartPosition = FormStartPosition.Manual, Location = new(-16000, -16000) };
                pos.Show();
                pos.Navigate("sale");
                await Until(() => All(pos).OfType<Button>().Any(b => b.Name.StartsWith("SelectMenu_") && b.Enabled));
                Capture(pos, Path.Combine(output, "pos-menu.png"));
                var pickerDone = new TaskCompletionSource();
                host.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        await Until(() => Application.OpenForms.Cast<Form>().Any(f => f.Name == "CatalogSelectionForm"));
                        var picker = Application.OpenForms.Cast<Form>().Single(f => f.Name == "CatalogSelectionForm");
                        await Task.Delay(100);
                        Capture(picker, Path.Combine(output, "pos-config.png"));
                        All(picker).OfType<Button>().Single(b => b.Text == "Chọn cấu hình").PerformClick();
                        pickerDone.SetResult();
                    }
                    catch (Exception e) { pickerDone.SetException(e); }
                }));
                All(pos).OfType<Button>().First(b => b.Name.StartsWith("SelectMenu_") && b.Enabled).PerformClick();
                await pickerDone.Task;
                Capture(pos, Path.Combine(output, "pos-selected.png"));
                pos.Close();
                exit = 0;
            }
            catch (Exception e) { Console.Error.WriteLine(e); }
            finally { host.Close(); }
        };
        Application.Run(host);
        return exit;
    }
    /// <summary>Duyệt controls thật để xác định đúng nhãn/ô, không phụ thuộc tọa độ màn hình.</summary>
    private static IEnumerable<Control> All(Control root)
    {
        foreach (Control c in root.Controls)
        {
            yield return c;
            foreach (var n in All(c))
                yield return n;
        }
    }
    /// <summary>Chờ async UI với timeout hữu hạn; vẫn nhường message loop để form không treo.</summary>
    private static async Task Until(Func<bool> ready)
    {
        var watch = Stopwatch.StartNew();
        while (!ready())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(20))
                throw new TimeoutException("UI ADM-03 không sẵn sàng.");
            await Task.Delay(50);
        }
    }
    /// <summary>Lưu ảnh kiểm tra trực quan tại artifacts, không ảnh hưởng tài nguyên thương hiệu.</summary>
    private static void Capture(Form form, string path)
    {
        using var bmp = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bmp, new(Point.Empty, form.Size));
        bmp.Save(path);
    }
}
