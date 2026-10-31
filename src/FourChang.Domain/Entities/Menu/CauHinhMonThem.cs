namespace FourChang.Domain.Entities;

public sealed class CauHinhMonThem
{
    public long MaCauHinhMon { get; set; }
    public long MaMonThem { get; set; }
    public int GioiHanMoiLy { get; set; }
    public bool DangSuDung { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
