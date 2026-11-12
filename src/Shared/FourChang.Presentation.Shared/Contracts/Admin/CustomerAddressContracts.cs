namespace FourChang.Presentation.Shared.Contracts.Admin;

/// <summary>Sổ địa chỉ của một khách, bao gồm bản ngừng dùng cho Admin tra cứu; không trả mật khẩu hoặc token.</summary>
public sealed record AdminCustomerAddress(long MaDiaChi, string? NhanDiaChi, string TenNguoiNhan, string DienThoaiNguoiNhan,
    string DiaChiChiTiet, decimal? ViDo, decimal? KinhDo, string? GhiChuGiaoHang, bool LaDiaChiMacDinh, bool DangSuDung, byte[] PhienBanDuLieu);
public sealed record CustomerAddressBook(CustomerAccount Account, IReadOnlyList<AdminCustomerAddress> DiaChi);
public sealed record AdminAddressAction(Guid MaThaoTac, byte[] PhienBanHoSo, byte[] PhienBanDiaChi);
