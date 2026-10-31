namespace FourChang.Domain.Entities;

public sealed class DonViTinh
{
    public long MaDonVi { get; set; }
    public string KyHieu { get; set; } = string.Empty;
    public string TenDonVi { get; set; } = string.Empty;
    public bool DangSuDung { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
