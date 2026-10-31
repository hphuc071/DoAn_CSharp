namespace FourChang.Domain.Entities;

public sealed class CauHinhMon
{
    public long MaCauHinhMon { get; set; }
    public long MaMon { get; set; }
    public string KichCo { get; set; } = string.Empty;
    public string NhietDo { get; set; } = string.Empty;
    public decimal GiaBan { get; set; }
    public bool DangBan { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
