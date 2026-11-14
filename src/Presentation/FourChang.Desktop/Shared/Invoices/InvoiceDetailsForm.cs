using System.Globalization;
using System.Text.Json;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Orders;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Shared.Invoices;

/// <summary>Dòng món của hóa đơn đã chốt; đơn giá bao gồm topping cho một ly.</summary>
public sealed record InvoiceDisplayLine(string Mon, string Size, string Topping, int SoLuong, decimal DonGia)
{
    public decimal ThanhTien => SoLuong * DonGia;
}

/// <summary>Đọc snapshot hóa đơn POS/Web hiện có, không lấy giá hoặc số lượng hiện tại của đơn thay thế.</summary>
public static class InvoiceSnapshotReader
{
    /// <summary>Giữ nguyên số ly, tên, size và giá đã phát hành; hóa đơn cũ thiếu dòng món trả danh sách rỗng.</summary>
    public static IReadOnlyList<InvoiceDisplayLine> Read(string snapshot)
    {
        try
        {
            using var document = JsonDocument.Parse(snapshot);
            var root = document.RootElement;
            var lines = Field(root, "lines"); var toppings = Field(root, "toppings");
            if (lines.ValueKind != JsonValueKind.Array) return [];
            var result = new List<InvoiceDisplayLine>();
            foreach (var line in lines.EnumerateArray())
            {
                var id = Number(line, "MaChiTietDonHang");
                var extras = toppings.ValueKind == JsonValueKind.Array
                    ? toppings.EnumerateArray().Where(x => Number(x, "MaChiTietDonHang") == id).ToList() : [];
                var quantity = Number(line, "SoLuongDat"); var name = Text(line, "TenMonDaChot");
                if (id <= 0 || quantity <= 0 || quantity != decimal.Truncate(quantity) || quantity > int.MaxValue || string.IsNullOrWhiteSpace(name) || Field(line, "GiaMonDaChot").ValueKind != JsonValueKind.Number) return [];
                var price = Number(line, "GiaMonDaChot") + extras.Sum(x => Number(x, "SoPhanMoiLy") * Number(x, "GiaMoiPhanDaChot"));
                result.Add(new(name, Text(line, "KichCoDaChot"), string.Join(", ", extras.Select(x => Text(x, "TenMonThemDaChot") + " × " + Number(x, "SoPhanMoiLy"))), (int)quantity, price));
            }
            return result;
        }
        catch (Exception error) when (error is JsonException or OverflowException or InvalidOperationException) { return []; }
    }

    /// <summary>Chấp nhận khác biệt casing của serializer cũ/mới, không đọc giá từ chuỗi tên món.</summary>
    private static JsonElement Field(JsonElement item, string name) => item.ValueKind == JsonValueKind.Object
        ? item.EnumerateObject().FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Value : default;

    /// <summary>Đọc chuỗi của snapshot; thuộc tính không có được để trống, không tự đặt thông tin khách.</summary>
    private static string Text(JsonElement item, string name) => Field(item, name).ValueKind == JsonValueKind.String ? Field(item, name).GetString() ?? "" : "";

    /// <summary>Đọc số decimal chính xác đã lưu; không làm tròn giá trước khi tính thành tiền.</summary>
    private static decimal Number(JsonElement item, string name) => Field(item, name).ValueKind == JsonValueKind.Number ? Field(item, name).GetDecimal() : 0;
}

