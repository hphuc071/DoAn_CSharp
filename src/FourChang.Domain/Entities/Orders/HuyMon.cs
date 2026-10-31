namespace FourChang.Domain.Entities;

public sealed class HuyMon
{
    public long MaHuyMon { get; set; }
    public Guid MaNhomHuy { get; set; }
    public long MaChiTietDonHang { get; set; }
    public int SoLuongHuy { get; set; }
    public string LyDoHuy { get; set; } = string.Empty;
    public long? MaKhachHangYeuCau { get; set; }
    public long? MaCaLamViec { get; set; }
    public long? MaQuanLyYeuCau { get; set; }
    public long? MaQuanLyDuyet { get; set; }
    public string TrangThai { get; set; } = string.Empty;
    public decimal TienHangGiam { get; set; }
    public decimal PhiGiaoHangGiam { get; set; }
    public DateTime? XuLyLuc { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
