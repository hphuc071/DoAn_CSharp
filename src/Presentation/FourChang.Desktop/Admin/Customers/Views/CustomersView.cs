using FourChang.Desktop.Shared.Preview;
using FourChang.Presentation.Shared.Preview;
namespace FourChang.Desktop.Admin.Customers.Views;

/// <summary>Bản xem trước giữ đúng nhóm chức năng của UC-ADM-01 tinh gọn.</summary>
public static class CustomersView
{
    /// <summary>Dựng mẫu tìm/xem/sửa, khóa/mở và reset; không có tạo/xóa/ngừng khách.</summary>
    public static Control Create() => ManagementPage.Create("Hồ sơ khách hàng", DemoCatalog.Customers,
        "Khách tự đăng ký trên Website. Quản lý cập nhật hồ sơ, khóa/mở và cấp lại mật khẩu; giữ lịch sử.",
        ("Xem hồ sơ", ["Mã khách hàng"]),
        ("Sửa hồ sơ", ["Họ tên", "Email", "Điện thoại"]),
        ("Khóa / mở tài khoản", ["Trạng thái"]),
        ("Đặt lại mật khẩu", ["Mật khẩu tạm", "Xác nhận mật khẩu"]));
}
