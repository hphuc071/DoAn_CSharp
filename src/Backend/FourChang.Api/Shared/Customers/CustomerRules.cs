using FourChang.Api.Shared.Customers;
using System.Net.Mail;
using System.Text.RegularExpressions;
using FourChang.Domain.Entities.Accounts;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Api.Shared.Customers;

internal static class CustomerRules
{
    // BR-06: cùng một định danh phải có cùng cách chuẩn hóa ở đăng ký và quản trị.
    /// <summary>Chuẩn hóa định danh dùng chung cho đăng ký, sửa hồ sơ và quản trị để kiểm tra trùng nhất quán.</summary>
    internal static string? Normalize(string? value, bool lower = false) =>
        string.IsNullOrWhiteSpace(value) ? null : lower ? value.Trim().ToLowerInvariant() : value.Trim();

    /// <summary>Kiểm tra hồ sơ và điện thoại 10 chữ số; tạo mới có mật khẩu tạm, sửa hồ sơ không nhận mật khẩu.</summary>
    internal static string? Validate(CustomerSaveInput input, bool creating)
    {
        var username = Normalize(input.TenDangNhap, true);
        var email = Normalize(input.Email, true);
        var phone = Normalize(input.SoDienThoai);
        if (username is null || !Regex.IsMatch(username, "^[a-z][a-z0-9._-]{2,99}$"))
            return "Tên đăng nhập dài 3–100 ký tự, bắt đầu bằng chữ a-z; dùng chữ không dấu, số, dấu chấm, gạch dưới hoặc gạch ngang.";
        if (Normalize(input.HoTen) is not { Length: >= 1 and <= 120 }) return "Họ tên bắt buộc và tối đa 120 ký tự.";
        if (phone is null) return "Số điện thoại bắt buộc, gồm đúng 10 chữ số từ 0 đến 9.";
        if (email is not null && (email.Length > 254 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)) return "Email không hợp lệ.";
        if (phone is not null && !Regex.IsMatch(phone, @"^[0-9]{10}$")) return "Số điện thoại phải gồm đúng 10 chữ số từ 0 đến 9.";
        if (creating && !ValidPassword(input.MatKhauTam)) return "Mật khẩu tạm phải có 5–128 ký tự.";
        if (!creating && input.MatKhauTam is not null) return "Đặt lại mật khẩu bằng chức năng riêng để ghi lý do và thu hồi phiên.";
        return null;
    }

    /// <summary>Kiểm tra độ dài mật khẩu trước khi băm; không lưu hoặc ghi log mật khẩu rõ.</summary>
    internal static bool ValidPassword(string? value) => value is { Length: >= 5 and <= 128 };
    /// <summary>Chuyển entity tài khoản sang DTO chỉ chứa trường được phép hiển thị, không trả hash mật khẩu.</summary>
    internal static CustomerAccount View(TaiKhoanKhachHang user) => new(user.MaKhachHang,
        user.TenDangNhapChuanHoa, user.HoTen, user.EmailChuanHoa, user.SoDienThoaiChuanHoa,
        user.TrangThai, user.BatBuocDoiMatKhau, user.ThoiDiemTao, user.PhienBanDuLieu);
}
