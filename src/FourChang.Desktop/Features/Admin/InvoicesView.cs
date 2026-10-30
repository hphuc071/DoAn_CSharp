using FourChang.Presentation.Shared.PreviewData;
namespace FourChang.Desktop.Features.Admin;
public static class InvoicesView
{
    public static Control Create()=>ManagementPage.Create("Lịch sử hóa đơn và điều chỉnh",DemoCatalog.Invoices,"Bản gốc bất biến. Điều chỉnh phải có nguồn, lý do và số tiền chênh lệch; đối soát tiền không bằng cách sửa chứng từ gốc.",
        ("Xem hóa đơn",["Số hóa đơn"]),("Phát hành điều chỉnh",["Hóa đơn gốc","Nguồn hủy / hoàn hợp lệ","Lý do","Giá trị điều chỉnh"]),("Đối chiếu thu / hoàn",["Đơn nguồn","Khoản thu đã xác minh","Khoản chi đã xác minh"]),("In lại bản sao",["Hóa đơn","Máy in","Số bản"]),("Xuất PDF",["Hóa đơn","Tên tệp"]));
}
