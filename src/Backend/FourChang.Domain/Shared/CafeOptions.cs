namespace FourChang.Domain.Shared;

/// <summary>Quy tắc cố định cho một quán; không tạo bảng danh mục/đơn vị/lựa chọn trùng lặp.</summary>
public static class CafeOptions
{
    public static readonly (long Id, string Name)[] Categories = [(1, "Cà phê"), (2, "Trà"), (3, "Đá xay")];
    public static readonly int[] Percentages = [0, 25, 50, 75, 100];

    /// <summary>Đổi mã lựa chọn của client thành loại món hợp lệ để lưu trực tiếp.</summary>
    public static string Category(long id) => Categories.SingleOrDefault(x => x.Id == id).Name
        ?? throw new ArgumentException("Loại món không hợp lệ.");

    /// <summary>Mã tương thích DTO; không phải khóa ngoại đến một bảng danh mục.</summary>
    public static long CategoryId(string name) => Categories.SingleOrDefault(x => x.Name == name).Id;

    /// <summary>Chỉ nhận Gam và Mililit, không nhận đơn vị quy đổi hoặc mã tự sinh.</summary>
    public static string Unit(long id) => id switch
    {
        1 => "g",
        2 => "ml",
        _ => throw new ArgumentException("Chỉ hỗ trợ Gam hoặc Mililit.")
    };

    /// <summary>Đổi ký hiệu lưu trong database sang lựa chọn cố định của giao diện.</summary>
    public static long UnitId(string unit) => unit switch { "g" => 1, "ml" => 2, _ => 0 };

    /// <summary>Kiểm tại server: nóng không có đá, lạnh chọn mức đường/đá trong danh sách chung.</summary>
    public static bool ValidBrew(string temperature, int sugar, int ice) =>
        Percentages.Contains(sugar) && Percentages.Contains(ice) &&
        (temperature == "ICED" || temperature == "HOT" && ice == 0);

    /// <summary>Sinh lựa chọn để Web/POS đọc cùng chính sách, không sao chép thành các dòng SQL.</summary>
    public static IEnumerable<(int Sugar, int Ice)> BrewChoices(string temperature) =>
        from sugar in Percentages
        from ice in (temperature == "HOT" ? new[] { 0 } : Percentages)
        select (sugar, ice);
}
