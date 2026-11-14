namespace FourChang.Desktop.Admin.Orders;

/// <summary>Màn hình Quản lý dùng workspace chung và giữ đủ hành động quản trị hiện có.</summary>
public sealed class OrdersManagementPage : Shared.Orders.OrdersWorkspacePage
{
    /// <summary>Bật chế độ Admin; không cấp quyền này cho màn hình POS.</summary>
    public OrdersManagementPage() : base(false) { }
}
