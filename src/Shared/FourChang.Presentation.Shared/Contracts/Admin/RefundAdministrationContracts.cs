namespace FourChang.Presentation.Shared.Contracts.Admin;

public sealed record RefundRequestInput(long MaDonHang, long MaThanhToan, long? MaHuyMon, string LoaiNguon,
    decimal SoTien, string PhuongThuc, string NguoiNhan, string? NganHang, string? TaiKhoan,
    string LyDo, string ChungCu, byte[]? PhienBanDuLieu, Guid MaThaoTac);
public sealed record RefundDecisionInput(bool Duyet, decimal SoTien, string LyDo, bool DaKiemTraNguoiNhan,
    byte[] PhienBanDuLieu, Guid MaThaoTac);
public sealed record RefundAttemptInput(decimal SoTien, string? MaGiaoDich, DateTime ThoiDiemChi,
    string ChungCu, bool DaKiemTraNguoiNhan, long? MaCa, byte[] PhienBanDuLieu, Guid MaThaoTac);
public sealed record RefundVerificationInput(string KetQua, string ChungCuSaoKe, bool QuanLyDaDoiChieu,
    string? TaiKhoanQuanAlias, string? LyDoThatBai, byte[] PhienBanDuLieu, Guid MaThaoTac);
public sealed record RefundAttemptView(long MaLanChi, int Lan, decimal SoTien, string PhuongThuc, string TrangThai,
    string? MaGiaoDich, DateTime BatDauLuc, DateTime? CoKetQuaLuc, string? ChungCu, byte[] PhienBanDuLieu, long? MaCa = null, string? NguoiChi = null);
public sealed record RefundView(long MaHoanTien, string MaHoan, long MaDonHang, long MaThanhToan, long? MaHuyMon,
    string LoaiNguon, decimal YeuCau, decimal? DuocDuyet, string TrangThai, string PhuongThuc, string? NguoiNhan,
    string? NganHang, string? TaiKhoanDaChe, string LyDo, string? ChungCu, byte[] PhienBanDuLieu, IReadOnlyList<RefundAttemptView> LanChi, string? MaDon = null, DateTime? TaoLuc = null)
{
    /// <summary>Chỉ lần chi thành công mới tính đã hoàn; chờ xử lý/thất bại không tăng tiền thực trả.</summary>
    public decimal DaHoan => LanChi.Where(x => x.TrangThai == "SUCCEEDED").Sum(x => x.SoTien);
    public decimal ConHoan => Math.Max(0, (DuocDuyet ?? 0) - DaHoan);
    /// <summary>Trạng thái theo dõi không coi hồ sơ đã duyệt hoặc nhập chứng cứ là khách đã nhận tiền.</summary>
    public string TrangThaiTheoDoi => DuocDuyet is > 0 && DaHoan >= DuocDuyet ? "PAID" :
        LanChi.Any(x => x.TrangThai is "PENDING" or "RECONCILING") ? "CHECK" :
        DaHoan > 0 ? "PARTIAL" : PhuongThuc == "CASH" && TrangThai == "APPROVED" ? "WAITING" : "CHECK";
}
public sealed record RefundDetail(RefundView HoSo, OrderMoney TaiChinh, decimal ThuGoc, decimal DaChiTuKhoanThu,
    IReadOnlyList<AdminTraceRow> LichSu);
public sealed record IncomingReconciliationInput(long MaThanhToan, string NhaCungCap, string TaiKhoanQuanAlias,
    string MaGiaoDich, decimal SoTien, DateTime ThoiDiemNganHang, string ChungCuSaoKe, string LyDo,
    bool QuanLyDaDoiChieu, byte[] PhienBanDuLieu, Guid MaThaoTac);
public sealed record IncomingPaymentView(long MaThanhToan, long MaDonHang, decimal SoTienYeuCau, decimal SoTienDaXacMinh, string TrangThai, byte[] PhienBanDuLieu);
