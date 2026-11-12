using FourChang.Presentation.Shared.Contracts.Customer;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Globalization;
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

    /// <summary>Lấy danh sách bàn mô phỏng từ API chung để Web không tự khai báo mã bàn.</summary>
    public Task<List<CustomerTableOption>> GetCustomerTablesAsync() => SendAsync<List<CustomerTableOption>>(HttpMethod.Get, "/api/menu/tables");
    public Task<List<MenuVariantItem>> GetMenuVariantsAsync(long productId) =>
        SendAsync<List<MenuVariantItem>>(HttpMethod.Get, $"/api/menu/{productId}/variants");

    public Task<OrderPriceSummary> QuoteCustomerOrderAsync(CreateCustomerOrderRequest input) =>
        SendAsync<OrderPriceSummary>(HttpMethod.Post, "/api/customer/orders/quote", input);

    public async Task<FulfillmentPreview> PreviewCustomerFulfillmentAsync(FulfillmentPreviewRequest input,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/customer/orders/fulfillment-preview")
        { Content = JsonContent.Create(input) };
        if (Token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            string? code = null;
            try
            {
                var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                if (problem.ValueKind == JsonValueKind.Object && problem.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String)
                    code = c.GetString();
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException) { }
            throw new CafeApiException("Chưa xác minh được hình thức nhận món. Giữ nguyên giỏ và thao tác đang chờ.", response.StatusCode, code);
        }
        // Preview is a synchronous read: 201/202 must never masquerade as a valid quote.
        if (response.StatusCode != HttpStatusCode.OK)
            throw new CafeApiException("Trạng thái báo giá không hợp lệ.", HttpStatusCode.BadGateway);
        try
        {
            return await response.Content.ReadFromJsonAsync<FulfillmentPreview>(cancellationToken: ct)
                ?? throw new CafeApiException("Báo giá rỗng.", HttpStatusCode.BadGateway);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        { throw new CafeApiException("Báo giá không hợp lệ.", HttpStatusCode.BadGateway); }
    }

    // Request-scoped headers: never put an operation key on HttpClient.DefaultRequestHeaders.
    public async Task<(CustomerOrderReceipt Receipt, int Status)> CreateCustomerOrderAsync(
        CreateCustomerOrderRequest input, Guid operation, decimal acceptedTotal,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/customer/orders");
        request.Content = JsonContent.Create(input);
        request.Headers.Add("Idempotency-Key", operation.ToString("D"));
        request.Headers.Add("X-Checkout-Accepted-Total", acceptedTotal.ToString(CultureInfo.InvariantCulture));
        if (Token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode is not HttpStatusCode.OK and not HttpStatusCode.Created)
        {
            var message = "Không tạo được đơn. Giữ nguyên mã thao tác để thử lại.";
            string? code = null;
            try
            {
                var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                if (problem.ValueKind == JsonValueKind.Object)
                {
                    if (problem.TryGetProperty("code", out var errorCode) && errorCode.ValueKind == JsonValueKind.String)
                        code = errorCode.GetString();
                    foreach (var name in new[] { "message", "detail" })
                        if (problem.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                        { message = value.GetString() ?? message; break; }
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException) { }
            throw new CafeApiException(message, response.StatusCode, code);
        }
        var receipt = await response.Content.ReadFromJsonAsync<CustomerOrderReceipt>(cancellationToken: ct)
            ?? throw new JsonException("Backend trả biên nhận rỗng.");
        return (receipt, (int)response.StatusCode);
    }
}
