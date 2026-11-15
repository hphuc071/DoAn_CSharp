using FourChang.Tests.Shared;
using Xunit;
using System.Diagnostics;
using FourChang.Desktop.Shared.Preview;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Desktop.Admin.Customers.Views;
using FourChang.Desktop.Shell;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Common;
using FourChang.Presentation.Shared.Contracts.Customer;
using FourChang.Presentation.Shared.Contracts.Staff;

public sealed class CustomersUiTests
{

    /// <summary>
    /// Chuẩn bị API test rồi chạy giao diện quản trị khách hàng trong worker riêng để cô lập phiên và proxy mất phản hồi.
    /// </summary>
    [Fact, Trait("Actor", "QuanLy"), Trait("UseCase", "UC-ADM-01"), Trait("Layer", "WinForms")]
    public async Task GiaoDienVaHanhVi_UC_ADM_01()
    {
        await using var api = await TestApi.Start("UC-ADM-01-WinForms");
        var values = new Dictionary<string, string?>
        {
            ["FOURCHANG_API_URL"] = api.Url,
            ["FOURCHANG_TEST_API_URL"] = api.Url,
            ["FOURCHANG_TEST_PROXY_URL"] = TestApi.FreeUrl() + "/",
            ["FOURCHANG_TEST_ADMIN_PASSWORD"] = api.AdminPassword,
            ["FOURCHANG_POS_CREDENTIALS_PATH"] = Path.Combine(api.Artifacts, "device.dpapi")
        };
        await DesktopWorker.Run(api, "UC-ADM-01", values);
    }

    private static string output = "";
    private static int passed;

