using FourChang.Api.Shared.WorkSessions;
namespace FourChang.Api.Features.Admin.Menu;

/// <summary>Quy tắc trường ADM-03: chặn dữ liệu sai trước khi SQL ghi; không chuẩn hóa giá ở UI.</summary>
internal static class MenuValidation
{
    /// <summary>Bắt buộc chuỗi có nghĩa và đúng giới hạn cột; không dùng tên làm khóa nhận diện.</summary>
    internal static string Text(string? text, int max, string label)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > max)
            throw new WorkforceRule(400, label + " không hợp lệ.");
        return text.Trim();
    }
    /// <summary>Chuỗi tùy chọn rỗng được lưu NULL, vượt giới hạn bị từ chối thay vì cắt dữ liệu.</summary>
    internal static string? Optional(string? text, int max)
    {
        if (text?.Length > max)
            throw new WorkforceRule(400, "Nội dung quá dài.");
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
    /// <summary>Giá VND nguyên, không âm và nằm trong decimal(18,0); không âm thầm làm tròn.</summary>
    internal static void Price(decimal price)
    {
        if (price < 0 || price > 999999999999999999m || decimal.Truncate(price) != price)
            throw new WorkforceRule(400, "Giá phải là số VND nguyên không âm.");
    }
    /// <summary>Chấp nhận tài nguyên có sẵn, sprite hoặc HTTPS; cấm đường dẫn chạy mã và thoát thư mục.</summary>
    internal static bool Image(string? path) => !string.IsNullOrWhiteSpace(path) && path.Length <= 500 &&
        (path.StartsWith("sprite:") && int.TryParse(path[7..], out var n) && n is >= 0 and < 6 ||
        path.StartsWith("/assets/", StringComparison.Ordinal) && !path.Contains("..") && !path.Contains('\\') && !path.Contains('%') && !path.Any(char.IsControl) && File.Exists(Path.Combine(AppContext.BaseDirectory, "Assets", Path.GetFileName(path))) ||
        Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo));
    /// <summary>Liên kết banner chỉ cho điều hướng nội bộ hoặc HTTP(S), không cho javascript/data URL.</summary>
    internal static bool Link(string? path) => string.IsNullOrEmpty(path) || path.Length <= 500 &&
        (!path.Any(char.IsControl) && !path.Contains('\\') && (path.StartsWith('/') && !path.StartsWith("//") && !path.Contains("..") && !path.Contains('%') ||
        Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" && string.IsNullOrEmpty(uri.UserInfo)));
    /// <summary>Chỉ nhận thời điểm có múi giờ rõ; lưu UTC, tránh lịch công bố phụ thuộc máy Admin.</summary>
    internal static DateTime Utc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Unspecified)
            throw new WorkforceRule(400, "Thời điểm phải có múi giờ UTC.");
        return value.ToUniversalTime();
    }
}
