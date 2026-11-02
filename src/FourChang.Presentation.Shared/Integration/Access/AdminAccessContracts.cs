namespace FourChang.Presentation.Shared.Integration;

// UC-ADM-02 chỉ trả thông tin hiển thị và rowversion; mật khẩu băm/khóa thiết bị không nằm trong DTO.
public sealed record AdminAccessProfile(long MaQuanLy, string TenDangNhap, string HoTen,
    string TrangThai, bool BatBuocDoiMatKhau, DateTime? DangNhapLanCuoiLuc, byte[] PhienBanDuLieu);
public sealed record AdminPasswordInput(Guid MaThaoTac, byte[] PhienBanDuLieu,
    string MatKhauHienTai, string MatKhauMoi, string XacNhanMatKhau);
public sealed record AdminPasswordReceipt(Guid MaThaoTac, bool CanDangNhapLai, DateTime HoanTatLuc);
public sealed record AccessPermission(string ChucNang, string KhachHang, string NhanVien, string QuanLy);
