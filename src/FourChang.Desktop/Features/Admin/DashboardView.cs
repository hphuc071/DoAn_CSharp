using FourChang.Desktop.Controls;
using FourChang.Presentation.Shared.Models;
using FourChang.Presentation.Shared.PreviewData;
namespace FourChang.Desktop.Features.Admin;
public static class DashboardView
{
    public static Control Create()
    {
        var page=new TableLayoutPanel { Dock=DockStyle.Top,AutoSize=true,ColumnCount=1 };page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        page.Controls.Add(Ui.Kpis(("Doanh thu","8.450.000đ","↑ 12% so với kỳ trước"),("Đơn hoàn tất","126","Đã phục vụ / giao thành công"),("Chờ hoàn tiền","3","Chưa xác minh chi"),("Nguyên liệu sắp hết","4","Cần kiểm tra khả dụng")));
        page.Controls.Add(Ui.Columns(Ui.Panel("Doanh thu theo giờ",Ui.Row(Ui.Label("Tiền mặt",9,true,FourChang.Desktop.DesignSystem.CafeTheme.Coffee),Ui.Label("    Chuyển khoản",9,true,Color.FromArgb(153,123,91))),new RevenueChart()),Ui.Panel("Món bán chạy",Ui.Grid(new PreviewTable(["Món","Số ly","Tỷ lệ"],[["Latte","48","22%"],["Cà phê sữa","36","16%"],["Trà đào","28","13%"]]),200)),34));
        page.Controls.Add(Ui.Columns(Ui.Panel("Cảnh báo nguyên liệu",Ui.Grid(new PreviewTable(["Nguyên liệu","Khả dụng","Trạng thái"],[["Sữa tươi","2.500 ml","Sắp hết"],["Hạt cà phê","4.700 g","Bình thường"],["Cold brew nền","0 ml","Tạm hết"]]),185)),Ui.Panel("Nhật ký gần đây",Ui.Grid(new PreviewTable(["Giờ","Người / Ca","Nội dung"],[["10:24","Admin","Điều chỉnh kho"],["10:18","Lan / CA024","Hủy món WEB019"],["10:05","Admin","Xác minh chi hoàn"]]),185)),48));
        return page;
    }
}

