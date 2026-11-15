using FourChang.Desktop.Shared.Controls;
namespace FourChang.Desktop.Admin.Recipes;
/// <summary>Preview công thức thuộc thực đơn tinh gọn, không giả lập phiên bản/lịch/mô phỏng.</summary>
public static class RecipesView
{
    /// <summary>Hướng dẫn mở màn hình thật để đọc và lưu công thức từ API.</summary>
    public static Control Create() => Ui.Panel("Công thức pha chế", Ui.Label("Mở Thực đơn và công thức để thêm hoặc sửa định mức nguyên liệu.", 10));
}
