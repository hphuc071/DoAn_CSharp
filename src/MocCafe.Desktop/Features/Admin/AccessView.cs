using MocCafe.Desktop.Controls;
using MocCafe.Desktop.Preview;
using MocCafe.Presentation.Shared.Models;
namespace MocCafe.Desktop.Features.Admin;
public static class AccessView
{
    public static Control Create()=>Ui.Tabs(
        ("Tài khoản quản trị",ManagementPage.Create("Admin và quyền quản trị",new PreviewTable(["Mã","Tên đăng nhập","Vai trò","Trạng thái","Lần truy cập"],[["ADM01","quanly","Quản lý","Hoạt động","10:24"],["ADM02","kiemsoat","Kiểm soát hoàn","Hoạt động","09:15"]]),"Không vô hiệu Admin cuối cùng; thay quyền cần lý do và ghi lịch sử. Không tạo tài khoản nhân viên.",("Thêm Admin",["Tên đăng nhập","Họ tên","Vai trò","Mật khẩu tạm"]),("Sửa / Khóa Admin",["Vai trò","Trạng thái","Lý do"]),("Reset mật khẩu",["Mật khẩu tạm","Lý do"]))),
        ("Vai trò và quyền",ManagementPage.Create("Quyền theo vai trò",new PreviewTable(["Vai trò","Thực đơn","Kho","Hoàn tiền","Báo cáo","Audit"],[["Quản lý","Có","Có","Có","Có","Có"],["Kiểm soát hoàn","Xem","Không","Duyệt","Xem","Theo phạm vi"],["Staff trên POS","Chỉ đọc","Không","Không","Không","Không"]]),"Quyền báo cáo, audit và quyền xuất tách riêng. Tên tự nhập trên POS không cấp quyền Admin.",("Cấu hình quyền",["Vai trò","Danh sách quyền","Phạm vi dữ liệu","Lý do thay đổi"]))),
        ("Thiết bị POS",ManagementPage.Create("Máy được phép",new PreviewTable(["TerminalId","Tên máy","Trạng thái","Ca hiện tại","Quyền"],[["POS01","Quầy chính","Được phép","CA024 / Lan","Staff cố định"],["POS02","Quầy phụ","Ngừng","Không có ca","Staff cố định"]]),"Thiết bị bị thu hồi không nhận lệnh mới; ca đang mở phải bàn giao và giữ lịch sử.",("Đăng ký máy",["TerminalId","Tên máy","Tập quyền POS"]),("Thu hồi thiết bị",["TerminalId","Lý do","Bàn giao ca liên quan"]))));
    public static void ShowLogin(IWin32Window? owner)=>PreviewDialog.ShowMessage(owner,"Đăng nhập quản lý","Nhập tài khoản và mật khẩu quản lý.","Tài khoản Admin","Mật khẩu Admin");
}
