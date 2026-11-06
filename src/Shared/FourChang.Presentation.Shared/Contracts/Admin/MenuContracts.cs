namespace FourChang.Presentation.Shared.Contracts.Admin;

/// <summary>Dữ liệu ADM-03 dùng chung; giá chỉ có một nguồn từ API/database.</summary>
public sealed record MenuCategory(long MaDanhMuc, string TenDanhMuc, string? MoTa, int ThuTuHienThi, bool DaCongBo, bool DangSuDung, int SoMon, byte[] PhienBanDuLieu);
public sealed record MenuDish(long MaMon, long MaDanhMuc, string TenMon, string? MoTa, string? DuongDanAnh, int ThuTuHienThi, bool DaCongBo, bool DangBan, DateTime? CongBoTuLuc, byte[] PhienBanDuLieu);
public sealed record MenuChoice(int PhanTramDuong, int PhanTramDa, bool LaMacDinh);
public sealed record MenuToppingLink(long MaMonThem, int GioiHanMoiLy);
public sealed record MenuVariant(long MaCauHinhMon, long MaMon, string KichCo, string NhietDo, decimal GiaBan, bool DangBan, bool LaMacDinh, byte[] PhienBanDuLieu, IReadOnlyList<MenuChoice> TuyChon, IReadOnlyList<MenuToppingLink> MonThem);
public sealed record MenuTopping(long MaMonThem, string TenMonThem, string? MoTa, decimal GiaMoiPhan, int GioiHanMacDinhMoiLy, string? DuongDanAnh, bool DangSuDung, byte[] PhienBanDuLieu);
public sealed record MenuBanner(long MaBanner, string TenBanner, string? NoiDung, string DuongDanAnh, string? LienKet, string ViTri, int ThuTuHienThi, DateTime BatDauLuc, DateTime? KetThucLuc, bool DaCongBo, byte[] PhienBanDuLieu);
public sealed record MenuAdminSnapshot(IReadOnlyList<MenuCategory> DanhMuc, IReadOnlyList<MenuDish> Mon, IReadOnlyList<MenuVariant> CauHinh, IReadOnlyList<MenuTopping> MonThem, IReadOnlyList<MenuBanner> Banner);
public sealed record MenuCategoryInput(string TenDanhMuc, string? MoTa, int ThuTuHienThi, bool DaCongBo, bool DangSuDung, bool XacNhanTacDong, byte[]? PhienBanDuLieu, Guid MaThaoTac);
public sealed record MenuDishInput(long MaDanhMuc, string TenMon, string? MoTa, string? DuongDanAnh, int ThuTuHienThi, bool DaCongBo, bool DangBan, DateTime? CongBoTuLuc, byte[]? PhienBanDuLieu, Guid MaThaoTac);
public sealed record MenuVariantInput(long MaMon, string KichCo, string NhietDo, decimal GiaBan, bool DangBan, bool LaMacDinh, IReadOnlyList<MenuChoice> TuyChon, IReadOnlyList<MenuToppingLink> MonThem, byte[]? PhienBanDuLieu, Guid MaThaoTac);
public sealed record MenuToppingInput(string TenMonThem, string? MoTa, decimal GiaMoiPhan, int GioiHanMacDinhMoiLy, string? DuongDanAnh, bool DangSuDung, byte[]? PhienBanDuLieu, Guid MaThaoTac);
public sealed record MenuBannerInput(string TenBanner, string? NoiDung, string DuongDanAnh, string? LienKet, string ViTri, int ThuTuHienThi, DateTime BatDauLuc, DateTime? KetThucLuc, bool DaCongBo, byte[]? PhienBanDuLieu, Guid MaThaoTac);
public sealed record MenuDeleteInput(byte[] PhienBanDuLieu, Guid MaThaoTac);
public sealed record MenuSaveReceipt(string Loai, long Ma, byte[]? PhienBanDuLieu);
