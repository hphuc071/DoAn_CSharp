using FourChang.Desktop.Controls;
using FourChang.Desktop.Preview;
using FourChang.Presentation.Shared.PreviewData;
namespace FourChang.Desktop.Features.Admin;
public static class ReportsView
{
    public static Control Create()=>Ui.Tabs(
        ("Dashboard",DashboardView.Create()),
        ("Thống kê / Xuất báo cáo",Ui.Panel("Báo cáo theo kỳ",Ui.Filters(Ui.Input("Từ ngày","28/10/2026",150),Ui.Input("Đến ngày","28/10/2026",150),Ui.Select("Mọi nguồn","Web","POS"),Ui.Select("Mọi ca","CA024 / Lan","CA023 / An")),Ui.Kpis(("Tiền mặt thực thu","3.200.000đ","Dòng tiền"),("Chuyển khoản thực thu","5.400.000đ","Đã xác minh"),("Hoàn thành công","150.000đ","Theo thời điểm chi")),new RevenueChart(),Ui.Row(Ui.Button("Tỷ lệ đơn / Hủy / Hoàn"),Ui.Button("Top món / Kho sắp hết"),Ui.Button("Xuất Excel",null,true),Ui.Button("Xuất PDF")))),
        ("Nhật ký hệ thống",Ui.Panel("Audit Log",Ui.Filters(Ui.Input("Đối tượng / correlation ID","",260),Ui.Select("Mọi người / ca","Admin","Lan / CA024 / POS01"),Ui.Select("Mọi thao tác","Hủy món","Chi hoàn","Thay giá","Ghi sổ kho","Phân quyền")),Ui.Grid(DemoCatalog.Audit,260),Ui.Row(Ui.Button("Xem trước / Sau",()=>PreviewDialog.ShowMessage(null,"Chi tiết nhật ký","Tác nhân: Lan · tên tự khai / CA024 / POS01. Đối tượng WEB019. Trước: CONFIRMED. Sau: CANCELLED. Lý do: Khách đổi ý. CorrelationId: DEMO-0024."),true),Ui.Button("Xuất nhật ký"),Ui.Button("Kiểm tra quyền audit",()=>PreviewDialog.ShowMessage(null,"Quyền audit độc lập","Có quyền xem báo cáo chưa đồng nghĩa quyền đọc/xuất nhật ký. Trường bí mật phải được che."))))));
}
