namespace FourChang.Domain.Entities;

public sealed class MonThemChiTietDonHang
{
    public long MaChiTietDonHang { get; set; }
    public long MaMonThem { get; set; }
    public long MaCongThuc { get; set; }
    public string TenMonThemDaChot { get; set; } = string.Empty;
    public int SoPhanMoiLy { get; set; }
    public decimal GiaMoiPhanDaChot { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
