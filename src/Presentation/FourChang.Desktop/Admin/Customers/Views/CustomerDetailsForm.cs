using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Theme;
using FourChang.Desktop.Shared.Database;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Preview;

namespace FourChang.Desktop.Admin.Customers.Views;

/// <summary>Hồ sơ khách hàng theo thẻ thông tin; lịch sử và địa chỉ chỉ đọc không thay đổi dữ liệu nguồn.</summary>
public sealed class CustomerDetailsForm : Form
{
    /// <summary>Giữ đầy đủ thông tin/tham chiếu, dùng trạng thái tiếng Việt và giao diện rỗng gọn khi chưa có giao dịch.</summary>
    public CustomerDetailsForm(CustomerDetails detail)
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        var customer = detail.Account;
        Text = "Hồ sơ khách hàng"; Name = "CustomerDetailsForm"; ClientSize = new Size(960, 800); MinimumSize = new Size(820, 660); StartPosition = FormStartPosition.CenterParent;
        Control transactions = detail.GiaoDichGanDay.Count == 0
            ? AdminProfileLayout.Empty("Chưa có giao dịch", "Các đơn hàng và chứng từ của khách sẽ hiển thị tại đây.")
            : Ui.Grid(new PreviewTable(["Loại giao dịch", "Mã", "Trạng thái"], detail.GiaoDichGanDay.Select(x => new[] { x.Loai, x.Ma, x.TrangThai }).ToArray()), 230);
        var items = detail.ThamChieu.Where(x => x.SoLuong > 0).ToList();
        Control references = items.Count == 0 ? AdminProfileLayout.Empty("Chưa có dữ liệu liên kết", "Hồ sơ hiện chưa có nguồn dữ liệu tham chiếu.")
            : Ui.Grid(new PreviewTable(["Nguồn tham chiếu", "Số bản ghi"], items.Select(x => new[] { DatabaseLabels.Table(x.Bang.Split('.')[0]), x.SoLuong.ToString() }).ToArray()), 230);
        var tabs = Ui.Tabs(("Giao dịch gần đây", transactions), ("Dữ liệu liên kết", references)); tabs.Height = 300;
        var close = Ui.Button("Đóng hồ sơ", Close);
        var personal = AdminProfileLayout.Card("Thông tin cá nhân", AdminProfileLayout.Info(
            ("Mã khách hàng", $"#{customer.MaKhachHang:0000}"), ("Tên đăng nhập", customer.TenDangNhap),
            ("Email", customer.Email), ("Điện thoại", customer.SoDienThoai),
            ("Trạng thái tài khoản", Status(customer.TrangThai)), ("Mật khẩu", customer.BatBuocDoiMatKhau ? "Cần đổi mật khẩu tạm" : "Đã thiết lập")));
        var pages = AdminProfileLayout.Tabs(("Thông tin cá nhân", personal),
            ("Địa chỉ giao hàng", new CustomerAddressBookPanel(customer, false)),
            ("Lịch sử & liên kết", tabs));
        pages.Name = "CustomerProfileTabs"; pages.Height = ClientSize.Height - 240;
        Resize += (_, _) => pages.Height = Math.Max(350, ClientSize.Height - 240);
        AdminProfileLayout.Mount(this, AdminProfileLayout.Header(customer.HoTen, $"Khách hàng · #{customer.MaKhachHang:0000}", Status(customer.TrangThai)), pages, close);
        Shown += (_, _) => ActiveControl = close;
    }

    /// <summary>Chuyển mã trạng thái API thành nhãn dễ đọc; không sửa giá trị tài khoản.</summary>
    internal static string Status(string status) => status switch { "ACTIVE" => "Hoạt động", "LOCKED" => "Đã khóa", "INACTIVE" => "Đã ngừng", _ => status };
}
