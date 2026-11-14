namespace FourChang.Desktop.Staff.Invoices;

/// <summary>Thu ngân chi tiền mặt ở màn hình riêng, không trộn nút chi tiền với thao tác gửi yêu cầu hủy.</summary>
public sealed class StaffCounterRefundPage : FourChang.Desktop.Shared.Orders.OrdersWorkspacePage
{
    /// <summary>Chỉ liệt kê hồ sơ tiền mặt đã được Quản lý duyệt và lịch sử đã chi, không tự cấp quyền duyệt.</summary>
    public StaffCounterRefundPage() : base(true, true, true) { }
}
