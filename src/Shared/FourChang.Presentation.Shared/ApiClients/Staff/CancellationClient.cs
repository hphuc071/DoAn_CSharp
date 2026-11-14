using System.Net.Http.Json;
using System.Text.Json;
using FourChang.Presentation.Shared.Contracts.Orders;

namespace FourChang.Presentation.Shared.ApiClients;

public sealed partial class CafeApiClient
{
    /// <summary>Đúng endpoint theo vai trò, không dùng token POS gọi API Admin.</summary>
    public Task<CancellationInfo> CancellationActionAsync(long id, CancellationCommand input, bool staff) =>
        SendAsync<CancellationInfo>(HttpMethod.Post, $"/api/{(staff ? "pos" : "admin")}/orders/{id}/cancellation", input);
    /// <summary>Tra kết quả của thao tác chuyển/duyệt khi mất phản hồi, giữ nguyên GUID cũ.</summary>
    public Task<CancellationInfo> CancellationReceiptAsync(Guid id, bool staff) =>
        SendAsync<CancellationInfo>(HttpMethod.Get, $"/api/{(staff ? "pos" : "admin")}/orders/operations/{id}");
    /// <summary>Chỉ liệt kê khoản tiền mặt đã được duyệt và còn chi, không trả dữ liệu ngân hàng.</summary>
    public Task<IReadOnlyList<CounterRefund>> CounterRefundsAsync(long id) => SendAsync<IReadOnlyList<CounterRefund>>(HttpMethod.Get, $"/api/pos/orders/{id}/counter-refunds");
    /// <summary>Ghi nhận giao tiền thực tế theo đúng phiên ca; máy chủ quyết định số tiền trong hạn mức.</summary>
    public Task<JsonElement> PayCounterRefundAsync(long id, CounterRefundCommand input) => SendAsync<JsonElement>(HttpMethod.Post, $"/api/pos/orders/counter-refunds/{id}", input);
    /// <summary>Tra biên nhận chi đúng ca sau timeout để không hoàn hai lần.</summary>
    public Task<JsonElement> CounterRefundReceiptAsync(Guid id) => SendAsync<JsonElement>(HttpMethod.Get, $"/api/pos/orders/counter-refunds/operations/{id}");
    /// <summary>Khách chỉ gửi đề nghị, không được tự ghi đã hủy hoặc đã hoàn tiền.</summary>
    public async Task RequestCancellationAsync(long id, string reason, string version, Guid operation, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/customer/orders/{id}/cancel") { Content = JsonContent.Create(new { Reason = reason, ExpectedVersion = version }) };
        request.Headers.Authorization = new("Bearer", Token);
        request.Headers.Add("Idempotency-Key", operation.ToString());
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new CafeApiException("Không gửi được yêu cầu hủy. Tải lại đơn để kiểm tra trạng thái trước khi thử lại.", response.StatusCode);
    }
}
