namespace FourChang.Domain.Entities;

public sealed class BienDongKho
{
    public long MaBienDongKho { get; set; }
    public long MaNguyenLieu { get; set; }
    public long? MaDongPhieuKho { get; set; }
    public long? MaChiTietDonHang { get; set; }
    public string LoaiBienDong { get; set; } = string.Empty;
    public decimal SoLuongThayDoi { get; set; }
    public Guid MaThaoTac { get; set; }
    public long? MaBienDongBiDao { get; set; }
    public DateTime PhatSinhLuc { get; set; }
    public string? GhiChu { get; set; }
    public DateTime ThoiDiemTao { get; set; }
}
