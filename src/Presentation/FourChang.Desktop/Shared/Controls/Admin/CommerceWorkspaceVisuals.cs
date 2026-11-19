using FourChang.Desktop.Shared.Theme;
namespace FourChang.Desktop.Shared.Controls.Admin;

/// <summary>Chuẩn hóa màn hình đơn/hóa đơn, chỉ căn chỉnh control và định dạng dữ liệu, không sửa nghiệp vụ.</summary>
internal static class CommerceWorkspaceVisuals
{
    /// <summary>Bộ lọc hai hàng dùng riêng cho bốn trang giao dịch Admin; giữ control/handler và giá trị đang chọn.</summary>
    internal static void AdminFilters(FlowLayoutPanel filters, TextBox search, DateTimePicker from, DateTimePicker to,
        params (string Caption, Control Input)[] extra)
    {
        filters.Controls.Clear(); search.Width = 360; from.Width = to.Width = 150;
        var end = Ui.Field("Đến ngày", to);
        filters.Controls.AddRange([Ui.Field("Tìm kiếm", search), Ui.Field("Từ ngày", from), end]);
        filters.SetFlowBreak(end, true);
        foreach (var (caption, input) in extra)
        {
            input.Width = 150;
            if (input is ComboBox choice) choice.DropDownWidth = Math.Max(230, choice.Width);
            filters.Controls.Add(Ui.Field(caption, input));
        }
        foreach (Control field in filters.Controls) field.Margin = new Padding(0, 0, 12, 8);
        OperationsPresentation.FitRows(filters);
    }

