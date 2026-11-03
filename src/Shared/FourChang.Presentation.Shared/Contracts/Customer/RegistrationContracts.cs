namespace FourChang.Presentation.Shared.Contracts.Customer;

// Hợp đồng dữ liệu; giữ nguyên tên trường để các phân hệ tiếp tục dùng cùng API.
public sealed record RegisterCustomer(string TenDangNhap, string HoTen, string? Email, string? SoDienThoai, string MatKhau, Guid MaThaoTac, string XacNhanMatKhau);
