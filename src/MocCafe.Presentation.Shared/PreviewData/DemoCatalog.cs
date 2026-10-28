using MocCafe.Presentation.Shared.Models;
namespace MocCafe.Presentation.Shared.PreviewData;

// Fixed, in-memory design fixtures. No persistence, inventory deduction or real payments.
public static class DemoCatalog
{
    public static IReadOnlyList<ProductPreview> Products { get; } = [
        new("coffee-milk", "Cà phê sữa", "Cà phê", 35000, 0, true, "Bán chạy"),
        new("bac-xiu", "Bạc xỉu", "Cà phê", 39000, 1),
        new("latte", "Latte", "Cà phê", 45000, 2, true, "Món mới"),
        new("peach-tea", "Trà đào", "Trà", 42000, 3),
        new("matcha", "Matcha latte", "Trà", 49000, 4),
        new("cold-brew", "Cold brew", "Cà phê", 45000, 5, false, "Tạm hết")
    ];
    public static PreviewTable Orders => new(["Mã đơn", "Nguồn / nhận", "Khách / bàn", "Thanh toán", "Tiến trình", "Giá trị"], [
        ["WEB024", "Web / Giao hàng", "Nguyễn Minh", "Đã xác minh đủ", "Chờ xác nhận", "70.000đ"],
        ["POS018", "POS / Mang đi", "Khách lẻ", "Chưa thu", "Đã xác nhận", "84.000đ"],
        ["WEB025", "Web / Mang đi", "Thanh An", "Đã xác minh đủ", "Đang pha chế", "42.000đ"],
        ["WEB026", "Web / Tại bàn", "Bàn 03", "Đã xác minh đủ", "Sẵn sàng", "49.000đ"],
        ["WEB021", "Web / Giao hàng", "Hoàng Lan", "Đã xác minh đủ", "Đang giao", "99.000đ"],
        ["WEB019", "Web / Mang đi", "Ngọc Mai", "Chờ hoàn 45.000đ", "Đã hủy", "45.000đ"]]);
    public static PreviewTable Customers => new(["Mã khách", "Họ tên", "Email", "Điện thoại", "Trạng thái", "Số đơn"], [
        ["KH001", "Nguyễn Minh", "minh@example.com", "0900000001", "Hoạt động", "12"],
        ["KH002", "Trần Ngọc Mai", "mai@example.com", "0900000002", "Hoạt động", "8"],
        ["KH003", "Lê Thanh An", "an@example.com", "0900000003", "Tạm khóa", "4"]]);
    public static PreviewTable Ingredients => new(["Mã", "Nguyên liệu", "Đơn vị", "Quy đổi", "An toàn", "Cảnh báo", "Trạng thái"], [
        ["NL001", "Sữa tươi", "ml", "1 lít = 1.000 ml", "1.000", "5.000", "Đang dùng"],
        ["NL002", "Hạt cà phê", "g", "1 kg = 1.000 g", "500", "2.000", "Đang dùng"],
        ["NL003", "Siro đào", "ml", "1 chai = 750 ml", "200", "1.000", "Đang dùng"],
        ["NL004", "Bột matcha", "g", "1 hộp = 500 g", "100", "500", "Đang dùng"]]);
    public static PreviewTable Inventory => new(["Nguyên liệu", "ĐVT", "Thực tế", "Giữ chỗ", "An toàn", "Khả dụng", "Cảnh báo"], [
        ["Sữa tươi", "ml", "4.500", "1.000", "1.000", "2.500", "Sắp hết"],
        ["Hạt cà phê", "g", "6.000", "800", "500", "4.700", "Bình thường"],
        ["Siro đào", "ml", "1.800", "300", "200", "1.300", "Bình thường"],
        ["Cold brew nền", "ml", "280", "180", "100", "0", "Hết khả dụng"]]);
    public static PreviewTable Invoices => new(["Số hóa đơn", "Đơn", "Ngày", "Phương thức", "Giá trị", "Chứng từ"], [
        ["HD000128", "POS017", "28/10/2026 09:40", "Tiền mặt", "84.000đ", "Bản gốc"],
        ["HD000127", "WEB020", "28/10/2026 09:15", "Chuyển khoản", "70.000đ", "Bản gốc"],
        ["DC000014", "WEB019", "28/10/2026 09:10", "Chuyển khoản", "−45.000đ", "Điều chỉnh HD000126"]]);
    public static PreviewTable Refunds => new(["Hồ sơ", "Đơn", "Thu đã xác minh", "Còn phải hoàn", "Lý do", "Trạng thái"], [
        ["HT0008", "WEB019", "45.000đ", "45.000đ", "Khách hủy khi chờ xác nhận", "REQUESTED"],
        ["HT0007", "WEB016", "70.000đ", "35.000đ", "Hủy một món chưa pha", "APPROVED"],
        ["HT0006", "WEB010", "55.000đ", "10.000đ", "Chuyển khoản thừa", "RECONCILING"]]);
    public static PreviewTable Audit => new(["Thời gian", "Người / ca / máy", "Thao tác", "Đối tượng", "Kết quả"], [
        ["10:24", "Admin / ADM01", "Điều chỉnh tồn kho", "KK0004", "Thành công"],
        ["10:18", "Lan / CA024 / POS01", "Hủy món trước pha", "WEB019", "Chờ hoàn"],
        ["10:05", "Admin / ADM01", "Xác nhận chi hoàn", "HT0005", "SUCCEEDED"],
        ["09:58", "Dịch vụ đối soát", "Xác minh khoản thu", "WEB024", "PAID"]]);
    public static PreviewTable StockDocuments => new(["Mã phiếu", "Loại", "Thời điểm", "Nguồn / lý do", "Người lập", "Trạng thái"], [
        ["PN0009", "Nhập kho", "28/10/2026 08:00", "Nhà cung cấp A", "Admin", "POSTED"],
        ["PX0003", "Xuất kho", "28/10/2026 09:00", "Hư hỏng", "Admin", "DRAFT"],
        ["KK0004", "Kiểm kê", "28/10/2026 10:00", "Kiểm kê cuối ca", "Admin", "DRAFT"]]);
}

