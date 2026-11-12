namespace FourChang.Presentation.Shared.Contracts.Admin;

public sealed record AdminInvoiceView(long MaHoaDon, string SoHoaDon, long MaDonHang, string Loai,
    long? MaHoaDonGoc, long? MaHuyMon, string? NguoiMua, decimal SoTien, string TrangThai,
    DateTime? PhatHanhLuc, long? MaCa, byte[] PhienBanDuLieu, long? MaHoanTien = null);
public sealed record AdminInvoiceDetail(AdminInvoiceView HoaDon, string SnapshotJson, OrderMoney TaiChinh,
    IReadOnlyList<AdminInvoiceView> DieuChinh, IReadOnlyList<AdminTraceRow> LichSu);
public sealed record InvoiceAdjustmentInput(long? MaHuyMon, string LyDo, byte[] PhienBanDuLieu, Guid MaThaoTac, long? MaHoanTien = null);
public sealed record DocumentLogInput(string HanhDong, string? ThamChieu, string? Loi, Guid MaThaoTac);
public sealed record DocumentLogReceipt(long Ma, bool BanSao, Guid MaCongViec);
