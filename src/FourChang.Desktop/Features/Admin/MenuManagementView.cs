using FourChang.Desktop.Controls;
using FourChang.Presentation.Shared.Models;
using FourChang.Presentation.Shared.PreviewData;
namespace FourChang.Desktop.Features.Admin;
public static class MenuManagementView
{
    public static Control Create()=>Ui.Tabs(
        ("Món và biến thể",ManagementPage.Create("Danh sách món",new PreviewTable(["Mã","Tên món","Danh mục","Giá S","Giá M","Giá L","Bán / kho"],DemoCatalog.Products.Select(p=>new[]{p.Id,p.Name,p.Category,(p.Price-5000).ToString("N0"),p.Price.ToString("N0"),(p.Price+5000).ToString("N0"),p.Available?"Đang bán":"Tạm hết"}).ToArray()),"Bật bán không vượt thiếu BOM/khả dụng. Giá mới không sửa snapshot đơn đã gửi.",("Thêm / Sửa món",["Tên món","Danh mục","Giá size S","Giá size M","Giá size L","Nóng / lạnh hỗ trợ","Mức đường và đá","Ảnh món"]),("Bật / Tắt bán",["Trạng thái bán","Ngày công bố","Lý do"]),("Xóa khi chưa dùng",["Mã món","Lý do xóa"]))),
        ("Danh mục",ManagementPage.Create("Nhóm thực đơn",new PreviewTable(["Mã","Danh mục","Thứ tự","Số món","Trạng thái"],[["DM01","Cà phê","1","4","Công bố"],["DM02","Trà","2","2","Công bố"],["DM03","Đá xay","3","0","Nháp"]]),"Ẩn danh mục còn món cần hiển thị tác động; chỉ xóa khi không có quan hệ phụ thuộc.",("Thêm / Sửa danh mục",["Mã danh mục","Tên danh mục","Mô tả","Thứ tự","Trạng thái"]),("Ẩn / Xóa",["Danh mục","Phương án chuyển món","Lý do"]))),
        ("Topping",ManagementPage.Create("Topping và giá",new PreviewTable(["Mã","Tên","Giá / phần","Giới hạn","BOM","Trạng thái"],[["TP01","Kem cheese","10.000đ","2 phần/ly","BOM-TP01","Còn"],["TP02","Trân châu","10.000đ","2 phần/ly","BOM-TP02","Còn"]]),"Mỗi topping gắn BOM theo phần; topping thiếu chỉ khóa lựa chọn đó. Bản đã bán chỉ ngừng, không xóa lịch sử.",("Thêm / Sửa topping",["Tên topping","Giá mỗi phần","Giới hạn số phần","Món được gắn","BOM topping","Trạng thái"]),("Ngừng topping",["Topping","Lý do"]))),
        ("Banner và nội dung",ManagementPage.Create("Nội dung trang chủ",new PreviewTable(["Tên banner","Vị trí","Bắt đầu","Kết thúc","Trạng thái"],[["Một ngày dễ chịu","Trang chủ","28/10/2026","28/11/2026","Công bố"],["Món mới Matcha","Thực đơn","28/10/2026","Không giới hạn","Nháp"]]),"Liên kết và ảnh phải hợp lệ. Nội dung lỗi không ghi đè bản đang công bố tốt.",("Thêm / Sửa banner",["Tên","Ảnh","Nội dung","Liên kết","Bắt đầu","Kết thúc","Trạng thái"]),("Gỡ công bố",["Banner","Lý do"]))));
}
