namespace FourChang.Domain.Entities;

public sealed class DiaChiKhachHang
{
    public long MaDiaChi { get; set; }
    public long MaKhachHang { get; set; }
    public string? NhanDiaChi { get; set; }
    public string TenNguoiNhan { get; set; } = string.Empty;
    public string DienThoaiNguoiNhan { get; set; } = string.Empty;
    public string DiaChiChiTiet { get; set; } = string.Empty;
    public decimal? ViDo { get; set; }
    public decimal? KinhDo { get; set; }
    public string? GhiChuGiaoHang { get; set; }
    public bool LaDiaChiMacDinh { get; set; }
    public bool DangSuDung { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
