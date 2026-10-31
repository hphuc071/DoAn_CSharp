namespace FourChang.Domain.Entities;

public sealed class CongThuc
{
    public long MaCongThuc { get; set; }
    public long? MaCauHinhMon { get; set; }
    public long? MaMonThem { get; set; }
    public int SoPhienBan { get; set; }
    public string TenCongThuc { get; set; } = string.Empty;
    public string TrangThai { get; set; } = string.Empty;
    public DateTime? HieuLucTu { get; set; }
    public DateTime? HieuLucDen { get; set; }
    public long MaQuanLyTao { get; set; }
    public long? MaQuanLyDuyet { get; set; }
    public string? GhiChu { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
