// UC: UC-POS-05
// Chức năng: Chọn hồ sơ hoàn đã duyệt và ghi nhận lần trả tiền mặt cho khách.
// Cần nhớ: Chỉ ghi nhận đã hoàn khi việc chi trả được xác nhận thành công.

namespace FourChang.Desktop.Staff.Invoices;

/// <summary>Thu ngân chi tiền mặt ở màn hình riêng, không trộn nút chi tiền với thao tác gửi yêu cầu hủy.</summary>
public sealed class StaffCounterRefundPage : FourChang.Desktop.Shared.Orders.OrdersWorkspacePage
{
    /// <summary>Chỉ liệt kê hồ sơ tiền mặt đã được Quản lý duyệt và lịch sử đã chi, không tự cấp quyền duyệt.</summary>
    public StaffCounterRefundPage() : base(true, true, true) { }
}
