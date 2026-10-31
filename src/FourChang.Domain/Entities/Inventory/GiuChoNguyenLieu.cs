namespace FourChang.Domain.Entities;

public sealed class GiuChoNguyenLieu
{
    public long MaChiTietDonHang { get; set; }
    public long MaNguyenLieu { get; set; }
    public decimal SoLuongGiuCho { get; set; }
    public decimal SoLuongDaTieuHao { get; set; }
    public decimal SoLuongDaGiaiPhong { get; set; }
    public DateTime? HetHanLuc { get; set; }
    public string TrangThai { get; set; } = string.Empty;
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
