using FourChang.Desktop.Controls;
using FourChang.Desktop.DesignSystem;
namespace FourChang.Desktop.Preview;

// Every action opens a visual preview. This class must never call an API or printer.
public static class PreviewDialog
{
    public static void ShowMessage(IWin32Window? owner, string title, string? description = null, params string[] fields)
    {
        using var f = Create(title, description, fields);f.ShowDialog(owner);
    }
    public static Form Create(string title, string? description = null, params string[] fields)
    {
        var f = new Form { Text = "4CHANG LINH NGU LAM · " + title, StartPosition = FormStartPosition.CenterParent, Size = new Size(590,Math.Min(790,330+fields.Length*72)), MinimumSize = new Size(520,300), BackColor = CafeTheme.Cream, Font = CafeTheme.Body(), MinimizeBox=false, MaximizeBox=false, AutoScroll=true, Padding=new Padding(24) };
        var content = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize=true,ColumnCount=1 };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        content.Controls.Add(Ui.Label(title,20,true));
        var desc = new Label { Text=description??"Nhập thông tin cần thiết bên dưới.",AutoSize=true,MaximumSize=new Size(490,0),Font=CafeTheme.Body(10),ForeColor=CafeTheme.Muted,Margin=new Padding(0,8,0,18) };content.Controls.Add(desc);
        foreach(var name in fields) { Control input=name.Contains("Lý do")||name.Contains("Ghi chú")||name.Contains("Chứng cứ")?new TextBox { Multiline=true, Height=58, Width=480, PlaceholderText=name, Font=CafeTheme.Body() }:Ui.Input(name,"",480); if(name.Contains("Mật khẩu")&&input is TextBox box)box.UseSystemPasswordChar=true;content.Controls.Add(Ui.Field(name,input)); }
        content.Controls.Add(Ui.Button("Đóng",()=>f.Close(),true));f.Controls.Add(content);return f;
    }
}
