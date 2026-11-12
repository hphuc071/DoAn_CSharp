using System.Text.Json;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.Contracts.Customer;
namespace FourChang.Desktop.Staff.Sales;

/// <summary>Chọn cấu hình có thật từ menu chung: size, nhiệt độ, cặp đường/đá, topping, số ly và ghi chú.</summary>
public sealed class PosItemEditorForm : Form
{
    private readonly MenuProduct product;
    private readonly List<MenuVariantItem> variants;
    private readonly PosDraftLine? old;
    private readonly ComboBox variant = Ui.Select();
    private readonly ComboBox brew = Ui.Select();
    private readonly NumericUpDown quantity = new() { Minimum=1, Maximum=99, Value=1, Width=150 };
    private readonly TextBox note = Ui.Input("Ghi chú pha chế", "", 590);
    private readonly FlowLayoutPanel toppings = Ui.Row();
    private readonly Label total = Ui.Label("", 14, true);
    private readonly Label message = Ui.Label("", 9, false, CafeTheme.Danger);
    private readonly Button save;
    private readonly List<(MenuToppingItem Top, NumericUpDown Count)> portions = [];
    private bool busy;
    public PosDraftLine? Result { get; private set; }
    private sealed record Choice<T>(string Label, T Value) { public override string ToString()=>Label; }

