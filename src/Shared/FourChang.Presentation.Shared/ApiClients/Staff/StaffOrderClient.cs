using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Presentation.Shared.ApiClients;

/// <summary>Client POS dùng hợp đồng đơn chung để không khai báo giá/trạng thái riêng ở giao diện.</summary>
public sealed partial class CafeApiClient
{
    /// <summary>Tra đơn với bộ lọc và phân trang máy chủ, không chỉ lọc một trang đã tải ở UI.</summary>
    public Task<AdminPage<AdminOrderRow>> GetStaffOrdersAsync(AdminOrderFilter f) => SendAsync<AdminPage<AdminOrderRow>>(HttpMethod.Get,
        "/api/pos/orders?" + EncodeQuery(new() { ["Kenh"] = f.Kenh, ["HinhThuc"] = f.HinhThuc, ["TrangThai"] = f.TrangThai,
            ["TuLuc"] = f.TuLuc?.ToString("O"), ["DenLuc"] = f.DenLuc?.ToString("O"), ["MaCa"] = f.MaCa?.ToString(),
            ["NhomDon"] = f.NhomDon, ["PhamViCa"] = f.PhamViCa, ["TimKiem"] = f.TimKiem, ["Trang"] = f.Trang.ToString(), ["KichThuocTrang"] = f.KichThuocTrang.ToString() }));
    /// <summary>Đọc cấu hình đã chốt, tiền, giữ chỗ và lịch sử một đơn.</summary>
    public Task<AdminOrderDetail> GetStaffOrderAsync(long id) => SendAsync<AdminOrderDetail>(HttpMethod.Get, $"/api/pos/orders/{id}");
    /// <summary>Gửi cùng lệnh tiến trình mà POS dùng; không cập nhật tiền/trạng thái cục bộ.</summary>
    public Task<OrderActionReceipt> StaffOrderActionAsync(long id, OrderActionInput input) => SendAsync<OrderActionReceipt>(HttpMethod.Post, $"/api/pos/orders/{id}/actions", input);
    /// <summary>Tra kết quả đúng ca thực hiện khi mất phản hồi; không tra biên nhận của Admin hay ca khác.</summary>
    public Task<OrderActionReceipt> StaffOrderReceiptAsync(Guid id) => SendAsync<OrderActionReceipt>(HttpMethod.Get, $"/api/pos/orders/operations/{id}");
    /// <summary>Tìm hóa đơn trên toàn bộ dữ liệu, cận ngày UTC và phân trang máy chủ.</summary>
    public Task<AdminPage<AdminInvoiceView>> GetStaffInvoicesAsync(string? search, DateTime? from, DateTime? to, int page = 1) =>
        SendAsync<AdminPage<AdminInvoiceView>>(HttpMethod.Get, "/api/pos/invoices?" + EncodeQuery(new() { ["search"] = search, ["from"] = from?.ToString("O"), ["to"] = to?.ToString("O"), ["page"] = page.ToString() }));
    /// <summary>Đọc đúng hóa đơn theo ID, không phụ thuộc giới hạn danh sách bảng thô.</summary>
    public Task<FourChang.Presentation.Shared.Contracts.Staff.StaffInvoiceSnapshot> GetStaffInvoiceAsync(long id) =>
        SendAsync<FourChang.Presentation.Shared.Contracts.Staff.StaffInvoiceSnapshot>(HttpMethod.Get, $"/api/pos/invoices/{id}");
}
