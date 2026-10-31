namespace FourChang.Domain.Entities;

public sealed class ThanhToan
{
    public long MaThanhToan { get; set; }
    public string MaThanhToanHienThi { get; set; } = string.Empty;
    public long MaDonHang { get; set; }
    public long? MaCaLamViec { get; set; }
    public string PhuongThuc { get; set; } = string.Empty;
    public decimal SoTienYeuCau { get; set; }
    public decimal SoTienDaXacMinh { get; set; }
    public decimal? TienKhachDua { get; set; }
    public string? MaThamChieuChuyenKhoan { get; set; }
    public string? NoiDungQR { get; set; }
    public string TrangThai { get; set; } = string.Empty;
    public DateTime? HetHanLuc { get; set; }
    public DateTime? XacMinhLuc { get; set; }
    public long? MaQuanLyXacMinh { get; set; }
    public string? ThamChieuChungCu { get; set; }
    public Guid MaThaoTac { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
