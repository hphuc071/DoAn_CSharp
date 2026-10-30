using FourChang.Desktop.Controls;
using FourChang.Desktop.Preview;
using FourChang.Presentation.Shared.PreviewData;
namespace FourChang.Desktop.Features.Admin;
public static class StockDocumentsView
{
    public static Control Create()=>Ui.Tabs(
        ("Danh sách phiếu",ManagementPage.Create("Chứng từ kho",DemoCatalog.StockDocuments,"DRAFT chưa đổi tồn. POSTED bất biến; sửa sai bằng phiếu đảo/điều chỉnh có nguồn.",("Chi tiết chứng từ",["Mã phiếu"]),("Đảo chứng từ",["Chứng từ nguồn","Lý do đảo","Phần tồn / giữ chỗ liên quan"]),("Xóa nháp",["Mã phiếu nháp","Lý do"]))),
        ("Nhập kho",Ui.Panel("Phiếu nhập PN0010 · DRAFT",Ui.Row(Ui.Field("Nhà cung cấp",Ui.Input("Nguồn nhập","Nhà cung cấp A")),Ui.Field("Ngày nhập",Ui.Input("Ngày","28/10/2026"))),Ui.Grid(DemoCatalog.Ingredients,220),Ui.Row(Ui.Button("Thêm dòng nhập",()=>PreviewDialog.ShowMessage(null,"Dòng nhập kho",null,"Nguyên liệu","Số lượng","Đơn vị","Giá nhập nếu dùng")),Ui.Button("Lưu nháp"),Ui.Button("Xem ghi sổ phiếu nhập",()=>PreviewDialog.ShowMessage(null,"Ghi sổ phiếu nhập","Kiểm tra toàn phiếu, quyền và version. Lỗi một dòng thì không ghi sổ dòng nào; "),true)))),
        ("Xuất kho",Ui.Panel("Phiếu xuất PX0004 · DRAFT",Ui.Field("Mục đích",Ui.Select("Hư hỏng","Hết hạn","Nội bộ")),Ui.Field("Lý do",Ui.Input("Bắt buộc nhập lý do","Hư hỏng khi bảo quản",600)),Ui.Grid(DemoCatalog.Ingredients,200),Ui.Row(Ui.Button("Thêm dòng xuất",()=>PreviewDialog.ShowMessage(null,"Dòng xuất kho",null,"Nguyên liệu","Số lượng","Đơn vị","Diễn giải")),Ui.Button("Lưu nháp"),Ui.Button("Xem ghi sổ xuất",()=>PreviewDialog.ShowMessage(null,"Ghi sổ phiếu xuất","Không xuất lấn phần giữ chỗ, không trùng tiêu hao BOM tự động. "),true)))),
        ("Kiểm kê / Điều chỉnh",Ui.Panel("Kiểm kê KK0005 · DRAFT",Ui.Grid(DemoCatalog.Inventory,230),Ui.Row(Ui.Button("Nhập số thực đếm",()=>PreviewDialog.ShowMessage(null,"Thực đếm nguyên liệu",null,"Nguyên liệu","Số sổ đã đối chiếu","Số thực đếm","Lý do chênh lệch")),Ui.Button("Xem chênh lệch"),Ui.Button("Duyệt / Chốt kiểm kê",()=>PreviewDialog.ShowMessage(null,"Chốt kiểm kê","Chênh lệch = thực đếm − tồn sổ đã đối chiếu. Chênh lệch 0 không tạo biến động giả."),true)))));
}
