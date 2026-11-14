namespace FourChang.Presentation.Shared.Contracts.Orders;

/// <summary>Trạng thái yêu cầu tách khỏi trạng thái đơn: yêu cầu chưa được duyệt không có nghĩa đã hủy.</summary>
public sealed record CancellationInfo(Guid MaNhomHuy, string TrangThai, string LyDo);
public sealed record CancellationCommand(string HanhDong, Guid MaNhomHuy, string LyDo, byte[] PhienBanDuLieu, Guid MaThaoTac, bool DaKiemTraNguoiNhan = false);
public sealed record CounterRefund(long MaHoanTien, string MaHoan, string NguoiNhan, decimal SoTien, byte[] PhienBanDuLieu);
public sealed record CounterRefundCommand(string NguoiNhan, string ChungCu, bool DaGiaoTienVaKhachDaNhan, byte[] PhienBanDuLieu, Guid MaThaoTac);
