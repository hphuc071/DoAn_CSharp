using MocCafe.Desktop.Controls;
using MocCafe.Presentation.Shared.PreviewData;
namespace MocCafe.Desktop.Features.Admin;
public static class CustomersView
{
    public static Control Create()=>ManagementPage.Create("Hồ sơ khách hàng",DemoCatalog.Customers,"Chỉ tài khoản Customer. Có lịch sử thì ngừng/khóa; không xóa mất đơn hoặc nghĩa vụ hoàn. Không có tài khoản nhân viên.",
        ("Thêm khách hàng",["Họ tên","Email hoặc điện thoại","Mật khẩu tạm"]),
        ("Sửa hồ sơ",["Họ tên","Email","Điện thoại"]),
        ("Khóa / Mở",["Trạng thái","Lý do thay đổi"]),
        ("Đặt lại mật khẩu",["Mật khẩu tạm","Lý do cấp lại"]),
        ("Xóa có điều kiện",["Xác nhận chưa có tham chiếu","Lý do xóa"]),
        ("Lịch sử giao dịch",["Mã khách hàng"]));
}
