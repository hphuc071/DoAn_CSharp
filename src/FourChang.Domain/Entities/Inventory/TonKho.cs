namespace FourChang.Domain.Entities;

public sealed class TonKho
{
    public long MaNguyenLieu { get; set; }
    public decimal TonThucTe { get; set; }
    public decimal TongGiuCho { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
