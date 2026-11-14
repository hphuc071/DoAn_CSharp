namespace FourChang.Presentation.Shared.Contracts.Admin;

/// <summary>Dữ liệu ca không chứa khóa máy hoặc token; mọi thời điểm trao đổi theo UTC.</summary>
public sealed record WorkSessionInfo(long MaCaLamViec, string MaCaHienThi, long MaNhanVien, string HoTen,
    string TenCa, string MaMay, DateTime BatDauLuc, DateTime? KetThucLuc, string TrangThai,
    decimal TienMatDauCa, decimal? TienMatDuKienCuoiCa, decimal? TienMatThucDem, string? LyDoChenhLech,
    long? MaCaBanGiaoTruoc, long? MaQuanLyDuyet, byte[] PhienBanDuLieu);
public sealed record WorkSessionPage(IReadOnlyList<WorkSessionInfo> Items, int Page, int PageSize, int Total);
public sealed record ShiftTransaction(string Loai, long Ma, long? MaDonHang, string ThamChieu, string PhuongThuc, decimal SoTien, string TrangThai);
public sealed record ShiftOrder(long MaDonHang, string MaDonHienThi, string TrangThai, long? MaCaNguon);
public sealed record ShiftAudit(DateTime ThoiDiem, string NguoiThucHien, string HanhDong, string? LyDo);
public sealed record HandoverInfo(long MaCaNguon, long MaNhanVienNhan, string MaMayNhan, string LyDo,
    long[] MaDonBanGiao, string[] KhoanChuaRo);
public sealed record WorkSessionDetails(WorkSessionInfo Ca, decimal ThuTienMat, decimal HoanTienMat,
    decimal TienMatDuKienHienTai, decimal? ChenhLech, IReadOnlyList<ShiftTransaction> GiaoDich,
    IReadOnlyList<ShiftOrder> DonDangXuLy, IReadOnlyList<string> KhoanChuaRo, IReadOnlyList<ShiftAudit> NhatKy,
    HandoverInfo? BanGiao, DateTime DuLieuTaiLuc, string MaDoiChieu, bool DangChoDoiChieu, bool CoYeuCauKhoiPhuc, string? SuKienTrangThai = null, int SoDonHoanThanh = 0, decimal TongTienDonHoanThanh = 0)
{
    /// <summary>Tiền mặt thuần từ giao dịch của chính ca; không tính lại khoản thu của ca bàn giao.</summary>
    public decimal TienThuDuoc => ThuTienMat - HoanTienMat;
    /// <summary>Chuyển khoản ghi nhận ở ca, hiển thị riêng và không cộng vào tiền mặt kiểm đếm.</summary>
    public decimal ThuChuyenKhoan => GiaoDich.Where(x => x.Loai == "Thu" && x.PhuongThuc == "BANK_TRANSFER" && x.TrangThai == "VERIFIED").Sum(x => x.SoTien);
}
public sealed record CloseWorkSessionInput(byte[] PhienBanDuLieu, Guid MaThaoTac, string MaDoiChieu);
public sealed record HandoverWorkSessionInput(long MaNhanVienNhan, string MaMayNhan, string LyDo,
    long[] MaDonBanGiao, string[] KhoanChuaRo, byte[] PhienBanDuLieu, Guid MaThaoTac, string MaDoiChieu);
/// <summary>POS gửi số thực đếm để Admin đối chiếu, không tự cho Admin nhập thay nhân viên.</summary>
public sealed record SubmitReconciliation(decimal? TienMatThucDem, string? LyDoChenhLech, byte[] PhienBanDuLieu, Guid MaThaoTac);

/// <summary>Cấp mã phục hồi chỉ gửi đối chiếu; không đưa mã bí mật vào audit/receipt.</summary>
public sealed record IssueShiftRecoveryInput(string LyDo, byte[] PhienBanDuLieu, Guid MaThaoTac);
public sealed record ShiftRecoveryReceipt(long MaCaLamViec, long MaNhanVien, Guid MaYeuCau, DateTime HetHanLuc, string? MaPhucHoi = null);
public sealed record RedeemShiftRecoveryInput(long MaCaLamViec, long MaNhanVien, string HoTen, string MaPhucHoi);
public sealed record ShiftSessionContext(FourChang.Presentation.Shared.Contracts.Common.SessionResult Phien,
    string TrangThai, bool ChiDoiChieu, bool DangChoDoiChieu);
