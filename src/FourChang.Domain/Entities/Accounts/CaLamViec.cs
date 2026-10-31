namespace FourChang.Domain.Entities;

public sealed class CaLamViec
{
    public long MaCaLamViec { get; set; }
    public string MaCaHienThi { get; set; } = string.Empty;
    public long MaNhanVien { get; set; }
    public string TenCa { get; set; } = string.Empty;
    public string MaMay { get; set; } = string.Empty;
    public DateTime BatDauLuc { get; set; }
    public DateTime? KetThucLuc { get; set; }
    public string TrangThai { get; set; } = string.Empty;
    public decimal TienMatDauCa { get; set; }
    public decimal? TienMatDuKienCuoiCa { get; set; }
    public decimal? TienMatThucDem { get; set; }
    public string? LyDoChenhLech { get; set; }
    public long? MaCaBanGiaoTruoc { get; set; }
    public long? MaQuanLyDuyet { get; set; }
    public string? GhiChu { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
