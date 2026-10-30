using FourChang.Desktop.Controls;
using FourChang.Desktop.Preview;
using FourChang.Presentation.Shared.Models;
namespace FourChang.Desktop.Features.Admin;
public static class RecipesView
{
    public static Control Create()=>Ui.Panel("BOM Latte M · Phiên bản 02",
        Ui.Filters(Ui.Select("Latte","Cà phê sữa","Matcha latte","Kem cheese"),Ui.Select("Size M","Size S","Size L"),Ui.Select("Lạnh","Nóng"),Ui.Select("50% đường / 50% đá","0% đường / 0% đá","100% đường / 100% đá")),
        Ui.Grid(new PreviewTable(["Nguyên liệu","Đơn vị","Định mức / ly","Hao hụt chuẩn","Phiên bản"],[["Hạt cà phê","g","18","0%","02"],["Sữa tươi","ml","150","0%","02"],["Siro đường","ml","10","0%","02"],["Đá","g","120","0%","02"]]),240),
        Ui.Row(Ui.Button("Thêm / Sửa dòng",()=>PreviewDialog.ShowMessage(null,"Dòng định mức","Định mức dùng đơn vị cơ sở, tổng hợp thành phần dùng chung và không cộng hao hụt hai lần.","Nguyên liệu","Số lượng","Đơn vị","Hao hụt chuẩn"),true),Ui.Button("Sao chép phiên bản",()=>PreviewDialog.ShowMessage(null,"Phiên bản BOM mới","Không sửa BOM snapshot của đơn cũ.","Ngày hiệu lực","Lý do thay đổi")),Ui.Button("Ánh xạ đường / đá",()=>PreviewDialog.ShowMessage(null,"Ánh xạ tùy chọn","Ghi chú tự do không tự đổi định mức hoặc giá; chỉ ánh xạ cấu trúc đã khai báo.","Mức đường","Mức đá","Định mức tương ứng")),Ui.Button("BOM topping",()=>PreviewDialog.ShowMessage(null,"Định mức topping","Cộng đúng số phần topping mỗi ly.","Topping","Số phần","Nguyên liệu và định mức"))),
        Ui.Row(Ui.Field("Số ly mô phỏng",Ui.Input("Số lượng","10",150)),Ui.Button("Mô phỏng nhu cầu",()=>PreviewDialog.ShowMessage(null,"Nhu cầu 10 Latte M","180 g cà phê, 1.500 ml sữa, 100 ml đường, 1.200 g đá. ")),Ui.Button("Công bố phiên bản"),Ui.Button("Ngừng / Xóa nháp")));
}
