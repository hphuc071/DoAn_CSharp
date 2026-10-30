using FourChang.Presentation.Shared.Models;
namespace FourChang.Presentation.Shared.Catalog;

public static class ScreenCatalog
{
    public static IReadOnlyList<ScreenDefinition> All { get; } = [
        new("UC-WEB-01", "Đăng ký tài khoản", "/Account/Register", "Customer", "Tạo hồ sơ khách hàng và hiển thị lỗi định danh hoặc mật khẩu."),
        new("UC-WEB-02", "Đăng nhập và đăng xuất", "/Account/Login", "Customer", "Đăng nhập, đăng xuất, tài khoản khóa và mật khẩu tạm."),
        new("UC-WEB-03", "Thông tin cá nhân", "/Account/Profile", "Customer", "Hồ sơ, sổ địa chỉ và đổi mật khẩu."),
        new("UC-WEB-04", "Thực đơn", "/", "Customer", "Banner, tìm kiếm, lọc, món mới/bán chạy và món tạm hết."),
        new("UC-WEB-05", "Giỏ hàng", "/Cart", "Customer", "Tùy chỉnh size, nóng/lạnh, đường/đá, topping và ghi chú."),
        new("UC-WEB-06", "Đặt món", "/Checkout", "Customer", "QR bàn, mang đi hoặc giao hàng có báo phí."),
        new("UC-WEB-07", "Thanh toán VietQR", "/Checkout/Payment", "Customer", "Chờ tiền, đã xác minh, thiếu/thừa tiền, hết hạn và đối soát."),
        new("UC-WEB-08", "Đơn của tôi", "/Orders", "Customer", "Danh sách, tiến trình, hóa đơn, yêu cầu hủy khi chờ xác nhận và theo dõi hoàn."),
        new("UC-POS-01", "Ca làm việc", "shift", "Staff", "Nhập tên mở ca, khóa/tiếp tục, chốt và bàn giao; không tài khoản nhân viên."),
        new("UC-POS-02", "Bán hàng", "sale", "Staff", "Đơn tại bàn/mang đi, cấu hình món và giỏ POS."),
        new("UC-POS-03", "Đơn hàng", "orders", "Staff", "Tiếp nhận/từ chối, pha từng phần, sẵn sàng, bàn giao và sự cố giao."),
        new("UC-POS-04", "Hủy món", "cancel", "Staff", "Chọn số ly chưa pha, lý do bắt buộc và khoản hoàn dự kiến."),
        new("UC-POS-05", "Thu tiền và hóa đơn", "payment", "Staff", "Tiền mặt/VietQR, tiền thừa, hóa đơn, in lại/xuất; Web không thu lại."),
        new("UC-ADM-01", "Khách hàng", "customers", "Admin", "Hồ sơ Customer, khóa/mở, mật khẩu tạm, lịch sử và xóa có điều kiện."),
        new("UC-ADM-02", "Phân quyền", "access", "Admin", "Tài khoản Admin, vai trò, thiết bị POS và quyền theo ca."),
        new("UC-ADM-03", "Thực đơn", "menu", "Admin", "Danh mục, món/size/giá, tùy chọn, topping, banner và bật/tắt bán."),
        new("UC-ADM-04", "Nguyên liệu", "ingredients", "Admin", "Đơn vị cơ sở, quy đổi, ngưỡng cảnh báo và ngừng sử dụng."),
        new("UC-ADM-05", "Công thức BOM", "recipes", "Admin", "Định mức theo biến thể/topping, phiên bản, hiệu lực và mô phỏng."),
        new("UC-ADM-06", "Tồn kho", "inventory", "Admin", "Thực tế, giữ chỗ, an toàn, khả dụng, sổ kho và món bị ảnh hưởng."),
        new("UC-ADM-07", "Chứng từ kho", "stock", "Admin", "Nhập, xuất, kiểm kê/điều chỉnh, nháp, ghi sổ và đảo chứng từ."),
        new("UC-ADM-08", "Đơn hàng", "manage-orders", "Admin", "Đơn đa kênh, địa chỉ, truy vết ca và sự cố sau pha/giao thất bại."),
        new("UC-ADM-09", "Hóa đơn", "invoices", "Admin", "Tra cứu, đối chiếu, điều chỉnh liên kết, in lại và xuất."),
        new("UC-ADM-10", "Hoàn tiền", "refunds", "Admin", "Duyệt, thông tin nhận, chứng cứ chi và đối soát; chưa xác minh không ghi đã hoàn."),
        new("UC-ADM-11", "Báo cáo và nhật ký", "reports", "Admin", "Doanh thu TM/CK, tỷ lệ đơn, top món, cảnh báo kho và audit theo quyền riêng.")
    ];
    public static ScreenDefinition Find(string role, string route) => All.First(s => s.Role == role && s.Route == route);
}
