namespace FourChang.Presentation.Shared.Contracts.Admin;

/// <summary>Dữ liệu dòng phiếu; kiểm kê gửi kèm mốc và phiên bản tồn mà người đếm đã đối chiếu.</summary>
public sealed record StockDocumentLineInput(long MaNguyenLieu, decimal SoLuong, decimal? DonGiaNhap = null,
    decimal? TonSoDaChot = null, byte[]? PhienBanTonKho = null, string? GhiChu = null);
public sealed record StockDocumentInput(string LoaiPhieu, string? Nguon, string? MaMucDich, string LyDo,
    IReadOnlyList<StockDocumentLineInput> ThanhPhan, byte[]? PhienBanDuLieu, Guid MaThaoTac);
public sealed record StockDocumentAction(string LyDo, byte[] PhienBanDuLieu, Guid MaThaoTac);
public sealed record StockDocumentLineView(long MaDong, long MaNguyenLieu, string TenNguyenLieu, string DonVi,
    decimal SoLuong, decimal ChenhLech, decimal? DonGiaNhap, decimal? TonSo, decimal? ThucDem, byte[]? PhienBanTonKho, string? GhiChu);
public sealed record StockDocumentView(long MaPhieuKho, string SoPhieuKho, string LoaiPhieu, string TrangThai,
    string? Nguon, string? MaMucDich, string? LyDo, DateTime? MocKiemKe, long? MaPhieuBiDao,
    long NguoiLap, long? NguoiGhiSo, DateTime TaoLuc, DateTime? GhiSoLuc, byte[] PhienBanDuLieu,
    IReadOnlyList<StockDocumentLineView> ThanhPhan);
