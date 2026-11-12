namespace FourChang.Web.Services;
/// <summary>Nhãn chỉ phục vụ giao diện; giữ nguyên mã trạng thái trong request, database và snapshot.</summary>
public static class CommerceLabels
{
    /// <summary>Chuyển trạng thái đơn, thanh toán và hóa đơn sang chữ dễ hiểu; mã lạ giữ nguyên để không đoán.</summary>
    public static string State(string value) => value switch
    {
        "DRAFT"=>"Bản nháp", "PENDING_PAYMENT"=>"Chờ thanh toán", "PENDING_CONFIRMATION"=>"Chờ xác nhận",
        "ACCEPTED"=>"Đã xác nhận", "PREPARING"=>"Đang pha chế", "READY"=>"Sẵn sàng nhận", "DELIVERING"=>"Đang giao",
        "COMPLETED"=>"Hoàn tất", "CANCELLED"=>"Đã hủy", "REJECTED"=>"Đã từ chối", "EXPIRED"=>"Đã hết hạn",
        "DELIVERY_FAILED"=>"Giao không thành công", "VERIFIED"=>"Đã xác nhận", "PARTIAL"=>"Chưa đủ tiền", "PENDING"=>"Đang chờ",
        "RECONCILING"=>"Đang đối soát", "FAILED"=>"Chưa thành công", "ISSUED"=>"Đã phát hành", "ORIGINAL"=>"Hóa đơn gốc",
        "ADJUSTMENT"=>"Điều chỉnh", "REQUESTED"=>"Đã yêu cầu", "APPROVED"=>"Đã duyệt", "PROCESSING"=>"Đang xử lý",
        "PARTIALLY_REFUNDED"=>"Đã hoàn một phần", "REFUNDED"=>"Đã hoàn tiền", _=>value
    };
    /// <summary>Giữ đúng hình thức nhận đã chốt; không suy trạng thái giao hàng từ cách trình bày.</summary>
    public static string Fulfillment(string value)=>value switch{"TAKEAWAY"=>"Mang đi","DINE_IN"=>"Tại bàn","DELIVERY"=>"Giao tận nơi",_=>value};
    /// <summary>Hiển thị nóng/lạnh từ cấu hình đã lưu, không thay tùy chọn pha chế.</summary>
    public static string Temperature(string value)=>value switch{"HOT"=>"Nóng","ICED"=>"Lạnh",_=>value};
}
