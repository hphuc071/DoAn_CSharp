using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace FourChang.Presentation.Shared.Integration;

public sealed class CafeApiException(string message) : Exception(message);
public sealed class CafeApiClient(HttpClient http)
{
    public string? Token { get; set; }
    public string? PosKey { get; set; }
    public static Uri BaseAddress => new(Environment.GetEnvironmentVariable("FOURCHANG_API_URL") ?? "http://localhost:5181");
    public Task<List<MenuProduct>> GetMenuAsync(CancellationToken ct = default) => SendAsync<List<MenuProduct>>(HttpMethod.Get,"/api/menu",ct:ct);
    public Task<List<Employee>> GetEmployeesAsync(bool admin = false) => SendAsync<List<Employee>>(HttpMethod.Get,admin?"/api/admin/employees":"/api/pos/employees");
    public Task<SessionResult> LoginAdminAsync(SignIn input) => SendAsync<SessionResult>(HttpMethod.Post,"/api/auth/admin/login",input);
    public Task<SessionResult> LoginCustomerAsync(SignIn input) => SendAsync<SessionResult>(HttpMethod.Post,"/api/auth/customer/login",input);
    public Task<SessionResult> RegisterAsync(RegisterCustomer input) => SendAsync<SessionResult>(HttpMethod.Post,"/api/auth/customer/register",input);
    public Task<SessionResult> OpenShiftAsync(OpenShift input) => SendAsync<SessionResult>(HttpMethod.Post,"/api/pos/shifts",input);
    public Task<JsonElement> CloseShiftAsync(CloseShift input) => SendAsync<JsonElement>(HttpMethod.Post,"/api/pos/shifts/current/close",input);
    public Task<JsonElement> LogoutAsync() => SendAsync<JsonElement>(HttpMethod.Post,"/api/auth/logout");
    public Task<TableData> ReadTableAsync(string name) => SendAsync<TableData>(HttpMethod.Get,"/api/admin/data/"+Uri.EscapeDataString(name));
    public Task<TableData> ReadPosTableAsync(string name) => SendAsync<TableData>(HttpMethod.Get,"/api/pos/data/"+Uri.EscapeDataString(name));
    public Task<DashboardData> DashboardAsync() => SendAsync<DashboardData>(HttpMethod.Get,"/api/admin/dashboard");
    public Task<Employee> SaveEmployeeAsync(EmployeeInput input,long? id = null) => SendAsync<Employee>(id is null?HttpMethod.Post:HttpMethod.Put,id is null?"/api/admin/employees":"/api/admin/employees/"+id,input);
    private async Task<T> SendAsync<T>(HttpMethod method,string url,object? body = null,CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(method,url);
            if (body is not null) request.Content = JsonContent.Create(body);
            if (Token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",Token);
            if (url.StartsWith("/api/pos/",StringComparison.Ordinal) && PosKey is not null) request.Headers.Add("X-POS-Key",PosKey);
            using var response = await http.SendAsync(request,ct);
            if (!response.IsSuccessStatusCode)
            {
                var detail="Không thể thực hiện thao tác. Kiểm tra kết nối hoặc đăng nhập lại.";
                try { var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken:ct); if(problem.TryGetProperty("detail",out var message))detail=message.GetString()??detail; } catch(JsonException) { }
                throw new CafeApiException(detail);
            }
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken:ct) ?? throw new CafeApiException("Backend trả dữ liệu rỗng.");
        }
        catch(HttpRequestException) { throw new CafeApiException("Không kết nối được backend. Hãy chạy Chay-Backend.cmd trước."); }
        catch(TaskCanceledException) when (!ct.IsCancellationRequested) { throw new CafeApiException("Backend phản hồi quá lâu. Vui lòng thử lại."); }
    }
}
