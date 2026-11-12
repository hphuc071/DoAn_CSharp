namespace FourChang.Presentation.Shared.Contracts.Admin;

/// <summary>Bộ lọc dùng chung cho đơn và báo cáo; ngày UTC có cận trên loại trừ.</summary>
public sealed record AdminOrderFilter(string? Kenh = null, string? HinhThuc = null, string? TrangThai = null,
    DateTime? TuLuc = null, DateTime? DenLuc = null, long? MaCa = null, string? TimKiem = null, int Trang = 1, int KichThuocTrang = 50);
public sealed record AdminPage<T>(IReadOnlyList<T> DuLieu, int TongSo, int Trang, int KichThuocTrang, DateTime DocLuc);
public sealed record OrderMoney(decimal NghiaVu, decimal DaThu, decimal DaHoan, decimal DangGiuHanMuc,
    decimal ConThu, decimal PhaiHoan);
public sealed record AdminOrderRow(long MaDonHang, string MaDon, string Kenh, string HinhThuc, string TrangThai,
    long? MaKhachHang, long? MaCa, string? NhanVien, string? MaMay, DateTime TaoLuc, DateTime? HoanTatLuc,
    decimal TienHang, decimal PhiGiaoHang, OrderMoney TaiChinh, byte[] PhienBanDuLieu, string CanhBao);
public sealed record AdminOrderLine(long MaDong, string TenMon, string KichCo, string NhietDo, int Duong, int Da,
    string? GhiChu, int SoLuong, int DaPha, int PhaXong, int DaHuy, decimal DonGia, byte[] PhienBanDuLieu, string MonThem);
public sealed record AdminOrderDetail(AdminOrderRow Don, IReadOnlyList<AdminOrderLine> Mon,
    string? DiaChi, IReadOnlyList<StockReservationView> GiuCho, IReadOnlyList<StockMovementView> BienDong,
    IReadOnlyList<AdminTraceRow> LichSu, IReadOnlyList<AdminOrderIngredient>? DinhMuc = null);
/// <summary>Định mức đã chốt khi đặt, luôn kèm mã/tên/đơn vị để không nhầm các hàng giữ chỗ.</summary>
public sealed record AdminOrderIngredient(long MaDong, long MaNguyenLieu, string Ten, string DonVi, decimal DinhMucMoiLy, decimal DaGiu, decimal TieuHao, decimal GiaiPhong);
public sealed record AdminTraceRow(long Ma, DateTime Luc, string Nguoi, long? MaCa, string? MaMay,
    string HanhDong, string? LyDo, string KetQua, string? Truoc, string? Sau, Guid CorrelationId, long? MaNhanVien = null, string? LoaiDoiTuong = null, string? MaDoiTuong = null);
public sealed record OrderActionInput(string HanhDong, string LyDo, byte[] PhienBanDuLieu, Guid MaThaoTac,
    long? MaDong = null, int SoLuong = 0, string? ChungCu = null, string? ResolutionCode = null, bool DaDoiChieuSaoKe = false);
public sealed record OrderAddressInput(string DiaChiMoi, decimal ViDo, decimal KinhDo, decimal KhoangCachKm,
    bool KhachDongY, bool QuanLyXacNhanGiaoDuoc, string LyDo, string ChungCu,
    byte[] PhienBanDuLieu, Guid MaThaoTac);
public sealed record OrderActionReceipt(long MaDonHang, string TrangThai, byte[] PhienBanDuLieu);