    /// <summary>Đồng bộ hàng, chữ, tiền và đường phân cách; chỉ opt-in ở bốn trang Admin, không đổi POS/trang khác.</summary>
    internal static void AdminStyle(FlowLayoutPanel filters, FlowLayoutPanel actions, DataGridView grid)
    {
        foreach (var button in filters.Controls.OfType<FlowLayoutPanel>().SelectMany(x => x.Controls.OfType<Button>()))
        {
            button.MinimumSize = new Size(106, 44);
            if (button.Text == "Tìm kiếm") { button.BackColor = CafeTheme.Coffee; button.ForeColor = Color.White; button.FlatAppearance.BorderColor = CafeTheme.Coffee; }
        }
        foreach (var button in actions.Controls.OfType<Button>()) { button.MinimumSize = new Size(110, 44); button.Margin = new Padding(0, 0, 10, 8); }
        void Style()
        {
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.AdvancedCellBorderStyle.Left = grid.AdvancedCellBorderStyle.Right = DataGridViewAdvancedCellBorderStyle.None;
            grid.AdvancedCellBorderStyle.Top = DataGridViewAdvancedCellBorderStyle.None;
            grid.AdvancedCellBorderStyle.Bottom = DataGridViewAdvancedCellBorderStyle.Single;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.ColumnHeadersHeight = 48; grid.RowTemplate.Height = 64; grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            foreach (DataGridViewRow row in grid.Rows) row.Height = 64;
            grid.DefaultCellStyle.Font = CafeTheme.Body(9.5f);
            grid.DefaultCellStyle.Padding = new Padding(10, 10, 10, 10);
            grid.DefaultCellStyle.SelectionBackColor = CafeTheme.Selection;
            grid.DefaultCellStyle.SelectionForeColor = CafeTheme.Ink;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(251, 248, 243);
            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (column.DataPropertyName is "TenKhachHang" or "NguoiMua" or "NguoiNhan" or "TaoLuc" or "PhatHanhLuc") column.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                if (column.DataPropertyName is "MaDon" or "SoHoaDon") column.DefaultCellStyle.Font = CafeTheme.Strong(9.5f);
                if (column.DataPropertyName is "SoTien" or "TienHang" or "PhiGiaoHang" or "TaiChinh" or "DuocDuyet" or "DaHoan" or "ConHoan" or "TienMatThucDem")
                {
                    column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
                    column.DefaultCellStyle.Font = CafeTheme.Strong(9.5f);
                }
            }
        }
        Style(); grid.DataBindingComplete += (_, _) => Style();
        grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (grid.Columns[e.ColumnIndex].DataPropertyName is "TaoLuc" or "PhatHanhLuc" && grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value is DateTime at)
            { e.Value = at.ToLocalTime().ToString("dd/MM/yyyy\nHH:mm"); e.FormattingApplied = true; }
        };
    }

    /// <summary>Badge chung: xanh đã xong, đỏ lỗi/từ chối, nâu chờ; không suy trạng thái thanh toán từ màu.</summary>
    internal static void StatusBadge(DataGridView grid, DataGridViewCellPaintingEventArgs e, string caption, bool positive, bool attention = false)
    {
        e.PaintBackground(e.ClipBounds, true);
        var bounds = Rectangle.Inflate(e.CellBounds, -8, -15);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        var background = positive ? Color.FromArgb(231, 241, 228) : attention ? Color.FromArgb(251, 235, 230) : Color.FromArgb(244, 234, 218);
        var foreground = positive ? Color.FromArgb(44, 101, 61) : attention ? CafeTheme.Danger : CafeTheme.Coffee;
        using var path = CafeTheme.Rounded(bounds, 8); using var brush = new SolidBrush(background);
        e.Graphics!.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; e.Graphics.FillPath(brush, path);
        TextRenderer.DrawText(e.Graphics, caption, grid.Font, Rectangle.Inflate(bounds, -5, 0), foreground,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        e.Handled = true;
    }
    /// <summary>Đưa phân trang xuống cuối danh sách, giữ nguyên button/event hiện có và vị trí dữ liệu.</summary>
    internal static void Apply(FlowLayoutPanel actions, Label counter, Label pagination, DataGridView grid)
    {
        var buttons = actions.Controls.OfType<Button>().Where(x => x.Text is "Trang trước" or "Trang sau").ToArray();
        if (counter.Parent is TableLayoutPanel parent)
        {
            var row = parent.GetRow(counter);
            parent.Controls.Remove(counter);
            var footer = Ui.Row(); footer.Name = "CommercePagination";
            foreach (var button in buttons) { actions.Controls.Remove(button); footer.Controls.Add(button); }
            actions.Controls.Remove(pagination); footer.Controls.Add(pagination); footer.Controls.Add(counter);
            parent.Controls.Add(footer,0,row);
        }
        foreach (var button in actions.Controls.OfType<Button>())
        {
            button.MinimumSize = new Size(106,44); button.Margin = new Padding(0,0,8,8);
            if (button.Text.StartsWith("Hủy", StringComparison.Ordinal)) button.ForeColor = CafeTheme.Danger;
        }
        var end = actions.Controls.OfType<Button>().FirstOrDefault(x => x.Text == "Bàn giao / hoàn tất");
        if (end is not null) actions.SetFlowBreak(end,true);
        FormatGrid(grid);
    }

    /// <summary>Tiền/ngày/trạng thái giữ giá trị gốc; bảng có hàng cao, chữ không sát viền và selected state rõ.</summary>
    internal static void FormatGrid(DataGridView grid)
    {
        grid.BorderStyle=BorderStyle.None;grid.BackgroundColor=CafeTheme.Surface;
        grid.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;
        grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersHeight=46;grid.RowTemplate.Height=48;
        grid.DefaultCellStyle.Padding=new Padding(10,8,10,8);
        grid.ColumnHeadersDefaultCellStyle.Padding=new Padding(10,8,10,8);
        grid.DefaultCellStyle.SelectionBackColor=CafeTheme.Selection;grid.DefaultCellStyle.SelectionForeColor=CafeTheme.Ink;
        grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(251,248,243);
        grid.ShowCellToolTips=true;
        foreach(DataGridViewColumn column in grid.Columns)
        {
            column.MinimumWidth=Math.Max(column.MinimumWidth,TextRenderer.MeasureText(column.HeaderText,CafeTheme.Strong(9)).Width+28);
            if(column.DataPropertyName is "SoTien" or "TienHang" or "PhiGiaoHang")
            {column.DefaultCellStyle.Format="#,##0'đ'";column.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleRight;}
        }
    }
}
