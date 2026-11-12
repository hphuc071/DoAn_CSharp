using System.Security.Cryptography;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Desktop.Shared.Security;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;
namespace FourChang.Desktop.Admin.Access.Views;

// Một form cho cấp và thu hồi cấu hình máy; không có đăng ký tài khoản nhân viên.
public sealed class PosDeviceForm : Form
{
    private PosDeviceInfo? device;private readonly bool revoke;
    private readonly TextBox machine=Ui.Input("Mã máy","",450);
    private readonly TextBox name=Ui.Input("Tên máy","",450);
    private readonly TextBox key=Ui.Input("Khóa thiết bị","",450);
    private readonly TextBox reason=Ui.Input("Lý do","",450);
    private readonly Label feedback=Ui.Label("",9,false,CafeTheme.Danger);
    private readonly Button save;
    private readonly CheckBox local=new(){Text="Cấu hình POS trên máy Windows này",AutoSize=true};
    private Func<Task<PosDeviceOperation>>? pending;private Guid operation;private bool busy;private bool uncertain;private bool saved;
    public PosDeviceForm(PosDeviceInfo? device=null,bool revoke=false)
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        this.device=device;this.revoke=revoke;Text=revoke?"Thu hồi thiết bị POS":device is null?"Đăng ký thiết bị POS":"Cấp lại cấu hình POS";
        Name="PosDeviceForm";ClientSize=new Size(540,690);Font=CafeTheme.Body();BackColor=CafeTheme.Cream;StartPosition=FormStartPosition.CenterParent;AutoScroll=true;
        machine.Name="PosDeviceMachine";name.Name="PosDeviceName";key.Name="PosDeviceKey";reason.Name="PosDeviceReason";
        machine.Text=device?.MaMay??Environment.MachineName.ToUpperInvariant();machine.ReadOnly=device is not null;machine.MaxLength=50;
        name.Text=device?.TenMay??"Máy POS";name.MaxLength=120;
        key.Text=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));key.UseSystemPasswordChar=true;key.MaxLength=256;
        reason.Multiline=true;reason.Height=70;reason.MaxLength=500;
        feedback.Visible=false;feedback.MaximumSize=new Size(450,0);
        var show=new CheckBox{Text="Hiện khóa để cấu hình thiết bị",AutoSize=true};show.CheckedChanged+=(_,_)=>key.UseSystemPasswordChar=!show.Checked;
        void LocalState(){local.Enabled=string.Equals(machine.Text.Trim(),Environment.MachineName,StringComparison.OrdinalIgnoreCase);if(!local.Enabled)local.Checked=false;}
        machine.TextChanged+=(_,_)=>LocalState();LocalState();local.Checked=local.Enabled;
        save=new AdminActionButton(revoke?"Xác nhận thu hồi":"Cấp cấu hình",revoke?AdminIcon.Lock:AdminIcon.Add,true){Name="PosDeviceSave"};
        var close=new AdminActionButton("Hủy",AdminIcon.Previous);close.Click+=(_,_)=>Close();
        var fields=new List<Control>{Ui.Label(Text,16,true),Ui.Field("Mã máy",machine)};
        if(!revoke)fields.AddRange([Ui.Field("Tên máy",name),Ui.Field("Khóa thiết bị riêng",key),show,local]);
        else if(device?.SoCaChuaKetThuc>0){var warning=Ui.Label($"Máy có {device.SoCaChuaKetThuc} ca chưa kết thúc. Thu hồi sẽ chặn lệnh POS mới; ca và giao dịch vẫn được giữ để xử lý.",9,false,CafeTheme.Danger);warning.MaximumSize=new Size(450,0);fields.Add(warning);}
        fields.Add(Ui.Field("Lý do bắt buộc",reason));fields.Add(feedback);fields.Add(Ui.Row(save,close));
        var card=new AdminSurface{Dock=DockStyle.Top,AutoSize=true};card.Controls.Add(Ui.Stack(fields.ToArray()));Controls.Add(card);
        save.Click+=async(_,_)=>await Submit();
        FormClosing+=(_,e)=>{if(saved){DialogResult=DialogResult.OK;return;}if(busy||uncertain){e.Cancel=true;Error("Chưa rõ kết quả; chọn Kiểm tra / thử lại trước khi đóng.");}};
    }
    // Chỉ ghi DPAPI sau khi server xác nhận cấp máy. Nếu lưu cục bộ lỗi, chỉ thử lại bước cục bộ.
    private async Task Submit()
    {
        if(busy)return;
        if(saved)
        {
            try{PosDeviceCredentials.Save(machine.Text.Trim(),key.Text);RefreshLocalClient();feedback.ForeColor=Color.DarkGreen;Error("Đã lưu cấu hình POS được bảo vệ trên máy này.");save.Visible=false;}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or CryptographicException or InvalidOperationException){Error("Cấu hình máy đã được lưu trên server; chưa áp dụng được trên máy Windows này. Hãy thử lại.");}
            return;
        }
        if(pending is null)
        {
            operation=Guid.NewGuid();
            if(revoke){var input=new RevokePosDevice(operation,device!.PhienBanDuLieu,reason.Text);var id=device.MaThietBiPOS;pending=()=>LiveSession.Api.RevokePosDeviceAsync(id,input);}
            else{var input=new RegisterPosDevice(operation,machine.Text,name.Text,key.Text,reason.Text,device?.PhienBanDuLieu);pending=()=>LiveSession.Api.RegisterPosDeviceAsync(input);}
        }
        busy=true;SetEnabled(false);
        try
        {
            var result=uncertain?await LiveSession.Api.GetDeviceOperationAsync(operation):null;result??=await pending();
            device=result.Device;saved=true;uncertain=false;pending=null;
            if(!revoke&&local.Checked)
            {
                try{PosDeviceCredentials.Save(device.MaMay,key.Text);RefreshLocalClient();}
                catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or CryptographicException or InvalidOperationException)
                {save.Text="Lưu cấu hình máy này";Error("Đã cấp máy trên server. Chưa lưu được cấu hình POS cục bộ; chọn Lưu cấu hình máy này để thử lại.");return;}
            }
            if(revoke){DialogResult=DialogResult.OK;Close();return;}
            feedback.ForeColor=Color.DarkGreen;Error("Đã cấp cấu hình. Khóa chỉ xuất hiện ở biểu mẫu này; không thể đọc lại từ server.");
            key.ReadOnly=true;key.Enabled=true;save.Visible=false;
        }
        catch(CafeApiException ex)
        {
            uncertain=ex.StatusCode is null||(int)ex.StatusCode>=500;Error(ex.Message);
            if(!uncertain)pending=null;save.Text=uncertain?"Kiểm tra / thử lại":revoke?"Xác nhận thu hồi":"Cấp cấu hình";
        }
        finally{busy=false;if(!IsDisposed){SetEnabled(!saved);save.Enabled=true;if(saved)key.Enabled=!revoke;}}
    }
    private void SetEnabled(bool enabled){foreach(var control in new Control[]{machine,name,key,reason,local})control.Enabled=enabled&&!uncertain;save.Enabled=enabled;}
    private static void RefreshLocalClient(){var credentials=PosDeviceCredentials.Load();if(credentials is not null){LiveSession.Api.PosMachine=credentials.MaMay;LiveSession.Api.PosKey=credentials.KhoaThietBi;}}
    private void Error(string text){feedback.Text=text;feedback.Visible=true;}
}
