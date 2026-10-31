namespace FourChang.Domain.Entities;

public sealed class LanChiHoanTien
{
    public long MaLanChiHoan { get; set; }
    public long MaHoanTien { get; set; }
    public int SoThuTuLanChi { get; set; }
    public Guid MaChongChiLap { get; set; }
    public decimal SoTien { get; set; }
    public string PhuongThuc { get; set; } = string.Empty;
    public string? NhaCungCap { get; set; }
    public string? MaThamChieuBenNgoai { get; set; }
    public string TrangThai { get; set; } = string.Empty;
    public DateTime BatDauLuc { get; set; }
    public DateTime? CoKetQuaLuc { get; set; }
    public long? MaCaLamViec { get; set; }
    public long? MaQuanLyXacMinh { get; set; }
    public string? ThamChieuChungCu { get; set; }
    public string? LyDoThatBai { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
