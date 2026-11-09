using System;
using System.Collections.Generic;

namespace FourChang.Desktop.Features.Pos.Models
{
    // Cấu hình tùy chọn kích cỡ món
    public class MenuItemOption
    {
        public string Size { get; set; } = "M";
        public decimal BasePrice { get; set; }
        public bool IsHotAllowed { get; set; } = true;
        public bool IsColdAllowed { get; set; } = true;
    }

    // Thông tin topping cấu hình
    public class ToppingItem
    {
        public string MaTopping { get; set; } = null!;
        public string TenTopping { get; set; } = null!;
        public decimal GiaMoiPhan { get; set; }
        public int GioiHanSoPhan { get; set; } = 3;
    }

    // Thành phần nguyên liệu trong công thức BOM
    public class RecipeIngredient
    {
        public string MaNguyenLieu { get; set; } = null!;
        public string TenNguyenLieu { get; set; } = null!;
        public string DonViCoSo { get; set; } = null!;
        public decimal DinhMucCoSo { get; set; }
        public string LoaiDinhMuc { get; set; } = "FIXED";
        public decimal TyLeHaoHut { get; set; } = 0m;
    }

    // Công thức pha chế có phiên bản
    public class RecipeDetail
    {
        public string MaCongThuc { get; set; } = null!;
        public int PhienBan { get; set; } = 1;
        public string MaMon { get; set; } = null!;
        public string? Size { get; set; }
        public bool? IsHot { get; set; }
        public string? MaTopping { get; set; }
        public List<RecipeIngredient> Ingredients { get; set; } = new();
    }

    // Snapshot định mức nguyên liệu đã chốt trên từng dòng món
    public class OrderLineIngredientSnapshot
    {
        public string MaNguyenLieu { get; set; } = null!;
        public string TenNguyenLieu { get; set; } = null!;
        public string DonViCoSo { get; set; } = null!;
        public decimal DinhMucMoiLy { get; set; }
        public decimal TongLuongDinhMuc { get; set; }
    }

    // Snapshot topping đã chốt trên từng dòng món
    public class OrderLineTopping
    {
        public string MaTopping { get; set; } = null!;
        public string TenToppingSnapshot { get; set; } = null!;
        public int SoPhanTrenLy { get; set; }
        public decimal GiaMoiPhan { get; set; }
        public string? MaCongThucSnapshot { get; set; }
    }

    // Dòng món trong đơn hàng POS hoặc WEB
    public class OrderLineItem
    {
        public string MaDong { get; set; } = Guid.NewGuid().ToString("N");
        public string MaMon { get; set; } = null!;
        public string TenMonSnapshot { get; set; } = null!;
        public string Size { get; set; } = null!;
        public bool IsHot { get; set; }
        public int MucDuongPercent { get; set; }
        public int MucDaPercent { get; set; }
        public List<OrderLineTopping> Toppings { get; set; } = new();
        public List<OrderLineIngredientSnapshot> IngredientsSnapshot { get; set; } = new();
        public string? MaCongThucSnapshot { get; set; }
        public int SoLy { get; set; }
        public int SoLyBatDauPha { get; set; }
        public int SoLyPhaXong { get; set; }
        public int SoLyDaHuy { get; set; }
        public decimal DonGiaLy { get; set; }
        public decimal ThanhTienDong { get; set; }
        public string? GhiChu { get; set; }
        public string Status { get; set; } = "ACCEPTED";

        // Số ly còn lại chưa bắt đầu pha
        public int PhanChuaPha => Math.Max(0, SoLy - SoLyBatDauPha - SoLyDaHuy);

        // Số ly đang thực hiện pha chế
        public int PhanDangPha => Math.Max(0, SoLyBatDauPha - SoLyPhaXong);

        // Kiểm tra toàn bộ số ly hiệu lực đã hoàn tất pha chế
        public bool IsLineReady => (SoLyPhaXong >= (SoLy - SoLyDaHuy)) && (SoLy > SoLyDaHuy);
    }

    // Bản ghi thanh toán đơn hàng
    public class PosPaymentRecord
    {
        public string MaThanhToan { get; set; } = Guid.NewGuid().ToString("N");
        public string MaDonHang { get; set; } = null!;
        public string PhuongThuc { get; set; } = "CASH";
        public decimal SoTienYeuCau { get; set; }
        public decimal SoTienDaXacMinh { get; set; }
        public decimal? TienKhachDua { get; set; }
        public decimal? TienThua { get; set; }
        public string? MaThamChieuChuyenKhoan { get; set; }
        public string TrangThai { get; set; } = "PAID";
        public string? MaCaLamViec { get; set; }
        public string MaThaoTac { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime ThoiDiem { get; set; } = DateTime.UtcNow;
    }

