namespace FourChang.Domain.Entities;

public sealed class PhienDangNhap
{
    public long MaPhienDangNhap { get; set; }
    public long? MaKhachHang { get; set; }
    public long? MaQuanLy { get; set; }
    public byte[] MaLamMoiDaBam { get; set; } = [];
    public string PhamViPhien { get; set; } = string.Empty;
    public string LoaiUngDung { get; set; } = string.Empty;
    public string? ThongTinThietBi { get; set; }
    public DateTime HetHanLuc { get; set; }
    public DateTime? HoatDongLanCuoiLuc { get; set; }
    public DateTime? ThuHoiLuc { get; set; }
    public string? LyDoThuHoi { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
