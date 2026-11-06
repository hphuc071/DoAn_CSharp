namespace FourChang.Domain.Entities.Menu;

/// <summary>Nội dung banner công bố theo thời gian; không chứa giá bán.</summary>
public sealed class Banner
{
    public long MaBanner { get; set; }
    public string TenBanner { get; set; } = string.Empty;
    public string? NoiDung { get; set; }
    public string DuongDanAnh { get; set; } = string.Empty;
    public string? LienKet { get; set; }
    public string ViTri { get; set; } = string.Empty;
    public int ThuTuHienThi { get; set; }
    public DateTime BatDauLuc { get; set; }
    public DateTime? KetThucLuc { get; set; }
    public bool DaCongBo { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
