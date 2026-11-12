namespace FourChang.Presentation.Shared.Contracts.Admin;

public sealed record ReportFilter(DateTime TuLuc, DateTime DenLuc, string? Kenh = null, string? HinhThuc = null, long? MaCa = null);
public sealed record ReportColumn(string Ten, string Kieu = "text");
public sealed record ReportTable(string Ten, IReadOnlyList<ReportColumn> Cot, IReadOnlyList<IReadOnlyList<string?>> Dong);
public sealed record ReportKpis(decimal TienHang, decimal PhiGiaoHang, decimal DieuChinhKhieuNai, decimal DoanhThuThuan,
    decimal ThuTienMat, decimal ThuChuyenKhoan, decimal HoanTienMat, decimal HoanChuyenKhoan, decimal DongTienRong,
    int TongDon, int HoanTat, int HuyToanDon, int HetHan, int HuyMotPhan, int SoLyHuy, decimal? TyLeHoanTat);
public sealed record ReportSnapshot(Guid MaSnapshot, DateTime MocDuLieu, ReportFilter BoLoc, string MuiGio,
    string DinhNghia, ReportKpis ChiSo, IReadOnlyList<ReportTable> Bang, string Loai = "REPORT");
public sealed record AuditFilter(DateTime? TuLuc = null, DateTime? DenLuc = null, string? HanhDong = null,
    string? LoaiDoiTuong = null, string? MaDoiTuong = null, string? Nguoi = null, long? MaCa = null,
    string? MaMay = null, Guid? CorrelationId = null, int Trang = 1);
public sealed record ReportExportInput(Guid MaSnapshot, string DinhDang, Guid MaThaoTac);
public sealed record ReportExportJob(Guid MaCongViec, string TrangThai, int TienDo, string? Loi,
    Guid MaSnapshot, string DinhDang, DateTime MocDuLieu);
