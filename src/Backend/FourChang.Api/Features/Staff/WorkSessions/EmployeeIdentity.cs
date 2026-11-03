using System.Text;
using System.Text.RegularExpressions;
namespace FourChang.Api.Features.Staff.WorkSessions;

// Kiểm tra họ tên nhập bằng tay. Giữ dấu tiếng Việt; cho phép khác chữ hoa/thường và khoảng trắng thừa.
// Không thêm mã hiển thị hay tài khoản: MaNhanVien là khóa hiện có trong danh sách do Admin quản lý.
internal static class EmployeeIdentity
{
    internal static string Normalize(string? value) => Regex.Replace((value ?? "").Trim().Normalize(NormalizationForm.FormC), @"\s+", " ");
    internal static bool Matches(string stored, string? supplied) =>
        !string.IsNullOrWhiteSpace(supplied) && supplied.Length <= 120 && string.Equals(Normalize(stored), Normalize(supplied), StringComparison.OrdinalIgnoreCase);
}