    /// <summary>Biểu mẫu chỉ lấy giá API; lưu thành nháp khi cấu hình vẫn còn đúng ở lần kiểm tra cuối.</summary>
    public PosItemEditorForm(MenuProduct item, List<MenuVariantItem> data, PosDraftLine? existing=null)
    {
        product=item; variants=data; old=existing; Name="PosItemEditor"; Text="Cấu hình · "+item.Name;
        ClientSize=new(740,700); MinimumSize=new(680,620); StartPosition=FormStartPosition.CenterParent; Font=CafeTheme.Body(); BackColor=CafeTheme.Cream;
        variant.Name="PosVariant"; variant.Width=590; brew.Name="PosBrew"; brew.Width=590;
        quantity.Name="PosQuantity"; note.Name="PosNote"; note.MaxLength=500;
        foreach(var v in data.Where(x=>x.Available)) variant.Items.Add(new Choice<MenuVariantItem>($"{v.KichCo} · {(v.NhietDo=="HOT"?"Nóng":"Lạnh")} · {v.GiaBan:N0}đ",v));
        variant.SelectedIndexChanged+=(_,_)=>Configure(); quantity.ValueChanged+=(_,_)=>RefreshPrice();
        save=Ui.Button("Thêm vào đơn",()=>{},true);save.Name="PosAddConfiguredItem";save.Click+=async(_,_)=>await Save();
        var cancel=Ui.Button("Hủy",()=>Close());
        var body=Ui.Panel(item.Name, Ui.Field("Kích cỡ / nóng lạnh",variant),Ui.Field("Đường / đá",brew),Ui.Label("Topping · số phần trên mỗi ly",10,true),toppings,
            Ui.Field("Số ly",quantity),Ui.Field("Ghi chú",note),total,message);
        AdminUi.Compose(this,body,save,cancel);
        if(existing is not null){quantity.Value=existing.Quantity;note.Text=existing.Note;save.Text="Lưu cấu hình";}
        variant.SelectedIndex=existing is null ? (variant.Items.Count>0?0:-1) : variant.Items.Cast<Choice<MenuVariantItem>>().ToList().FindIndex(x=>x.Value.MaCauHinhMon==existing.Variant.MaCauHinhMon);
        if(variant.SelectedIndex<0){save.Enabled=false;message.Text="Cấu hình cũ đã hết/ngừng bán. Chọn cấu hình khác trước khi lưu.";}
        FormClosing+=(_,e)=>{if(busy)e.Cancel=true;};
    }
    /// <summary>Đổi biến thể thì nạp đúng cặp đường/đá và topping tương thích; không tự khai báo tùy chọn không có trên server.</summary>
    private void Configure()
    {
        if(variant.SelectedItem is not Choice<MenuVariantItem> selected)return;
        brew.Items.Clear(); foreach(var pair in selected.Value.TuyChonPhaChe)brew.Items.Add(new Choice<MenuBrewOption>($"Đường {pair.PhanTramDuong}% · Đá {pair.PhanTramDa}%",pair));
        var match=old is not null && old.Variant.MaCauHinhMon==selected.Value.MaCauHinhMon;
        brew.SelectedIndex=match?brew.Items.Cast<Choice<MenuBrewOption>>().ToList().FindIndex(x=>x.Value==old!.Brew):-1;
        if(brew.SelectedIndex<0 && brew.Items.Count>0)brew.SelectedIndex=0;
        foreach(Control c in toppings.Controls.Cast<Control>().ToArray())c.Dispose();toppings.Controls.Clear();portions.Clear();
        foreach(var top in selected.Value.Toppings.Where(x=>x.Available))
        {
            var count=new NumericUpDown{Minimum=0,Maximum=top.GioiHanMoiLy,Width=150,Name="PosTopping"+top.MaMonThem};
            count.Value=match?Math.Min(top.GioiHanMoiLy,old!.Toppings.FirstOrDefault(x=>x.MaMonThem==top.MaMonThem)?.SoPhanMoiLy??0):0;
            count.ValueChanged+=(_,_)=>RefreshPrice();portions.Add((top,count));toppings.Controls.Add(Ui.Field($"{top.TenMonThem} · {top.GiaMoiPhan:N0}đ",count));
        }
        brew.SelectedIndexChanged+=(_,_)=>RefreshPrice();RefreshPrice();
    }
    /// <summary>Hiển thị tạm tính từ biến thể và số phần topping; máy chủ vẫn tính lại lúc thu.</summary>
    private void RefreshPrice()
    {
        if(variant.SelectedItem is not Choice<MenuVariantItem> v)return;
        total.Text=$"Tạm tính: {(v.Value.GiaBan+portions.Sum(x=>x.Top.GiaMoiPhan*x.Count.Value))*quantity.Value:N0}đ";
        if(save is not null)save.Enabled=!busy&&brew.SelectedItem is not null;
    }
    /// <summary>Kiểm tra catalog mới nhất trước thêm/sửa; lỗi giữ nguyên dữ liệu nhập, không làm mất giỏ.</summary>
    private async Task Save()
    {
        if(busy||variant.SelectedItem is not Choice<MenuVariantItem> v||brew.SelectedItem is not Choice<MenuBrewOption> b)return;
        busy=true;save.Enabled=false;
        try
        {
            var latest=await LiveSession.Api.GetMenuVariantsAsync(product.MaMon);
            var current=latest.SingleOrDefault(x=>x.MaCauHinhMon==v.Value.MaCauHinhMon);
            if(current is null||!current.Available||JsonSerializer.Serialize(current)!=JsonSerializer.Serialize(v.Value))
                throw new InvalidOperationException("Giá/cấu hình đã thay đổi. Đóng và chọn lại món để xem thông tin mới; giỏ cũ vẫn giữ.");
            Result=new(old?.Key??Guid.NewGuid(),product.MaMon,product.Name,current,b.Value,(int)quantity.Value,note.Text.Trim(),
                portions.Where(x=>x.Count.Value>0).Select(x=>new OrderToppingInput(x.Top.MaMonThem,(int)x.Count.Value)).ToList());
            busy=false;DialogResult=DialogResult.OK;Close();
        }
        catch(Exception ex) when(ex is FourChang.Presentation.Shared.ApiClients.CafeApiException or InvalidOperationException){message.Text=ex.Message;}
        finally{busy=false;if(!IsDisposed)RefreshPrice();}
    }
}
