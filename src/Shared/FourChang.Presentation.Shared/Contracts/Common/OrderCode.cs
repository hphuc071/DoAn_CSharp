using System.Globalization;

namespace FourChang.Presentation.Shared.Contracts.Common;

/// <summary>Một quy ước mã đơn cho Web/POS/API: dùng khóa có sẵn, không đếm MAX+1 hoặc thêm số 0 đệm.</summary>
public static class OrderCode
{
    /// <summary>Định dạng mã công khai sau khi đã có ID; ID tăng chung nên không cam kết số liên tục riêng từng kênh.</summary>
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
