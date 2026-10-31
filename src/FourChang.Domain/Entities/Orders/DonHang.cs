namespace FourChang.Domain.Entities;

public sealed class DonHang
{
    public long MaDonHang { get; set; }
    public string MaDonHienThi { get; set; } = string.Empty;
    public long? MaKhachHang { get; set; }
    public long? MaCaLamViec { get; set; }
    public long? MaDonLienQuan { get; set; }
    public string KenhDatHang { get; set; } = string.Empty;
    public string HinhThucNhanMon { get; set; } = string.Empty;
    public long? MaBan { get; set; }
    public string? MaBanDaChot { get; set; }
    public string? TenNguoiNhan { get; set; }
    public string? DienThoaiNguoiNhan { get; set; }
    public string? DiaChiGiaoDaChot { get; set; }
    public decimal? ViDo { get; set; }
    public decimal? KinhDo { get; set; }
    public DateTime? ThoiDiemHenNhan { get; set; }
    public decimal? KhoangCachGiaoKm { get; set; }
    public string? ChinhSachGiaoHangDaChotJson { get; set; }
    public DateTime? HanBaoPhi { get; set; }
    public decimal PhiGiaoHangBanDau { get; set; }
    public decimal PhiGiaoHangHienTai { get; set; }
    public string TrangThai { get; set; } = string.Empty;
    public string? GhiChu { get; set; }
    public DateTime? HetHanLuc { get; set; }
    public DateTime? TiepNhanLuc { get; set; }
    public DateTime? HoanTatLuc { get; set; }
    public DateTime ThoiDiemTao { get; set; }
    public DateTime ThoiDiemCapNhat { get; set; }
    public byte[] PhienBanDuLieu { get; set; } = [];
}
