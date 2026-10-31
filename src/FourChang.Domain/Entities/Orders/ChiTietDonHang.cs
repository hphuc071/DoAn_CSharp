namespace FourChang.Domain.Entities;

public sealed class ChiTietDonHang
{
    public long MaChiTietDonHang { get; set; }
    public long MaDonHang { get; set; }
    public long MaCauHinhMon { get; set; }
    public long MaCongThuc { get; set; }
    public string TenMonDaChot { get; set; } = string.Empty;
    public string KichCoDaChot { get; set; } = string.Empty;
    public string NhietDoDaChot { get; set; } = string.Empty;
    public int PhanTramDuong { get; set; }
    public int PhanTramDa { get; set; }
    public decimal GiaMonDaChot { get; set; }
    public int SoLuongDat { get; set; }
    public int SoLuongBatDauPha { get; set; }
    public int SoLuongPhaXong { get; set; }
    public int SoLuongDaHuy { get; set; }
    public string? GhiChu { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
