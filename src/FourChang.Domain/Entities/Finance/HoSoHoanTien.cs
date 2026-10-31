namespace FourChang.Domain.Entities;

public sealed class HoSoHoanTien
{
    public long MaHoanTien { get; set; }
    public string MaHoanTienHienThi { get; set; } = string.Empty;
    public long MaDonHang { get; set; }
    public long MaThanhToan { get; set; }
    public long? MaHuyMon { get; set; }
    public string LyDoHoan { get; set; } = string.Empty;
    public decimal SoTienYeuCau { get; set; }
    public decimal? SoTienDuocDuyet { get; set; }
    public string PhuongThucHoan { get; set; } = string.Empty;
    public string? TenNguoiNhan { get; set; }
    public string? NganHangNguoiNhan { get; set; }
    public byte[]? TaiKhoanNhanMaHoa { get; set; }
    public string? TaiKhoanNhanDaChe { get; set; }
    public string TrangThai { get; set; } = string.Empty;
    public long? MaKhachHangYeuCau { get; set; }
    public long? MaCaYeuCau { get; set; }
    public long? MaQuanLyYeuCau { get; set; }
    public long? MaQuanLyDuyet { get; set; }
    public DateTime? DuyetLuc { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
