using System.Drawing.Drawing2D;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Shared.Orders;

public partial class OrdersWorkspacePage
{
    /// <summary>Hai hàng lọc rõ ràng: thông tin đơn ở hàng đầu, khoảng ngày/ca và nút tìm ở hàng sau.</summary>
    private void BuildOrderFilters()
    {
        SearchBox.Width = 300; SearchBox.PlaceholderText = "Mã đơn hoặc tên khách hàng";
        channel.Width = fulfillment.Width = state.Width = 150;
        var statusField = Ui.Field(cancellations ? "Kết quả yêu cầu" : "Trạng thái đơn", state);
        Filters.Controls.AddRange([Ui.Field("Tìm đơn hàng", SearchBox), Ui.Field("Nguồn đơn", channel), Ui.Field("Nhận món", fulfillment), statusField]);
        Filters.SetFlowBreak(statusField, true);
        Filters.Controls.Add(Ui.Field("Từ ngày", from)); Filters.Controls.Add(Ui.Field("Đến ngày", to));
        if(staff&&!cancellations){shiftGroup.Name="OrderShiftGroup";shiftGroup.Width=260;Filters.Controls.Add(Ui.Field("Nhóm đơn theo ca",shiftGroup));}
        else Filters.Controls.Add(Ui.Field("Ca làm việc (0: tất cả)", shift));
        foreach (Control field in Filters.Controls) field.Margin = new Padding(0, 0, 12, 12);
        if (counterOnly) { state.Items.Remove("PENDING_CONFIRMATION"); state.SelectedIndex = 0; }
    }

    /// <summary>Chỉ là nhãn trình bày, không đổi mã trạng thái hoặc coi hủy thành công là đã chi tiền.</summary>
    private string OrderStatusCaption(AdminOrderRow row)
    {
        if (counterOnly) return row.TaiChinh.PhaiHoan > 0 ? "Chờ hoàn tiền tại quầy" : "Đã hoàn tiền";
        if (!cancellations) return OperationsPresentation.Label(row.TrangThai);
        return row.YeuCauHuy?.TrangThai switch
        {
            "REQUESTED" => "Chờ gửi duyệt", "FORWARDED" => "Chờ Quản lý duyệt",
            _ when row.TrangThai == "CANCELLED" => "Hủy thành công", _ => OperationsPresentation.Label(row.TrangThai)
        };
    }

    /// <summary>Chuẩn hóa bảng và badge, ưu tiên mã đơn/trạng thái/ngày, giảm các cột kỹ thuật ở POS.</summary>
    private void ConfigureOrderAppearance()
    {
        Grid.RowTemplate.Height = 56; Grid.ColumnHeadersHeight = 46;
        Grid.DataBindingComplete += (_, _) => RefineOrderGridBorders();
        RefineOrderGridBorders();
        Grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        Grid.DefaultCellStyle.Padding = new Padding(12, 8, 12, 8);
        Grid.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex < 0 || e.RowIndex < 0 || Grid.Rows[e.RowIndex].DataBoundItem is not AdminOrderRow row) return;
            var property = Grid.Columns[e.ColumnIndex].DataPropertyName;
            if (property == "TrangThai") { e.Value = OrderStatusCaption(row); e.FormattingApplied = true; }
            if (property == "TaiChinh") { e.Value = (counterOnly ? row.TaiChinh.PhaiHoan : row.TaiChinh.DaThu).ToString("N0") + "đ"; e.FormattingApplied = true; }
            if (property == "TaoLuc") { e.Value = row.TaoLuc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"); e.FormattingApplied = true; }
        };
        Grid.CellPainting += PaintOrderStatus;
    }

    /// <summary>Bỏ các vạch dọc nặng mắt; mã đơn ngắn đậm, tiền căn phải, nền chọn dịu cùng palette cà phê.</summary>
    private void RefineOrderGridBorders()
    {
        Grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        Grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        Grid.GridColor = Color.FromArgb(232, 221, 207);
        Grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(243, 230, 210);
        foreach (DataGridViewColumn column in Grid.Columns)
        {
            if (column.DataPropertyName == "MaDon") column.DefaultCellStyle.Font = CafeTheme.Strong(10);
            if (column.DataPropertyName is "TienHang" or "PhiGiaoHang" or "TaiChinh") column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
    }

    /// <summary>Vẽ badge gọn trong ô, giữ nguyên nền chọn dòng và màu chữ dễ đọc.</summary>
    private void PaintOrderStatus(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || Grid.Columns[e.ColumnIndex].DataPropertyName != "TrangThai" || Grid.Rows[e.RowIndex].DataBoundItem is not AdminOrderRow row) return;
        e.PaintBackground(e.ClipBounds, true);
        var positive = counterOnly ? row.TaiChinh.PhaiHoan == 0 : row.TrangThai is "CANCELLED" or "COMPLETED";
        var foreground = positive ? Color.FromArgb(44, 101, 61) : CafeTheme.Coffee;
        var background = positive ? Color.FromArgb(231, 241, 228) : Color.FromArgb(244, 234, 218);
        var bounds = Rectangle.Inflate(e.CellBounds, -9, -13);
        using var path = CafeTheme.Rounded(bounds, 8); using var fill = new SolidBrush(background);
        e.Graphics!.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.FillPath(fill, path);
        TextRenderer.DrawText(e.Graphics, OrderStatusCaption(row), Grid.Font, Rectangle.Inflate(bounds, -6, 0), foreground,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        e.Handled = true;
    }
}
