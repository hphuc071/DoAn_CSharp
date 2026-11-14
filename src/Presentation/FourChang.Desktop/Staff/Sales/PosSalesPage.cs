using System.Text.Json;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Customer;
using FourChang.Presentation.Shared.Contracts.Staff;
namespace FourChang.Desktop.Staff.Sales;

/// <summary>POS nhiều món: catalog API bên trái, giỏ có cấu hình và thu ngân bên phải. Không khai báo giá riêng.</summary>
public sealed class PosSalesPage : UserControl
{
    private readonly PosDraftCart cart=PosDraftCart.ForShift(LiveSession.Current?.ShiftId);
    private readonly TextBox search=Ui.Input("Tìm tên món…","",250);
    private readonly ComboBox category=Ui.Select("Tất cả");
    private readonly FlowLayoutPanel products=Ui.Row();
    private readonly DataGridView grid=Ui.Grid(new([],[]),300);
    private readonly ComboBox fulfillment=Ui.Select("Mang đi","Tại bàn");
    private readonly ComboBox table=Ui.Select();
    private readonly ComboBox method=Ui.Select("Tiền mặt","Chuyển khoản");
    private readonly Label total=Ui.Label("0đ",18,true);
    private readonly Label message=Ui.Label("",9,false,CafeTheme.Muted);
    private readonly Button pay;
    private readonly Button edit;
    private readonly Button remove;
    private readonly Button qr;
    private readonly System.Windows.Forms.Timer refresh=new(){Interval=5000};
    private List<MenuProduct> catalog=[];
    private List<PosDiningTable> dining=[];
    private bool busy;
    private bool refreshing;
    private bool updatingCatalog;
    private string? renderedMenu;
    private string? renderedCart;
    private sealed record TableChoice(PosDiningTable Table){public override string ToString()=>Table.TenBan;}

