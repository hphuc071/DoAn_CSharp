using System.Net;
using System.Net.Http.Headers;

namespace FourChang.Presentation.Shared.ApiClients;

public sealed partial class CafeApiClient
{
    public async Task<byte[]> GetCustomerInvoicePdfAsync(long orderId, long invoiceId, CancellationToken ct)
    {
        if (orderId <= 0 || invoiceId <= 0) throw new ArgumentOutOfRangeException(nameof(orderId));
        if (string.IsNullOrWhiteSpace(Token)) throw new CafeApiException("Cần đăng nhập.", HttpStatusCode.Unauthorized);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/customer/orders/{orderId}/invoices/{invoiceId}/pdf");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, budget.Token);
            if (response.StatusCode != HttpStatusCode.OK)
                throw new CafeApiException("Chưa tải được PDF đã phát hành. Không tạo hóa đơn mới.", response.StatusCode);
            if (response.Content.Headers.ContentType?.MediaType != "application/pdf" ||
                response.Content.Headers.ContentLength > 10485760)
                throw new CafeApiException("Nguồn PDF không hợp lệ.", HttpStatusCode.BadGateway);
            using var output = new MemoryStream();
            await using var stream = await response.Content.ReadAsStreamAsync(budget.Token);
            var buffer = new byte[16384];
            int count;
            while ((count = await stream.ReadAsync(buffer, budget.Token)) != 0)
            {
                if (output.Length + count > 10485760) throw new CafeApiException("PDF vượt giới hạn.", HttpStatusCode.BadGateway);
                output.Write(buffer, 0, count);
            }
            var bytes = output.ToArray();
            if (bytes.Length < 17 || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8) ||
                !System.Text.Encoding.ASCII.GetString(bytes.AsSpan(Math.Max(0, bytes.Length - 1024))).TrimEnd().EndsWith("%%EOF", StringComparison.Ordinal))
                throw new CafeApiException("Dữ liệu PDF không hợp lệ.", HttpStatusCode.BadGateway);
            return bytes;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        { throw new CafeApiException("Không kết nối được nguồn PDF.", HttpStatusCode.ServiceUnavailable); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new CafeApiException("Nguồn PDF phản hồi quá lâu.", HttpStatusCode.GatewayTimeout); }
    }
}
