using System.ComponentModel.DataAnnotations.Schema;
using FourChang.Domain.Shared;
namespace FourChang.Domain.Entities.Inventory;

public sealed class NguyenLieu
{
    public long MaNguyenLieu { get; set; }
    public string TenNguyenLieu { get; set; } = string.Empty;
    public string DonViCoSo { get; set; } = "g";
    [NotMapped] public long MaDonViCoSo { get => CafeOptions.UnitId(DonViCoSo); set => DonViCoSo = CafeOptions.Unit(value); }
    public decimal TonAnToan { get; set; }
    public decimal NguongCanhBao { get; set; }
    public string? MoTa { get; set; }
    public bool DangSuDung { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
