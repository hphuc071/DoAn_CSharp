namespace FourChang.Domain.Entities;

public sealed class PhieuKho
{
    public long MaPhieuKho { get; set; }
    public string SoPhieuKho { get; set; } = string.Empty;
    public string LoaiPhieu { get; set; } = string.Empty;
    public string TrangThai { get; set; } = string.Empty;
    public string? TenNhaCungCapDaChot { get; set; }
    public string? MaMucDich { get; set; }
    public string? LyDo { get; set; }
    public DateTime? MocKiemKe { get; set; }
    public long? MaPhieuBiDao { get; set; }
    public long MaQuanLyLap { get; set; }
    public long? MaQuanLyDuyet { get; set; }
    public DateTime? GhiSoLuc { get; set; }
    public Guid MaThaoTac { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
