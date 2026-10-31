namespace FourChang.Domain.Entities;

public sealed class QuyDoiDonViNguyenLieu
{
    public long MaNguyenLieu { get; set; }
    public long MaDonVi { get; set; }
    public decimal HeSoVeDonViCoSo { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
