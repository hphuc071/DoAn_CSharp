using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Customer;
using System.Net;
using System.Text.Json;
namespace FourChang.Presentation.Shared.ApiClients;

public sealed partial class CafeApiClient
{
    /// <summary>Tìm khách hiện hữu và phân trang từ API Admin.</summary>
    public Task<CustomerPage> GetCustomersAsync(string search, int page, int pageSize = 20) =>
        SendAsync<CustomerPage>(HttpMethod.Get, $"/api/admin/customers?search={Uri.EscapeDataString(search)}&page={page}&pageSize={pageSize}");
    /// <summary>Đọc hồ sơ theo mã khách, không lấy mật khẩu hoặc quyền xóa.</summary>
    public Task<CustomerDetails> GetCustomerAsync(long id) => SendAsync<CustomerDetails>(HttpMethod.Get, $"/api/admin/customers/{id}");
    /// <summary>Chỉ sửa khách hiện hữu; khách mới tự đăng ký qua Web.</summary>
    public Task<CustomerMutation> SaveCustomerAsync(long id, CustomerSaveInput input) =>
        SendAsync<CustomerMutation>(HttpMethod.Put, $"/api/admin/customers/{id}", input);
    /// <summary>Gửi đúng hai lệnh khóa/mở hoặc reset; không còn nhánh xóa khách.</summary>
    public Task<CustomerMutation> ActOnCustomerAsync(long id, string action, CustomerActionInput input)
    {
        if (action is not ("status" or "reset-password")) throw new ArgumentOutOfRangeException(nameof(action));
        return SendAsync<CustomerMutation>(HttpMethod.Post, $"/api/admin/customers/{id}/{action}", input);
    }
    /// <summary>Tra receipt của đúng Admin khi chưa rõ kết quả cập nhật.</summary>
    public async Task<CustomerMutation?> GetCustomerOperationAsync(Guid operation)
    {
        try { return await SendAsync<CustomerMutation>(HttpMethod.Get, $"/api/admin/customers/operations/{operation}"); }
        catch (CafeApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { return null; }
    }
    /// <summary>Đọc phiên đổi mật khẩu tạm của khách sau khi Admin cấp lại.</summary>
    public Task<JsonElement> CheckTemporaryPasswordAsync() => SendAsync<JsonElement>(HttpMethod.Get, "/api/auth/customer/temporary-password");
    /// <summary>Khách đổi mật khẩu tạm và thu hồi phiên hạn chế.</summary>
    public Task<JsonElement> ChangeTemporaryPasswordAsync(ChangeTemporaryPassword input) => SendAsync<JsonElement>(HttpMethod.Post, "/api/auth/customer/temporary-password", input);
}
