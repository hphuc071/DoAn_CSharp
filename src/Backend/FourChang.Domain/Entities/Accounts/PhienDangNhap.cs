namespace FourChang.Domain.Entities.Accounts;

public sealed class PhienDangNhap
{
    public long MaPhienDangNhap { get; set; }
    public long? MaKhachHang { get; set; }
    public long? MaQuanLy { get; set; }
    public byte[] MaLamMoiDaBam { get; set; } = [];
    public string PhamViPhien { get; set; } = string.Empty;
    public DateTime HetHanLuc { get; set; }
    public DateTime? ThuHoiLuc { get; set; }
    public string? LyDoThuHoi { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
