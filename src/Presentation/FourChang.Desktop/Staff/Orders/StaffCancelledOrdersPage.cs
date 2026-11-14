namespace FourChang.Desktop.Staff.Orders;

/// <summary>Danh sách hủy riêng của Staff: chuyển yêu cầu và chi tiền mặt đã duyệt.</summary>
public sealed class StaffCancelledOrdersPage : FourChang.Desktop.Shared.Orders.OrdersWorkspacePage
{
    /// <summary>Giữ phiên ca và quyền Staff; không tạo quyền Quản lý khi chuyển màn hình.</summary>
    public StaffCancelledOrdersPage() : base(true, true) { }
}
