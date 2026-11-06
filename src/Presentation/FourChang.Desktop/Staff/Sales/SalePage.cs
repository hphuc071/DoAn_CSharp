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
        var category = Ui.Select("Tất cả");
        var products = new TableLayoutPanel { Dock=DockStyle.Top,Height=400,ColumnCount=3,RowCount=2,Margin=Padding.Empty };
        for(int column=0;column<3;column++)products.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/3));
        var error = Ui.Label("",9,false,CafeTheme.Danger); error.Visible=false;
        // Nhãn luôn hiển thị; lớp bo góc chỉ bao control cũ, giữ nguyên bộ lọc và sự kiện.
        var searchFrame = new FourChang.Desktop.Shared.Controls.Entry.EntryInputFrame(search) { Width = 300 };
        var categoryFrame = new FourChang.Desktop.Shared.Controls.Entry.EntryInputFrame(category) { Width = 180 };
        var menu = Ui.Panel("Thực đơn", Ui.Row(Ui.Field("Tìm món", searchFrame), Ui.Field("Danh mục", categoryFrame)), error, products);
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
        // Snapshot đọc chung với Web. SSE chỉ làm mới menu, không xóa ghi chú/tiền nhập của ca hiện tại.
        var lifetime=new CancellationTokenSource();var token=lifetime.Token;bool stopped=false;bool started=false;
        FourChang.Presentation.Shared.Contracts.Customer.CatalogSnapshot? snapshot=null;
        CatalogSelection? selected=null;

        void Render()
        {
            if(products.IsDisposed||snapshot is null)return;
            products.SuspendLayout();foreach(Control child in products.Controls.Cast<Control>().ToArray())child.Dispose();products.Controls.Clear();
            var matches=snapshot.Mon.Where(m=>m.CauHinh.Count>0&&(category.Text=="Tất cả"||m.TenDanhMuc==category.Text)&&m.TenMon.Contains(search.Text.Trim(),StringComparison.CurrentCultureIgnoreCase)).ToList();
            var rows=Math.Max(1,(matches.Count+2)/3);products.RowCount=rows;products.Height=rows*220;products.RowStyles.Clear();
            for(int row=0;row<rows;row++)products.RowStyles.Add(new RowStyle(SizeType.Percent,100f/rows));
            foreach(var product in matches)
            {
                var v=product.CauHinh.FirstOrDefault(v=>v.LaMacDinh)??product.CauHinh[0];var available=product.CauHinh.Any(v=>v.ConHang);
                var card=new SurfacePanel{Dock=DockStyle.Fill,Height=214,Padding=Padding.Empty,Margin=new Padding(0,0,10,10)};
                var photo=CatalogImage.Create(product.DuongDanAnh,product.TenMon,128);
                var text=Ui.Label(product.TenMon+"\n"+MoneyDisplay.Format(v.GiaBan)+(available?"":" · Tạm hết"),9,true,available?CafeTheme.Ink:CafeTheme.Muted);text.Dock=DockStyle.Fill;text.AutoSize=false;text.Padding=new(4,3,2,0);
                var add=Ui.Button("+",()=>
                {
                    using var picker=new CatalogSelectionForm(product);
                    if(picker.ShowDialog(page.FindForm())!=DialogResult.OK||picker.Selection is null)return;
                    selected=picker.Selection;price=selected.UnitPrice;selectedName.Text=product.TenMon;selectedOptions.Text=selected.Summary;selectedPrice.Text=MoneyDisplay.Format(price.Value);change.Text=cash.Enabled?MoneyDisplay.Format(Math.Max(0,cash.Value-price.Value)):"—";
                },true);
                add.Name="SelectMenu_"+product.MaMon;add.AutoSize=false;add.MinimumSize=Size.Empty;add.Size=new(44,44);add.Padding=Padding.Empty;add.Dock=DockStyle.None;add.Anchor=AnchorStyles.None;add.Margin=Padding.Empty;add.Enabled=available;if(add is CafeButton round)round.CornerRadius=22;
                var footer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new(5,3,7,3),Margin=Padding.Empty};footer.ColumnStyles.Add(new(SizeType.Percent,100));footer.ColumnStyles.Add(new(SizeType.Absolute,50));footer.RowStyles.Add(new(SizeType.Percent,100));footer.Controls.Add(text,0,0);footer.Controls.Add(add,1,0);card.Controls.Add(footer);card.Controls.Add(photo);products.Controls.Add(card);
            }
            products.ResumeLayout(true);CafeVisuals.Apply(products);
        }
        async Task Refresh()
        {
            try
            {
                var fresh=await LiveSession.Api.GetCatalogAsync(token);if(page.IsDisposed)return;
                snapshot=fresh;var oldCategory=category.Text;category.BeginUpdate();category.Items.Clear();category.Items.Add("Tất cả");category.Items.AddRange((fresh.DanhMuc?.Select(c=>c.TenDanhMuc)??fresh.Mon.Select(m=>m.TenDanhMuc)).Distinct().Cast<object>().ToArray());category.SelectedItem=oldCategory;if(category.SelectedIndex<0)category.SelectedIndex=0;category.EndUpdate();Render();error.Visible=false;
                if(selected is not null)
                {
                    var m=fresh.Mon.FirstOrDefault(m=>m.CauHinh.Any(v=>v.MaCauHinhMon==selected.Variant));var v=m?.CauHinh.FirstOrDefault(v=>v.MaCauHinhMon==selected.Variant);
                    var valid=v is not null&&v.TuyChon.Any(c=>c.PhanTramDuong==selected.Sugar&&c.PhanTramDa==selected.Ice&&c.ConHang)&&selected.Toppings.All(t=>v.MonThem.Any(x=>x.MaMonThem==t.Key&&x.ConHang&&t.Value<=x.GioiHanMoiLy));
                    if(!valid){price=null;selectedPrice.Text="Chọn lại cấu hình";error.Text="Món đã thay đổi hoặc tạm hết. Chọn lại trước khi tiếp tục.";error.Visible=true;}
                    else{price=v!.GiaBan+selected.Toppings.Sum(t=>v.MonThem.Single(x=>x.MaMonThem==t.Key).GiaMoiPhan*t.Value);selectedName.Text=m!.TenMon;selectedPrice.Text=MoneyDisplay.Format(price.Value);if(price!=selected.UnitPrice){error.Text="Giá đã cập nhật từ Quản lý. Kiểm tra lại trước khi tiếp tục.";error.Visible=true;}}
                    change.Text=price is null?"—":cash.Enabled?MoneyDisplay.Format(Math.Max(0,cash.Value-price.Value)):"—";
                }
            }
            catch(CafeApiException ex){if(!page.IsDisposed){error.Text="Menu có thể đã cũ. "+ex.Message;error.Visible=true;}}
            catch(OperationCanceledException)when(token.IsCancellationRequested){}
        }
        search.TextChanged+=(_,_)=>Render();category.SelectedIndexChanged+=(_,_)=>Render();
        page.HandleCreated+=async(_,_)=>{if(started)return;started=true;await Refresh();if(!page.IsDisposed)_=LiveSession.Api.WatchCatalogAsync(Refresh,token);};
        page.Disposed+=(_,_)=>{if(stopped)return;stopped=true;lifetime.Cancel();lifetime.Dispose();};
        return page;
    }
}
