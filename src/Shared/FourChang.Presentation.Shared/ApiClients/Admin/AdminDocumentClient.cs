using System.Net.Http.Headers;

namespace FourChang.Presentation.Shared.ApiClients;

public sealed partial class CafeApiClient
{
    /// <summary>Tải PDF hóa đơn đã phát hành, không gửi lệnh tạo hoặc thu tiền.</summary>
    public Task<byte[]> GetAdminInvoicePdfAsync(long id, CancellationToken ct = default) => ReadAdminDocument($"/api/admin/invoices/{id}/pdf", "application/pdf", ct);
    /// <summary>Tải file job sau khi server kiểm tra lại quyền và trạng thái READY.</summary>
    public Task<byte[]> GetReportFileAsync(Guid id, bool pdf, CancellationToken ct = default) => ReadAdminDocument($"/api/admin/reports/exports/{id}/file",
        pdf ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ct);
    /// <summary>Giới hạn dung lượng, xác minh MIME và độ dài; không lưu file nhận dở dưới tên kết quả cuối.</summary>
    private async Task<byte[]> ReadAdminDocument(string path, string mime, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) throw new CafeApiException("Chưa tải được tệp hoàn chỉnh. Kiểm tra quyền và trạng thái rồi thử lại.", response.StatusCode);
            if (response.Content.Headers.ContentType?.MediaType != mime || response.Content.Headers.ContentLength > 100 * 1024 * 1024) throw new CafeApiException("Tệp xuất không đúng định dạng hoặc quá lớn.");
            using var output = new MemoryStream(); await using var stream = await response.Content.ReadAsStreamAsync(ct); var buffer = new byte[16384]; int count;
            while ((count = await stream.ReadAsync(buffer, ct)) > 0)
            { if (output.Length + count > 100 * 1024 * 1024) throw new CafeApiException("Tệp vượt giới hạn tải."); output.Write(buffer, 0, count); }
            if (response.Content.Headers.ContentLength is { } length && length != output.Length) throw new CafeApiException("Tệp chưa nhận đủ dữ liệu.");
            return output.ToArray();
        }
        catch (HttpRequestException) { throw new CafeApiException("Không kết nối được nguồn tệp. Thử lại thao tác tải, không tạo lại nghiệp vụ."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new CafeApiException("Quá thời gian tải tệp. Có thể tải lại cùng kết quả."); }
    }
}
