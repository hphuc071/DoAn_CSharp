using FourChang.Desktop.Admin.Access.Controllers;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Desktop.Shell;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Preview;
namespace FourChang.Desktop.Admin.Access.Views;

// Điều phối ba phạm vi của UC-ADM-02; mọi thao tác nhạy cảm gọi API để không tin quyền từ giao diện.
public sealed class AccessPage : UserControl
{
    private readonly AccessController controller = new(LiveSession.Api);
    private readonly Label error=Ui.Label("",9,false,CafeTheme.Danger);
    private readonly Label accountInfo=Ui.Label("Đang tải tài khoản…",11);
    private readonly Label accountState=Ui.Label("",10,false,AdminColors.Muted);
    public AccessPage()
    {
        Name="AccessPage";Dock=DockStyle.Top;AutoSize=true;BackColor=CafeTheme.Cream;
        error.Visible=false;error.MaximumSize=new Size(1000,0);
        var password=Button("Đổi mật khẩu",AdminIcon.Key,ChangePassword,true);password.Name="AdminChangePassword";
        var logout=Button("Đăng xuất",AdminIcon.Lock,Logout);logout.Name="AdminLogout";
        var accountReload=Button("Tải lại",AdminIcon.Refresh,LoadAccount);
        accountInfo.Name="AdminAccountInfo";
        var account=Card("Tài khoản Quản lý duy nhất",accountInfo,accountState,
            Ui.Label("Chỉ tài khoản Quản lý được mở Dashboard. Nhân viên chọn hồ sơ và ca trên POS, không có tài khoản đăng nhập.",9,false,AdminColors.Muted),
            Ui.Row(password,logout,accountReload));
        var permissions=Ui.Grid(new PreviewTable(["Phạm vi","Khách hàng","Nhân viên POS","Quản lý"],[
            ["Tài khoản cá nhân","Của mình","Không có tài khoản","Quản lý khách hàng"],
            ["Bán hàng / xử lý đơn","Đơn của mình","Theo phiên ca đã mở","Giám sát / xử lý theo nghiệp vụ"],
            ["Thực đơn / kho","Chỉ xem món công bố","Chỉ đọc thực đơn","Quản trị"],
            ["Hoàn tiền / ghi sổ kho","Yêu cầu theo điều kiện","Không phê duyệt","Được phép theo nghiệp vụ"],

            ["Báo cáo / nhật ký","Không","Không","Được phép"]]),380);
        var rights=Card("Ranh giới quyền truy cập",Ui.Label("Quyền POS cố định; chọn tên nhân viên hoặc biết mã ca không cấp quyền Quản lý.",10,false,AdminColors.Muted),permissions);
        var tabs=Ui.Tabs(("Tài khoản Quản lý",account),("Phạm vi quyền",rights));tabs.Height=650;tabs.Name="AccessTabs";
        var outer=Card("Quyền truy cập hệ thống",Ui.Label("Tài khoản quản trị và phiên ca POS được kiểm tra tại máy chủ.",10,false,AdminColors.Muted),error,tabs);
        Controls.Add(outer);var initialized=false;
        HandleCreated+=async(_,_)=>{if(initialized)return;initialized=true;await LoadAccount();};
    }
    private static Control Card(string title,params Control[] children)
    {
        var surface=new AdminSurface{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink};
        var body=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,Margin=Padding.Empty};body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        var heading=Ui.Label(title,14,true);heading.Margin=new Padding(0,0,0,16);body.Controls.Add(heading);
        foreach(var child in children){child.Margin=new Padding(0,0,0,14);if(child is Label label)label.MaximumSize=new Size(1000,0);body.Controls.Add(child);}
        surface.Controls.Add(body);return surface;
    }
    private Button Button(string text,AdminIcon icon,Func<Task> action,bool primary=false)
    {
        var button=new AdminActionButton(text,icon,primary);
        button.Click+=async(_,_)=>{button.Enabled=false;try{await action();}catch(CafeApiException ex){Error(ex.Message);}finally{if(!button.IsDisposed)button.Enabled=true;}};return button;
    }
    private async Task LoadAccount()
    {
        try{var account=await controller.LoadAccountAsync();if(IsDisposed)return;
            accountInfo.Text=$"{account.HoTen}  ·  {account.TenDangNhap}";
            accountState.Text=$"Trạng thái: {(account.TrangThai=="ACTIVE"?"Hoạt động":"Không hoạt động")}  ·  Lần đăng nhập: {account.DangNhapLanCuoiLuc?.ToLocalTime():dd/MM/yyyy HH:mm}";error.Visible=false;}
        catch(CafeApiException ex){if(!IsDisposed)Error(ex.Message);}
    }
    private async Task ChangePassword()
    {
        var current=await controller.LoadPasswordContextAsync();using var form=new AdminPasswordForm(current);
        if(form.ShowDialog(FindForm())==DialogResult.OK){LiveSession.Api.Token=null;LiveSession.Current=null;(FindForm() as MainForm)?.RequestReauthentication();}
    }
    private async Task Logout()
    {
        if(MessageBox.Show(FindForm(),"Đăng xuất tài khoản Quản lý? Ca POS vẫn tiếp tục độc lập.","Đăng xuất",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
        await controller.LogoutAsync();LiveSession.Api.Token=null;LiveSession.Current=null;(FindForm() as MainForm)?.RequestReauthentication();
    }
    private void Error(string text){error.Text=text;error.Visible=true;}
}
