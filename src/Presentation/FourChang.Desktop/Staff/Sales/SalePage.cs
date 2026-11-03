using FourChang.Desktop.Shared.Database;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Common;
namespace FourChang.Desktop.Staff.Sales;

public static class SalePage
{
    /// <summary>Bố cục POS theo mẫu: thực đơn bên trái, vùng đơn/thu ngân bên phải; giữ dữ liệu API thật.</summary>
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach(Control child in root.Controls){yield return child;foreach(var nested in Descendants(child))yield return nested;}
    }

    public static Control Create()
    {
        var search = Ui.Input("Tìm món yêu thích…", "", 270);
        var category = Ui.Select("Tất cả", "Cà phê", "Trà", "Đá xay");
        var products = new TableLayoutPanel { Dock=DockStyle.Top,Height=400,ColumnCount=3,RowCount=2,Margin=Padding.Empty };
        for(int column=0;column<3;column++)products.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/3));
        var error = Ui.Label("",9,false,CafeTheme.Danger); error.Visible=false;
        var menu = Ui.Panel("Thực đơn", Ui.Row(search,category), error,products);
        var online = DatabaseViews.Table("DonHang", false);
        foreach(var grid in Descendants(online).OfType<DataGridView>())grid.Height=150;
        
        var left = new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,Margin=Padding.Empty};
        left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        menu.Dock=DockStyle.Top;online.Dock=DockStyle.Top;
        left.Controls.Add(menu,0,0);left.Controls.Add(online,0,1);
        var selectedName = Ui.Label("Chọn món từ thực đơn",12,true);
        var selectedOptions = Ui.Label("Thông tin món được hiển thị tại đây.",9,false,CafeTheme.Muted);
        var selectedPrice = Ui.Label("—",16,true);
        var cash = new NumericUpDown { Maximum=1_000_000_000,ThousandsSeparator=true,Width=180,Font=CafeTheme.Body() };
        var change = Ui.Label("—",13,true,Color.FromArgb(77,112,58));
        var cashChoice=Ui.Button("Tiền mặt",()=>{},true);
        var bankChoice=Ui.Button("Chuyển khoản",()=>{});
        var detail = Ui.Panel("Đơn tại quầy",selectedName,selectedOptions,Ui.Label("Giá món",9,false,CafeTheme.Muted),selectedPrice,
            Ui.Field("Hình thức nhận",Ui.Select("Mang đi","Tại bàn")),
            Ui.Field("Ghi chú pha chế",Ui.Input("Size, nóng/lạnh, đường, đá, topping…","",280)),
            Ui.Label("Phương thức thanh toán",10,true),Ui.Row(cashChoice,bankChoice),
            Ui.Field("Khách đưa",cash),Ui.Field("Tiền thừa",change),
            Ui.Row(Ui.Unavailable("Thu tiền"),Ui.Unavailable("In hóa đơn")));
        // Các UC tạo đơn/thu tiền chưa triển khai không được giả vờ lưu hoặc xác minh thanh toán.
        decimal? price=null;
        cash.ValueChanged+=(_,_)=>change.Text=price is null?"—":MoneyDisplay.Format(Math.Max(0,cash.Value-price.Value));
        cashChoice.Click+=(_,_)=>{cash.Enabled=true;cashChoice.BackColor=CafeTheme.Coffee;cashChoice.ForeColor=Color.White;bankChoice.BackColor=CafeTheme.Surface;bankChoice.ForeColor=CafeTheme.Ink;};
        bankChoice.Click+=(_,_)=>{cash.Enabled=false;change.Text="—";bankChoice.BackColor=CafeTheme.Coffee;bankChoice.ForeColor=Color.White;cashChoice.BackColor=CafeTheme.Surface;cashChoice.ForeColor=CafeTheme.Ink;};
        var page=Ui.Columns(left,detail,36);
        page.HandleCreated+=async(_,_)=>
        {
            try
            {
                var data=await LiveSession.Api.GetMenuAsync();if(products.IsDisposed)return;
                void Render()
                {
                    products.SuspendLayout();foreach(Control child in products.Controls.Cast<Control>().ToArray())child.Dispose();products.Controls.Clear();
                    var matches=data.Where(p=>(category.Text=="Tất cả"||p.Category==category.Text)&&p.Name.Contains(search.Text.Trim(),StringComparison.CurrentCultureIgnoreCase)).ToArray();
                    var rows=Math.Max(1,(matches.Length+2)/3);products.RowCount=rows;products.Height=rows*194;
                    products.RowStyles.Clear();for(int row=0;row<rows;row++)products.RowStyles.Add(new RowStyle(SizeType.Percent,100f/rows));
                    foreach(var product in matches)
                    {
                        var card=new SurfacePanel {Dock=DockStyle.Fill,Height=188,Padding=Padding.Empty,Margin=new Padding(0,0,10,10)};
                        var photo=new ProductImage {ImageIndex=product.ImageIndex,Dock=DockStyle.Top,Height=128};
                        var text=Ui.Label(product.Name+"\n"+MoneyDisplay.Format(product.Price),9,true,product.Available?CafeTheme.Ink:CafeTheme.Muted);text.Dock=DockStyle.Fill;text.AutoSize=false;text.Padding=new Padding(4,3,2,0);
                        var add=Ui.Button("+",()=>{price=product.Price;selectedName.Text=product.Name;selectedOptions.Text=product.Category;selectedPrice.Text=MoneyDisplay.Format(product.Price);change.Text=cash.Enabled?MoneyDisplay.Format(Math.Max(0,cash.Value-product.Price)):"—";},true);
                        add.AutoSize=false;add.MinimumSize=Size.Empty;add.Size=new Size(28,28);add.Padding=Padding.Empty;add.Dock=DockStyle.Fill;add.Margin=new Padding(0,7,0,7);if(add is CafeButton round)round.CornerRadius=18;add.Enabled=product.Available;
                        var footer=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(5,3,7,3),Margin=Padding.Empty};
                        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,36));
                        footer.RowStyles.Add(new RowStyle(SizeType.Percent,100));footer.Controls.Add(text,0,0);footer.Controls.Add(add,1,0);
                        card.Controls.Add(footer);card.Controls.Add(photo);
                        if(!product.Available)text.Text+=" · Tạm hết";
                        products.Controls.Add(card);
                    }
                    products.ResumeLayout(true);
                }
                search.TextChanged+=(_,_)=>Render();category.SelectedIndexChanged+=(_,_)=>Render();Render();
            }
            catch(CafeApiException ex){if(!error.IsDisposed){error.Text=ex.Message;error.Visible=true;}}
        };
        return page;
    }
}
