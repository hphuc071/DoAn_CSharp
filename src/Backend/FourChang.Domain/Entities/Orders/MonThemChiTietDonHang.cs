namespace FourChang.Domain.Entities.Orders;

public sealed class MonThemChiTietDonHang
{
    public long MaChiTietDonHang { get; set; }
    public long MaMonThem { get; set; }
    public long MaCongThuc { get; set; }
    public string TenMonThemDaChot { get; set; } = string.Empty;
    public int SoPhanMoiLy { get; set; }
    public decimal GiaMoiPhanDaChot { get; set; }
}
