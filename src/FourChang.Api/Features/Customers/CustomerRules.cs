using System.Net.Mail;
using System.Text.RegularExpressions;
using FourChang.Domain.Entities;
using FourChang.Presentation.Shared.Integration;

namespace FourChang.Api.Features.Customers;

internal static class CustomerRules
{
    // BR-06: cùng một định danh phải có cùng cách chuẩn hóa ở đăng ký và quản trị.
    internal static string? Normalize(string? value, bool lower = false) =>
        string.IsNullOrWhiteSpace(value) ? null : lower ? value.Trim().ToLowerInvariant() : value.Trim();

    internal static string? Validate(CustomerSaveInput input, bool creating)
    {
        var username = Normalize(input.TenDangNhap, true);
        var email = Normalize(input.Email, true);
        var phone = Normalize(input.SoDienThoai);
        if (username is null || !Regex.IsMatch(username, "^[a-z][a-z0-9._-]{2,99}$"))
            return "Tên đăng nhập dài 3–100 ký tự, bắt đầu bằng chữ a-z; dùng chữ không dấu, số, dấu chấm, gạch dưới hoặc gạch ngang.";
        if (Normalize(input.HoTen) is not { Length: >= 1 and <= 120 }) return "Họ tên bắt buộc và tối đa 120 ký tự.";
        if (email is null && phone is null) return "Cần ít nhất email hoặc số điện thoại.";
        if (email is not null && (email.Length > 254 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)) return "Email không hợp lệ.";
        if (phone is not null && !Regex.IsMatch(phone, @"^\+?[0-9]{9,15}$")) return "Số điện thoại phải có 9–15 chữ số, có thể bắt đầu bằng +.";
        if (creating && !ValidPassword(input.MatKhauTam)) return "Mật khẩu tạm phải có 10–128 ký tự.";
        if (!creating && input.MatKhauTam is not null) return "Đặt lại mật khẩu bằng chức năng riêng để ghi lý do và thu hồi phiên.";
        return null;
    }

    internal static bool ValidPassword(string? value) => value is { Length: >= 10 and <= 128 };
    internal static CustomerAccount View(TaiKhoanKhachHang user) => new(user.MaKhachHang,
        user.TenDangNhapChuanHoa, user.HoTen, user.EmailChuanHoa, user.SoDienThoaiChuanHoa,
        user.TrangThai, user.BatBuocDoiMatKhau, user.ThoiDiemTao, user.PhienBanDuLieu);
}
