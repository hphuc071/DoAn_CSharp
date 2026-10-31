using FourChang.Desktop.Controls;
using FourChang.Desktop.Data;
using FourChang.Desktop.DesignSystem;
using FourChang.Presentation.Shared.Integration;
namespace FourChang.Desktop.Shell;

public sealed class RoleApplicationContext : ApplicationContext
{
    public RoleApplicationContext(string role)
    {
        var entry=new EntryForm(role,name=>{var previous=MainForm;MainForm=new MainForm(role,name);MainForm.Show();previous?.Close();});
        MainForm=entry;entry.Show();
    }
}
public sealed class EntryForm:Form
{
    public EntryForm(string role,Action<string> onContinue)
    {
        var staff=role=="Staff";
        Text=$"{CafeTheme.Brand} · {(staff?"Nhân viên — Mở ca":"Quản lý — Đăng nhập")}";
        ClientSize=new Size(1060,760);MinimumSize=new Size(940,720);AutoScaleMode=AutoScaleMode.Dpi;
        StartPosition=FormStartPosition.CenterScreen;BackColor=CafeTheme.Cream;Font=CafeTheme.Body();
        var visual=new Panel{Dock=DockStyle.Left,Width=380,BackColor=CafeTheme.Dark,Padding=new Padding(30,40,30,24)};
        var brand=Ui.Label("4CHANG\nLINH NGU LAM",24,true,Color.White);brand.Dock=DockStyle.Top;brand.AutoSize=false;brand.Height=110;
        var caption=Ui.Label(staff?"Một ca làm việc tốt,\nbắt đầu từ đây.":"Quản lý dễ dàng,\nvận hành hiệu quả.",19,true,Color.FromArgb(239,222,199));caption.AutoSize=false;caption.Height=100;caption.Dock=DockStyle.Top;
        visual.Controls.Add(new ProductImage{ImageIndex=2,Dock=DockStyle.Top,Height=270});visual.Controls.Add(caption);visual.Controls.Add(brand);
        var main=new Panel{Dock=DockStyle.Fill,Padding=new Padding(36,32,36,20),AutoScroll=true};
        var name=Ui.Input("Tên đăng nhập","quanly",430);
        var password=Ui.Input("Mật khẩu","",430);password.UseSystemPasswordChar=true;
        var employees=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=430,Font=CafeTheme.Body(),DisplayMember=nameof(Employee.HoTen)};
        var shift=Ui.Select("Sáng","Chiều","Tối");shift.Width=430;
        var opening=new NumericUpDown{Width=430,Maximum=1_000_000_000,Minimum=0,Increment=10000,ThousandsSeparator=true,Value=500000,Font=CafeTheme.Body()};
        employees.FormattingEnabled=true;
        employees.Format+=(_,e)=>{if(e.ListItem is Employee item)e.Value=$"{item.HoTen} · #{item.MaNhanVien}";};
        foreach(var combo in new[]{employees,shift})
        {
            combo.DrawMode=DrawMode.OwnerDrawFixed;combo.ItemHeight=24;
            combo.DrawItem+=(_,e)=>{e.DrawBackground();var index=e.Index>=0?e.Index:combo.SelectedIndex;if(index>=0&&index<combo.Items.Count){var text=combo.GetItemText(combo.Items[index]);TextRenderer.DrawText(e.Graphics,text,combo.Font,e.Bounds,e.ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);}e.DrawFocusRectangle();};
        }
        var error=Ui.Label("",9,false,CafeTheme.Danger);error.MaximumSize=new Size(440,0);error.Visible=false;
        var fields=new List<Control>{Ui.Label(staff?"KHÔNG GIAN NHÂN VIÊN":"KHÔNG GIAN QUẢN LÝ",9,true,CafeTheme.Coffee),Ui.Label(staff?"Chào ca mới":"Chào mừng trở lại",24,true)};
        if(staff){fields.Add(Ui.Field("Nhân viên",employees));fields.Add(Ui.Field("Ca làm việc",shift));fields.Add(Ui.Field("Tiền đầu ca",opening));}
        else{fields.Add(Ui.Field("Tài khoản quản lý",name));fields.Add(Ui.Field("Mật khẩu",password));}
        fields.Add(error);
        var button=Ui.Button(staff?"Mở ca":"Đăng nhập",()=>{},true);button.MinimumSize=new Size(430,48);
        button.Click+=async(_,_)=>
        {
            error.Visible=false;button.Enabled=false;
            try
            {
                if(LiveSession.Preview){onContinue(staff?"Nhân viên":"Quản lý");return;}
                SessionResult session;
                if(staff)
                {
                    if(employees.SelectedItem is not Employee employee)throw new CafeApiException("Chọn nhân viên trước khi mở ca.");
                    session=await LiveSession.Api.OpenShiftAsync(new(employee.MaNhanVien,shift.Text,Environment.MachineName,opening.Value));
                }
                else session=await LiveSession.Api.LoginAdminAsync(new(name.Text,password.Text));
                LiveSession.Current=session;LiveSession.Api.Token=session.Token;
                onContinue(session.Name);
            }
            catch(CafeApiException exception){error.Text=exception.Message;error.Visible=true;}
            finally{if(!button.IsDisposed)button.Enabled=true;}
        };
        fields.Add(button);
        var layout=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        foreach(var field in fields){field.Margin=new Padding(0,0,0,18);layout.Controls.Add(field);}
        main.Controls.Add(layout);Controls.Add(main);Controls.Add(visual);AcceptButton=button;
        Shown+=async(_,_)=>
        {
            if(!staff||LiveSession.Preview)return;
            button.Enabled=false;
            try{employees.DataSource=await LiveSession.Api.GetEmployeesAsync();employees.SelectedIndex=employees.Items.Count>0?0:-1;}
            catch(CafeApiException exception){error.Text=exception.Message;error.Visible=true;}
            finally{if(!button.IsDisposed)button.Enabled=true;}
        };
    }
}
