namespace FourChang.Domain.Entities;

public sealed class NhanVien
{
    public long MaNhanVien { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public string? SoDienThoai { get; set; }
    public DateTime NgayVaoLam { get; set; }
    public string TrangThai { get; set; } = string.Empty;
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
