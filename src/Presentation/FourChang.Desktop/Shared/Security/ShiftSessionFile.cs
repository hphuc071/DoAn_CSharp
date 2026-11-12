using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FourChang.Presentation.Shared.Contracts.Common;

namespace FourChang.Desktop.Shared.Security;

/// <summary>Lưu phiên ca bằng DPAPI theo người dùng Windows; không lưu token dạng rõ hoặc đặt trong repository.</summary>
public static class ShiftSessionFile
{
    private static string FilePath => Environment.GetEnvironmentVariable("FOURCHANG_SHIFT_SESSION_PATH") ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FourChang", "shift-session.bin");

    /// <summary>Chỉ lưu phiên Staff, ghi file tạm rồi thay nguyên tử; lỗi đĩa không làm mất ca đã mở trên server.</summary>
    public static bool Save(SessionResult session)
    {
        if (session.ShiftId is null || session.Role is not ("STAFF" or "STAFF_RECONCILIATION")) return false;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(session);
        try
        {
            var path = Path.GetFullPath(FilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path + ".tmp", PosDeviceCredentials.Protect(bytes, false));
            File.Move(path + ".tmp", path, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException) { return false; }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    /// <summary>Đọc được cache vẫn phải hỏi API kiểm tra chữ ký, hạn và trạng thái; không phục hồi chỉ theo mã máy.</summary>
    public static SessionResult? Load()
    {
        if (!File.Exists(FilePath)) return null;
        try
        {
            var bytes = PosDeviceCredentials.Protect(File.ReadAllBytes(FilePath), true);
            try
            {
                var value = JsonSerializer.Deserialize<SessionResult>(bytes);
                return value?.ShiftId is not null && value.Role is "STAFF" or "STAFF_RECONCILIATION" ? value : null;
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or JsonException) { return null; }
    }

    /// <summary>Xóa đúng file phiên của ứng dụng sau ca đã kết thúc, không xóa thông tin hoặc dữ liệu khác.</summary>
    public static void Clear()
    {
        try { File.Delete(FilePath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
