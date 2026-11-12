namespace FourChang.Presentation.Shared.Contracts.Customer;

// Hợp đồng dữ liệu; giữ nguyên tên trường để các phân hệ tiếp tục dùng cùng API.
public sealed record MenuProduct(long Id, long MaMon, string Name, string Category, decimal Price, int ImageIndex, bool Available, string Badge);

public sealed record MenuBrewOption(int PhanTramDuong, int PhanTramDa);

public sealed record MenuToppingItem(long MaMonThem, string TenMonThem, decimal GiaMoiPhan, int GioiHanMoiLy, bool Available);

public sealed record MenuVariantItem(
    long MaCauHinhMon,
    string KichCo,
    string NhietDo,
    decimal GiaBan,
    bool Available,
    List<MenuBrewOption> TuyChonPhaChe,
    List<MenuToppingItem> Toppings);

public sealed record OrderToppingInput(long MaMonThem, int SoPhanMoiLy);

public sealed record OrderItemInput(
    long MaCauHinhMon,
    int SoLuong,
    int PhanTramDuong,
    int PhanTramDa,
    string? GhiChu,
    List<OrderToppingInput> Toppings);

public sealed record CreateCustomerOrderRequest(
    string HinhThucNhanMon,
    string? MaBanHienThi,
    string? TenNguoiNhan,
    string? DienThoaiNguoiNhan,
    string? DiaChiGiao,
    decimal? KhoangCachKm,
    string? GhiChu,
    List<OrderItemInput> Items);

public sealed record OrderPriceSummary(decimal TienHang, decimal PhiGiaoHang, decimal TongCong);

public sealed record CustomerOrderReceipt(
    long MaDonHang,
    string MaDonHienThi,
    string TrangThai,
    decimal TienHang,
    decimal PhiGiaoHang,
    decimal TongCong,
    DateTime? HetHanLuc);

/// <summary>Thông tin bàn hiển thị cho luồng mô phỏng, không chứa bí mật QR.</summary>
public sealed record CustomerTableOption(string MaBanHienThi, string TenBan);
