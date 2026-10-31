namespace FourChang.Domain.Entities;

public sealed class DanhMucMon
{
    public long MaDanhMuc { get; set; }
    public string TenDanhMuc { get; set; } = string.Empty;
    public string? MoTa { get; set; }
    public int ThuTuHienThi { get; set; }
    public bool DaCongBo { get; set; }
    public bool DangSuDung { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
