using FourChang.Desktop.Controls;
using FourChang.Desktop.DesignSystem;
using FourChang.Desktop.Preview;
namespace FourChang.Desktop.Features.Pos;
public static class ShiftView
{
    public static Control Create(string employeeName="Lan")=>Ui.Panel($"Ca sáng · {employeeName}",
        Ui.Kpis(("Mã ca","CA024","Máy POS01"),("Tiền đầu ca","500.000đ","08:00 · 28/10/2026"),("Tiền mặt dự kiến","1.340.000đ","Không cộng chuyển khoản")),
        Ui.Row(Ui.Field("Tên nhân viên",Ui.Input("Tên tự khai",employeeName)),Ui.Field("Mã máy",Ui.Input("TerminalId","POS01"))),
        Ui.Row(Ui.Button("Tạm khóa ca",()=>PreviewDialog.ShowMessage(null,"Tạm khóa ca","Khóa màn hình bán/hủy/thu. Tiếp tục đúng mã ca và máy; không mở quyền Admin.")),Ui.Button("Tiếp tục ca"),Ui.Button("Bàn giao ca",()=>PreviewDialog.ShowMessage(null,"Bàn giao ca","Cần Admin xác nhận; không sửa tên snapshot của giao dịch cũ.","Người nhận bàn giao","Lý do bàn giao","Giao dịch chưa rõ"))),
        Ui.Row(Ui.Field("Tiền thực đếm",Ui.Input("Nhập số tiền","1340000")),Ui.Field("Lý do chênh lệch",Ui.Input("Nhập khi có chênh lệch"))),
        Ui.Row(Ui.Button("Xem biên bản chốt ca",()=>PreviewDialog.ShowMessage(null,"Chốt ca CA024","Đầu ca + thu CASH − khoản chi/hoàn CASH đã duyệt. Khoản CK chưa rõ phải bàn giao, không tính vào tiền mặt.","Tiền mặt thực đếm","Lý do chênh lệch","Ghi chú bàn giao"),true),Ui.Button("Khôi phục ca sau sự cố",()=>PreviewDialog.ShowMessage(null,"Khôi phục ca","Tra ca hiện có theo máy, không mở ca trùng. "))));
}
