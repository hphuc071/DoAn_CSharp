namespace FourChang.Presentation.Shared.Contracts.Admin;

/// <summary>Danh mục và quy đổi nguyên liệu; không chứa quyền sửa tồn trực tiếp.</summary>
public sealed record IngredientView(long MaNguyenLieu, string TenNguyenLieu, long MaDonViCoSo,
    decimal TonAnToan, decimal NguongCanhBao, string? MoTa, bool DangSuDung, byte[] PhienBanDuLieu,
    IReadOnlyList<ConversionInput> QuyDoi, long SoThamChieu);
public sealed record UnitView(long MaDonVi, string KyHieu, string TenDonVi, bool DangSuDung, byte[] PhienBanDuLieu);
public sealed record ConversionInput(long MaDonVi, decimal HeSoVeDonViCoSo);
public sealed record IngredientSnapshot(IReadOnlyList<IngredientView> NguyenLieu, IReadOnlyList<UnitView> DonVi);
public sealed record IngredientInput(string TenNguyenLieu, long MaDonViCoSo, decimal TonAnToan,
    decimal NguongCanhBao, string? MoTa, bool DangSuDung, IReadOnlyList<ConversionInput> QuyDoi,
    byte[]? PhienBanDuLieu, Guid MaThaoTac);
public sealed record UnitInput(string KyHieu, string TenDonVi, bool DangSuDung, byte[]? PhienBanDuLieu, Guid MaThaoTac);
public sealed record AdminDeleteInput(byte[] PhienBanDuLieu, Guid MaThaoTac);
public sealed record AdminSaveReceipt(long Ma, byte[]? PhienBanDuLieu, bool DaXoa = false);

/// <summary>Dòng nhập BOM có đơn vị; máy chủ đổi về cơ sở trước khi lưu, không suy từ ghi chú.</summary>
public sealed record RecipeLineInput(long MaNguyenLieu, long MaDonVi, decimal DinhMuc, string LoaiDinhMuc, decimal TyLeHaoHut);
public sealed record RecipeLineView(long MaNguyenLieu, string TenNguyenLieu, string DonViCoSo, decimal DinhMucCoSo, string LoaiDinhMuc, decimal TyLeHaoHut);
public sealed record RecipeView(long MaCongThuc, long? MaCauHinhMon, long? MaMonThem, int SoPhienBan,
    string TenCongThuc, string TrangThai, DateTime? HieuLucTu, DateTime? HieuLucDen,
    string? GhiChu, byte[] PhienBanDuLieu, IReadOnlyList<RecipeLineView> ThanhPhan);
public sealed record RecipeInput(long? MaCauHinhMon, long? MaMonThem, string TenCongThuc, string? GhiChu,
    IReadOnlyList<RecipeLineInput> ThanhPhan, byte[]? PhienBanDuLieu, Guid MaThaoTac);
public sealed record RecipePublishInput(DateTime HieuLucTu, DateTime? HieuLucDen, byte[] PhienBanDuLieu, Guid MaThaoTac);
public sealed record RecipeStopInput(string LyDo, byte[] PhienBanDuLieu, Guid MaThaoTac);
public sealed record RecipeToppingInput(long MaMonThem, int SoPhanMoiLy);
public sealed record RecipeSimulationInput(long MaCongThuc, int SoLy, int PhanTramDuong, int PhanTramDa,
    IReadOnlyList<RecipeToppingInput> MonThem);
public sealed record RecipeDemand(long MaNguyenLieu, string TenNguyenLieu, string DonVi, decimal SoLuongCan, decimal? KhaDung, bool DuNguyenLieu);
public sealed record StockView(long MaNguyenLieu, string TenNguyenLieu, string DonVi, decimal? TonThucTe,
    decimal? GiuCho, decimal TonAnToan, decimal NguongCanhBao, decimal? KhaDung, string CanhBao,
    DateTime? CapNhatLuc, byte[]? PhienBanDuLieu);
public sealed record StockSnapshot(DateTime DocLuc, IReadOnlyList<StockView> NguyenLieu);
public sealed record StockMovementView(long MaBienDong, string Loai, decimal SoLuong, DateTime PhatSinhLuc,
    long? MaDongPhieu, long? MaChiTietDonHang, long? MaPhieuKho, long? MaDonHang,
    long? MaCaLamViec, string? NhanVien, string? MaMay, string? GhiChu);
public sealed record StockReservationView(long MaChiTietDonHang, long MaDonHang, decimal DaGiu,
    decimal TieuHao, decimal GiaiPhong, string TrangThai, DateTime? HetHanLuc);
public sealed record StockAffectedView(long MaCongThuc, long? MaCauHinhMon, long? MaMonThem, string TenCongThuc, string TrangThai);
public sealed record StockDetail(StockView NguyenLieu, DateTime DocLuc, IReadOnlyList<StockMovementView> BienDong,
    IReadOnlyList<StockReservationView> GiuCho, IReadOnlyList<StockAffectedView> AnhHuong);
