namespace FourChang.Domain.Entities.Orders;

public sealed class NguyenLieuChiTietDonHang
{
    public long MaChiTietDonHang { get; set; }
    public long MaNguyenLieu { get; set; }
    public string TenNguyenLieuDaChot { get; set; } = string.Empty;
    public string DonViCoSoDaChot { get; set; } = string.Empty;
    public decimal DinhMucMoiLy { get; set; }
    public decimal SoLuongGiuCho { get; set; }
    public decimal SoLuongDaTieuHao { get; set; }
    public decimal SoLuongDaGiaiPhong { get; set; }
    public DateTime? HetHanLuc { get; set; }
    public string TrangThai { get; set; } = "RESERVED";
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
    public DateTime ThoiDiemTao { get; set; }
}
