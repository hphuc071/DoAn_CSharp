using MocCafe.Desktop.Controls;
using MocCafe.Desktop.Preview;
using MocCafe.Presentation.Shared.PreviewData;
namespace MocCafe.Desktop.Features.Pos;
public static class OrdersView
{
    public static Control Create()=>Ui.Panel("Hàng đợi đơn hàng",
        Ui.Filters(Ui.Input("Tìm mã đơn, bàn hoặc khách…","",280),Ui.Select("Mọi nguồn","Website","Tại quầy"),Ui.Select("Mọi trạng thái","Chờ xác nhận","Đã xác nhận","Đang pha chế","Sẵn sàng","Đang giao")),
        Ui.Grid(DemoCatalog.Orders,280),
        Ui.Row(Ui.Button("Tiếp nhận đơn",()=>PreviewDialog.ShowMessage(null,"Tiếp nhận WEB024","Đã xác minh đủ tiền; tiếp nhận chưa trừ nguyên liệu. "),true),Ui.Button("Từ chối",()=>PreviewDialog.ShowMessage(null,"Từ chối đơn chưa pha","Giải phóng giữ chỗ và lập phải hoàn nếu đã thu; bắt buộc lý do.","Lý do từ chối"),false,true),Ui.Button("Chi tiết pha chế",()=>PreviewDialog.ShowMessage(null,"Chi tiết từng phần món","Latte M × 2 · 1 ly CONFIRMED, 1 ly PREPARING. Cấu hình: lạnh, 50% đường, 50% đá, không topping. Ghi chú: giao cùng lúc."))),
        Ui.Row(Ui.Button("Bắt đầu pha từng phần",()=>PreviewDialog.ShowMessage(null,"Bắt đầu pha","Chỉ chuyển phần CONFIRMED; tiêu hao theo BOM snapshot đúng một lần tại thời điểm bắt đầu. ","Số ly bắt đầu pha")),Ui.Button("Sẵn sàng"),Ui.Button("Bàn giao / Đang giao"),Ui.Button("Hoàn tất"),Ui.Button("Hủy phần chưa pha",()=>CancelView.Show(null),false,true)),
        Ui.Row(Ui.Button("Xem xung đột trạng thái",()=>PreviewDialog.ShowMessage(null,"Dữ liệu đã thay đổi","Khách vừa hủy hoặc máy khác đã tiếp nhận. Tải version mới, không xử lý trùng.")),Ui.Button("Ghi nhận giao thất bại",()=>PreviewDialog.ShowMessage(null,"Sự cố giao hàng","Giữ đơn chưa hoàn tất, chuyển quản lý xử lý; không tự hoàn nguyên liệu đã pha.","Lý do giao thất bại","Chứng cứ hoặc ghi chú"))));
}
