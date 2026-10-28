using MocCafe.Desktop.Controls;
using MocCafe.Desktop.Preview;
using MocCafe.Presentation.Shared.PreviewData;
namespace MocCafe.Desktop.Features.Admin;
public static class RefundsView
{
    public static Control Create()=>Ui.Panel("Hồ sơ hoàn tiền",
        Ui.Filters(Ui.Input("Tìm hồ sơ hoặc đơn…","",300),Ui.Select("Mọi trạng thái","REQUESTED","APPROVED","PROCESSING","SUCCEEDED","FAILED","RECONCILING")),
        Ui.Grid(DemoCatalog.Refunds,210),
        Ui.Kpis(("HT0008 cần hoàn","45.000đ","Thu đã xác minh 45.000đ"),("Đã chi thành công","0đ","Chưa có chứng cứ chi"),("Khoản đang xử lý","0đ","Chưa giữ hạn mức")),
        Ui.Row(Ui.Button("Kiểm tra và duyệt",()=>PreviewDialog.ShowMessage(null,"Duyệt HT0008","Chỉ duyệt trong hạn mức còn hoàn; khoản lớn áp dụng phân tách người tạo/người duyệt. Chưa chi thì chưa giảm tiền đã thu.","Người nhận","Ngân hàng","Tài khoản nhận","Số tiền","Lý do","Người duyệt"),true),Ui.Button("Ghi nhận thực hiện chi",()=>PreviewDialog.ShowMessage(null,"Thực hiện chi hoàn","Nhập chứng cứ không tự đồng nghĩa SUCCEEDED.","Mã giao dịch ngân hàng","Thời điểm chi","Số tiền chi","Người nhận","Chứng cứ")),Ui.Button("Xác minh kết quả chi",()=>PreviewDialog.ShowMessage(null,"Xác minh chi thực tế","Chỉ sau khi kiểm tra chứng cứ tin cậy và chống trùng mới xác nhận SUCCEEDED. ","Nguồn đối soát","Kết quả xác minh","Người xác minh")),Ui.Button("Từ chối / Bổ sung",()=>PreviewDialog.ShowMessage(null,"Hồ sơ cần bổ sung","Không xóa nghĩa vụ hoàn thực tế chỉ vì lập sai hồ sơ.","Lý do từ chối hoặc bổ sung"))),
        Ui.Row(Ui.Button("Đối soát thiếu / thừa / muộn",()=>PreviewDialog.ShowMessage(null,"Đối soát khoản thu","Tiền đến muộn của đơn hết hạn không tự khôi phục đơn. Ghép bằng chứng đúng trước xác định khoản hoàn.","Giao dịch nguồn","Tham chiếu đơn","Số tiền thực nhận","Chứng cứ ghép")),Ui.Button("Xem timeout / FAILED",()=>PreviewDialog.ShowMessage(null,"Kết quả chi chưa rõ","RECONCILING khi timeout; không tạo chi mới. FAILED chỉ khi thất bại chắc chắn và nghĩa vụ hoàn còn tồn tại."))));
}
