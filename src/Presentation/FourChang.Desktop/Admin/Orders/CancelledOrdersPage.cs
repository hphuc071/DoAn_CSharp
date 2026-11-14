namespace FourChang.Desktop.Admin.Orders;

/// <summary>Trang con của quản lý đơn, không thêm UC hay thay đổi quyền duyệt/hoàn.</summary>
public sealed class CancelledOrdersPage : FourChang.Desktop.Shared.Orders.OrdersWorkspacePage
{
    /// <summary>Chỉ đọc yêu cầu hủy đang chờ và đơn đã hủy, lọc/phân trang tại API.</summary>
    public CancelledOrdersPage() : base(false, true) { }
}
