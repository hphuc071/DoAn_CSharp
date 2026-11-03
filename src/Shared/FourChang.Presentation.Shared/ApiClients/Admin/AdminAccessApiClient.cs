using FourChang.Presentation.Shared.Contracts.Admin;
using System.Net;
namespace FourChang.Presentation.Shared.ApiClients;

public sealed partial class CafeApiClient
{
    public Task<AdminAccessProfile> GetAdminAccountAsync() => SendAsync<AdminAccessProfile>(HttpMethod.Get, "/api/admin/access/account");
    public Task<AdminAccessProfile> GetAdminPasswordContextAsync() => SendAsync<AdminAccessProfile>(HttpMethod.Get, "/api/auth/admin/password-context");
    public Task<AdminPasswordReceipt> ChangeAdminPasswordAsync(AdminPasswordInput input) => SendAsync<AdminPasswordReceipt>(HttpMethod.Post, "/api/auth/admin/password", input);
    public async Task<AdminPasswordReceipt?> GetAdminPasswordReceiptAsync(Guid operation)
    {
        try { return await SendAsync<AdminPasswordReceipt>(HttpMethod.Get, $"/api/auth/admin/password-operations/{operation}"); }
        catch (CafeApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { return null; }
    }
}
