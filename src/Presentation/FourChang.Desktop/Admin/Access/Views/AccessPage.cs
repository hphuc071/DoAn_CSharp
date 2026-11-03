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
    private readonly DataGridView devices=Ui.Grid(new PreviewTable([],[]),330);
    private readonly Label accountInfo=Ui.Label("Đang tải tài khoản…",11);
    private readonly Label accountState=Ui.Label("",10,false,AdminColors.Muted);
    private List<PosDeviceInfo> records=[];private bool loading;
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
        devices.Name="PosDevicesGrid";devices.AutoGenerateColumns=false;devices.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
        foreach(var (property,title) in new[]{("MaMay","Mã máy"),("TenMay","Tên máy"),("TrangThai","Trạng thái"),("SoCaChuaKetThuc","Ca chưa kết thúc"),("LyDo","Lý do gần nhất")})
            devices.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName=property,HeaderText=title,MinimumWidth=110});
        devices.ColumnHeadersDefaultCellStyle.BackColor=AdminColors.Soft;devices.RowTemplate.Height=50;
        devices.CellFormatting+=(_,e)=>{if(e.ColumnIndex==2&&e.Value is string status)e.Value=status=="ACTIVE"?"Được phép":"Đã thu hồi";};
        var register=Button("Đăng ký máy",AdminIcon.Add,()=>EditDevice(null),true);register.Name="RegisterPosDevice";
        var grant=Button("Cấp lại",AdminIcon.Key,()=>SelectedDevice(d=>EditDevice(d)));
        var revoke=Button("Thu hồi",AdminIcon.Lock,()=>SelectedDevice(d=>EditDevice(d,true)));revoke.Name="RevokePosDevice";
        var shifts=Button("Ca liên quan",AdminIcon.View,()=>SelectedDevice(ShowShifts));shifts.Name="DeviceRelatedShifts";
        var reload=Button("Tải lại",AdminIcon.Refresh,LoadDevices);
        var devicePage=Card("Thiết bị POS",Ui.Row(register,grant,revoke,shifts,reload),devices,
            Ui.Label("Mỗi máy có khóa riêng và tập quyền Nhân viên cố định. Thu hồi không xóa ca hoặc giao dịch đã phát sinh.",9,false,AdminColors.Muted));
        var permissions=Ui.Grid(new PreviewTable(["Phạm vi","Khách hàng","Nhân viên POS","Quản lý"],[
            ["Tài khoản cá nhân","Của mình","Không có tài khoản","Quản lý khách hàng"],
            ["Bán hàng / xử lý đơn","Đơn của mình","Theo ca và máy được phép","Giám sát / xử lý theo nghiệp vụ"],
            ["Thực đơn / kho","Chỉ xem món công bố","Chỉ đọc thực đơn","Quản trị"],
            ["Hoàn tiền / ghi sổ kho","Yêu cầu theo điều kiện","Không phê duyệt","Được phép theo nghiệp vụ"],
            ["Thiết bị / quyền Admin","Không","Không","Quản lý"],
            ["Báo cáo / nhật ký","Không","Không","Được phép"]]),380);
        var rights=Card("Ranh giới quyền truy cập",Ui.Label("Quyền POS cố định; chọn tên nhân viên hoặc biết mã ca không cấp quyền Quản lý.",10,false,AdminColors.Muted),permissions);
        var tabs=Ui.Tabs(("Tài khoản Quản lý",account),("Thiết bị POS",devicePage),("Phạm vi quyền",rights));tabs.Height=650;tabs.Name="AccessTabs";
        var outer=Card("Quyền truy cập hệ thống",Ui.Label("Tài khoản quản trị, quyền POS và trạng thái thiết bị được kiểm tra tại máy chủ.",10,false,AdminColors.Muted),error,tabs);
        Controls.Add(outer);var initialized=false;
        HandleCreated+=async(_,_)=>{if(initialized)return;initialized=true;await LoadAccount();await LoadDevices();};
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
        button.Click+=async(_,_)=>{if(loading)return;button.Enabled=false;try{await action();}catch(CafeApiException ex){Error(ex.Message);}finally{if(!button.IsDisposed)button.Enabled=true;}};return button;
    }
    private async Task LoadAccount()
    {
        try{var account=await controller.LoadAccountAsync();if(IsDisposed)return;
            accountInfo.Text=$"{account.HoTen}  ·  {account.TenDangNhap}";
            accountState.Text=$"Trạng thái: {(account.TrangThai=="ACTIVE"?"Hoạt động":"Không hoạt động")}  ·  Lần đăng nhập: {account.DangNhapLanCuoiLuc?.ToLocalTime():dd/MM/yyyy HH:mm}";error.Visible=false;}
        catch(CafeApiException ex){if(!IsDisposed)Error(ex.Message);}
    }
    private async Task LoadDevices()
    {
        loading=true;try{var data=await controller.LoadDevicesAsync();if(IsDisposed)return;records=data;devices.DataSource=records;error.Visible=false;}
        catch(CafeApiException ex){if(!IsDisposed)Error(ex.Message);}finally{loading=false;}
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
    private async Task EditDevice(PosDeviceInfo? device,bool revoke=false)
    {using var form=new PosDeviceForm(device,revoke);if(form.ShowDialog(FindForm())==DialogResult.OK)await LoadDevices();}
    private async Task SelectedDevice(Func<PosDeviceInfo,Task> action)
    {if(devices.CurrentRow?.DataBoundItem is not PosDeviceInfo selected){Error("Chọn một thiết bị trong danh sách.");return;}await action(selected);}
    private async Task ShowShifts(PosDeviceInfo device)
    {
        var shifts=await controller.LoadShiftsAsync(device.MaThietBiPOS);
        using var form=new Form{Text="Ca liên quan · "+device.MaMay,ClientSize=new Size(850,610),BackColor=CafeTheme.Cream,Font=CafeTheme.Body(),StartPosition=FormStartPosition.CenterParent};
        var grid=Ui.Grid(new PreviewTable(["Mã ca","Nhân viên","Ca","Trạng thái","Tiền dự kiến"],shifts.Select(s=>new[]{s.MaCaHienThi,s.NhanVien,s.TenCa,s.TrangThai,s.TienMatDuKien.ToString("N0")}).ToArray()),280);
        var actual=new NumericUpDown{Maximum=1_000_000_000,Width=280,ThousandsSeparator=true,Font=CafeTheme.Body()};
        var reason=Ui.Input("Lý do chốt / chênh lệch","",650);reason.MaxLength=500;
        var feedback=Ui.Label("",9,false,CafeTheme.Danger);feedback.MaximumSize=new Size(700,0);
        var close=new AdminActionButton("Chốt ca máy thu hồi",AdminIcon.Lock,true){Enabled=device.TrangThai=="REVOKED"&&shifts.Count>0};
        var operation=Guid.NewGuid();CloseDeviceShift? pending=null;long? pendingShift=null;bool unknown=false;bool busy=false;bool completed=false;
        close.Click+=async(_,_)=>
        {
            if(grid.CurrentRow is null||busy)return;busy=true;close.Enabled=false;
            try
            {
                var selected=shifts.Single(x=>x.MaCaHienThi==Convert.ToString(grid.CurrentRow.Cells[0].Value));pendingShift??=selected.MaCaLamViec;
                pending??=new(operation,selected.PhienBanDuLieu,actual.Value,reason.Text);
                var result=unknown?await controller.FindOperationAsync(operation):null;
                result??=await controller.CloseShiftAsync(device.MaThietBiPOS,pendingShift.Value,pending);
                completed=true;form.DialogResult=DialogResult.OK;form.Close();
            }
            catch(CafeApiException ex){unknown=ex.StatusCode is null||(int)ex.StatusCode>=500;feedback.Text=ex.Message;if(!unknown){pending=null;pendingShift=null;operation=Guid.NewGuid();}close.Text=unknown?"Kiểm tra / thử lại":"Chốt ca máy thu hồi";}
            finally{busy=false;if(!close.IsDisposed){close.Enabled=true;actual.Enabled=!unknown;reason.Enabled=!unknown;grid.Enabled=!unknown;}}
        };
        form.FormClosing+=(_,e)=>{if(!completed&&(busy||unknown)){e.Cancel=true;feedback.Text="Chưa rõ kết quả; chọn Kiểm tra / thử lại trước khi đóng.";}};
        form.Controls.Add(Card("Ca chưa kết thúc",grid,Ui.Field("Tiền mặt thực đếm",actual),Ui.Field("Lý do bắt buộc",reason),feedback,close));
        form.ShowDialog(FindForm());await LoadDevices();
    }
    private void Error(string text){error.Text=text;error.Visible=true;}
}
