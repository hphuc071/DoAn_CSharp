using FourChang.Presentation.Shared.Contracts.Customer;
using System.Net;
namespace FourChang.Presentation.Shared.ApiClients;

// Tập trung URL và DTO của hồ sơ cá nhân để Razor Pages không tự gửi HTTP rời rạc.
public sealed partial class CafeApiClient
{
    public Task<CustomerSelfProfile> GetSelfProfileAsync() => SendAsync<CustomerSelfProfile>(HttpMethod.Get, "/api/customer/profile");
    public Task<CustomerSelfReceipt> SaveSelfProfileAsync(SaveSelfProfile input) => SendAsync<CustomerSelfReceipt>(HttpMethod.Put, "/api/customer/profile", input);
    public Task<CustomerSelfReceipt> SaveSelfAddressAsync(long? id, SaveSelfAddress input) => SendAsync<CustomerSelfReceipt>(id is null ? HttpMethod.Post : HttpMethod.Put, "/api/customer/addresses" + (id is null ? "" : "/" + id), input);
    public Task<CustomerSelfReceipt> RemoveSelfAddressAsync(long id, RemoveSelfAddress input) => SendAsync<CustomerSelfReceipt>(HttpMethod.Post, $"/api/customer/addresses/{id}/remove", input);
    public Task<CustomerSelfReceipt> ChangeSelfPasswordAsync(ChangeSelfPassword input) => SendAsync<CustomerSelfReceipt>(HttpMethod.Post, "/api/customer/password", input);
    public async Task<CustomerSelfReceipt?> GetSelfOperationAsync(Guid operation)
    { try { return await SendAsync<CustomerSelfReceipt>(HttpMethod.Get, $"/api/customer/account/operations/{operation}"); } catch (CafeApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { return null; } }
    public async Task<RegistrationReceipt?> GetRegistrationAsync(Guid operation)
    { try { return await SendAsync<RegistrationReceipt>(HttpMethod.Get, $"/api/auth/customer/registrations/{operation}"); } catch (CafeApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { return null; } }

    public Task<List<MenuVariantItem>> GetMenuVariantsAsync(long productId) =>
        SendAsync<List<MenuVariantItem>>(HttpMethod.Get, $"/api/menu/{productId}/variants");

    public Task<OrderPriceSummary> QuoteCustomerOrderAsync(CreateCustomerOrderRequest input) =>
        SendAsync<OrderPriceSummary>(HttpMethod.Post, "/api/customer/orders/quote", input);

    public Task<CustomerOrderReceipt> CreateCustomerOrderAsync(CreateCustomerOrderRequest input) =>
        SendAsync<CustomerOrderReceipt>(HttpMethod.Post, "/api/customer/orders", input);
}
