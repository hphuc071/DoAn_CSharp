namespace FourChang.Domain.Entities;

public sealed class NhatKyHeThong
{
    public long MaNhatKy { get; set; }
    public Guid MaLienKetXuLy { get; set; }
    public Guid? MaThaoTac { get; set; }
    public string LoaiTacNhan { get; set; } = string.Empty;
    public long? MaKhachHang { get; set; }
    public long? MaQuanLy { get; set; }
    public long? MaCaLamViec { get; set; }
    public string TenNguoiThucHienDaChot { get; set; } = string.Empty;
    public string? MaMayDaChot { get; set; }
    public string HanhDong { get; set; } = string.Empty;
    public string LoaiDoiTuong { get; set; } = string.Empty;
    public string? MaDoiTuong { get; set; }
    public string? DuLieuTruocJson { get; set; }
    public string? DuLieuSauJson { get; set; }
    public string? LyDo { get; set; }
    public string KetQua { get; set; } = string.Empty;
    public byte[]? MaBamYeuCau { get; set; }
    public string? KetQuaDaChotJson { get; set; }
    public DateTime PhatSinhLuc { get; set; }
    public DateTime ThoiDiemTao { get; set; }
}
