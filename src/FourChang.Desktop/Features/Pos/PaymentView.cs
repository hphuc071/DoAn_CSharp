using FourChang.Desktop.Controls;
using FourChang.Desktop.DesignSystem;
using FourChang.Desktop.Preview;
using FourChang.Presentation.Shared.PreviewData;
namespace FourChang.Desktop.Features.Pos;
public static class PaymentView
{
    public static Control Create()
    {
        var cash=Ui.Panel("Thu tiền mặt · POS018",Ui.Label("Latte M 45.000đ + Bạc xỉu M 39.000đ",11),Ui.Label("Cần thu 84.000đ",24,true),Ui.Field("Khách đưa",Ui.Input("Số tiền khách đưa","100000",360)),Ui.Label("Tiền thừa: 16.000đ",12,true),Ui.Button("Xem xác nhận thu tiền",()=>PreviewDialog.ShowMessage(null,"Thu tiền mặt","Khách đưa 100.000đ, nghĩa vụ 84.000đ, trả thừa 16.000đ. "),true));
        var bank=Ui.Panel("Chuyển khoản VietQR",Ui.Label("Đơn POS018 · Cần thu 84.000đ",18,true),Ui.Field("Trạng thái thanh toán",Ui.Select("Đang chờ xác minh","Đã xác minh đủ","Thiếu tiền","Thừa tiền","Hết hạn","Đối soát chưa rõ")),Ui.Button("Kiểm tra trạng thái",()=>PreviewDialog.ShowMessage(null,"Kiểm tra chuyển khoản","Không dùng ảnh chụp hoặc việc bấm Đã chuyển để xác nhận thu. Chưa tích hợp VietQR thật.")),Ui.Button("Đổi phương thức",()=>PreviewDialog.ShowMessage(null,"Đổi phương thức trước thu","Chỉ khi xác minh yêu cầu cũ chưa nhận tiền; kết quả chưa rõ không tự chuyển sang CASH.")));
        var invoice=Ui.Panel("Hóa đơn và chứng từ",Ui.Grid(DemoCatalog.Invoices,200),Ui.Row(Ui.Button("Lập hóa đơn",()=>PreviewDialog.ShowMessage(null,"Phát hành hóa đơn","Cần đơn đã tiếp nhận, không còn dòng PENDING và thu đủ. Đơn Web đã trả trước không tạo khoản thu mới.","Thông tin người mua"),true),Ui.Button("Xem trước",()=>PreviewDialog.ShowMessage(null,"HÓA ĐƠN HD000128","Latte M × 1: 45.000đ\nBạc xỉu M × 1: 39.000đ\nTổng cộng: 84.000đ\nTiền mặt 100.000đ · Trả thừa 16.000đ\n\n")),Ui.Button("In / In lại"),Ui.Button("Xuất PDF")),Ui.Button("Lỗi phát hành sau khi đã thu",()=>PreviewDialog.ShowMessage(null,"Chưa phát hành được hóa đơn","Khoản thu đã xác minh vẫn được giữ. Thử lại phát hành theo cùng OrderId; không yêu cầu khách trả tiền lần nữa.")));
        return Ui.Tabs(("Tiền mặt",cash),("Chuyển khoản",bank),("Hóa đơn / In / Xuất",invoice));
    }
}
