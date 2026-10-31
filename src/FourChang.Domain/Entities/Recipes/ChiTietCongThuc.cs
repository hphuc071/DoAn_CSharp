namespace FourChang.Domain.Entities;

public sealed class ChiTietCongThuc
{
    public long MaCongThuc { get; set; }
    public long MaNguyenLieu { get; set; }
    public string LoaiDinhMuc { get; set; } = string.Empty;
    public decimal DinhMucCoSo { get; set; }
    public decimal TyLeHaoHut { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
