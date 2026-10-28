using MocCafe.Desktop.Controls;
using MocCafe.Desktop.Preview;
using MocCafe.Presentation.Shared.Models;
using MocCafe.Presentation.Shared.PreviewData;
namespace MocCafe.Desktop.Features.Admin;
public static class InventoryView
{
    public static Control Create()=>Ui.Panel("Tồn kho và khả dụng",
        Ui.Kpis(("Nguyên liệu","24","Danh mục nguyên liệu"),("Sắp hết","4","Cần bổ sung"),("Hết khả dụng","1","Cold brew nền")),
        Ui.Grid(DemoCatalog.Inventory,230),
        Ui.Row(Ui.Button("Sổ kho",()=>PreviewDialog.ShowMessage(null,"Sổ kho Sữa tươi","PN0009 +5.000 ml; giữ chỗ WEB024 150 ml; tiêu hao POS017 −150 ml. ","Khoảng thời gian","Chứng từ / đơn nguồn")),Ui.Button("Món bị ảnh hưởng",()=>PreviewDialog.ShowMessage(null,"Ảnh hưởng thiếu nguyên liệu","Cold brew: Tạm hết ở biến thể có nhu cầu vượt khả dụng. Web và POS hiển thị cùng trạng thái.")),Ui.Button("Mở phiếu nhập"),Ui.Button("Mở kiểm kê")),
        Ui.Grid(new PreviewTable(["Món / biến thể","Nguyên liệu thiếu","Trạng thái Web","Trạng thái POS"],[["Cold brew M","Cold brew nền","Tạm hết","Tạm hết"],["Latte M","Không thiếu","Còn","Còn"]]),130));
}

