namespace FourChang.Domain.Entities;

public sealed class Mon
{
    public long MaMon { get; set; }
    public long MaDanhMuc { get; set; }
    public string TenMon { get; set; } = string.Empty;
    public string? MoTa { get; set; }
    public string? DuongDanAnh { get; set; }
    public int ThuTuHienThi { get; set; }
    public bool DaCongBo { get; set; }
    public bool DangBan { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
