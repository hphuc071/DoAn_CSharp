namespace FourChang.Domain.Entities;

public sealed class NguyenLieuChiTietDonHang
{
    public long MaChiTietDonHang { get; set; }
    public long MaNguyenLieu { get; set; }
    public string TenNguyenLieuDaChot { get; set; } = string.Empty;
    public string DonViCoSoDaChot { get; set; } = string.Empty;
    public decimal DinhMucMoiLy { get; set; }
    public DateTime ThoiDiemTao { get; set; }
}
