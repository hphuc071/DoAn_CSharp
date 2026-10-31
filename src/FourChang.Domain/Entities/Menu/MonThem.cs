namespace FourChang.Domain.Entities;

public sealed class MonThem
{
    public long MaMonThem { get; set; }
    public string TenMonThem { get; set; } = string.Empty;
    public string? MoTa { get; set; }
    public decimal GiaMoiPhan { get; set; }
    public int GioiHanMacDinhMoiLy { get; set; }
    public string? DuongDanAnh { get; set; }
    public bool DangSuDung { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
