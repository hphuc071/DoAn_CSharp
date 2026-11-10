using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace FourChang.Presentation.Shared.ApiClients;

public sealed record CustomerOrderCancelReceipt(
    long MaDonHang,
    string TrangThai,
    decimal SoTienDeNghiHoan,
    string ThongBao);

public sealed partial class CafeApiClient
{
    public async Task<CustomerOrderCancelReceipt> CancelCustomerOrderAsync(
        long orderId,
        string reason,
        string expectedVersion,
        Guid operationId,
        CancellationToken ct = default)
    {
        if (orderId <= 0)
            throw new ArgumentOutOfRangeException(nameof(orderId));

        if (operationId == Guid.Empty)
            throw new ArgumentException(
                "Mã thao tác không hợp lệ.", nameof(operationId));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Thiếu lý do hủy.", nameof(reason));

        var normalizedReason = reason.Trim();

        if (normalizedReason.Length is < 3 or > 400 ||
            normalizedReason.Any(char.IsControl))
            throw new ArgumentException(
                "Lý do phải từ 3 đến 400 ký tự, không chứa ký tự điều khiển.",
                nameof(reason));

        if (string.IsNullOrWhiteSpace(expectedVersion))
            throw new ArgumentException(
                "Thiếu phiên bản đơn hàng.", nameof(expectedVersion));

        byte[] version;
        try
        {
            version = Convert.FromBase64String(expectedVersion);
        }
        catch (FormatException)
        {
            throw new ArgumentException(
                "Phiên bản đơn hàng không hợp lệ.", nameof(expectedVersion));
        }

        if (version.Length != 8)
            throw new ArgumentException(
                "Phiên bản đơn hàng phải là SQL rowversion 8 byte.",
                nameof(expectedVersion));

        if (string.IsNullOrWhiteSpace(Token))
            throw new CafeApiException(
                "Cần đăng nhập.", HttpStatusCode.Unauthorized);

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/customer/orders/{orderId}/cancel");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", Token);

            request.Headers.Add(
                "Idempotency-Key",
                operationId.ToString("D"));

            request.Headers.CacheControl =
                new CacheControlHeaderValue { NoStore = true };

            request.Content = JsonContent.Create(new
            {
                reason = normalizedReason,
                expectedVersion
            });

            using var response = await http.SendAsync(request, ct);

            JsonElement payload;
            try
            {
                payload = await response.Content
                    .ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            }
            catch (Exception ex)
                when (ex is JsonException or NotSupportedException)
            {
                throw new CafeApiException(
                    "Backend trả phản hồi không hợp lệ.",
                    response.IsSuccessStatusCode
                        ? HttpStatusCode.BadGateway
                        : response.StatusCode);
            }

            static string? ReadField(JsonElement json, string field)
            {
                return json.ValueKind == JsonValueKind.Object &&
                       json.TryGetProperty(field, out var value) &&
                       value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var code = ReadField(payload, "code");
                var message = ReadField(payload, "message")
                    ?? ReadField(payload, "detail")
                    ?? "Không thể hủy đơn. Vui lòng tải lại trạng thái.";

                throw new CafeApiException(
                    message, response.StatusCode, code);
            }

            CustomerOrderCancelReceipt? receipt;
            try
            {
                receipt = payload.Deserialize<CustomerOrderCancelReceipt>(
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch (JsonException)
            {
                throw new CafeApiException(
                    "Biên nhận hủy đơn không hợp lệ.",
                    HttpStatusCode.BadGateway);
            }

            if (receipt is null ||
                receipt.MaDonHang != orderId ||
                receipt.TrangThai != "CANCELLED" ||
                receipt.SoTienDeNghiHoan < 0)
            {
                throw new CafeApiException(
                    "Biên nhận hủy đơn không hợp lệ. Hãy tải lại trạng thái.",
                    HttpStatusCode.BadGateway);
            }

            return receipt;
        }
        catch (HttpRequestException)
        {
            throw new CafeApiException(
                "Mất kết nối Backend. Chưa xác định yêu cầu hủy " +
                "đã được ghi nhận hay chưa. Hãy tải lại trạng thái.",
                HttpStatusCode.ServiceUnavailable);
        }
        catch (OperationCanceledException)
            when (!ct.IsCancellationRequested)
        {
            throw new CafeApiException(
                "Backend phản hồi quá lâu. Chưa xác định yêu cầu hủy " +
                "đã được ghi nhận hay chưa. Hãy tải lại trạng thái.",
                HttpStatusCode.GatewayTimeout);
        }
    }
}