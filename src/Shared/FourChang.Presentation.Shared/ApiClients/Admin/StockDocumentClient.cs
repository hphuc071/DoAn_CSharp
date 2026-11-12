using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Presentation.Shared.ApiClients;

public sealed partial class CafeApiClient
{
    /// <summary>Đọc phiếu và dòng đã chốt; giao diện không truy cập SQL trực tiếp.</summary>
    public Task<IReadOnlyList<StockDocumentView>> GetStockDocumentsAsync() => SendAsync<IReadOnlyList<StockDocumentView>>(HttpMethod.Get, "/api/admin/stock-documents");
    /// <summary>Lưu nháp theo ID và mã thao tác ổn định để phục hồi khi timeout.</summary>
    public Task<AdminSaveReceipt> SaveStockDocumentAsync(long? id, StockDocumentInput input) => SendAsync<AdminSaveReceipt>(id is null ? HttpMethod.Post : HttpMethod.Put, "/api/admin/stock-documents" + (id is null ? "" : "/" + id), input);
    /// <summary>Ghi sổ hoặc đảo; mọi điều kiện tồn/quyền/version do máy chủ quyết định.</summary>
    public Task<AdminSaveReceipt> ActStockDocumentAsync(long id, string action, StockDocumentAction input) => SendAsync<AdminSaveReceipt>(HttpMethod.Post, $"/api/admin/stock-documents/{id}/{action}", input);
    /// <summary>Xóa nháp; không cung cấp thao tác sửa hoặc xóa phiếu POSTED.</summary>
    public Task<AdminSaveReceipt> DeleteStockDocumentAsync(long id, AdminDeleteInput input) => SendAsync<AdminSaveReceipt>(HttpMethod.Post, $"/api/admin/stock-documents/{id}/delete", input);
    /// <summary>Tra kết quả cũ sau mất phản hồi, không gửi một mã thao tác mới theo suy đoán.</summary>
    public Task<AdminSaveReceipt> StockDocumentReceiptAsync(Guid operation) => SendAsync<AdminSaveReceipt>(HttpMethod.Get, $"/api/admin/stock-documents/operations/{operation}");
}
