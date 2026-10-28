using MocCafe.Desktop.Controls;
using MocCafe.Desktop.DesignSystem;
namespace MocCafe.Desktop.Shell;

public sealed class RoleApplicationContext : ApplicationContext
{
    public RoleApplicationContext(string role)
    {
        var entry=new EntryForm(role,name=>{
            var previous=MainForm;
            MainForm=new MainForm(role,name);
            MainForm.Show();
            previous?.Close();
        });
        MainForm=entry;entry.Show();
    }
}

// Dedicated opening screen per executable. No role switcher.
public sealed class EntryForm:Form
{
    public EntryForm(string role,Action<string> onContinue)
    {
        bool staff=role=="Staff";
        Text=$"{CafeTheme.Brand} · {(staff?"Nhân viên — Mở ca":"Quản lý — Đăng nhập")}";
        ClientSize=new Size(1060,720);MinimumSize=new Size(940,700);AutoScaleMode=AutoScaleMode.Dpi;
        StartPosition=FormStartPosition.CenterScreen;BackColor=CafeTheme.Cream;Font=CafeTheme.Body();
        var visual=new Panel{Dock=DockStyle.Left,Width=400,BackColor=CafeTheme.Dark,Padding=new Padding(32,40,32,24)};
        var brand=Ui.Label("4CHANG\nLINH NGU LAM",24,true,Color.White);brand.Dock=DockStyle.Top;brand.AutoSize=false;brand.Height=110;
        var caption=Ui.Label(staff?"Một ca làm việc tốt,\nbắt đầu từ đây.":"Quản lý dễ dàng,\nvận hành hiệu quả.",19,true,Color.FromArgb(239,222,199));caption.AutoSize=false;caption.Height=100;caption.Dock=DockStyle.Top;
        var picture=new ProductImage{ImageIndex=2,Dock=DockStyle.Top,Height=270};
        visual.Controls.Add(picture);visual.Controls.Add(caption);visual.Controls.Add(brand);
        var main=new Panel{Dock=DockStyle.Fill,Padding=new Padding(38,45,38,25),AutoScroll=true};
        var name=Ui.Input(staff?"Nhập họ tên của bạn":"Tên đăng nhập quản lý",staff?"":"admin.demo",430);
        var fields=new List<Control>{Ui.Label(staff?"KHÔNG GIAN NHÂN VIÊN":"KHÔNG GIAN QUẢN LÝ",9,true,CafeTheme.Coffee),
            Ui.Label(staff?"Chào ca mới":"Chào mừng trở lại",24,true),
            Ui.Label(staff?"Sẵn sàng phục vụ những ly cà phê ngon.":"Theo dõi hoạt động và quản lý quán mỗi ngày.",10,false,CafeTheme.Muted),
            Ui.Field(staff?"Tên nhân viên tự khai":"Tài khoản quản lý",name)};
        if(staff)fields.Add(Ui.Field("Tiền đầu ca",Ui.Input("Số tiền","500000",430)));
        else {var password=Ui.Input("Nhập mật khẩu","",430);password.UseSystemPasswordChar=true;fields.Add(Ui.Field("Mật khẩu",password));}
        var error=Ui.Label("Vui lòng nhập tên trước khi mở giao diện ca.",9,false,CafeTheme.Danger);error.Visible=false;fields.Add(error);
        var button=Ui.Button(staff?"Vào bán hàng":"Đăng nhập",()=>{
            if(staff&&string.IsNullOrWhiteSpace(name.Text)){error.Visible=true;name.Focus();return;}
            onContinue(staff?name.Text.Trim():"Admin");
        },true);button.MinimumSize=new Size(430,48);fields.Add(button);
        var layout=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        foreach(var field in fields){field.Margin=new Padding(0,0,0,22);layout.Controls.Add(field);}
        main.Controls.Add(layout);Controls.Add(main);Controls.Add(visual);AcceptButton=button;
    }
}
