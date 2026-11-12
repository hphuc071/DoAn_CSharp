using FourChang.Desktop.Shared.Controls;
namespace FourChang.Desktop.Admin.Menu;
/// <summary>Khung xem bố cục offline; dữ liệu/giá thật chỉ có ở MenuManagementPage sau đăng nhập.</summary>
public static class MenuManagementView
{
    /// <summary>Không tự khai báo giá size/topping thứ hai trong preview; giữ nhóm tab để xem bố cục.</summary>
    public static Control Create() => Ui.Tabs(("Món", Notice("Danh sách món")), ("Cấu hình món", Notice("Giá và tùy chọn")), ("Danh mục", Notice("Danh mục thực đơn")), ("Topping", Notice("Topping")), ("Banner", Notice("Nội dung công bố")));
    /// <summary>Preview không giả lập thao tác CRUD hoặc thay đổi catalog khi API chưa kết nối.</summary>
    private static Control Notice(string title) => Ui.Panel(title, Ui.Label("Kết nối backend và đăng nhập Quản lý để xem dữ liệu thực đơn.", 10));
}
