using System.Globalization;

namespace FourChang.Presentation.Shared.Contracts.Common;

/// <summary>Định dạng và kiểm tra mã công khai. Số hiển thị độc lập với khóa database; API đọc MaDonHienThi đã lưu.</summary>
public static class OrderCode
{
    /// <summary>Định dạng một số hiển thị đã cấp; không dùng hàm này để suy mã công khai từ khóa database.</summary>
    public static string Format(long id, string channel)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        if (channel is not ("WEB" or "POS")) throw new ArgumentException("Kênh đơn không hợp lệ.", nameof(channel));
        return channel + "-" + id.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Tách mã tìm kiếm như WEB-125 thành kênh/ID; không suy danh tính từ đoạn GUID cũ hoặc chuỗi gần giống.</summary>
    public static bool TryParse(string? input, out string channel, out long id)
    {
        channel = ""; id = 0;
        var text = input?.Trim().ToUpperInvariant() ?? "";
        var split = text.IndexOf('-');
        if (split < 0 || text[..split] is not ("WEB" or "POS") ||
            !long.TryParse(text[(split + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out id) || id <= 0) return false;
        channel = text[..split]; return true;
    }
}
