using MocCafe.Desktop.Controls;
using MocCafe.Desktop.Preview;
using MocCafe.Presentation.Shared.PreviewData;
namespace MocCafe.Desktop.Features.Admin;
public static class OrdersManagementView
{
    public static Control Create()=>Ui.Panel("Giám sát đơn đa kênh",
        Ui.Filters(Ui.Input("Mã đơn / tên khách / ca…","",300),Ui.Select("Mọi nguồn","Web","POS"),Ui.Select("Mọi hình thức","Tại bàn","Mang đi","Giao hàng"),Ui.Select("Mọi trạng thái","Chờ lâu","Giao thất bại","Chưa rõ khoản thu")),
        Ui.Grid(DemoCatalog.Orders,280),
        Ui.Row(Ui.Button("Chi tiết / Truy vết",()=>PreviewDialog.ShowMessage(null,"WEB024 · Chuỗi xử lý","Cấu hình món → BOM snapshot → giữ chỗ → thu xác minh → pha → bàn giao. Truy vết Lan / CA024 / POS01."),true),Ui.Button("Đổi địa chỉ có xác nhận",()=>PreviewDialog.ShowMessage(null,"Đổi địa chỉ đơn đã nhận","Ghi đề nghị và chấp thuận của khách; không tự phụ thu hoặc sửa snapshot âm thầm.","Địa chỉ mới","Khách xác nhận","Phương án phí","Lý do")),Ui.Button("Xử lý sự cố",()=>PreviewDialog.ShowMessage(null,"Giao thất bại sau pha","Chọn giao lại hoặc FULFILLMENT_FAILED kèm kết quả giải quyết. Không giả COMPLETED, không hủy như trước pha và không tự hoàn nguyên liệu đã dùng.","Loại sự cố","Lý do","Chứng cứ","Phương án giao lại / kết thúc","Nghĩa vụ cần hoàn"))),
        Ui.Row(Ui.Button("Tiếp nhận hợp lệ"),Ui.Button("Hủy phần chưa pha",()=>Features.Pos.CancelView.Show(null),false,true),Ui.Button("Mở hồ sơ hoàn tiền")));
}
