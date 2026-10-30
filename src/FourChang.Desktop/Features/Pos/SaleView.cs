using FourChang.Desktop.Controls;
using FourChang.Desktop.DesignSystem;
using FourChang.Desktop.Preview;
using FourChang.Presentation.Shared.Models;
using FourChang.Presentation.Shared.PreviewData;
namespace FourChang.Desktop.Features.Pos;

public static class SaleView
{
    public static Control Create()
    {
        var catalog=new TableLayoutPanel { Dock=DockStyle.Top,AutoSize=true,ColumnCount=1 };
        catalog.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        var search=Ui.Input("Tìm món theo tên…","",300);var category=Ui.Select("Tất cả","Cà phê","Trà","Đá xay");
        catalog.Controls.Add(Ui.Row(search,category));
        var products=new TableLayoutPanel { Dock=DockStyle.Top,Height=430,ColumnCount=3,RowCount=2,Margin=new Padding(0,0,0,18) };
        for(int i=0;i<3;i++)products.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.33f));for(int i=0;i<2;i++)products.RowStyles.Add(new RowStyle(SizeType.Percent,50));
        foreach(var item in DemoCatalog.Products)
        {
            var card=new SurfacePanel { Dock=DockStyle.Fill,Margin=new Padding(0,0,12,12),Padding=new Padding(0) };
            var image=new ProductImage { ImageIndex=item.ImageIndex,Dock=DockStyle.Top,Height=130 };
            var caption=new Label { Text=item.Name+"\n"+MoneyDisplay.Format(item.Price)+(item.Available?"":" · Tạm hết"),Dock=DockStyle.Fill,Font=CafeTheme.Strong(10),Padding=new Padding(0,4,0,0),ForeColor=item.Available?CafeTheme.Ink:CafeTheme.Muted };
            var add=Ui.Button("+",()=>PreviewDialog.ShowMessage(card.FindForm(),"Tùy chỉnh "+item.Name,"Chọn cấu hình trước khi thêm vào đơn.","Kích cỡ S/M/L","Nóng / lạnh","Mức đường","Mức đá","Topping và số phần","Ghi chú pha chế"),true);add.Enabled=item.Available;add.MinimumSize=new Size(30,30);add.Size=new Size(32,30);add.AutoSize=false;add.Padding=Padding.Empty;add.Margin=Padding.Empty;add.Dock=DockStyle.Right;add.Width=36;
            var footer=new Panel{Dock=DockStyle.Fill,Padding=new Padding(12,7,10,9),BackColor=CafeTheme.Surface};footer.Controls.Add(caption);footer.Controls.Add(add);
            card.Controls.Add(footer);card.Controls.Add(image);products.Controls.Add(card);
        }
        catalog.Controls.Add(products);
        var online=Ui.Panel("Đơn hàng online mới",Ui.Row(Ui.Button("Tiếp nhận",()=>PreviewDialog.ShowMessage(null,"Tiếp nhận WEB024","Đơn Web đã xác minh đủ tiền. "),true),Ui.Button("Chi tiết đơn",()=>PreviewDialog.ShowMessage(null,"WEB024 · Chi tiết pha chế","Latte M × 1 · Lạnh · 50% đường · 50% đá · Kem cheese. Tại giai đoạn xác nhận chưa tiêu hao thực tế."))));catalog.Controls.Add(online);
        var order=Ui.Panel("Đơn #POS018 · Mang đi",
            Ui.Label("Latte M × 1                         45.000đ",10,true),Ui.Label("Lạnh · 50% đường · 50% đá",9,false,CafeTheme.Muted),
            Ui.Row(Ui.Button("Sửa tùy chọn"),Ui.Button("Hủy món",()=>CancelView.Show(null),false,true)),
            Ui.Label("Bạc xỉu M × 1                     39.000đ",10,true),Ui.Label("Lạnh · 50% đường · 50% đá",9,false,CafeTheme.Muted),
            Ui.Row(Ui.Select("Mang đi","Tại bàn"),Ui.Button("Chọn bàn")),Ui.Field("Ghi chú đơn",Ui.Input("Lời nhắn cho quầy…","",250)),
            Ui.Label("Tổng cộng · 84.000đ",15,true),
            Ui.Row(Ui.Button("Tạo đơn tại quầy",null,true),Ui.Button("Xóa giỏ nháp")));
        return Ui.Columns(catalog,order,35);
    }
}

