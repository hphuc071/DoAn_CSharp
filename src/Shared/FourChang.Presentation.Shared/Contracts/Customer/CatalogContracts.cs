using FourChang.Presentation.Shared.Contracts.Admin;
namespace FourChang.Presentation.Shared.Contracts.Customer;
/// <summary>Catalog công khai: chỉ dữ liệu đã công bố; giữ mã/giá gốc thống nhất Web và POS.</summary>
public sealed record CatalogTopping(long MaMonThem, string TenMonThem, decimal GiaMoiPhan, int GioiHanMoiLy, bool ConHang);
public sealed record CatalogChoice(int PhanTramDuong, int PhanTramDa, bool LaMacDinh, bool ConHang);
public sealed record CatalogVariant(long MaCauHinhMon, string KichCo, string NhietDo, decimal GiaBan, bool LaMacDinh, bool ConHang, IReadOnlyList<CatalogChoice> TuyChon, IReadOnlyList<CatalogTopping> MonThem);
public sealed record CatalogDish(long MaMon, long MaDanhMuc, string TenDanhMuc, string TenMon, string? MoTa, string? DuongDanAnh, int ThuTuHienThi, bool DangBan, IReadOnlyList<CatalogVariant> CauHinh);
public sealed record CatalogCategory(long MaDanhMuc, string TenDanhMuc, int ThuTuHienThi);
public sealed record CatalogSnapshot(IReadOnlyList<CatalogDish> Mon, IReadOnlyList<MenuBanner> Banner, IReadOnlyList<CatalogCategory>? DanhMuc = null);
