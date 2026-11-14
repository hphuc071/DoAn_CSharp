namespace FourChang.Domain.Entities.Orders;

public sealed class Ban
{
    public long MaBan { get; set; }
    public string MaBanHienThi { get; set; } = string.Empty;
    public string TenBan { get; set; } = string.Empty;
    public string? TenKhuVuc { get; set; }
    public int? SoChoNgoi { get; set; }
    public bool DangSuDung { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
