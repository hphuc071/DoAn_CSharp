using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Shared.Orders;

/// <summary>Chi tiết đơn dùng chung cho Admin/POS, chỉ có thông tin đơn, khách, món và thanh toán.</summary>
public sealed class OrderDetailsForm : Form
{
    /// <summary>Hiển thị snapshot đã chốt; không sửa đơn, giá, kho hoặc trạng thái thanh toán.</summary>
    public OrderDetailsForm(AdminOrderDetail data)
    {
        Name = "BasicOrderDetails"; Text = "Chi tiết đơn " + data.Don.MaDon;
        ClientSize = new(1080, 790); MinimumSize = new(900, 650);
        StartPosition = FormStartPosition.CenterParent; Font = CafeTheme.Body(); BackColor = CafeTheme.Cream;
        var order = data.Don;
        var information = Section("Thông tin đơn", Value("Mã đơn", order.MaDon),
            Value("Thời gian đặt", order.TaoLuc.ToLocalTime().ToString("dd/MM/yyyy HH:mm")),
            Value("Trạng thái", OperationsPresentation.Label(order.TrangThai)), Value("Nguồn", order.Kenh),
            Value("Hình thức nhận", OperationsPresentation.Label(order.HinhThuc)));
        information.Name = "OrderInformation";
        var customerFields = new List<Control> { Value("Khách hàng", string.IsNullOrWhiteSpace(data.TenNguoiNhan) ? "Khách tại quầy" : data.TenNguoiNhan), Value("Số điện thoại", data.DienThoai) };
        if (order.HinhThuc == "DINE_IN") customerFields.Add(Value("Bàn", data.Ban));
        // Giao hàng dùng địa chỉ đã chốt; đơn khác hiển thị địa chỉ hồ sơ khách nếu có.
        customerFields.Add(order.HinhThuc == "DELIVERY"
            ? Value("Địa chỉ giao hàng", data.DiaChi, 480)
            : Value("Địa chỉ khách hàng", data.DiaChiKhachHang, 480));
        var customer = Section("Thông tin khách", customerFields.ToArray()); customer.Name = "OrderCustomer";
        var items = Section("Danh sách món", ItemGrid(data.Mon)); items.Name = "OrderItems";
        var methods = string.Join(" · ", (data.ThanhToan ?? []).Select(x => PaymentMethod(x.PhuongThuc)).Distinct());
        var paymentFields = new List<Control> { Value("Tiền món", Money(order.TienHang)) };
        if (order.HinhThuc == "DELIVERY") paymentFields.Add(Value("Phí giao", Money(order.PhiGiaoHang)));
        paymentFields.Add(Value("Tổng tiền", Money(order.TaiChinh.NghiaVu)));
        paymentFields.Add(Value("Phương thức", string.IsNullOrWhiteSpace(methods) ? "Chưa ghi nhận" : methods));
        paymentFields.Add(Value("Trạng thái thanh toán", PaymentStatus(data), 210));
        var payment = Section("Thanh toán", paymentFields.ToArray()); payment.Name = "OrderPayment";
        var content = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 4, Padding = new Padding(16) };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 4; row++) content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        foreach (var section in new Control[] { information, customer, items, payment }) { section.Dock = DockStyle.Top; content.Controls.Add(section); }
        var close = Ui.Button("Đóng", () => Close());
        AdminUi.Compose(this, content, close); CancelButton = close;
        // Không để grid tự nhận focus rồi kéo cửa sổ xuống giữa đơn ngay khi mở.
        Shown += (_, _) => { close.Focus(); if (content.Parent is ScrollableControl viewport) viewport.AutoScrollPosition = Point.Empty; };
    }

    /// <summary>Nhóm chỉ đọc có chiều cao tính theo số hàng thực, tránh Panel AutoSize giãn khoảng trắng.</summary>
    internal static Control Section(string title, params Control[] children)
    {
        var panel = new SurfacePanel { Padding = new Padding(16), Margin = new Padding(0, 0, 0, 12) };
        var heading = Ui.Label(title, 12, true); heading.Dock = DockStyle.Top; heading.AutoSize = false; heading.Height = 34;
        var body = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Margin = Padding.Empty };
        body.Controls.AddRange(children); panel.Controls.Add(body); panel.Controls.Add(heading);
        void Fit()
        {
            var width = Math.Max(1, panel.ClientSize.Width - panel.Padding.Horizontal);
            var used = 0; var rowHeight = 0; var height = 0;
            foreach (Control child in body.Controls)
            {
                if (child is DataGridView) child.Width = width;
                else if (child.Tag is int desiredWidth) child.Width = Math.Min(desiredWidth, width);
                var itemWidth = child.Width + child.Margin.Horizontal;
                if (used > 0 && used + itemWidth > width) { height += rowHeight; used = 0; rowHeight = 0; }
                used += itemWidth; rowHeight = Math.Max(rowHeight, child.Height + child.Margin.Vertical);
            }
            var target = panel.Padding.Vertical + heading.Height + height + rowHeight;
            if (panel.Height != target) panel.Height = target;
        }
        panel.SizeChanged += (_, _) => Fit(); Fit(); return panel;
    }

    /// <summary>Cặp nhãn/giá trị có chiều rộng ổn định; nội dung dài tự xuống dòng, không hiện ID kỹ thuật.</summary>
    internal static Control Value(string caption, string? text, int width = 180)
    {
        var panel = new FlowLayoutPanel { Width = width, Tag = width, Height = 62, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 0, 12, 0) };
        var label = Ui.Label(caption, 9); label.Margin = new Padding(0, 0, 0, 4);
        var value = Ui.Label(string.IsNullOrWhiteSpace(text) ? "Chưa cung cấp" : text, 10, true); value.MaximumSize = new(width - 8, 0);
        panel.Controls.Add(label); panel.Controls.Add(value);
        value.SizeChanged += (_, _) => panel.Height = Math.Max(62, label.Height + label.Margin.Vertical + value.Height + 8);
        return panel;
    }

    /// <summary>Chỉ hiển thị thông tin khách đặt, ẩn ID, phiên bản, số liệu kho và cột tiến trình kỹ thuật.</summary>
    private static DataGridView ItemGrid(IReadOnlyList<AdminOrderLine> lines)
    {
        var grid = Ui.Grid(new([], []), Math.Clamp(46 + lines.Count * 78, 125, 370));
        grid.Name = "OrderItemsGrid"; grid.Margin = Padding.Empty; grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True; grid.RowTemplate.MinimumHeight = 60;
        grid.RowsDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.DataSource = lines.Select(x => new
        {
            Mon = x.TenMon,
            TuyChon = $"{x.KichCo} · {(x.NhietDo == "HOT" ? "Nóng" : "Lạnh")} · Đường {x.Duong}%" + (x.NhietDo == "HOT" ? "" : $" · Đá {x.Da}%"),
            Them = string.Join(Environment.NewLine, new[] { string.IsNullOrWhiteSpace(x.MonThem) ? null : x.MonThem, string.IsNullOrWhiteSpace(x.GhiChu) ? null : "Ghi chú: " + x.GhiChu, x.DaHuy > 0 ? $"Đã hủy {x.DaHuy}/{x.SoLuong} ly" : null }.Where(t => t is not null)),
            SoLy = x.SoLuong - x.DaHuy, DonGia = x.DonGia, ThanhTien = x.DonGia * (x.SoLuong - x.DaHuy)
        }).ToList();
        grid.DataBindingComplete += (_, _) =>
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                column.HeaderText = column.DataPropertyName switch { "Mon" => "Món", "TuyChon" => "Cấu hình", "Them" => "Topping / ghi chú", "SoLy" => "Số ly", "DonGia" => "Đơn giá", _ => "Thành tiền" };
                column.FillWeight = column.DataPropertyName switch { "Mon" => 100, "TuyChon" => 180, "Them" => 160, "SoLy" => 45, _ => 95 };
                if (column.DataPropertyName is "DonGia" or "ThanhTien") { column.DefaultCellStyle.Format = "#,##0'đ'"; column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; column.MinimumWidth = 105; }
                if (column.DataPropertyName == "SoLy") column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                if (column.DataPropertyName == "Them") column.Visible = lines.Any(x => !string.IsNullOrWhiteSpace(x.MonThem) || !string.IsNullOrWhiteSpace(x.GhiChu) || x.DaHuy > 0);
            }
            grid.AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCells);
        };
        grid.SizeChanged += (_, _) => { if (grid.Columns.Count > 0) grid.AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCells); };
        return grid;
    }

    /// <summary>Định dạng tiền Việt Nam, dùng giá đã chốt trong phản hồi API.</summary>
    private static string Money(decimal value) => value.ToString("N0") + "đ";

    /// <summary>Dịch phương thức thanh toán; không thay mã nghiệp vụ lưu trong database.</summary>
    private static string PaymentMethod(string method) => method switch { "CASH" => "Tiền mặt", "BANK_TRANSFER" => "Chuyển khoản", _ => method };

    /// <summary>Tóm tắt tình trạng thực thu/hoàn và số dư; không coi đơn hoàn tất là bằng chứng đã thu tiền.</summary>
    internal static string PaymentStatus(AdminOrderDetail data)
    {
        var money = data.Don.TaiChinh;
        if (money.PhaiHoan > 0) return "Còn tiền cần hoàn";
        if (money.DaHoan > 0 && money.DaHoan >= money.DaThu) return "Đã hoàn tiền";
        if (money.ConThu > 0) return money.DaThu > 0 ? "Đã thanh toán một phần" : "Chưa thanh toán";
        if (money.DaThu > 0) return money.DaHoan > 0 ? "Đã thanh toán · hoàn một phần" : "Đã thanh toán";
        return "Không còn tiền phải thanh toán";
    }
}
