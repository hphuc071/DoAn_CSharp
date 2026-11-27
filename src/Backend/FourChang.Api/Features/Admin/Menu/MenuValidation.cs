// UC: UC-ADM-02
// Chức năng: Kiểm tra dữ liệu thực đơn và hình ảnh trước khi lưu.
// Cần nhớ: Đọc hàm từ bước kiểm tra dữ liệu đến bước trả kết quả; các quyết định nghiệp vụ do Backend kiểm tra.

using FourChang.Api.Shared.Operations;
namespace FourChang.Api.Features.Admin.Menu;

/// <summary>Quy tắc trường ADM-02: chặn dữ liệu sai trước khi SQL ghi; không chuẩn hóa giá ở UI.</summary>
internal static class MenuValidation
{
    /// <summary>Bắt buộc chuỗi có nghĩa và đúng giới hạn cột; không dùng tên làm khóa nhận diện.</summary>
    internal static string Text(string? text, int max, string label)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > max)
            throw new AdminRule(400, label + " không hợp lệ.");
        return text.Trim();
    }
    /// <summary>Chuỗi tùy chọn rỗng được lưu NULL, vượt giới hạn bị từ chối thay vì cắt dữ liệu.</summary>
    internal static string? Optional(string? text, int max)
    {
        if (text?.Length > max)
            throw new AdminRule(400, "Nội dung quá dài.");
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
    /// <summary>Giá VND nguyên, không âm và nằm trong decimal(18,0); không âm thầm làm tròn.</summary>
    internal static void Price(decimal price)
    {
        if (price < 0 || price > 999999999999999999m || decimal.Truncate(price) != price)
            throw new AdminRule(400, "Giá phải là số VND nguyên không âm.");
    }
    /// <summary>Chấp nhận tài nguyên có sẵn, sprite hoặc HTTPS; cấm đường dẫn chạy mã và thoát thư mục.</summary>
    internal static bool Image(string? path, string? imageFolder = null) => !string.IsNullOrWhiteSpace(path) && path.Length <= 500 &&
        (MenuImages.IsName(path.StartsWith("/menu-images/", StringComparison.Ordinal) ? path[13..] : "") && imageFolder is not null && File.Exists(Path.Combine(imageFolder, path[13..])) ||
        path.StartsWith("sprite:") && int.TryParse(path[7..], out var n) && n is >= 0 and < 6 ||
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
            throw new AdminRule(400, "Thời điểm phải có múi giờ UTC.");
        return value.ToUniversalTime();
    }
}

/// <summary>Kho ảnh menu dùng chung; tên theo nội dung, không dùng tên/đường dẫn của máy Admin.</summary>
internal static class MenuImages
{
    internal const int MaxBytes = 5 * 1024 * 1024;
    /// <summary>Lưu ngoài DLL để build lại không làm mất ảnh; test truyền thư mục riêng.</summary>
    internal static string Folder(IConfiguration config, IWebHostEnvironment env) =>
        Path.GetFullPath(config["MenuImages:Directory"] ?? Path.Combine(env.ContentRootPath, "artifacts", "menu-images"));
    /// <summary>Chỉ nhận tên SHA256 PNG do server cấp, không nhận đường dẫn thư mục.</summary>
    internal static bool IsName(string name) => System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-f0-9]{64}\\.png$");
    /// <summary>Nhận PNG giới hạn dung lượng/kích thước, ghi nguyên tử và trả cùng đường dẫn khi thử lại.</summary>
    internal static async Task<IResult> Upload(FourChang.Presentation.Shared.Contracts.Admin.MenuImageInput input, IConfiguration config, IWebHostEnvironment env, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Data) || input.Data.Length > 7 * 1024 * 1024)
            throw new AdminRule(400, "Ảnh tối đa 5 MB. Chọn ảnh PNG hoặc JPG khác.");
        byte[] bytes;
        try { bytes = System.Convert.FromBase64String(input.Data); }
        catch (FormatException) { throw new AdminRule(400, "Dữ liệu ảnh không hợp lệ."); }
        // Chỉ phục vụ PNG dưới MIME ảnh cố định; chặn SVG, HTML và kích thước quá lớn.
        if (bytes.Length < 45 || bytes.Length > MaxBytes || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}) ||
            !bytes.AsSpan(12,4).SequenceEqual("IHDR"u8) || !bytes.AsSpan(bytes.Length - 8,4).SequenceEqual("IEND"u8))
            throw new AdminRule(400, "Ảnh PNG không hợp lệ hoặc vượt 5 MB.");
        var width = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4));
        var height = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4));
        if (width is 0 or > 4096 || height is 0 or > 4096)
            throw new AdminRule(400, "Ảnh tối đa 4096 × 4096 điểm ảnh.");
        var folder = Folder(config, env); Directory.CreateDirectory(folder);
        var name = System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant() + ".png";
        var destination = Path.Combine(folder, name);
        if (!File.Exists(destination))
        {
            var temporary = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                await File.WriteAllBytesAsync(temporary, bytes, ct);
                try { File.Move(temporary, destination, false); }
                catch (IOException) when (File.Exists(destination)) { /* Cùng ảnh vừa được yêu cầu khác lưu. */ }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        return Results.Ok(new FourChang.Presentation.Shared.Contracts.Admin.MenuImageReceipt("/menu-images/" + name));
    }
    /// <summary>Đọc ảnh công khai theo mã hợp lệ, không cho tải file tùy ý trong server.</summary>
    internal static IResult Read(string file, HttpContext context, IConfiguration config, IWebHostEnvironment env)
    {
        if (!IsName(file)) return Results.NotFound();
        var path = Path.Combine(Folder(config, env), file);
        if (!File.Exists(path)) return Results.NotFound();
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        return Results.File(path, "image/png");
    }
}
