namespace FourChang.Presentation.Shared.Contracts.Admin;

// Hợp đồng dữ liệu; giữ nguyên tên trường để các phân hệ tiếp tục dùng cùng API.
public sealed record EmployeeInput(string HoTen, string? SoDienThoai, DateTime NgayVaoLam, string TrangThai, byte[]? PhienBanDuLieu = null);
