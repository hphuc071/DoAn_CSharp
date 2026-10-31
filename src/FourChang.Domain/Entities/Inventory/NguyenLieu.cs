namespace FourChang.Domain.Entities;

public sealed class NguyenLieu
{
    public long MaNguyenLieu { get; set; }
    public string TenNguyenLieu { get; set; } = string.Empty;
    public long MaDonViCoSo { get; set; }
    public decimal TonAnToan { get; set; }
    public decimal NguongCanhBao { get; set; }
    public string? MoTa { get; set; }
    public bool DangSuDung { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