    /// <summary>Giữ nháp theo ca khi đổi trang; chỉ xóa giỏ sau server xác nhận đã thu hoặc đã tạo đơn chờ QR.</summary>
    public PosSalesPage()
    {
        Name="PosSalesPage";Dock=DockStyle.Top;AutoSize=true;BackColor=CafeTheme.Cream;
        search.Name="PosMenuSearch";grid.Name="PosCartGrid";method.Name="PosPaymentMethod";
        products.Name="PosProductGrid";products.Dock=DockStyle.Top;products.AutoSize=true;products.WrapContents=true;
        grid.MinimumSize=new Size(0,160);grid.Height=200;
        fulfillment.Width=145;table.Width=150;method.Width=145;
        message.MaximumSize=new Size(500,0);
        grid.AutoGenerateColumns=false;
        foreach(var (property,title,weight) in new[]{("TenMon","Món",120f),("CauHinh","Cấu hình",180f),("Quantity","SL",40f),("DonGia","Đơn giá",70f),("ThanhTien","Thành tiền",80f)})
            grid.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName=property,HeaderText=title,FillWeight=weight,SortMode=DataGridViewColumnSortMode.NotSortable});
        grid.Columns[0].MinimumWidth=90;grid.Columns[1].MinimumWidth=130;
        grid.Columns[2].MinimumWidth=35;grid.Columns[3].MinimumWidth=90;grid.Columns[4].MinimumWidth=95;
        grid.Columns[2].HeaderText="SL";grid.Columns[3].HeaderText="Giá/ly";grid.Columns[4].HeaderText="Thành tiền";
        grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;
        grid.Columns[0].DefaultCellStyle.WrapMode=DataGridViewTriState.True;
        grid.Columns[1].DefaultCellStyle.WrapMode=DataGridViewTriState.True;
        grid.DefaultCellStyle.Padding=new Padding(6,6,6,6);grid.ColumnHeadersDefaultCellStyle.Padding=new Padding(6);
        grid.CellDoubleClick+=async(_,e)=>{if(e.RowIndex>=0)await Edit();};
        grid.CellFormatting+=(_,e)=>{if(e.Value is decimal price)e.Value=price.ToString("N0")+"đ";};
        edit=Ui.Button("Sửa món",()=>{});edit.Click+=async(_,_)=>await Edit();
        remove=Ui.Button("Bỏ món",()=>{if(!Frozen&&grid.CurrentRow?.DataBoundItem is PosDraftLine row){cart.Lines.RemoveAll(x=>x.Key==row.Key);RenderCart();}});
        pay=Ui.Button("Thanh toán đơn",()=>{},true);pay.Name="PosCheckout";pay.Click+=async(_,_)=>await Checkout();
        qr=Ui.Button("Mở QR / kiểm tra thanh toán",()=>{});qr.Click+=async(_,_)=>await ShowQr();
        fulfillment.SelectedIndexChanged+=(_,_)=>ApplyState();method.SelectedIndexChanged+=(_,_)=>RenderCart();
        search.TextChanged+=(_,_)=>RenderMenu();category.SelectedIndexChanged+=(_,_)=>{if(!updatingCatalog)RenderMenu();};
        var menu=Ui.Panel("Chọn món",Ui.Row(Ui.Field("Tìm món",search),Ui.Field("Danh mục",category)),products);
        var cashier=Ui.Panel("Giỏ hàng tại quầy",grid,Ui.Row(edit,remove),
            Ui.Row(Ui.Field("Nhận món",fulfillment),Ui.Field("Bàn",table),Ui.Field("Thanh toán",method)),
            Ui.Row(Ui.Field("Tổng tiền",total)),message,Ui.Row(pay,qr));
        Controls.Add(Ui.Columns(menu,cashier,48));
        HandleCreated+=async(_,_)=>{await LoadCatalog();RenderCart();refresh.Start();};
        refresh.Tick+=async(_,_)=>{if(Visible&&!busy&&FindForm()?.OwnedForms.Length==0)await LoadCatalog();};Disposed+=(_,_)=>{refresh.Stop();refresh.Dispose();};
    }
    private bool Frozen=>busy||cart.Pending is not null||cart.AwaitingTransfer is not null;
    /// <summary>Đọc menu/bàn chuẩn; lỗi tải không xóa nháp hoặc đặt giá về 0.</summary>
    private async Task LoadCatalog()
    {
        if(refreshing||IsDisposed)return;refreshing=true;
        try
        {
            var items=await LiveSession.Api.GetMenuAsync();var tables=await LiveSession.Api.GetPosTablesAsync();if(IsDisposed)return;
            catalog=items; updatingCatalog=true;
            var categories = new[] { "Tất cả" }.Concat(items.Select(x=>x.Category).Distinct()).ToArray();
            if (!category.Items.Cast<string>().SequenceEqual(categories))
            {
                var selected=category.Text; category.Items.Clear(); category.Items.AddRange(categories);
                category.SelectedItem=category.Items.Contains(selected)?selected:"Tất cả";
            }
            if (JsonSerializer.Serialize(dining) != JsonSerializer.Serialize(tables))
            {
                var tableId=(table.SelectedItem as TableChoice)?.Table.MaBan; dining=tables; table.Items.Clear();
                foreach(var t in tables)table.Items.Add(new TableChoice(t));
                table.SelectedIndex=tables.FindIndex(x=>x.MaBan==tableId);
            }
            updatingCatalog=false; products.Tag=DateTime.UtcNow; RenderMenu(); ApplyState();
        }
        catch(CafeApiException ex){if(!IsDisposed)message.Text=ex.Message;}
        finally{refreshing=false;updatingCatalog=false;}
    }
    /// <summary>Vẽ món từ catalog hiện tại, thêm mở form cấu hình thay vì ghi đè một món duy nhất.</summary>
    private void RenderMenu()
    {
        if(products.IsDisposed)return;
        var visible=catalog.Where(x=>(category.Text=="Tất cả"||x.Category==category.Text)&&x.Name.Contains(search.Text.Trim(),StringComparison.CurrentCultureIgnoreCase)).ToList();
        var snapshot=JsonSerializer.Serialize(visible);
        if(snapshot==renderedMenu){UpdateMenuActions();return;}
        products.SuspendLayout();
        try
        {
        foreach(Control child in products.Controls.Cast<Control>().ToArray())child.Dispose();products.Controls.Clear();
        foreach(var item in visible)
        {
            var button=Ui.Button("+",()=>{},true);
            button.AutoSize=false;button.MinimumSize=Size.Empty;button.Size=new Size(36,36);button.Padding=Padding.Empty;
            button.Enabled=item.Available&&!Frozen;button.Name="PosProduct"+item.MaMon;button.Tag=item.MaMon;
            button.Click+=async(_,_)=>await Configure(item,null);
            var caption=Ui.Label(item.Name+Environment.NewLine+item.Price.ToString("N0")+"đ",9,true);
            caption.AutoSize=false;caption.Dock=DockStyle.Fill;caption.Padding=new Padding(5,5,0,0);
            var footer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(0,0,6,0)};
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,38));
            button.Anchor=AnchorStyles.None;footer.Controls.Add(caption,0,0);footer.Controls.Add(button,1,0);
            var card=new SurfacePanel{Width=180,Height=190,Padding=Padding.Empty,Margin=new Padding(0,0,12,12)};
            card.Controls.Add(footer);card.Controls.Add(new ProductImage{ImageIndex=item.ImageIndex,Dock=DockStyle.Top,Height=115});
            if(!item.Available)caption.Text=item.Name+" · Hết"+Environment.NewLine+item.Price.ToString("N0")+"đ";
            products.Controls.Add(card);
        }
        renderedMenu=snapshot;
        }
        finally{products.ResumeLayout(true);}
    }
    /// <summary>Khóa/mở đúng nút món khi giao dịch bận, không xóa ảnh hay dựng lại thẻ để đổi trạng thái nút.</summary>
    private void UpdateMenuActions()
    {
        foreach(var button in ProductButtons(products))
            if(button.Tag is long id)button.Enabled=!Frozen&&catalog.Any(x=>x.MaMon==id&&x.Available);
    }
    /// <summary>Duyệt nút trong các thẻ, giữ control/focus hiện có qua các lần polling.</summary>
    private static IEnumerable<Button> ProductButtons(Control parent) => parent.Controls.Cast<Control>().SelectMany(x=>x is Button b?new[]{b}:ProductButtons(x));
    /// <summary>Nạp biến thể/topping của đúng món trước khi mở editor; lưu không làm mất các dòng đã thêm.</summary>
    private async Task Configure(MenuProduct item,PosDraftLine? old)
    {
        if(Frozen)return;busy=true;ApplyState();
        try{using var form=new PosItemEditorForm(item,await LiveSession.Api.GetMenuVariantsAsync(item.MaMon),old);if(form.ShowDialog(FindForm())==DialogResult.OK&&form.Result is { } row)cart.Save(row);}
        catch(Exception ex) when(ex is CafeApiException or InvalidOperationException){message.Text=ex.Message;}
        finally{busy=false;if(!IsDisposed){RenderCart();RenderMenu();}}
    }
    /// <summary>Sửa đúng dòng bằng khóa nháp, không suy món chỉ theo tên trùng.</summary>
    private async Task Edit()
    {
        if(grid.CurrentRow?.DataBoundItem is not PosDraftLine row)return;
        var item=catalog.SingleOrDefault(x=>x.MaMon==row.ProductId);
        if(item is null){message.Text="Món không còn trong menu; có thể bỏ dòng này.";return;}
        await Configure(item,row);
    }
    /// <summary>Gắn lại danh sách và tổng mà không đổi cấu hình người dùng vừa nhập.</summary>
    private void RenderCart()
    {
        var snapshot=JsonSerializer.Serialize(cart.Lines);
        if(snapshot!=renderedCart)
        {
            var selected=(grid.CurrentRow?.DataBoundItem as PosDraftLine)?.Key;
            grid.DataSource=cart.Lines.ToList();renderedCart=snapshot;
            var index=cart.Lines.FindIndex(x=>x.Key==selected);
            if(index>=0&&grid.Columns.Count>0)grid.CurrentCell=grid.Rows[index].Cells[0];
        }
        RenderTotals();ApplyState();
    }
    /// <summary>Chỉ hiển thị tổng tiền của giỏ; thu tiền mặt xác nhận đúng tổng khi nhân viên bấm thanh toán.</summary>
    private void RenderTotals()
    {
        total.Text=cart.Total.ToString("N0")+"đ";
    }
    /// <summary>Khóa giỏ khi kết quả ghi chưa rõ hoặc đang chờ QR, tránh thu trùng/đổi nội dung lệnh.</summary>
    private void ApplyState()
    {
        if(IsDisposed)return;
        edit.Enabled=remove.Enabled=!Frozen&&cart.Lines.Count>0;fulfillment.Enabled=method.Enabled=!Frozen;
        table.Enabled=!Frozen&&fulfillment.SelectedIndex==1;
        pay.Enabled=!busy&&cart.AwaitingTransfer is null&&(cart.Lines.Count>0||cart.Pending is not null);
        pay.Text=cart.Pending is null?"Thanh toán đơn":"Thử lại giao dịch đang chờ";qr.Enabled=!busy&&cart.AwaitingTransfer is not null;
        UpdateMenuActions();
    }
    /// <summary>Gửi lệnh nguyên tử với mã cố định; timeout giữ giỏ và mã, lỗi nghiệp vụ cho sửa lại.</summary>
    private async Task Checkout()
    {
        if(busy||cart.AwaitingTransfer is not null)return;
        if(cart.Pending is null)
        {
            if(cart.Lines.Count==0)return;
            if(fulfillment.SelectedIndex==1&&table.SelectedItem is not TableChoice){message.Text="Chọn bàn trước khi tạo đơn.";return;}
            cart.Pending=new(Guid.NewGuid(),fulfillment.SelectedIndex==0?"TAKEAWAY":"DINE_IN",fulfillment.SelectedIndex==1?(table.SelectedItem as TableChoice)!.Table.MaBan:null,
                method.SelectedIndex==0?"CASH":"BANK_TRANSFER",method.SelectedIndex==0?cart.Total:null,cart.Total,cart.Lines.Select(x=>x.Input()).ToList());
        }
        busy=true;ApplyState();
        try
        {
            var receipt=await LiveSession.Api.CheckoutPosAsync(cart.Pending);cart.Pending=null;
            if(receipt.TrangThaiThanhToan=="VERIFIED")Complete(receipt);
            else{cart.AwaitingTransfer=receipt;message.Text=$"Đơn {receipt.MaDon} đang chờ chuyển khoản; chưa ghi nhận đã thu.";}
        }
        catch(CafeApiException ex){message.Text=ex.Message;if(ex.StatusCode is { } code&&(int)code<500)cart.Pending=null;}
        finally{busy=false;if(!IsDisposed){RenderCart();RenderMenu();}}
        if(cart.AwaitingTransfer is not null)await ShowQr();
    }
    /// <summary>Mở lại đúng đơn đã tạo, không tạo thêm đơn khi đóng dialog QR hoặc mất mạng.</summary>
    private async Task ShowQr()
    {
        if(busy||cart.AwaitingTransfer is not { } pending)return;busy=true;ApplyState();
        try
        {
            var current=await LiveSession.Api.GetPosCheckoutAsync(pending.MaDonHang);
            if(current.TrangThaiThanhToan=="VERIFIED"){Complete(current);return;}
            using var dialog=new PosPaymentDialog(pending.MaDonHang,await LiveSession.Api.GetPosSimulationQrAsync(pending.MaDonHang));
            if(dialog.ShowDialog(FindForm())==DialogResult.OK&&dialog.Receipt is { } receipt)Complete(receipt);
        }
        catch(CafeApiException ex){message.Text=ex.Message+" Đơn chờ vẫn được giữ; không thu lại.";}
        finally{busy=false;if(!IsDisposed){RenderCart();RenderMenu();}}
    }
    /// <summary>Chỉ dọn đơn khi server đã xác nhận thu; đơn tiếp tục quy trình pha chế, không tự hoàn tất.</summary>
    private void Complete(PosCheckoutReceipt receipt)
    {
        cart.Lines.Clear();cart.Pending=null;cart.AwaitingTransfer=null;
        message.Text=$"Đã thanh toán đơn {receipt.MaDon} · {receipt.TongTien:N0}đ · Hóa đơn #{receipt.MaHoaDon}. Chọn món cho đơn mới.";
    }
}
