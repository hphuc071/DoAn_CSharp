namespace FourChang.Presentation.Shared.Contracts.Staff;

// Hợp đồng dữ liệu; giữ nguyên tên trường để các phân hệ tiếp tục dùng cùng API.
public sealed record Employee(long MaNhanVien, string HoTen, string? SoDienThoai, DateTime NgayVaoLam, string TrangThai, byte[]? PhienBanDuLieu = null, string? DiaChiLienHe = null);
public sealed record OpenShift(long MaNhanVien, string TenCa, string MaMay, decimal TienMatDauCa, string? HoTen = null);
public sealed record VerifyEmployee(long MaNhanVien, string HoTen);
public sealed record CloseShift(decimal? TienMatThucDem, string? LyDoChenhLech, byte[]? PhienBanDuLieu = null, Guid MaThaoTac = default);
