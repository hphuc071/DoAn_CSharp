namespace FourChang.Domain.Entities;

public sealed class ChiTietPhieuKho
{
    public long MaDongPhieuKho { get; set; }
    public long MaPhieuKho { get; set; }
    public long MaNguyenLieu { get; set; }
    public long MaDonViNhap { get; set; }
    public string DonViNhapDaChot { get; set; } = string.Empty;
    public decimal HeSoQuyDoiDaChot { get; set; }
    public decimal SoLuongTheoDonViNhap { get; set; }
    public decimal ChenhLechDonViCoSo { get; set; }
    public decimal? DonGiaNhap { get; set; }
    public decimal? TonSoDaChot { get; set; }
    public decimal? SoLuongThucDem { get; set; }
    public byte[]? PhienBanTonKhoDaChot { get; set; }
    public string? GhiChu { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