    // Đơn hàng thống nhất giữa POS và WEB
    public class PosOrder
    {
        public string MaDonHang { get; set; } = Guid.NewGuid().ToString("N");
        public string MaDonHienThi { get; set; } = string.Empty;
        public string Source { get; set; } = "POS";
        public string FulfillmentType { get; set; } = "AT_TABLE";
        public string? SoBan { get; set; }
        public string? TenNguoiNhan { get; set; }
        public string? DienThoaiNguoiNhan { get; set; }
        public string? DiaChiGiaoDaChot { get; set; }
        public decimal? KhoangCachKm { get; set; }
        public decimal PhiGiaoHang { get; set; } = 0;
        public string? MaCaLamViec { get; set; }
        public string? MaNhanVien { get; set; }
        public List<OrderLineItem> LineItems { get; set; } = new();
        public decimal TongTienHang { get; set; }
        public decimal TongThanhToan { get; set; }
        public decimal SoTienDaThanhToan { get; set; }
        public string OrderStatus { get; set; } = "PENDING_CONFIRMATION";
        public string TrangThaiThanhToan { get; set; } = "UNPAID";
        public decimal KhoanPhaiHoan { get; set; } = 0;
        public string? LyDoTuChoiHoacHuy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? AcceptedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int Version { get; set; } = 1;
        public List<PosPaymentRecord> Payments { get; set; } = new();

        // Số dư còn phải thu của đơn hàng
        public decimal SoDuConThu => Math.Max(0, TongThanhToan - SoTienDaThanhToan);
    }

    // Thông tin tồn kho nguyên liệu
    public class StockItem
    {
        public string MaNguyenLieu { get; set; } = null!;
        public string TenNguyenLieu { get; set; } = null!;
        public string DonViCoSo { get; set; } = null!;
        public decimal TonThucTe { get; set; }
        public decimal TongGiuCho { get; set; }
        public decimal TonAnToan { get; set; }

        // Tồn khả dụng theo BR-03
        public decimal TonKhaDung => TonThucTe - TongGiuCho - TonAnToan;
    }

    // Yêu cầu cấu hình món khi tạo dòng đơn
    public class ConfigureItemRequest
    {
        public string MaMon { get; set; } = null!;
        public string Size { get; set; } = "M";
        public bool IsHot { get; set; }
        public int MucDuongPercent { get; set; } = 100;
        public int MucDaPercent { get; set; } = 100;
        public List<ToppingSelectionDto> Toppings { get; set; } = new();
        public int SoLy { get; set; } = 1;
        public string? GhiChu { get; set; }
    }

    // DTO chọn topping
    public class ToppingSelectionDto
    {
        public string MaTopping { get; set; } = null!;
        public int SoPhan { get; set; } = 1;
    }

    // Yêu cầu tạo đơn POS tại quầy
    public class CreatePosOrderRequest
    {
        public string MaCaLamViec { get; set; } = null!;
        public string FulfillmentType { get; set; } = "AT_TABLE";
        public string? SoBan { get; set; }
        public string? TenNguoiNhan { get; set; }
        public string? DienThoaiNguoiNhan { get; set; }
        public string? DiaChiGiaoDaChot { get; set; }
        public decimal? KhoangCachKm { get; set; }
        public decimal PhiGiaoHang { get; set; } = 0;
        public string MaThaoTac { get; set; } = Guid.NewGuid().ToString("N");
        public List<ConfigureItemRequest> Items { get; set; } = new();
    }

    // Yêu cầu tiếp nhận đơn hàng
    public class AcceptOrderRequest
    {
        public string MaDonHang { get; set; } = null!;
        public string MaCaLamViec { get; set; } = null!;
    }

    // Yêu cầu từ chối đơn hàng
    public class RejectOrderRequest
    {
        public string MaDonHang { get; set; } = null!;
        public string MaCaLamViec { get; set; } = null!;
        public string LyDo { get; set; } = null!;
    }

    // Yêu cầu bắt đầu pha từng phần số ly
    public class StartLinePrepRequest
    {
        public string MaDonHang { get; set; } = null!;
        public string MaDong { get; set; } = null!;
        public int SoLyBatDau { get; set; } = 1;
        public string MaCaLamViec { get; set; } = null!;
    }

    // Yêu cầu cập nhật pha xong
    public class MarkLineReadyRequest
    {
        public string MaDonHang { get; set; } = null!;
        public string MaDong { get; set; } = null!;
        public int SoLyPhaXong { get; set; } = 1;
        public string MaCaLamViec { get; set; } = null!;
    }

    // Yêu cầu bàn giao phục vụ
    public class ServeOrderRequest
    {
        public string MaDonHang { get; set; } = null!;
        public string MaCaLamViec { get; set; } = null!;
    }

    // Yêu cầu hoàn tất đơn hàng
    public class CompleteOrderRequest
    {
        public string MaDonHang { get; set; } = null!;
        public string MaCaLamViec { get; set; } = null!;
    }

    // Yêu cầu hủy phần món chưa pha (POS-04)
    public class CancelLineItemRequest
    {
        public string MaDonHang { get; set; } = null!;
        public string MaDong { get; set; } = null!;
        public int SoLyHuy { get; set; } = 1;
        public string LyDo { get; set; } = null!;
        public string MaCaLamViec { get; set; } = null!;
    }

    // Yêu cầu thanh toán đơn hàng (POS-05)
    public class ProcessPaymentRequest
    {
        public string MaDonHang { get; set; } = null!;
        public string PhuongThuc { get; set; } = "CASH";
        public decimal SoTien { get; set; }
        public decimal? TienKhachDua { get; set; }
        public string? MaThamChieuChuyenKhoan { get; set; }
        public string MaCaLamViec { get; set; } = null!;
        public string MaThaoTac { get; set; } = Guid.NewGuid().ToString("N");
    }
}
