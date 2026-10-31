namespace FourChang.Domain.Entities;

public sealed class HoaDon
{
    public long MaHoaDon { get; set; }
    public string SoHoaDon { get; set; } = string.Empty;
    public long MaDonHang { get; set; }
    public string LoaiHoaDon { get; set; } = string.Empty;
    public long? MaHoaDonGoc { get; set; }
    public long? MaHuyMon { get; set; }
    public string? TenNguoiMua { get; set; }
    public string? MaSoThueNguoiMua { get; set; }
    public decimal SoTien { get; set; }
    public string NoiDungHoaDonDaChotJson { get; set; } = string.Empty;
    public string TrangThai { get; set; } = string.Empty;
    public string? LyDo { get; set; }
    public DateTime? PhatHanhLuc { get; set; }
    public long? MaQuanLyPhatHanh { get; set; }
    public long? MaCaLamViec { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
