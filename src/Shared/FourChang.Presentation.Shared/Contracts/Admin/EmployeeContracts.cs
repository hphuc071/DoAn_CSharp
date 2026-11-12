namespace FourChang.Presentation.Shared.Contracts.Admin;

/// <summary>Hồ sơ dùng chung, không chứa tài khoản; client giữ MaThaoTac khi lưu/tra lại sau timeout.</summary>
public sealed record EmployeeInput(string HoTen, string? SoDienThoai, DateTime NgayVaoLam, string TrangThai,
    byte[]? PhienBanDuLieu = null, Guid MaThaoTac = default, string? LyDo = null, string? DiaChiLienHe = null);
/// <summary>Trang hồ sơ Admin; không trả token hoặc thông tin xác thực.</summary>
public sealed record EmployeePage(IReadOnlyList<FourChang.Presentation.Shared.Contracts.Staff.Employee> Items, int Page, int PageSize, int Total);
/// <summary>Đổi trạng thái có lý do, kiểm tra phiên bản và ca đang mở.</summary>
public sealed record EmployeeStatusInput(string TrangThai, string LyDo, byte[] PhienBanDuLieu, Guid MaThaoTac);
/// <summary>Xóa hồ sơ chỉ khi không có tham chiếu; server kiểm tra lại trong giao dịch.</summary>
public sealed record DeleteEmployeeInput(byte[] PhienBanDuLieu, Guid MaThaoTac);
