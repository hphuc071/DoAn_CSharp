namespace FourChang.Desktop.Staff.Orders;

/// <summary>Nhân viên nhận đơn và cập nhật tiến trình bằng phiên ca, dùng chung layout đơn hàng.</summary>
public sealed class StaffOrdersPage : FourChang.Desktop.Shared.Orders.OrdersWorkspacePage
{
    /// <summary>Chỉ bật các thao tác Staff; giữ nguyên quyền Quản lý trên các ngoại lệ.</summary>
    public StaffOrdersPage() : base(true) { }
}
