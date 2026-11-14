using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Presentation.Shared.ApiClients;

public sealed partial class CafeApiClient
{
    /// <summary>Tra đơn với bộ lọc và phân trang máy chủ, không chỉ lọc một trang đã tải ở UI.</summary>
    public Task<AdminPage<AdminOrderRow>> GetAdminOrdersAsync(AdminOrderFilter f) => SendAsync<AdminPage<AdminOrderRow>>(HttpMethod.Get,
        "/api/admin/orders?" + EncodeQuery(new() { ["Kenh"] = f.Kenh, ["HinhThuc"] = f.HinhThuc, ["TrangThai"] = f.TrangThai,
            ["TuLuc"] = f.TuLuc?.ToString("O"), ["DenLuc"] = f.DenLuc?.ToString("O"), ["MaCa"] = f.MaCa?.ToString(),
            ["NhomDon"] = f.NhomDon, ["TimKiem"] = f.TimKiem, ["Trang"] = f.Trang.ToString(), ["KichThuocTrang"] = f.KichThuocTrang.ToString() }));
    /// <summary>Đọc cấu hình đã chốt, tiền, giữ chỗ và lịch sử một đơn.</summary>
    public Task<AdminOrderDetail> GetAdminOrderAsync(long id) => SendAsync<AdminOrderDetail>(HttpMethod.Get, $"/api/admin/orders/{id}");
    /// <summary>Gửi cùng lệnh tiến trình mà POS dùng; không cập nhật tiền/trạng thái cục bộ.</summary>
    public Task<OrderActionReceipt> AdminOrderActionAsync(long id, OrderActionInput input) => SendAsync<OrderActionReceipt>(HttpMethod.Post, $"/api/admin/orders/{id}/actions", input);
    /// <summary>Đổi địa chỉ có xác nhận của khách và Admin, giữ phí đã chốt.</summary>
    public Task<OrderActionReceipt> AdminOrderAddressAsync(long id, OrderAddressInput input) => SendAsync<OrderActionReceipt>(HttpMethod.Post, $"/api/admin/orders/{id}/address", input);
    /// <summary>Tra biên nhận đơn sau timeout trước gửi lại cùng payload.</summary>
    public Task<OrderActionReceipt> AdminOrderReceiptAsync(Guid id) => SendAsync<OrderActionReceipt>(HttpMethod.Get, $"/api/admin/orders/operations/{id}");
    /// <summary>Tra hóa đơn phân trang theo số/đơn/người mua/ngày/ca.</summary>
    public Task<AdminPage<AdminInvoiceView>> GetAdminInvoicesAsync(string? search = null, DateTime? from = null, DateTime? to = null, long? shift = null, int page = 1) =>
        SendAsync<AdminPage<AdminInvoiceView>>(HttpMethod.Get, "/api/admin/invoices?" + EncodeQuery(new() { ["search"] = search, ["from"] = from?.ToString("O"), ["to"] = to?.ToString("O"), ["shift"] = shift?.ToString(), ["page"] = page.ToString() }));
    /// <summary>Đọc snapshot phát hành và các chứng từ điều chỉnh liên quan.</summary>
    public Task<AdminInvoiceDetail> GetAdminInvoiceAsync(long id) => SendAsync<AdminInvoiceDetail>(HttpMethod.Get, $"/api/admin/invoices/{id}");
    /// <summary>Phát hành điều chỉnh một nguồn hợp lệ; không thực hiện thu hoặc hoàn.</summary>
    public Task<AdminSaveReceipt> AdjustInvoiceAsync(long id, InvoiceAdjustmentInput input) => SendAsync<AdminSaveReceipt>(HttpMethod.Post, $"/api/admin/invoices/{id}/adjustments", input);
    /// <summary>Tra kết quả phát hành điều chỉnh khi mất phản hồi.</summary>
    public Task<AdminSaveReceipt> InvoiceReceiptAsync(Guid id) => SendAsync<AdminSaveReceipt>(HttpMethod.Get, $"/api/admin/invoices/operations/{id}");
    /// <summary>Ghi lịch sử xem trước/in/in lại/xuất, không tạo hóa đơn.</summary>
    public Task<DocumentLogReceipt> LogInvoiceDocumentAsync(long id, DocumentLogInput input) => SendAsync<DocumentLogReceipt>(HttpMethod.Post, $"/api/admin/invoices/{id}/documents/log", input);
    /// <summary>Đọc các hồ sơ hoàn đã che tài khoản ngân hàng.</summary>
    public Task<IReadOnlyList<RefundView>> GetAdminRefundsAsync() => SendAsync<IReadOnlyList<RefundView>>(HttpMethod.Get, "/api/admin/refunds");
    /// <summary>Đọc tiền gốc, hạn mức còn lại và lịch sử duyệt/chi.</summary>
    public Task<RefundDetail> GetAdminRefundAsync(long id) => SendAsync<RefundDetail>(HttpMethod.Get, $"/api/admin/refunds/{id}");
    /// <summary>Lập hoặc bổ sung hồ sơ; số tài khoản chỉ gửi qua kênh được cấu hình và không ghi log phía UI.</summary>
    public Task<AdminSaveReceipt> SaveRefundAsync(long? id, RefundRequestInput input) => SendAsync<AdminSaveReceipt>(id is null ? HttpMethod.Post : HttpMethod.Put, "/api/admin/refunds" + (id is null ? "" : "/" + id), input);
    /// <summary>Duyệt/từ chối hồ sơ theo hạn mức do server kiểm tra.</summary>
    public Task<AdminSaveReceipt> DecideRefundAsync(long id, RefundDecisionInput input) => SendAsync<AdminSaveReceipt>(HttpMethod.Post, $"/api/admin/refunds/{id}/decision", input);
    /// <summary>Ghi nhận lần chi đang xử lý; chưa coi là đã hoàn.</summary>
    public Task<AdminSaveReceipt> BeginRefundAsync(long id, RefundAttemptInput input) => SendAsync<AdminSaveReceipt>(HttpMethod.Post, $"/api/admin/refunds/{id}/attempts", input);
    /// <summary>Xác minh thủ công ở bước riêng, không giả callback ngân hàng.</summary>
    public Task<AdminSaveReceipt> VerifyRefundAsync(long attempt, RefundVerificationInput input) => SendAsync<AdminSaveReceipt>(HttpMethod.Post, $"/api/admin/refunds/attempts/{attempt}/verify", input);
    /// <summary>Tra kết quả hồ sơ/lần chi bằng mã thao tác ổn định.</summary>
    public Task<AdminSaveReceipt> RefundReceiptAsync(Guid operation) => SendAsync<AdminSaveReceipt>(HttpMethod.Get, $"/api/admin/refunds/operations/{operation}");
    /// <summary>Đọc khoản thu cần ghép sao kê để lấy đúng rowversion, không tự nhập phiên bản.</summary>
    public Task<IncomingPaymentView> GetIncomingPaymentAsync(long id) => SendAsync<IncomingPaymentView>(HttpMethod.Get, $"/api/admin/refunds/incoming/{id}");
    /// <summary>Admin đối chiếu tiền vào thủ công, một giao dịch chỉ ghép một khoản thu.</summary>
    public Task<AdminSaveReceipt> ReconcileIncomingAsync(IncomingReconciliationInput input) => SendAsync<AdminSaveReceipt>(HttpMethod.Post, "/api/admin/refunds/incoming/reconcile", input);
    /// <summary>Lấy báo cáo thực tế có snapshot và định nghĩa chỉ tiêu.</summary>
    public Task<ReportSnapshot> GetReportAsync(ReportFilter input) => SendAsync<ReportSnapshot>(HttpMethod.Post, "/api/admin/reports/snapshot", input);
    /// <summary>Tra nhật ký có quyền riêng và che trường bí mật ở server.</summary>
    public Task<AdminPage<AdminTraceRow>> GetAuditAsync(AuditFilter f) => SendAsync<AdminPage<AdminTraceRow>>(HttpMethod.Get,
        "/api/admin/reports/audit?" + EncodeQuery(new() { ["TuLuc"] = f.TuLuc?.ToString("O"), ["DenLuc"] = f.DenLuc?.ToString("O"),
            ["HanhDong"] = f.HanhDong, ["LoaiDoiTuong"] = f.LoaiDoiTuong, ["MaDoiTuong"] = f.MaDoiTuong, ["Nguoi"] = f.Nguoi,
            ["MaCa"] = f.MaCa?.ToString(), ["MaMay"] = f.MaMay, ["CorrelationId"] = f.CorrelationId?.ToString(), ["Trang"] = f.Trang.ToString() }));
    /// <summary>Tạo tác vụ xuất từ mốc đã xem, không tự lấy số liệu mới.</summary>
    public Task<ReportExportJob> StartReportExportAsync(ReportExportInput input) => SendAsync<ReportExportJob>(HttpMethod.Post, "/api/admin/reports/exports", input);
    /// <summary>Tra job đã đăng ký sau timeout bằng đúng mã thao tác cũ.</summary>
    public Task<ReportExportJob> ReportExportReceiptAsync(Guid operation) => SendAsync<ReportExportJob>(HttpMethod.Get, $"/api/admin/reports/exports/operations/{operation}");
    /// <summary>Chụp nhật ký đã che theo toàn bộ bộ lọc và mốc, kiểm tra quyền xuất audit riêng.</summary>
    public Task<ReportSnapshot> GetAuditExportSnapshotAsync(AuditFilter input) => SendAsync<ReportSnapshot>(HttpMethod.Post, "/api/admin/reports/audit/snapshot", input);
    /// <summary>Đọc lại job đã tạo để nối tiếp sau đóng cửa sổ hoặc khởi động lại.</summary>
    public Task<IReadOnlyList<ReportExportJob>> GetReportExportsAsync() => SendAsync<IReadOnlyList<ReportExportJob>>(HttpMethod.Get, "/api/admin/reports/exports");
    /// <summary>Đọc tiến độ thật do worker cập nhật.</summary>
    public Task<ReportExportJob> ReportExportStatusAsync(Guid id) => SendAsync<ReportExportJob>(HttpMethod.Get, $"/api/admin/reports/exports/{id}");
    /// <summary>Hủy job đang đợi/chạy; không báo hủy lưu file là xuất thành công.</summary>
    public Task<ReportExportJob> CancelReportExportAsync(Guid id) => SendAsync<ReportExportJob>(HttpMethod.Post, $"/api/admin/reports/exports/{id}/cancel", new { });
    /// <summary>Encode từng giá trị, tránh ký tự tiếng Việt hoặc & làm thay đổi bộ lọc API.</summary>
    private static string EncodeQuery(Dictionary<string, string?> values) => string.Join("&", values.Where(x => !string.IsNullOrWhiteSpace(x.Value)).Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value!)));
}