/// <summary>Hóa đơn cơ bản chỉ có năm phần; không hiện bảng thuộc tính nội bộ hoặc JSON kỹ thuật.</summary>
public sealed class InvoiceDetailsForm : Form
{
    /// <summary>Đọc hóa đơn đã phát hành cùng thông tin nhận món và thanh toán của đơn; không ghi lại hóa đơn.</summary>
    public InvoiceDetailsForm(AdminInvoiceView invoice, string snapshot, AdminOrderDetail order)
    {
        Name = "BasicInvoiceDetails"; Text = "Hóa đơn " + invoice.SoHoaDon;
        ClientSize = new(1080, 860); MinimumSize = new(900, 650); StartPosition = FormStartPosition.CenterParent;
        BackColor = CafeTheme.Cream; Font = CafeTheme.Body();
        var shop = OrderDetailsForm.Section("Thông tin quán", new BrandMark { Width = 54, Height = 54, Margin = new Padding(0, 0, 16, 0) },
            OrderDetailsForm.Value("Tên quán", "4CHANG LINH NGU LAM", 330)); shop.Name = "InvoiceShop";
        var information = OrderDetailsForm.Section("Thông tin hóa đơn",
            OrderDetailsForm.Value("Số hóa đơn", invoice.SoHoaDon, 220), OrderDetailsForm.Value("Mã đơn", order.Don.MaDon),
            OrderDetailsForm.Value("Phát hành", invoice.PhatHanhLuc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm")),
            OrderDetailsForm.Value("Trạng thái", OperationsPresentation.Label(invoice.TrangThai), 160),
            OrderDetailsForm.Value("Loại", OperationsPresentation.Label(invoice.Loai), 150));
        information.Name = "InvoiceInformation";
        var name = string.IsNullOrWhiteSpace(invoice.NguoiMua) ? order.TenNguoiNhan : invoice.NguoiMua;
        var customerFields = new List<Control> { OrderDetailsForm.Value("Tên khách hàng", string.IsNullOrWhiteSpace(name) ? "Khách lẻ" : name, 260), OrderDetailsForm.Value("Số điện thoại", order.DienThoai) };
        if (order.Don.HinhThuc == "DELIVERY") customerFields.Add(OrderDetailsForm.Value("Địa chỉ giao hàng", order.DiaChi, 460));
        var customer = OrderDetailsForm.Section("Thông tin khách hàng", customerFields.ToArray()); customer.Name = "InvoiceCustomer";
        var lines = InvoiceSnapshotReader.Read(snapshot);
        var items = OrderDetailsForm.Section("Danh sách món", lines.Count == 0
            ? Ui.Label(invoice.Loai == "ADJUSTMENT" ? "Hóa đơn điều chỉnh không có danh sách món riêng." : "Hóa đơn cũ chưa lưu đầy đủ danh sách món.") : Items(lines));
        items.Name = "InvoiceItems";
        var amounts = new List<Control>(); var itemTotal = lines.Sum(x => x.ThanhTien);
        var known = invoice.Loai == "ORIGINAL" && lines.Count > 0 && itemTotal <= invoice.SoTien;
        amounts.Add(OrderDetailsForm.Value("Tiền món", known ? Money(itemTotal) : "Chưa lưu chi tiết"));
        if (order.Don.HinhThuc == "DELIVERY") amounts.Add(OrderDetailsForm.Value("Phí giao", known ? Money(invoice.SoTien - itemTotal) : "Chưa lưu chi tiết"));
        amounts.Add(OrderDetailsForm.Value(invoice.Loai == "ADJUSTMENT" ? "Tổng điều chỉnh" : "Tổng thanh toán", Money(invoice.SoTien)));
        var methods = string.Join(" · ", (order.ThanhToan ?? []).Select(x => OperationsPresentation.Label(x.PhuongThuc)).Distinct());
        amounts.Add(OrderDetailsForm.Value("Phương thức", string.IsNullOrWhiteSpace(methods) ? "Chưa ghi nhận" : methods));
        amounts.Add(OrderDetailsForm.Value("Trạng thái thanh toán", OrderDetailsForm.PaymentStatus(order), 220));
        var payment = OrderDetailsForm.Section("Thanh toán", amounts.ToArray()); payment.Name = "InvoicePayment";
        var content = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 5, Padding = new Padding(16) };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var section in new[] { shop, information, customer, items, payment }) { content.RowStyles.Add(new RowStyle(SizeType.AutoSize)); section.Dock = DockStyle.Top; content.Controls.Add(section); }
        var close = Ui.Button("Đóng", () => Close()); AdminUi.Compose(this, content, close); CancelButton = close;
        Shown += (_, _) => { close.Focus(); if (content.Parent is ScrollableControl viewport) viewport.AutoScrollPosition = Point.Empty; };
    }

    /// <summary>Bảng món chỉ có cột phục vụ đọc hóa đơn; hiển thị tiền đồng và giữ số lượng tại lúc phát hành.</summary>
    private static Control Items(IReadOnlyList<InvoiceDisplayLine> lines)
    {
        var grid = Ui.Grid(new([], []), Math.Clamp(44 + lines.Count * 58, 110, 320)); grid.Name = "InvoiceItemsGrid";
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells; grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.DataSource = lines.ToList();
        grid.DataBindingComplete += (_, _) =>
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.HeaderText = column.DataPropertyName switch { "Mon" => "Món", "Size" => "Size", "Topping" => "Topping", "SoLuong" => "Số lượng", "DonGia" => "Đơn giá", _ => "Thành tiền" };
                column.FillWeight = column.DataPropertyName switch { "Mon" => 160, "Size" => 50, "SoLuong" => 60, "Topping" => 180, _ => 110 };
                column.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                if (column.DataPropertyName == "Topping") column.Visible = lines.Any(x => !string.IsNullOrWhiteSpace(x.Topping));
                if (column.DataPropertyName is "DonGia" or "ThanhTien") { column.DefaultCellStyle.Format = "#,##0'đ'"; column.DefaultCellStyle.FormatProvider = CultureInfo.GetCultureInfo("vi-VN"); column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; column.MinimumWidth = 110; }
            }
        };
        return grid;
    }

    /// <summary>Hiển thị số tiền Việt Nam có phân cách hàng nghìn và ký hiệu đồng.</summary>
    private static string Money(decimal amount) => amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + "đ";
}
