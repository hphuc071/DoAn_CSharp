using MocCafe.Presentation.Shared.Models;
namespace MocCafe.Desktop.Data;

// Empty presentation schemas. Web fixtures are intentionally independent.
public static class DesktopData
{
    public static PreviewTable Orders => new(["Mã đơn","Nguồn / nhận","Khách / bàn","Thanh toán","Tiến trình","Giá trị"],[]);
    public static PreviewTable Customers => new(["Mã khách","Họ tên","Email","Điện thoại","Trạng thái","Số đơn"],[]);
    public static PreviewTable Ingredients => new(["Mã","Nguyên liệu","Đơn vị","Quy đổi","An toàn","Cảnh báo","Trạng thái"],[]);
    public static PreviewTable Inventory => new(["Nguyên liệu","ĐVT","Thực tế","Giữ chỗ","An toàn","Khả dụng","Cảnh báo"],[]);
    public static PreviewTable Invoices => new(["Số hóa đơn","Đơn","Ngày","Phương thức","Giá trị","Chứng từ"],[]);
    public static PreviewTable Refunds => new(["Hồ sơ","Đơn","Thu đã xác minh","Còn phải hoàn","Lý do","Trạng thái"],[]);
    public static PreviewTable Audit => new(["Thời gian","Người / ca / máy","Thao tác","Đối tượng","Kết quả"],[]);
    public static PreviewTable StockDocuments => new(["Mã phiếu","Loại","Thời điểm","Nguồn / lý do","Người lập","Trạng thái"],[]);
}
