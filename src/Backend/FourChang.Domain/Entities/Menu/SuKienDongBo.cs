namespace FourChang.Domain.Entities.Menu;

/// <summary>Outbox bền vững: commit cùng thay đổi menu, worker phát sau và thử lại khi lỗi.</summary>
public sealed class SuKienDongBo
{
    public Guid MaSuKien { get; set; }
    public Guid MaThaoTac { get; set; }
    public string LoaiSuKien { get; set; } = string.Empty;
    public string NoiDungJson { get; set; } = string.Empty;
    public DateTime PhatSinhLuc { get; set; }
    public DateTime? XuLyXongLuc { get; set; }
    public int SoLanThu { get; set; }
    public DateTime? ThuLaiLuc { get; set; }
    public DateTime? KhoaDenLuc { get; set; }
    public Guid? MaLanXuLy { get; set; }
    public string? LoiGanNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
