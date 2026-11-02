using FourChang.Desktop.Controls;
using FourChang.Desktop.DesignSystem;
using FourChang.Desktop.Features.Database;
using FourChang.Presentation.Shared.Integration;
using FourChang.Presentation.Shared.Models;
namespace FourChang.Desktop.Features.Admin.Customers;

public sealed class CustomerDetailsForm : Form
{
    public CustomerDetailsForm(CustomerDetails detail)
    {
        var customer = detail.Account;
        Text = "Hồ sơ khách hàng"; Name = "CustomerDetailsForm"; ClientSize = new Size(860, 720);
        MinimumSize = new Size(760, 600); StartPosition = FormStartPosition.CenterParent;
        BackColor = CafeTheme.Cream; Font = CafeTheme.Body(); AutoScroll = true;
        var transactions = Ui.Grid(new PreviewTable(["Loại giao dịch", "Mã", "Trạng thái"],
            detail.GiaoDichGanDay.Select(x => new[] { x.Loai, x.Ma, x.TrangThai }).ToArray()), 260);
        var references = Ui.Grid(new PreviewTable(["Nguồn tham chiếu", "Số bản ghi"],
            detail.ThamChieu.Where(x => x.SoLuong > 0).Select(x => new[] { DatabaseLabels.Table(x.Bang.Split('.')[0]), x.SoLuong.ToString() }).ToArray()), 200);
        // Chỉ xem chứng cứ/tham chiếu của tài khoản; không sửa dữ liệu tài chính trong UC này.
        var close = Ui.Button("Đóng hồ sơ", () => Close());
        var body = Ui.Panel(customer.HoTen, close,
            Ui.Label($"Mã khách: {customer.MaKhachHang} · Tên đăng nhập: {customer.TenDangNhap}"),
            Ui.Label($"Email: {customer.Email ?? "—"} · Điện thoại: {customer.SoDienThoai ?? "—"}"),
            Ui.Label($"Trạng thái: {customer.TrangThai} · Cần đổi mật khẩu tạm: {(customer.BatBuocDoiMatKhau ? "Có" : "Không")}"),
            Ui.Label(detail.CoTheXoa ? "Chưa có tham chiếu; việc xóa vẫn được kiểm tra lại khi xác nhận." : "Có tham chiếu; giữ lịch sử và dùng khóa/ngừng khi cần."),
            Ui.Tabs(("Giao dịch gần đây", transactions), ("Tham chiếu dữ liệu", references)));
        body.Padding = new Padding(20); Controls.Add(body);
        // Nút đầu form nhận focus để TabControl phía dưới không tự cuộn che tên khách khi vừa mở.
        Shown += (_, _) => { ActiveControl = close; AutoScrollPosition = Point.Empty; };
    }
}