    /// <summary>
    /// Khởi tạo giao diện WinForms và message loop thật trên STA, lưu bằng chứng rồi trả kết quả về worker.
    /// </summary>
    internal static int RunHarness(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetDefaultFont(CafeTheme.Body());
        output = args[0];
        Directory.CreateDirectory(output);
        var sourceApi = Environment.GetEnvironmentVariable("FOURCHANG_TEST_API_URL") ?? "http://localhost:5183";
        var proxyUrl = Environment.GetEnvironmentVariable("FOURCHANG_TEST_PROXY_URL") ?? "http://localhost:5185/";
        using var lostReply = new LostReplyProxy(sourceApi, proxyUrl);
        Environment.SetEnvironmentVariable("FOURCHANG_API_URL", proxyUrl);
        var exitCode = 1;
        using var host = new Form
        {
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-22000, -22000),
            Size = new Size(20, 20)
        };
        host.Shown += (_, _) => host.BeginInvoke(new Action(() =>
        {
            exitCode = RunTests();
            host.Close();
        }));
        Application.Run(host);
        return exitCode;
    }

    /// <summary>
    /// Thao tác đầy đủ trên biểu mẫu UC-ADM-01, đối chiếu dữ liệu API và kiểm tra phục hồi sau khi mất phản hồi.
    /// </summary>
    private static int RunTests()
    {
        try
        {
            var password = Environment.GetEnvironmentVariable("FOURCHANG_TEST_ADMIN_PASSWORD") ?? throw new InvalidOperationException("xUnit fixture chưa cấp môi trường kiểm thử.");
            var session = Wait(LiveSession.Api.LoginAdminAsync(new("quanly", password)));
            LiveSession.Api.Token = session.Token;
            LiveSession.Current = session;
            LiveSession.Preview = false;
            var stamp = Guid.NewGuid().ToString("N")[..12];
            var fixturePassword = TestData.Password();
            var registered = Wait(LiveSession.Api.RegisterAsync(new("uiqa" + stamp,
                "Khách kiểm tra giao diện", "uiqa" + stamp + "@example.test", TestData.Phone(), fixturePassword, Guid.NewGuid(), fixturePassword)));
            using (var shell = new MainForm("Admin", "Quản lý"))
            {
                Show(shell);
                shell.Navigate("customers");
                PumpUntil(() => All(shell).OfType<DataGridView>().Any(x => x.Name == "CustomersGrid" && x.Rows.Count > 0));
                Capture(shell, "customer-list");
                Check(All(shell).OfType<Button>().Any(x => x.Text == "Đặt lại mật khẩu"), "Màn hình Khách hàng có đúng thao tác reset");
                Check(!All(shell).OfType<Button>().Any(x => x.Text is "Thêm khách hàng" or "Xóa có điều kiện" or "Khóa / mở / ngừng"), "Không còn nút tạo/xóa/ngừng khách");
                var initialTotal = Wait(LiveSession.Api.GetCustomersAsync("", 1)).Total;
                var customerGrid = Field<DataGridView>(shell, "CustomersGrid");
                var search = Field<TextBox>(shell, "CustomerSearch");
                search.Text = "uiqa" + stamp;
                Field<Button>(shell, "CustomerFind").PerformClick();
                PumpUntil(() => customerGrid.Rows.Count == 1 && customerGrid.Rows[0].DataBoundItem is CustomerAccount found && found.TenDangNhap == "uiqa" + stamp);
                Check(customerGrid.Rows.Count == 1, "Tìm theo từ khóa chỉ hiện khách phù hợp");
                Capture(shell, "customer-filtered");
                search.Clear();
                Field<Button>(shell, "CustomerReload").PerformClick();
                PumpUntil(() => Field<Label>(shell, "CustomerResultCount").Text == $"{initialTotal:N0} khách hàng");
                Check(Field<Label>(shell, "CustomerResultCount").Text == $"{initialTotal:N0} khách hàng", "Xóa từ khóa rồi Tải lại khôi phục tổng khách ban đầu");
                Check(customerGrid.Rows.Count == Math.Min(20, initialTotal), "Xóa từ khóa rồi Tải lại khôi phục dữ liệu trang đầy đủ");
                Check(Field<Label>(shell, "CustomerPaging").Text.StartsWith("Trang 1 /"), "Tải lại từ khóa hiện tại đưa danh sách về trang 1");
                Capture(shell, "customer-reloaded");
                search.Text = "khong-co-ket-qua-" + stamp;
                Field<Button>(shell, "CustomerFind").PerformClick();
                PumpUntil(() => customerGrid.Rows.Count == 0);
                Check(Field<Label>(shell, "CustomerResultCount").Text == "0 khách hàng", "Từ khóa không tồn tại hiển thị danh sách rỗng và tổng 0");
                Capture(shell, "customer-empty");
                search.Clear();
                Field<Button>(shell, "CustomerReload").PerformClick();
                PumpUntil(() => customerGrid.Rows.Count == Math.Min(20, initialTotal));
                Check(Field<Label>(shell, "CustomerResultCount").Text == $"{initialTotal:N0} khách hàng", "Tải lại sau kết quả rỗng vẫn khôi phục được toàn bộ danh sách");
                shell.ClientSize = new Size(1240, 820);
                Pump(250);
                Capture(shell, "customer-list-compact");
                shell.Close();
            }

            var detail = Wait(LiveSession.Api.GetCustomerAsync(registered.MaKhachHang));
            using (var info = new CustomerDetailsForm(detail))
            {
                Show(info);
                Capture(info, "customer-details");
                info.Close();
            }

            foreach (var action in new[]
            {
                "status",
                "reset-password"
            }

            )
            {
                using var form = new CustomerEditorForm(detail, action);
                Show(form);
                Capture(form, "customer-" + action);
                form.Close();
            }

            using (var update = new CustomerEditorForm(detail, "update"))
            {
                Show(update);
                Field<TextBox>(update, "CustomerName").Text = "Khách đã sửa từ WinForms";
                Capture(update, "customer-edit");
                Field<Button>(update, "CustomerSave").PerformClick();
                PumpUntil(() => Field<Button>(update, "CustomerSave").Text == "Kiểm tra / thử lại" && Field<Button>(update, "CustomerSave").Enabled);
                Check(!Field<TextBox>(update, "CustomerName").Enabled, "Mất phản hồi giữ nguyên nội dung cập nhật");
                Capture(update, "customer-lost-reply");
                Field<Button>(update, "CustomerSave").PerformClick();
                PumpUntil(() => !update.Visible);
                Check(LostReplyProxy.UpdateWrites == 1, "Tra receipt không PUT lần hai");
                Check(update.DialogResult == DialogResult.OK, "Sửa từ WinForms hoàn tất và đóng form đúng kết quả");
            }

            detail = Wait(LiveSession.Api.GetCustomerAsync(registered.MaKhachHang));
            Check(detail.Account.HoTen == "Khách đã sửa từ WinForms", "Dữ liệu sửa từ giao diện có trong SQL qua API");
            using (var locked = new CustomerEditorForm(detail, "status"))
            {
                Show(locked);
                var options = Field<ComboBox>(locked, "CustomerStatus");
                Check(options.Items.Count == 2, "Chỉ còn Hoạt động và Đã khóa");
                Check(!All(locked).OfType<TextBox>().Any(x => x.Name == "CustomerReason"), "Khóa/mở không bắt nhập lý do");
                options.SelectedIndex = 1;
                Capture(locked, "customer-lock");
                Field<Button>(locked, "CustomerSave").PerformClick();
                PumpUntil(() => !locked.Visible);
            }
            Check(Wait(LiveSession.Api.GetCustomerAsync(registered.MaKhachHang)).Account.TrangThai == "LOCKED", "Khóa từ form lưu đúng SQL");
            Wait(LiveSession.Api.LogoutAsync());
            Console.WriteLine($"UI QA PASSED: {passed} checks, screenshots saved.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
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
    /// Tìm đúng ô nhập liệu theo nhãn hoặc tên control để thao tác trên giao diện thật thay vì gọi tắt nghiệp vụ.
    /// </summary>
    private static T Field<T>(Control root, string name)
        where T : Control => All(root).OfType<T>().Single(x => x.Name == name);

    /// <summary>
    /// Duyệt cây control đệ quy để tìm thành phần lồng trong panel, tab hoặc biểu mẫu con.
    /// </summary>
    private static IEnumerable<Control> All(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var inner in All(child))
            {
                yield return inner;
            }
        }
    }

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
        form.WindowState = FormWindowState.Normal;
        Pump(200);
    }

    /// <summary>
    /// Vẽ biểu mẫu vào ảnh và lưu bằng chứng của kịch bản sau khi hoàn tất bố cục.
    /// </summary>
    private static void Capture(Form form, string name)
    {
        form.PerformLayout();
        Pump(150);
        using var image = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
        image.Save(Path.Combine(output, name + ".png"));
    }

    /// <summary>
    /// Cho WinForms xử lý message trong khoảng thời gian ngắn để timer, bố cục và async continuation tiếp tục chạy.
    /// </summary>
    private static void Pump(int milliseconds)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < milliseconds)
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
                throw new TimeoutException("Giao diện chưa hoàn thành thao tác trong 30 giây.");
            }

            Application.DoEvents();
            Thread.Sleep(10);
        }

        Pump(80);
    }
}

