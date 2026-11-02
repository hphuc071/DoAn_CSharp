namespace FourChang.Presentation.Shared.Integration;

public sealed record PosDeviceInfo(long MaThietBiPOS, string MaMay, string TenMay, string TrangThai,
    DateTime CapLuc, DateTime? ThuHoiLuc, string LyDo, byte[] PhienBanDuLieu, int SoCaChuaKetThuc);
public sealed record RegisterPosDevice(Guid MaThaoTac, string MaMay, string TenMay, string KhoaThietBi,
    string LyDo, byte[]? PhienBanDuLieu = null);
public sealed record RevokePosDevice(Guid MaThaoTac, byte[] PhienBanDuLieu, string LyDo);
public sealed record PosDeviceOperation(Guid MaThaoTac, PosDeviceInfo Device, long? MaCaDaChot = null);
public sealed record DeviceShift(long MaCaLamViec, string MaCaHienThi, string NhanVien, string TenCa,
    string TrangThai, DateTime BatDauLuc, decimal TienMatDauCa, decimal TienMatDuKien,
    byte[] PhienBanDuLieu, bool ConDonChuaXuLy);
public sealed record CloseDeviceShift(Guid MaThaoTac, byte[] PhienBanDuLieu, decimal TienMatThucDem, string LyDo);
