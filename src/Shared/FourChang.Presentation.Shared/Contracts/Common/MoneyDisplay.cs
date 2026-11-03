namespace FourChang.Presentation.Shared.Contracts.Common;

public static class MoneyDisplay
{
    public static string Format(decimal value) => value.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + "đ";
}
