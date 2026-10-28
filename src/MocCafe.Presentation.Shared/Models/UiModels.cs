namespace MocCafe.Presentation.Shared.Models;

// Presentation-only records. They are not domain entities or database mappings.
public sealed record ProductPreview(string Id, string Name, string Category, decimal Price, int ImageIndex, bool Available = true, string Badge = "");
public sealed record ScreenDefinition(string Code, string Name, string Route, string Role, string Purpose);
public sealed record PreviewTable(string[] Columns, string[][] Rows);
public static class MoneyDisplay
{
    public static string Format(decimal value) => value.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + "đ";
}
