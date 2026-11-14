using FourChang.Presentation.Shared.Contracts.Customer;
using FourChang.Presentation.Shared.Contracts.Staff;
namespace FourChang.Desktop.Staff.Sales;

/// <summary>Một dòng nháp có cấu hình và giá vừa đọc từ API; chưa phải đơn đã lưu hoặc khoản thu.</summary>
public sealed record PosDraftLine(Guid Key, long ProductId, string TenMon, MenuVariantItem Variant,
    MenuBrewOption Brew, int Quantity, string Note, List<OrderToppingInput> Toppings)
{
    public string CauHinh => $"{Variant.KichCo} · {(Variant.NhietDo == "HOT" ? "Nóng" : "Lạnh")} · Đường {Brew.PhanTramDuong}%" + (Variant.NhietDo == "HOT" ? "" : $" · Đá {Brew.PhanTramDa}%") +
        (Toppings.Count == 0 ? "" : " · " + string.Join(", ", Toppings.Select(t => Variant.Toppings.Single(x => x.MaMonThem == t.MaMonThem).TenMonThem + " ×" + t.SoPhanMoiLy)));
    public decimal DonGia => Variant.GiaBan + Toppings.Sum(t => Variant.Toppings.Single(x => x.MaMonThem == t.MaMonThem).GiaMoiPhan * t.SoPhanMoiLy);
    public decimal ThanhTien => checked(DonGia * Quantity);
    /// <summary>Chỉ gửi ID/cấu hình, không gửi giá client làm giá chuẩn.</summary>
    public OrderItemInput Input() => new(Variant.MaCauHinhMon, Quantity, Brew.PhanTramDuong, Brew.PhanTramDa, Note, Toppings);
}

/// <summary>Giữ nháp trong phiên ca khi chuyển trang; bắt đầu ca khác không dùng lại giỏ của người trước.</summary>
public sealed class PosDraftCart
{
    private static long? currentShift;
    private static PosDraftCart current = new();
    public List<PosDraftLine> Lines { get; } = [];
    public PosCheckoutInput? Pending { get; set; }
    public PosCheckoutReceipt? AwaitingTransfer { get; set; }
    public decimal Total => Lines.Sum(x => x.ThanhTien);
    /// <summary>Lấy giỏ của đúng ca; chỉ đổi ca mới tạo giỏ khác.</summary>
    public static PosDraftCart ForShift(long? shift)
    {
        if (currentShift != shift) { currentShift = shift; current = new(); }
        return current;
    }
    /// <summary>Thêm hoặc sửa đúng khóa dòng; hai cấu hình cùng món vẫn là hai dòng độc lập.</summary>
    public void Save(PosDraftLine line)
    {
        if (Pending is not null || AwaitingTransfer is not null) throw new InvalidOperationException("Đơn đang xác nhận thanh toán; chưa được sửa giỏ.");
        if (line.Quantity is < 1 or > 99 || line.Note.Length > 500 || !line.Variant.Available || !line.Variant.TuyChonPhaChe.Contains(line.Brew))
            throw new InvalidOperationException("Cấu hình/số lượng không hợp lệ.");
        if (line.Toppings.Count > 10 || line.Toppings.Select(t => t.MaMonThem).Distinct().Count() != line.Toppings.Count || line.Toppings.Any(t =>
            !line.Variant.Toppings.Any(x => x.MaMonThem == t.MaMonThem && x.Available && t.SoPhanMoiLy >= 1 && t.SoPhanMoiLy <= x.GioiHanMoiLy)))
            throw new InvalidOperationException("Topping không còn hợp lệ.");
        var index = Lines.FindIndex(x => x.Key == line.Key);
        if (index < 0) { if (Lines.Count >= 50) throw new InvalidOperationException("Tối đa 50 dòng món."); Lines.Add(line); }
        else Lines[index] = line;
    }
}
