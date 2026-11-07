using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FourChang.Presentation.Shared.Contracts.Customer;

namespace FourChang.Presentation.Shared.ApiClients;

public sealed partial class CafeApiClient
{
    public Task<CustomerOrderPage> GetCustomerOrdersAsync(CustomerOrderFilter filter, CancellationToken ct = default, bool expiryReadEnabled = false)
    {
        if (filter.Validate(expiryReadEnabled) is { } error) throw new ArgumentException(error);
        var query = $"/api/customer/orders?page={filter.Page}&pageSize={filter.PageSize}";
        if (filter.Status is not null) query += "&status=" + Uri.EscapeDataString(filter.Status);
        if (filter.FromUtc is { } from) query += "&fromUtc=" + Uri.EscapeDataString(from.ToString("O", CultureInfo.InvariantCulture));
        if (filter.ToUtc is { } to) query += "&toUtc=" + Uri.EscapeDataString(to.ToString("O", CultureInfo.InvariantCulture));
        return ReadCustomerSnapshotAsync<CustomerOrderPage>(query, ct);
    }

    public Task<CustomerOrderDetail> GetCustomerOrderAsync(long orderId, CancellationToken ct = default)
    {
        if (orderId <= 0) throw new ArgumentOutOfRangeException(nameof(orderId));
        return ReadCustomerSnapshotAsync<CustomerOrderDetail>($"/api/customer/orders/{orderId}", ct);
    }

    private async Task<T> ReadCustomerSnapshotAsync<T>(string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Token)) throw new CafeApiException("Cần đăng nhập.", HttpStatusCode.Unauthorized);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            request.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode != HttpStatusCode.OK)
                throw new CafeApiException("Không tải được dữ liệu. Hãy tải lại hoặc đăng nhập lại.", response.StatusCode);
            var snapshot = await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
            if (snapshot is null) throw new JsonException();
            return snapshot;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        { throw new CafeApiException("Backend trả dữ liệu không hợp lệ.", HttpStatusCode.BadGateway); }
        catch (HttpRequestException)
        { throw new CafeApiException("Không kết nối được Backend. Chưa xác định trạng thái mới.", HttpStatusCode.ServiceUnavailable); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new CafeApiException("Backend phản hồi quá lâu. Hãy tải lại snapshot.", HttpStatusCode.GatewayTimeout); }
    }
}
