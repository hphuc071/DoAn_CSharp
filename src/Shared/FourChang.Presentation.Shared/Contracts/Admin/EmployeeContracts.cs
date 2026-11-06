namespace FourChang.Presentation.Shared.Contracts.Admin;

// Hợp đồng dữ liệu; giữ nguyên tên trường để các phân hệ tiếp tục dùng cùng API.
public sealed record EmployeeInput(string HoTen, string? SoDienThoai, DateTime NgayVaoLam, string TrangThai, byte[]? PhienBanDuLieu = null, Guid MaThaoTac = default, string? LyDo = null, string? DiaChiLienHe = null);

/// <summary>Kết quả phân trang ADM-12; giữ Employee dùng chung với POS, không đưa tài khoản vào hồ sơ.</summary>
public sealed record EmployeePage(IReadOnlyList<FourChang.Presentation.Shared.Contracts.Staff.Employee> Items, int Page, int PageSize, int Total);

/// <summary>Đổi trạng thái riêng có lý do; sửa thông tin không được lách điều kiện ngừng làm.</summary>
public sealed record EmployeeStatusInput(string TrangThai, string LyDo, byte[] PhienBanDuLieu, Guid MaThaoTac);
