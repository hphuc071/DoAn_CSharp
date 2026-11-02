namespace FourChang.Presentation.Shared.Integration;

// DTO chỉ chứa thông tin Admin được xem. Không đưa mật khẩu băm hoặc token vào phản hồi.
public sealed record CustomerAccount(long MaKhachHang, string TenDangNhap, string HoTen,
    string? Email, string? SoDienThoai, string TrangThai, bool BatBuocDoiMatKhau,
    DateTime ThoiDiemTao, byte[] PhienBanDuLieu);
public sealed record CustomerPage(List<CustomerAccount> Items, int Page, int PageSize, int Total);
public sealed record CustomerReference(string Loai, string Ma, string TrangThai);
public sealed record CustomerReferenceCount(string Bang, long SoLuong);
public sealed record CustomerDetails(CustomerAccount Account, bool CoTheXoa,
    List<CustomerReferenceCount> ThamChieu, List<CustomerReference> GiaoDichGanDay);

// Mỗi lệnh giữ nguyên MaThaoTac khi thử lại: server có thể trả kết quả cũ thay vì ghi hai lần.
public sealed record CustomerSaveInput(Guid MaThaoTac, string TenDangNhap, string HoTen,
    string? Email, string? SoDienThoai, string? MatKhauTam, byte[]? PhienBanDuLieu = null);
public sealed record CustomerActionInput(Guid MaThaoTac, byte[] PhienBanDuLieu,
    string LyDo, string? TrangThai = null, string? MatKhauTam = null);
public sealed record CustomerMutation(Guid MaThaoTac, CustomerAccount? Account, bool DaXoa);
public sealed record ChangeTemporaryPassword(string MatKhauMoi);
