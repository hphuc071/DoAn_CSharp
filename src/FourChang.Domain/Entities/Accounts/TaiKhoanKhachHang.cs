namespace FourChang.Domain.Entities;

public sealed class TaiKhoanKhachHang
{
    public long MaKhachHang { get; set; }
    public string TenDangNhapChuanHoa { get; set; } = string.Empty;
    public string MatKhauBam { get; set; } = string.Empty;
    public string TrangThai { get; set; } = string.Empty;
    public bool BatBuocDoiMatKhau { get; set; }
    public int SoLanDangNhapSai { get; set; }
    public DateTime? KhoaDenLuc { get; set; }
    public DateTime? DangNhapLanCuoiLuc { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public string? EmailChuanHoa { get; set; }
    public string? SoDienThoaiChuanHoa { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
