namespace FourChang.Domain.Entities.Menu;

public sealed class ChiTietCongThuc
{
    public long MaCongThuc { get; set; }
    public long MaNguyenLieu { get; set; }
    public string LoaiDinhMuc { get; set; } = string.Empty;
    public decimal DinhMucCoSo { get; set; }
}
