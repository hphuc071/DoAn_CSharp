// UC: UC-POS-03
// Chức năng: Mở danh sách đơn thường để nhân viên tiếp nhận và xử lý.
// Cần nhớ: Đọc hàm từ bước kiểm tra dữ liệu đến bước trả kết quả; các quyết định nghiệp vụ do Backend kiểm tra.

namespace FourChang.Desktop.Staff.Orders;

/// <summary>Nhân viên nhận đơn và cập nhật tiến trình bằng phiên ca, dùng chung layout đơn hàng.</summary>
public sealed class StaffOrdersPage : FourChang.Desktop.Shared.Orders.OrdersWorkspacePage
{
    /// <summary>Chỉ bật các thao tác Staff; giữ nguyên quyền Quản lý trên các ngoại lệ.</summary>
    public StaffOrdersPage() : base(true) { }
}
