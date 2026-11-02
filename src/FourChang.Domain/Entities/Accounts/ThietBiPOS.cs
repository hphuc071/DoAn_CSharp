namespace FourChang.Domain.Entities;

// Bảng duy nhất bổ sung theo xác nhận người dùng để thực hiện A3 của UC-ADM-02.
public sealed class ThietBiPOS
{
 public long MaThietBiPOS { get; set; }
 public string MaMay { get; set; } = "";
 public string TenMay { get; set; } = "";
 public byte[] MaBamKhoa { get; set; } = [];
 public string TrangThai { get; set; } = "ACTIVE";
 public long MaQuanLyCap { get; set; }
 public DateTime CapLuc { get; set; }
 public DateTime? ThuHoiLuc { get; set; }
 public string LyDo { get; set; } = "";
 public DateTime ThoiDiemTao { get; set; }
 public DateTime ThoiDiemCapNhat { get; set; }
 public byte[] PhienBanDuLieu { get; set; } = [];
}
