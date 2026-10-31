namespace FourChang.Domain.Entities;

public sealed class TuyChonPhaCheHopLe
{
    public long MaCauHinhMon { get; set; }
    public int PhanTramDuong { get; set; }
    public int PhanTramDa { get; set; }
    public bool DangSuDung { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
