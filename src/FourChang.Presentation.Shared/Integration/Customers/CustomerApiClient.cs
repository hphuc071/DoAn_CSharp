using System.Net;
using System.Text.Json;
namespace FourChang.Presentation.Shared.Integration;

public sealed partial class CafeApiClient
{
    public Task<CustomerPage> GetCustomersAsync(string search, int page, int pageSize = 20) =>
        SendAsync<CustomerPage>(HttpMethod.Get, $"/api/admin/customers?search={Uri.EscapeDataString(search)}&page={page}&pageSize={pageSize}");
    public Task<CustomerDetails> GetCustomerAsync(long id) => SendAsync<CustomerDetails>(HttpMethod.Get, $"/api/admin/customers/{id}");
    public Task<CustomerMutation> SaveCustomerAsync(long? id, CustomerSaveInput input) =>
        SendAsync<CustomerMutation>(id is null ? HttpMethod.Post : HttpMethod.Put, id is null ? "/api/admin/customers" : $"/api/admin/customers/{id}", input);
    public Task<CustomerMutation> ActOnCustomerAsync(long id, string action, CustomerActionInput input) =>
        SendAsync<CustomerMutation>(action == "delete" ? HttpMethod.Delete : HttpMethod.Post,
            action == "delete" ? $"/api/admin/customers/{id}" : $"/api/admin/customers/{id}/{action}", input);
    public async Task<CustomerMutation?> GetCustomerOperationAsync(Guid operation)
    {
        try { return await SendAsync<CustomerMutation>(HttpMethod.Get, $"/api/admin/customers/operations/{operation}"); }
        catch (CafeApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { return null; }
    }
    public Task<JsonElement> CheckTemporaryPasswordAsync() => SendAsync<JsonElement>(HttpMethod.Get, "/api/auth/customer/temporary-password");
    public Task<JsonElement> ChangeTemporaryPasswordAsync(ChangeTemporaryPassword input) => SendAsync<JsonElement>(HttpMethod.Post, "/api/auth/customer/temporary-password", input);
}
