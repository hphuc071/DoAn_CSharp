namespace FourChang.Presentation.Shared.Contracts.Common;

// Hợp đồng dữ liệu; giữ nguyên tên trường để các phân hệ tiếp tục dùng cùng API.
public sealed record SignIn(string TenDangNhap, string MatKhau, Guid? MaThaoTac = null);
public sealed record SessionResult(string Token, string Role, string Name, long Id, long? ShiftId = null, bool MustChangePassword = false);
