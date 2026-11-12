namespace FourChang.Presentation.Shared.Contracts.Admin;

/// <summary>Hai đơn vị cố định dùng chung; mã SQL vẫn đọc từ API, không gán cứng ID của từng máy.</summary>
public static class IngredientUnits
{
    public const string Gram = "g";
    public const string Milliliter = "ml";

    /// <summary>Nhận biết đơn vị được hỗ trợ, không chấp nhận kg/lít hoặc đơn vị tùy ý.</summary>
    public static bool IsSupported(string? symbol) => symbol is not null &&
        (symbol.Equals(Gram, StringComparison.OrdinalIgnoreCase) || symbol.Equals(Milliliter, StringComparison.OrdinalIgnoreCase));

    /// <summary>Nhãn tiếng Việt thống nhất trong form nguyên liệu, BOM và bảng danh sách.</summary>
    public static string Label(string symbol) => symbol.Equals(Gram, StringComparison.OrdinalIgnoreCase) ? "Gam (g)" :
        symbol.Equals(Milliliter, StringComparison.OrdinalIgnoreCase) ? "Mililit (ml)" : throw new ArgumentException("Đơn vị không được hỗ trợ.", nameof(symbol));
}
