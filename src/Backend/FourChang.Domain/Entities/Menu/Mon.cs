using System.ComponentModel.DataAnnotations.Schema;
using FourChang.Domain.Shared;
namespace FourChang.Domain.Entities.Menu;

public sealed class Mon
{
    public long MaMon { get; set; }
    public string LoaiMon { get; set; } = "Cà phê";
    [NotMapped] public long MaDanhMuc { get => CafeOptions.CategoryId(LoaiMon); set => LoaiMon = CafeOptions.Category(value); }
    public string TenMon { get; set; } = string.Empty;
    public string? MoTa { get; set; }
    public string? DuongDanAnh { get; set; }
    public int ThuTuHienThi { get; set; }
    public bool DaCongBo { get; set; }
    public bool DangBan { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public DateTime? CongBoTuLuc { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
