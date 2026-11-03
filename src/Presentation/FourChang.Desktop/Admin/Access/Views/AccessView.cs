using FourChang.Desktop.Admin.Access.Views;
using FourChang.Desktop.Shared.Preview;
namespace FourChang.Desktop.Admin.Access.Views;
// Cùng một màn hình cho route access; không giữ mẫu cũ có Admin thứ hai hoặc tài khoản nhân viên.
public static class AccessView
{
 public static Control Create()=>new AccessPage();
 public static void ShowLogin(IWin32Window? owner)=>PreviewDialog.ShowMessage(owner,"Đăng nhập quản lý","Nhập tài khoản quản lý đã cấp.","Tài khoản Admin","Mật khẩu Admin");
}
