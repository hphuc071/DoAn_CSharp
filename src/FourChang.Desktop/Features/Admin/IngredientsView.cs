using FourChang.Presentation.Shared.PreviewData;
namespace FourChang.Desktop.Features.Admin;
public static class IngredientsView
{
    public static Control Create()=>ManagementPage.Create("Danh mục nguyên liệu",DemoCatalog.Ingredients,"Không sửa trực tiếp số tồn ở danh mục. Quy đổi/đơn vị đã phát sinh phải giữ nhất quán; thay tồn dùng chứng từ kho.",
        ("Thêm nguyên liệu",["Mã nguyên liệu","Tên","Đơn vị cơ sở","Hệ số quy đổi","Tồn an toàn","Ngưỡng cảnh báo"]),
        ("Sửa thông tin",["Tên nguyên liệu","Quy đổi áp dụng tương lai","Tồn an toàn","Ngưỡng cảnh báo"]),
        ("Ngừng sử dụng",["Nguyên liệu","Ảnh hưởng BOM / giữ chỗ","Lý do"]),
        ("Xóa chưa tham chiếu",["Mã nguyên liệu","Xác nhận không có BOM / sổ kho"]));
}
