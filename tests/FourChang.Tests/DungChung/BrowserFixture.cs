using Microsoft.Playwright;

namespace FourChang.Tests.Shared;

// Playwright .NET được gọi trực tiếp trong Fact C#; không chạy Node script hay PowerShell.
public sealed class BrowserFixture : IAsyncDisposable
{
    public TestApi Api { get; private set; } = null!;
    public NetworkFaultProxy Proxy { get; private set; } = null!;
    public string WebUrl { get; } = TestApi.FreeUrl();
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;
    public IBrowserContext Context { get; private set; } = null!;
    public IPage Page { get; private set; } = null!;

    /// <summary>
    /// Giới hạn việc tạo fixture vào hàm Start để API, proxy, Web và trình duyệt được chuẩn bị cùng nhau.
    /// </summary>
    private BrowserFixture()
    {
    }

    /// <summary>
    /// Khởi tạo đầy đủ môi trường kiểm thử riêng; chờ dịch vụ sẵn sàng và dọn tài nguyên nếu khởi tạo thất bại.
    /// </summary>
    public static async Task<BrowserFixture> Start(string suite, Dictionary<string,string?>? runtime = null)
    {
        var fixture = new BrowserFixture();
        try
        {
            fixture.Api = await TestApi.Start(suite, runtime: runtime);
            fixture.Proxy = new(fixture.Api.Url);
            var webEnvironment = runtime is null ? new Dictionary<string,string?>() : new(runtime);
            webEnvironment["FOURCHANG_API_URL"] = fixture.Proxy.Url;
            var server = fixture.Api.StartDotnet("FourChang.Web", ["--urls", fixture.WebUrl], "web", webEnvironment);
            using var http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(3)
            };
            bool ready = false;
            for (int i = 0; i < 100; i++)
            {
                if (server.HasExited)
                {
                    throw new InvalidOperationException("Web test dừng sớm; xem artifacts.");
                }

                try
                {
                    using var response = await http.GetAsync(fixture.WebUrl + "/Account/Login");
                    if (response.IsSuccessStatusCode)
                    {
                        ready = true;
                        break;
                    }
                }
                catch (HttpRequestException)
                {
                }

                await Task.Delay(300);
            }

            Assert.True(ready, "Web kiểm thử chưa khởi động được.");
            fixture.Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            var options = new BrowserTypeLaunchOptions
            {
                Headless = true
            };
            var executable = Environment.GetEnvironmentVariable("FOURCHANG_TEST_CHROMIUM_PATH");
            if (!string.IsNullOrEmpty(executable))
            {
                options.ExecutablePath = executable;
            }
            else if (!File.Exists(fixture.Playwright.Chromium.ExecutablePath))
            {
                // Cài Chromium bằng API C#, thay cho playwright.ps1 của các mẫu hướng dẫn.
                Assert.Equal(0, Microsoft.Playwright.Program.Main(["install", "chromium"]));
            }

            fixture.Browser = await fixture.Playwright.Chromium.LaunchAsync(options);
            fixture.Context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1440, Height = 1080 } });
            fixture.Page = await fixture.Context.NewPageAsync();
            fixture.Page.Dialog += (_, dialog) => dialog.AcceptAsync();
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Tìm đúng ô nhập liệu theo nhãn hoặc tên control để thao tác trên giao diện thật thay vì gọi tắt nghiệp vụ.
    /// </summary>
    public ILocator Field(string name) => Page.GetByLabel(name, new() { Exact = true });

    /// <summary>
    /// Tìm nút theo đúng tên hiển thị để tránh bấm nhầm nút có nội dung tương tự.
    /// </summary>
    public ILocator Button(string name) => Page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    /// <summary>
    /// Đăng nhập bằng dữ liệu fixture và chờ phiên hoặc trang đích sẵn sàng trước khi kiểm tra bước tiếp theo.
    /// </summary>
    public async Task Login(string user, string password, string destination = "/")
    {
        await Page.GotoAsync(WebUrl + "/Account/Login");
        await Field("Tên đăng nhập, email hoặc số điện thoại").FillAsync(user);
        await Field("Mật khẩu").FillAsync(password);
        await Button("Đăng nhập").ClickAsync();
        await Page.WaitForURLAsync(WebUrl + destination);
    }

    /// <summary>
    /// Lưu ảnh toàn trang vào thư mục bằng chứng của lần chạy để dễ kiểm tra lỗi bố cục.
    /// </summary>
    public Task Screenshot(string name) => Page.ScreenshotAsync(new() { Path = Path.Combine(Api.Artifacts, name + ".png"), FullPage = true });

    /// <summary>
    /// Đọc token từ cookie của trình duyệt test để đối chiếu trạng thái thu hồi phía API; không ghi token ra log.
    /// </summary>
    public async Task<string> Token() => (await Context.CookiesAsync()).Single(x => x.Name == "fourchang_session").Value;

    /// <summary>
    /// Kiểm tra cookie có còn tồn tại hay không nhằm xác nhận hành vi đăng nhập và đăng xuất.
    /// </summary>
    public async Task<bool> HasCookie(string name) => (await Context.CookiesAsync()).Any(x => x.Name == name);

    /// <summary>
    /// Đóng trình duyệt, Playwright, proxy và API/database fixture trong finally để vẫn dọn sạch khi test gặp lỗi.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (Browser is not null)
            {
                await Browser.CloseAsync();
            }
        }
        finally
        {
            Playwright?.Dispose();
            if (Proxy is not null)
            {
                await Proxy.DisposeAsync();
            }

            if (Api is not null)
            {
                await Api.DisposeAsync();
            }
        }
    }
}
