using System.Net;
using FourChang.Desktop.Controls;
using FourChang.Desktop.Controls.Admin;
using FourChang.Desktop.Data;
using FourChang.Desktop.DesignSystem;
using FourChang.Presentation.Shared.Integration;
namespace FourChang.Desktop.Features.Admin.Access;

// Giữ nguyên mã thao tác và dữ liệu khi mất phản hồi, tránh gửi một lệnh đổi mật khẩu thứ hai.
public sealed class AdminPasswordForm : Form
{
    private AdminAccessProfile profile;
    private readonly TextBox current=Ui.Input("Mật khẩu hiện tại","",445);
    private readonly TextBox password=Ui.Input("Mật khẩu mới","",445);
    private readonly TextBox confirm=Ui.Input("Xác nhận mật khẩu mới","",445);
    private readonly Label feedback=Ui.Label("",9,false,CafeTheme.Danger);
    private readonly Button save=new AdminActionButton("Đổi mật khẩu",AdminIcon.Key,true);
    private readonly Button reload=new AdminActionButton("Đối chiếu phiên bản",AdminIcon.Refresh);
    private AdminPasswordInput? pending;private bool busy;private bool unknown;public bool Saved{get;private set;}
    public AdminPasswordForm(AdminAccessProfile profile,string oldPassword="")
    {
        this.profile=profile;Text=profile.BatBuocDoiMatKhau?"Đổi mật khẩu tạm của Quản lý":"Đổi mật khẩu Quản lý";
        Name="AdminPasswordForm";ClientSize=new Size(530,590);StartPosition=FormStartPosition.CenterParent;
        BackColor=CafeTheme.Cream;Font=CafeTheme.Body();AutoScroll=true;
        current.Name="AdminCurrentPassword";password.Name="AdminNewPassword";confirm.Name="AdminConfirmPassword";save.Name="AdminPasswordSave";
        foreach(var input in new[]{current,password,confirm}){input.UseSystemPasswordChar=true;input.MaxLength=128;}
        current.Text=oldPassword;feedback.MaximumSize=new Size(445,0);feedback.Visible=false;
        var cancel=new AdminActionButton("Hủy",AdminIcon.Previous);cancel.Click+=(_,_)=>Close();
        var card=new AdminSurface{Dock=DockStyle.Top,AutoSize=true};
        card.Controls.Add(Ui.Stack(Ui.Label(Text,16,true),Ui.Label(profile.TenDangNhap,10,true,AdminColors.Accent),
            Ui.Field("Mật khẩu hiện tại",current),Ui.Field("Mật khẩu mới (10–128 ký tự)",password),Ui.Field("Xác nhận mật khẩu mới",confirm),
            Ui.Label("Đổi thành công sẽ đăng xuất các phiên Quản lý. Ca POS vẫn được giữ.",9,false,AdminColors.Muted),feedback,Ui.Row(save,reload,cancel)));
        Controls.Add(card);save.Click+=async(_,_)=>await Submit();reload.Click+=async(_,_)=>await RefreshVersion();
        FormClosing+=(_,e)=>{if(Saved)return;if(busy||unknown){e.Cancel=true;Error("Kết quả chưa rõ. Hãy Kiểm tra / thử lại trước khi đóng.");}};
    }
    private async Task Submit()
    {
        if(busy||Saved)return;
        pending??=new(Guid.NewGuid(),profile.PhienBanDuLieu,current.Text,password.Text,confirm.Text);
        busy=true;SetEnabled(false);
        try
        {
            var receipt=unknown?await LiveSession.Api.GetAdminPasswordReceiptAsync(pending.MaThaoTac):null;
            receipt??=await LiveSession.Api.ChangeAdminPasswordAsync(pending);
            Saved=true;DialogResult=DialogResult.OK;Close();
        }
        catch(CafeApiException ex)
        {
            unknown=ex.StatusCode is null||(int)ex.StatusCode>=500;
            Error(ex.Message+(unknown?" Chưa rõ kết quả; dùng Kiểm tra / thử lại, không đổi sang yêu cầu mới.":" Kiểm tra lại thông tin hoặc đối chiếu phiên bản."));
            if(!unknown)pending=null;save.Text=unknown?"Kiểm tra / thử lại":"Đổi mật khẩu";
        }
        finally{busy=false;if(!IsDisposed)SetEnabled(true);}
    }
    private async Task RefreshVersion()
    {
        if(busy||unknown)return;
        try{var latest=await LiveSession.Api.GetAdminPasswordContextAsync();if(MessageBox.Show(this,"Dùng phiên bản mới của tài khoản và giữ dữ liệu đang nhập để kiểm tra lại?","Đối chiếu",MessageBoxButtons.YesNo)==DialogResult.Yes){profile=latest;pending=null;}}
        catch(CafeApiException ex){Error(ex.Message);}
    }
    private void SetEnabled(bool enabled){save.Enabled=enabled;reload.Enabled=enabled&&!unknown;foreach(var input in new[]{current,password,confirm})input.Enabled=enabled&&!unknown;}
    private void Error(string text){feedback.Text=text;feedback.Visible=true;}
}
