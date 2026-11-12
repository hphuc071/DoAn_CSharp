using FourChang.Presentation.Shared.Contracts.Customer;
namespace FourChang.Presentation.Shared.Contracts.Staff;

/// <summary>POS gửi cấu hình và tổng tiền đã được nhân viên chấp thuận; máy chủ tự lấy giá/BOM.</summary>
public sealed record PosCheckoutInput(Guid MaThaoTac, string HinhThuc, long? MaBan, string PhuongThuc,
    decimal? TienKhachDua, decimal TongTienDaChapNhan, List<OrderItemInput> Items);
public sealed record PosCheckoutReceipt(long MaDonHang, string MaDon, long MaThanhToan, decimal TongTien,
    decimal TienThua, string TrangThaiThanhToan, long? MaHoaDon);
public sealed record PosDiningTable(long MaBan, string TenBan);
/// <summary>QR mô phỏng có nhãn riêng, không phải bằng chứng thu tiền ngân hàng.</summary>
public sealed record SimulationQr(string PngDataUri, string Url, DateTime HetHanLuc, decimal SoTien);
