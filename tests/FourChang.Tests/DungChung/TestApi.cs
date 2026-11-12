using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace FourChang.Tests.Shared;

// Mỗi Fact sở hữu API và database GUID riêng, không phụ thuộc dữ liệu dự án đang dùng.
public sealed class TestApi : IAsyncDisposable
{
    public static readonly string Root = FindRoot();

    /// <summary>
    /// Tìm FourChang.sln từ thư mục chạy test để đường dẫn dữ liệu không phụ thuộc thư mục terminal hiện tại.
    /// </summary>
    public static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "FourChang.sln")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Không tìm thấy FourChang.sln từ thư mục test.");
    }

    public Dictionary<string, string> Settings { get; } = ReadSettings();
    public string Database { get; } = "FourChang_Xunit_" + Guid.NewGuid().ToString("N");
    public string Url { get; } = FreeUrl();
    public string Artifacts { get; }
    public string AdminPassword => Settings["ADMIN_INITIAL_PASSWORD"];

    private readonly HttpClient http = new()
    {
        Timeout = TimeSpan.FromSeconds(25)
    };
    private readonly List<ChildProcess> children = [];

    /// <summary>
    /// Tạo thư mục bằng chứng riêng cho fixture theo tên bộ test và tên database GUID.
    /// </summary>
    private TestApi(string suite)
    {
        Artifacts = Path.Combine(Root, "artifacts", "tests", "xunit", suite, Database);
        Directory.CreateDirectory(Artifacts);
    }

    /// <summary>
    /// Khởi tạo đầy đủ môi trường kiểm thử riêng; chờ dịch vụ sẵn sàng và dọn tài nguyên nếu khởi tạo thất bại.
    /// </summary>
    public static async Task<TestApi> Start(string suite, bool legacy = false, Dictionary<string, string?>? runtime = null)
    {
        var api = new TestApi(suite);
        try
        {
            if (legacy)
            {
                await api.CreateLegacyDatabase();
            }

            // Bootstrap chỉnh SQL login dùng chung, nên tuần tự hóa giữa các testhost.
            await Task.Run(() =>
            {
                using var gate = new Mutex(false, @"Local\FourChangXunitBootstrap");
                bool held = false;
                try
                {
                    try
                    {
                        held = gate.WaitOne(TimeSpan.FromMinutes(3));
                    }
                    catch (AbandonedMutexException)
                    {
                        held = true;
                    }

                    if (!held)
                    {
                        throw new TimeoutException("Chờ khởi tạo database test quá lâu.");
                    }

                    api.RunApiCommand("--init-db").GetAwaiter().GetResult();
                }
                finally
                {
                    if (held)
                    {
                        gate.ReleaseMutex();
                    }
                }
            });
            // Link QR trong test luôn trỏ API fixture riêng, không trỏ localhost của database quán.
            if (runtime is not null && !runtime.ContainsKey("Payments__SimulationPublicBaseUrl"))
                runtime["Payments__SimulationPublicBaseUrl"] = api.Url;
            var server = api.StartDotnet("FourChang.Api", ["--urls", api.Url], "api", runtime);
            for (int attempt = 0; attempt < 100; attempt++)
            {
                if (server.HasExited)
                {
                    throw new InvalidOperationException("API test dừng sớm; xem artifacts của lần chạy.");
                }

                try
                {
                    if ((await api.Send("GET", "/health/ready")).Status == 200)
                    {
                        return api;
                    }
                }
                catch (HttpRequestException)
                {
                }

                await Task.Delay(300);
            }

            throw new TimeoutException("API test chưa ready sau 30 giây.");
        }
        catch
        {
            await api.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Xin hệ điều hành cấp một cổng localhost trống để các dịch vụ test không chiếm cổng của ứng dụng đang chạy.
    /// </summary>
    public static string FreeUrl()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return $"http://localhost:{port}";
    }

    /// <summary>
    /// Đọc cấu hình local từ .env để mở môi trường test; không in hoặc sao chép bí mật vào tài liệu kết quả.
    /// </summary>
    public static Dictionary<string, string> ReadSettings()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(Path.Combine(Root, ".env")))
        {
            var text = line.Trim();
            if (text.Length == 0 || text.StartsWith('#'))
            {
                continue;
            }

            var at = text.IndexOf('=');
            if (at <= 0)
            {
                continue;
            }

            result[text[..at].Trim()] = text[(at + 1)..].Trim().Trim('"', '\'');
        }

        return result;
    }

    /// <summary>
    /// Tạo môi trường cho process con, ghi đè database bằng tên test và bỏ connection string có thể trỏ tới dữ liệu thật.
    /// </summary>
    public Dictionary<string, string?> ChildEnvironment()
    {
        var env = Settings.ToDictionary(x => x.Key, x => (string?)x.Value);
        env["FOURCHANG_DATABASE"] = Database;
        env["ConnectionStrings__FourChang"] = null;
        env["ASPNETCORE_ENVIRONMENT"] = "Development";
        env["Orders__FulfillmentSimulationEnabled"] = "false"; // Test chỉ mở mô phỏng khi runtime của chính test yêu cầu.
        env["FOURCHANG_LOCAL"] = "true";
        return env;
    }

    /// <summary>
    /// Mở API hoặc Web đã build với cấu hình riêng và ghi log vào thư mục fixture; không sửa cấu hình ứng dụng thật.
    /// </summary>
    public ChildProcess StartDotnet(string project, string[] args, string log, Dictionary<string, string?>? extra = null)
    {
        var group = project == "FourChang.Api" ? "Backend" : "Presentation";
        var dll = Path.Combine(Root, "src", group, project, "bin", "Debug", "net8.0", project + ".dll");
        if (!File.Exists(dll))
        {
            throw new InvalidOperationException($"Cần build {project} trước khi test.");
        }

        var env = ChildEnvironment();
        if (extra is not null)
        {
            foreach (var pair in extra)
            {
                env[pair.Key] = pair.Value;
            }
        }

        var child = new ChildProcess("dotnet", [dll, .. args], project == "FourChang.Web" ? Path.Combine(Root, "src", group, project) : Root, env, Path.Combine(Artifacts, log));
        children.Add(child);
        return child;
    }

    /// <summary>
    /// Chạy lệnh chẩn đoán C# của API trên database fixture và kiểm tra exit code để không bỏ qua lỗi schema hoặc ràng buộc.
    /// </summary>
    public async Task RunApiCommand(string argument)
    {
        var child = StartDotnet("FourChang.Api", [argument], argument.TrimStart('-') + Guid.NewGuid().ToString("N"));
        await child.Wait();
        Assert.True(child.ExitCode == 0, $"Lệnh C# {argument} thất bại; xem artifacts/tests/xunit. Không ghi thông tin kết nối vào kết quả test.");
    }

    /// <summary>
    /// Gửi HTTP request thật với JSON và header được chỉ định; trả mã trạng thái và nội dung để kịch bản tự đối chiếu.
    /// </summary>
    public async Task<ApiResponse> Send(string method, string path, JsonNode? body = null, Dictionary<string, string>? headers = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), Url + path);
        if (body is not null)
        {
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        if (headers is not null)
        {
            foreach (var (key, value) in headers)
            {
                request.Headers.TryAddWithoutValidation(key, value);
            }
        }

        using var response = await http.SendAsync(request);
        return new((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Tạo header Authorization từ phiên fixture để kiểm tra quyền bằng chính cơ chế xác thực của API.
    /// </summary>
    public static Dictionary<string, string> Bearer(JsonNode session) => new()
    {
        ["Authorization"] = "Bearer " + session.Text("token")
    };

    /// <summary>
    /// Đăng nhập tài khoản quản lý của database test và trả header phiên; không dùng token của ứng dụng đang mở.
    /// </summary>
    public async Task<Dictionary<string, string>> AdminLogin(string? password = null) => Bearer((await Send("POST", "/api/auth/admin/login", TestData.Body(("TenDangNhap", "quanly"), ("MatKhau", password ?? AdminPassword)))).Expect(200, "Đăng nhập Admin fixture").Json);

    /// <summary>
    /// Đăng ký khách với định danh duy nhất qua API để tạo dữ liệu hợp lệ cho bước kiểm thử tiếp theo.
    /// </summary>
    public async Task<Customer> NewCustomer(string suffix = "customer")
    {
        var stamp = Guid.NewGuid().ToString("N");
        var password = TestData.Password();
        var body = TestData.Body(("MaThaoTac", TestData.NewId()), ("TenDangNhap", "web" + stamp[..12] + suffix), ("HoTen", "Khách kiểm thử " + suffix), ("Email", stamp + "@example.test"), ("SoDienThoai", TestData.Phone()), ("MatKhau", password), ("XacNhanMatKhau", password));
        var receipt = (await Send("POST", "/api/auth/customer/register", body)).Expect(200, "Tạo khách fixture").Json;
        return new(body, receipt.Number("maKhachHang"));
    }

    /// <summary>
    /// Đăng nhập bằng dữ liệu fixture và chờ phiên hoặc trang đích sẵn sàng trước khi kiểm tra bước tiếp theo.
    /// </summary>
    public async Task<Dictionary<string, string>> Login(Customer customer) => Bearer((await Send("POST", "/api/auth/customer/login", TestData.Body(("TenDangNhap", customer.Input.Text("TenDangNhap")), ("MatKhau", customer.Input.Text("MatKhau")), ("MaThaoTac", TestData.NewId())))).Expect(200, "Đăng nhập khách fixture").Json);

    /// <summary>
    /// Đọc hồ sơ của phiên Customer fixture và xác nhận API đã trả thành công trước khi dùng dữ liệu.
    /// </summary>
    public async Task<JsonNode> Profile(Dictionary<string, string> headers) => (await Send("GET", "/api/customer/profile", headers: headers)).Expect(200, "Đọc hồ sơ khách fixture").Json;

    /// <summary>
    /// Tra hồ sơ khách qua quyền Admin để đối chiếu dữ liệu liên thông và lấy phiên bản cập nhật hiện tại.
    /// </summary>
    public async Task<JsonNode> CustomerAccount(long id, Dictionary<string, string> admin) => (await Send("GET", $"/api/admin/customers/{id}", headers: admin)).Expect(200, "Đọc hồ sơ quản trị fixture").Json["account"]!;

    /// <summary>
    /// Chuẩn bị thông tin khách cùng mật khẩu tạm và mã thao tác mới cho các tình huống quản trị tài khoản.
    /// </summary>
    public JsonObject CustomerInput(string suffix = "admin")
    {
        var stamp = Guid.NewGuid().ToString("N");
        return TestData.Body(("MaThaoTac", TestData.NewId()), ("TenDangNhap", "adm" + stamp[..12] + suffix), ("HoTen", "Khách kiểm thử " + suffix), ("Email", stamp + "@example.test"), ("SoDienThoai", TestData.Phone()), ("MatKhauTam", TestData.Password()));
    }

    /// <summary>
    /// Tạo yêu cầu thao tác từ rowversion hiện tại, lý do và các trường bổ sung để không vô tình ghi đè dữ liệu mới.
    /// </summary>
    public static JsonObject Action(JsonNode record, params (string Key, object? Value)[] extra)
    {
        var body = TestData.Body(("MaThaoTac", TestData.NewId()), ("PhienBanDuLieu", record.Text("phienBanDuLieu")), ("LyDo", "Kiểm thử xUnit"));
        foreach (var pair in extra)
        {
            body[pair.Key] = System.Text.Json.JsonSerializer.SerializeToNode(pair.Value);
        }

        return body;
    }

    /// <summary>
    /// Đăng ký thiết bị POS fixture qua Admin và trả khóa/header riêng cho đúng máy kiểm thử.
    /// </summary>
    public async Task<Device> NewDevice(Dictionary<string, string> admin, string? machine = null)
    {
        machine ??= "TEST-" + Guid.NewGuid().ToString("N")[..10];
        var key = "Device-" + Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var input = TestData.Body(("MaThaoTac", TestData.NewId()), ("MaMay", machine), ("TenMay", "Máy xUnit"), ("KhoaThietBi", key), ("LyDo", "Kiểm thử"));
        var record = (await Send("POST", "/api/admin/access/devices", input, admin)).Expect(200, "Đăng ký máy fixture").Json["device"]!;
        return new(input, record, new() { ["X-POS-Machine"] = machine, ["X-POS-Key"] = key });
    }

    /// <summary>
    /// Tạo hồ sơ nhân viên qua API Admin; không tạo tài khoản đăng nhập cá nhân cho nhân viên.
    /// </summary>
    public async Task<JsonNode> Employee(Dictionary<string, string> admin, string name = "Nhân viên trùng tên kiểm thử") => (await Send("POST", "/api/admin/employees", TestData.Body(("HoTen", name), ("SoDienThoai", "0912345678"), ("NgayVaoLam", "2026-11-01"), ("TrangThai", "ACTIVE"), ("MaThaoTac", TestData.NewId())), admin)).Expect(200, "Tạo nhân viên fixture").Json;

    /// <summary>
    /// Chuẩn bị yêu cầu mở ca theo mã, họ tên nhân viên và đúng mã máy đã cấp quyền.
    /// </summary>
    public JsonObject OpenBody(JsonNode employee, Device device) => TestData.Body(("MaNhanVien", employee.Number("maNhanVien")), ("HoTen", employee.Text("hoTen")), ("TenCa", "Sáng"), ("MaMay", device.Input.Text("MaMay")), ("TienMatDauCa", 500000));

    /// <summary>
    /// Thực thi SQL có kiểm soát trong database fixture hoặc master; từ chối tên database nằm ngoài phạm vi để bảo vệ dữ liệu thật.
    /// </summary>
    public async Task<string> Sql(string query, string? database = null)
    {
        // Tên database đích chỉ thuộc fixture hoặc master; không cho kiểm thử chạy nhầm database thật.
        if (database is not null && database != "master" && database != Database)
        {
            throw new InvalidOperationException("Database ngoài phạm vi fixture.");
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = Settings.GetValueOrDefault("FOURCHANG_SQL_SERVER", "127.0.0.1,14330"),
            InitialCatalog = database ?? Database,
            UserID = "sa",
            Password = Settings["SQL_SA_PASSWORD"],
            Encrypt = true,
            TrustServerCertificate = true,
            ConnectTimeout = 15,
            Pooling = false
        };
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(query, connection)
        {
            CommandTimeout = 120
        };
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        do
        {
            while (await reader.ReadAsync())
            {
                rows.Add(string.Join("\t", Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "" : Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture))));
            }
        }
        while (await reader.NextResultAsync());
        return string.Join("\n", rows);
    }

    /// <summary>
    /// Dựng schema 34 bảng thật và sơ đồ SSMS mẫu để kiểm chứng nâng cấp, không giả lập trên database đã có sẵn bảng mới.
    /// </summary>
    private async Task CreateLegacyDatabase()
    {
        await Sql($"CREATE DATABASE [{Database}]", "master");
        var script = await File.ReadAllTextAsync(Path.Combine(Root, "docs/database/34-bang/schema.sql"));
        foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(batch))
            {
                await Sql(batch);
            }
        }

        await Sql("CREATE TABLE dbo.sysdiagrams(diagram_id int PRIMARY KEY,name sysname); EXEC sys.sp_addextendedproperty @name=N'microsoft_database_tools_support',@value=1,@level0type=N'SCHEMA',@level0name=N'dbo',@level1type=N'TABLE',@level1name=N'sysdiagrams'; INSERT dbo.sysdiagrams VALUES(1,N'ERD đã lưu');");
    }

    /// <summary>
    /// Dừng các process do fixture tạo, kiểm tra tiền tố GUID rồi chỉ xóa đúng database test; không nhận tên database thật để dọn.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var child in children.AsEnumerable().Reverse())
        {
            await child.DisposeAsync();
        }

        http.Dispose();
        if (!Regex.IsMatch(Database, @"^FourChang_Xunit_[a-f0-9]{32}$"))
        {
            throw new InvalidOperationException("Từ chối dọn database sai phạm vi.");
        }

        await Sql($"IF DB_ID(N'{Database}') IS NOT NULL BEGIN ALTER DATABASE [{Database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{Database}]; END", "master");
    }
}
