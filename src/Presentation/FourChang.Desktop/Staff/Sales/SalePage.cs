// UC: UC-POS-02
// Chức năng: Chuẩn bị màn hình bán hàng của nhân viên.
// Cần nhớ: Đọc hàm từ bước kiểm tra dữ liệu đến bước trả kết quả; các quyết định nghiệp vụ do Backend kiểm tra.

namespace FourChang.Desktop.Staff.Sales;
/// <summary>Điểm vào tương thích router hiện có; phần bán hàng đặt trong PosSalesPage và editor riêng.</summary>
public static class SalePage
{
    /// <summary>Mở màn hình nhiều món, cấu hình chuẩn và thu ngân API theo ca hiện tại.</summary>
    public static Control Create() => new PosSalesPage();
}
