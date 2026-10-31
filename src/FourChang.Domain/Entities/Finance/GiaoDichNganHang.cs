namespace FourChang.Domain.Entities;

public sealed class GiaoDichNganHang
{
    public long MaGiaoDichNganHang { get; set; }
    public string TenNhaCungCap { get; set; } = string.Empty;
    public string MaTaiKhoanNganHangQuan { get; set; } = string.Empty;
    public string MaGiaoDichBenNgoai { get; set; } = string.Empty;
    public string ChieuGiaoDich { get; set; } = string.Empty;
    public decimal SoTien { get; set; }
    public string? NoiDungChuyenKhoan { get; set; }
    public DateTime ThoiDiemNganHang { get; set; }
    public string TrangThaiXacMinh { get; set; } = string.Empty;
    public DateTime? XacMinhLuc { get; set; }
    public long? MaThanhToan { get; set; }
    public long? MaLanChiHoan { get; set; }
    public byte[]? MaBamNoiDung { get; set; }
    public string? ThamChieuChungCu { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
