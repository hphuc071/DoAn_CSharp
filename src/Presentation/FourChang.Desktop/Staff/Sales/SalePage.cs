namespace FourChang.Desktop.Staff.Sales;
/// <summary>Điểm vào tương thích router hiện có; phần bán hàng đặt trong PosSalesPage và editor riêng.</summary>
public static class SalePage
{
    /// <summary>Mở màn hình nhiều món, cấu hình chuẩn và thu ngân API theo ca hiện tại.</summary>
    public static Control Create() => new PosSalesPage();
}
