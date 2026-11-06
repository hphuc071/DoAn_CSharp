using FourChang.Desktop.Admin.Employees;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.Contracts.Customer;
namespace FourChang.Desktop.Staff.Sales;

/// <summary>Lựa chọn menu đang xem; đây chưa phải dòng đơn, không giữ kho hoặc thay UC tạo đơn.</summary>
internal sealed record CatalogSelection(long Variant,int Sugar,int Ice,IReadOnlyDictionary<long,int> Toppings,decimal UnitPrice,string Summary);

/// <summary>POS hiển thị cấu hình/topping/giá từ catalog chung, tuyệt đối không có bảng giá riêng.</summary>
internal sealed class CatalogSelectionForm:Form
{
    private readonly CatalogDish dish;
    private readonly ComboBox variant=Ui.Select();
    private readonly ComboBox choice=Ui.Select();
    private readonly Label price=Ui.Label("",16,true);
    private readonly FlowLayoutPanel toppingPanel=Ui.Stack();
    private readonly List<(CatalogTopping Top,CheckBox Check,NumericUpDown Qty)> toppings=[];
    private readonly Button confirm=Ui.Button("Chọn cấu hình",()=>{},true);
    internal CatalogSelection? Selection {get;private set;}

    /// <summary>Chọn mặc định do Admin công bố; form không suy giá từ size hoặc tên topping.</summary>
    internal CatalogSelectionForm(CatalogDish dish)
    {
        this.dish=dish;Text=dish.TenMon;Name="CatalogSelectionForm";ClientSize=new(700,680);MinimumSize=new(600,480);StartPosition=FormStartPosition.CenterParent;BackColor=CafeTheme.Cream;Font=CafeTheme.Body();AutoScroll=true;
        variant.Width=choice.Width=600;variant.Name="PosVariant";choice.Name="PosBrewChoice";
        foreach(var v in dish.CauHinh)variant.Items.Add($"{v.KichCo} · {(v.NhietDo=="HOT"?"Nóng":"Lạnh")} · {v.GiaBan:N0}đ{(v.ConHang?"":" · Tạm hết")}");
        variant.SelectedIndexChanged+=(_,_)=>RenderVariant();choice.SelectedIndexChanged+=(_,_)=>Total();
        confirm.Click+=(_,_)=>{var v=Current();if(v is null||choice.SelectedIndex<0)return;var c=v.TuyChon[choice.SelectedIndex];var selected=toppings.Where(x=>x.Check.Checked).ToDictionary(x=>x.Top.MaMonThem,x=>(int)x.Qty.Value);Selection=new(v.MaCauHinhMon,c.PhanTramDuong,c.PhanTramDa,selected,Amount(),$"{v.KichCo} · {(v.NhietDo=="HOT"?"Nóng":"Lạnh")} · {c.PhanTramDuong}% đường · {c.PhanTramDa}% đá");DialogResult=DialogResult.OK;Close();};
        Controls.Add(Ui.Panel(dish.TenMon,Ui.Label(dish.MoTa??"",9,false,CafeTheme.Muted),AdminListLayout.Field("Cấu hình và giá",variant),AdminListLayout.Field("Đường / đá",choice),Ui.Label("Topping · số phần mỗi ly",10,true),toppingPanel,price,Ui.Row(Ui.Button("Hủy",()=>Close()),confirm)));
        var preferred=dish.CauHinh.ToList().FindIndex(v=>v.LaMacDinh);if(variant.Items.Count>0)variant.SelectedIndex=Math.Max(0,preferred);
        CafeVisuals.Apply(this);
    }
    /// <summary>Truy xuất đúng ID cấu hình tương ứng với snapshot; không định danh bằng text UI.</summary>
    private CatalogVariant? Current()=>variant.SelectedIndex>=0?dish.CauHinh[variant.SelectedIndex]:null;
    /// <summary>Đổi cấu hình tải lại các cặp hợp lệ/topping của chính cấu hình đó.</summary>
    private void RenderVariant()
    {
        choice.Items.Clear();foreach(Control c in toppingPanel.Controls.Cast<Control>().ToArray())c.Dispose();toppingPanel.Controls.Clear();toppings.Clear();
        var v=Current();if(v is null)return;
        foreach(var c in v.TuyChon)choice.Items.Add($"{c.PhanTramDuong}% đường · {c.PhanTramDa}% đá{(c.ConHang?"":" · Tạm hết")}");
        foreach(var top in v.MonThem)
        {
            var check=new CheckBox{Text=$"{top.TenMonThem} +{top.GiaMoiPhan:N0}đ/phần{(top.ConHang?"":" · Tạm hết")}",Enabled=top.ConHang,Width=450,Height=38};
            var qty=new NumericUpDown{Minimum=1,Maximum=top.GioiHanMoiLy,Value=1,Width=90,Enabled=false};
            toppings.Add((top,check,qty));check.CheckedChanged+=(_,_)=>{qty.Enabled=check.Checked;Total();};qty.ValueChanged+=(_,_)=>Total();toppingPanel.Controls.Add(Ui.Row(check,qty));
        }
        if(choice.Items.Count>0)choice.SelectedIndex=Math.Max(0,v.TuyChon.ToList().FindIndex(c=>c.LaMacDinh));Total();
    }
    /// <summary>Chỉ cộng giá từ response API với số phần đã chọn; không áp giá riêng cho POS.</summary>
    private decimal Amount()=>(Current()?.GiaBan??0)+toppings.Where(x=>x.Check.Checked).Sum(x=>x.Top.GiaMoiPhan*x.Qty.Value);
    /// <summary>Phản hồi chọn món nhanh; cấu hình/cặp hết nguyên liệu không thể xác nhận.</summary>
    private void Total(){price.Text=$"Giá mỗi ly: {Amount():N0}đ";var v=Current();confirm.Enabled=v is not null&&choice.SelectedIndex>=0&&v.TuyChon[choice.SelectedIndex].ConHang;}
}