// Proxy chỉ phục vụ QA: cho server commit lần cập nhật đầu rồi chủ động cắt phần thân phản hồi.
// Không dùng proxy này trong đồ án hoặc lưu nội dung request/mật khẩu.
internal sealed class LostReplyProxy : IDisposable
{
    private readonly System.Net.HttpListener listener = new();
    private readonly HttpClient http = new();
    public static int UpdateWrites;
    private readonly string sourceApi;

    /// <summary>
    /// Mở proxy chỉ dành cho test sửa khách; lần cập nhật đầu được commit nhưng phản hồi bị cắt để kiểm tra chống tạo trùng.
    /// </summary>
    public LostReplyProxy(string sourceApi, string proxyUrl)
    {
        this.sourceApi = sourceApi.TrimEnd('/');
        listener.Prefixes.Add(proxyUrl.TrimEnd('/') + "/");
        listener.Start();
        _ = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    _ = Forward(context);
                }
                catch (System.Net.HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        });
    }

    /// <summary>
    /// Chuyển request tới API thật; khi kịch bản yêu cầu, cắt phản hồi sau commit để kiểm tra cơ chế phục hồi.
    /// </summary>
    private async Task Forward(System.Net.HttpListenerContext context)
    {
        try
        {
            using var request = new HttpRequestMessage(new HttpMethod(context.Request.HttpMethod), sourceApi + context.Request.RawUrl);
            if (context.Request.HasEntityBody)
            {
                using var body = new MemoryStream();
                await context.Request.InputStream.CopyToAsync(body);
                request.Content = new ByteArrayContent(body.ToArray());
                if (context.Request.ContentType is { } type)
                {
                    request.Content.Headers.TryAddWithoutValidation("Content-Type", type);
                }
            }

            if (context.Request.Headers["Authorization"] is { } auth)
            {
                request.Headers.TryAddWithoutValidation("Authorization", auth);
            }

            using var response = await http.SendAsync(request);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            context.Response.StatusCode = (int)response.StatusCode;
            context.Response.ContentType = "application/json";
            if (context.Request.HttpMethod == "PUT" && System.Text.RegularExpressions.Regex.IsMatch(context.Request.Url!.AbsolutePath, @"^/api/admin/customers/[0-9]+$") && Interlocked.Increment(ref UpdateWrites) == 1 && response.IsSuccessStatusCode)
            {
                context.Response.ContentLength64 = bytes.Length + 10;
                await context.Response.OutputStream.WriteAsync(bytes.AsMemory(0, 10));
                context.Response.Abort();
                return;
            }

            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
        catch (Exception)
        {
            context.Response.Abort();
        }
    }

    /// <summary>
    /// Đóng listener và HttpClient của proxy để giải phóng cổng và kết nối sau kiểm thử.
    /// </summary>
    public void Dispose()
    {
        listener.Close();
        http.Dispose();
    }
}
