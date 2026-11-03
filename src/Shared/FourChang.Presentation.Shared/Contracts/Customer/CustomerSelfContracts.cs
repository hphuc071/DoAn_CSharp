namespace FourChang.Presentation.Shared.Contracts.Customer;

// Hợp đồng dùng chung Web/API cho UC-WEB-01/03. Không nhận mã chủ hồ sơ từ trình duyệt.
public sealed record RegistrationReceipt(Guid MaThaoTac, long MaKhachHang);
public sealed record CustomerSelfProfile(long MaKhachHang, string TenDangNhap, string HoTen,
    string? EmailChuanHoa, string? SoDienThoaiChuanHoa, byte[] PhienBanDuLieu, List<CustomerAddress> DiaChi);
public sealed record CustomerAddress(long MaDiaChi, string? NhanDiaChi, string TenNguoiNhan,
    string DienThoaiNguoiNhan, string DiaChiChiTiet, decimal? ViDo, decimal? KinhDo, string? GhiChuGiaoHang,
    bool LaDiaChiMacDinh, byte[] PhienBanDuLieu);
public sealed record SaveSelfProfile(Guid MaThaoTac, byte[] PhienBanDuLieu, string HoTen, string? Email, string? SoDienThoai);
public sealed record SaveSelfAddress(Guid MaThaoTac, byte[] PhienBanHoSo, byte[]? PhienBanDiaChi,
    string? NhanDiaChi, string TenNguoiNhan, string DienThoaiNguoiNhan, string DiaChiChiTiet,
    decimal? ViDo, decimal? KinhDo, string? GhiChuGiaoHang, bool LaDiaChiMacDinh);
public sealed record RemoveSelfAddress(Guid MaThaoTac, byte[] PhienBanHoSo, byte[] PhienBanDiaChi);
public sealed record ChangeSelfPassword(Guid MaThaoTac, byte[] PhienBanDuLieu, string MatKhauHienTai, string MatKhauMoi, string XacNhanMatKhau);
public sealed record CustomerSelfReceipt(Guid MaThaoTac, CustomerSelfProfile? Profile, bool CanDangNhapLai = false);
