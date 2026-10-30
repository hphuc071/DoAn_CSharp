using FourChang.Desktop.Controls;
using FourChang.Desktop.Preview;
using FourChang.Presentation.Shared.Models;
namespace FourChang.Desktop.Features.Pos;
public static class CancelView
{
    public static void Show(IWin32Window? owner)=>PreviewDialog.ShowMessage(owner,"Hủy món trước pha chế","Latte M còn 1 ly chưa pha. Dự kiến giảm nghĩa vụ 45.000đ. Đơn đã thu sẽ tạo khoản cần hoàn, không tự coi đã chi tiền.","Số ly chưa pha muốn hủy","Lý do hủy (bắt buộc)","Diễn giải thêm");
    public static Control Create()=>Ui.Panel("Hủy món trước pha chế",
        Ui.Grid(new PreviewTable(["Dòng món","Số ly","Đang pha","Có thể hủy","Thanh toán"],[["Latte M","2","1","1","Đã thu"],["Trà đào M","1","1","0","Đã thu"]]),150),
        Ui.Row(Ui.Field("Số ly hủy",Ui.Select("1 ly chưa pha")),Ui.Field("Lý do",Ui.Select("Khách đổi ý","Nhập sai món","Không thể phục vụ"))),
        Ui.Field("Diễn giải",Ui.Input("Bắt buộc ghi nhận lý do","Khách đổi ý trước khi bắt đầu pha",650)),
        Ui.Label("Giá trị hủy 45.000đ · Cần hoàn 45.000đ · Chưa chi hoàn",12,true),
        Ui.Row(Ui.Button("Xem xác nhận hủy",()=>Show(null),true),Ui.Button("Hủy thao tác")),
        Ui.Button("Xem lỗi cạnh tranh bắt đầu pha",()=>PreviewDialog.ShowMessage(null,"Không thể hủy","Dòng vừa chuyển PREPARING. Không hủy kho hoặc hoàn hai lần; cần tải trạng thái mới.")));
}
