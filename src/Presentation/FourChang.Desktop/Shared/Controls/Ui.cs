using System.Data;
using FourChang.Desktop.Shared.Theme;
using FourChang.Desktop.Shared.Controls.Entry;
using FourChang.Desktop.Shared.Preview;
using FourChang.Presentation.Shared.Preview;

namespace FourChang.Desktop.Shared.Controls;

/// <summary>Thành phần trình bày dùng chung. Không chứa giá, phép tính nghiệp vụ hoặc lệnh ghi database.</summary>
public static class Ui
{
    /// <summary>Tạo nhãn hỗ trợ tiếng Việt, dùng font hệ thống và palette hiện hành.</summary>
    public static Label Label(string text, float size = 10, bool bold = false, Color? color = null) => new()
    {
        Text = text,
        UseMnemonic = false,
        AutoSize = true,
        Font = bold ? CafeTheme.Strong(size) : CafeTheme.Body(size),
        ForeColor = color ?? CafeTheme.Ink,
        Margin = new Padding(0, 0, 0, 9)
    };

    /// <summary>Tạo nút native giữ Click/Tab/Enter; chỉ phân biệt hành động chính, phụ và nguy hiểm bằng giao diện.</summary>
    public static Button Button(string text, Action? action = null, bool primary = false, bool danger = false)
    {
        var button = new CafeButton
        {
            Text = text,
            AccessibleName = text,
            AutoSize = true,
            MinimumSize = new Size(96, 44),
            Padding = new Padding(12, 7, 12, 7),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = CafeTheme.Strong(9),
            BackColor = primary ? CafeTheme.Coffee : CafeTheme.Surface,
            ForeColor = primary ? Color.White : danger ? CafeTheme.Danger : CafeTheme.Ink,
            Margin = new Padding(0, 0, 8, 8)
        };
        button.FlatAppearance.BorderColor = danger ? CafeTheme.Danger : primary ? CafeTheme.Coffee : CafeTheme.InputBorder;
        button.Click += (_, _) => { if (action is not null) action(); else PreviewDialog.ShowMessage(button.FindForm(), text); };
        return button;
    }

    /// <summary>Giữ rõ trạng thái chưa khả dụng; không gắn nghiệp vụ giả vào nút bị khóa.</summary>
    public static Button Unavailable(string text) { var button = Button(text); button.Enabled = false; return button; }

    /// <summary>Trạng thái rỗng có tên trợ năng và hai cấp thông tin; không nhầm với đang tải/lỗi.</summary>
    public static Control EmptyState(string title, string detail, int height = 220)
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = height, BackColor = CafeTheme.Surface, AccessibleName = title + ". " + detail };
        panel.Paint += (_, args) =>
        {
            using var heading = CafeTheme.Strong(11); using var body = CafeTheme.Body(9);
            var bounds = new Rectangle(20, height / 2 - 35, Math.Max(0, panel.Width - 40), 40);
            TextRenderer.DrawText(args.Graphics, title, heading, bounds, CafeTheme.Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            bounds.Y += 42; bounds.Height = 55;
            TextRenderer.DrawText(args.Graphics, detail, body, bounds, CafeTheme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
        };
        return panel;
    }

    /// <summary>Nhóm thao tác tự xuống dòng, không định vị tuyệt đối khiến nút bị cắt khi đổi kích thước.</summary>
    public static FlowLayoutPanel Row(params Control[] children)
    {
        var panel = new ContentFlowPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, WrapContents = true, Margin = Padding.Empty };
        foreach (var button in children.OfType<Button>())
        {
            button.Margin = new Padding(0, 0, 8, 8);
            button.MinimumSize = new Size(button.MinimumSize.Width, Math.Max(44, button.MinimumSize.Height));
            if (!button.AutoSize) button.Height = Math.Max(44, button.Height);
        }
        panel.Controls.AddRange(children); return panel;
    }

    /// <summary>Xếp dọc các control cùng instance; các handler và binding được giữ nguyên.</summary>
    public static FlowLayoutPanel Stack(params Control[] children)
    {
        var panel = Row(children); panel.FlowDirection = FlowDirection.TopDown; panel.WrapContents = false; return panel;
    }

    /// <summary>Danh sách chọn dùng ComboBox thật để giữ tìm theo bàn phím và SelectedIndexChanged.</summary>
    public static ComboBox Select(params string[] values)
    {
        var input = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170, Font = CafeTheme.Body(10), Margin = new Padding(0, 0, 8, 8) };
        input.Items.AddRange(values); if (values.Length > 0) input.SelectedIndex = 0; return input;
    }

    /// <summary>Tạo ô nhập; không can thiệp kiểm tra hoặc chuẩn hóa dữ liệu tại server.</summary>
    public static TextBox Input(string placeholder, string value = "", int width = 250) => new()
    {
        PlaceholderText = placeholder,
        Text = value,
        Width = width,
        Font = CafeTheme.Body(10),
        Margin = new Padding(0, 0, 8, 8),
        BorderStyle = BorderStyle.FixedSingle
    };

    /// <summary>Gắn nhãn cố định và viền chung; giữ chính input được truyền vào, không sao chép giá trị.</summary>
    public static Control Field(string name, Control input)
    {
        var panel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0, 0, 16, 8) };
        input.AccessibleName ??= name;
        Control presented = input;
        if (input is TextBox { Multiline: false } or ComboBox or NumericUpDown or DateTimePicker)
        {
            // Constructor của frame căn lại control con. Chốt kích thước ban đầu trước khi tạo
            // để các ô số/combobox không cùng bị giãn thành 414px rồi đẩy nút xuống dòng.
            var requestedWidth = input.Width;
            presented = new EntryInputFrame(input) { Width = Math.Max(150, requestedWidth), Margin = Padding.Empty };
        }
        panel.Controls.Add(Label(name, 9, true)); panel.Controls.Add(presented); return panel;
    }

    /// <summary>Thẻ có tiêu đề và khoảng cách thống nhất; không thay đổi thứ tự thao tác bên trong.</summary>
    public static SurfacePanel Panel(string title, params Control[] children)
    {
        var panel = new SurfacePanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 0, 16), Padding = new Padding(20) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = children.Length + 1, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row <= children.Length; row++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var heading = Label(title, 12, true); heading.Margin = new Padding(0, 0, 0, 18); layout.Controls.Add(heading);
        foreach (var child in children) { child.Margin = new Padding(0, 0, 0, 14); layout.Controls.Add(child); }
        panel.Controls.Add(layout); return panel;
    }

    /// <summary>Bảng chỉ đọc dùng chung, không bật sửa/xóa hay thay đổi DataSource của các trang nghiệp vụ.</summary>
    public static DataGridView Grid(PreviewTable sample, int height = 230)
    {
        var data = new DataTable();
        foreach (var column in sample.Columns) data.Columns.Add(column);
        foreach (var row in sample.Rows) data.Rows.Add(row.Cast<object>().ToArray());
        var grid = new DataGridView
        {
            DataSource = data,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            Dock = DockStyle.Top,
            Height = height,
            BackgroundColor = CafeTheme.Surface,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = CafeTheme.Line,
            EnableHeadersVisualStyles = false,
            Font = CafeTheme.Body(10),
            ColumnHeadersHeight = 42,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
        };
        grid.ColumnHeadersDefaultCellStyle = new() { BackColor = CafeTheme.Soft, ForeColor = CafeTheme.Muted, Font = CafeTheme.Strong(9), Padding = new Padding(10), SelectionBackColor = CafeTheme.Soft, SelectionForeColor = CafeTheme.Ink };
        grid.DefaultCellStyle = new() { BackColor = CafeTheme.Surface, ForeColor = CafeTheme.Ink, SelectionBackColor = CafeTheme.Selection, SelectionForeColor = CafeTheme.Ink, Padding = new Padding(10, 4, 10, 4) };
        grid.RowTemplate.Height = 48; grid.AlternatingRowsDefaultCellStyle.BackColor = CafeTheme.Cream;
        grid.ShowCellToolTips = true;
        grid.CellToolTipTextNeeded += (_, args) => { if (args.RowIndex >= 0 && args.ColumnIndex >= 0) args.ToolTipText = grid.Rows[args.RowIndex].Cells[args.ColumnIndex].Value?.ToString(); };
        grid.Paint += (_, args) =>
        {
            if (grid.Rows.Count > 0) return;
            using var font = CafeTheme.Body(10);
            var bounds = new Rectangle(16, grid.ColumnHeadersHeight + 20, Math.Max(0, grid.Width - 32), Math.Max(0, grid.Height - grid.ColumnHeadersHeight - 30));
            TextRenderer.DrawText(args.Graphics, "Chưa có dữ liệu", font, bounds, CafeTheme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
        return grid;
    }

    /// <summary>Tab native vẽ theo theme; caption đầy đủ qua tooltip, bàn phím Ctrl+Tab vẫn hoạt động.</summary>
    public static TabControl Tabs(params (string Title, Control Body)[] pages)
    {
        var tabs = new TabControl { Dock = DockStyle.Top, Height = 560, Font = CafeTheme.Body(10), Padding = new Point(15, 9), DrawMode = TabDrawMode.OwnerDrawFixed, SizeMode = TabSizeMode.Normal, ItemSize = new Size(160, 42), ShowToolTips = true };
        foreach (var (title, body) in pages)
        {
            var page = new TabPage(title) { ToolTipText = title, BackColor = CafeTheme.Cream, Padding = new Padding(16), AutoScroll = true };
            body.Dock = DockStyle.Top; page.Controls.Add(body); tabs.TabPages.Add(page);
        }
        tabs.DrawItem += (_, args) =>
        {
            var selected = args.Index == tabs.SelectedIndex;
            using var brush = new SolidBrush(selected ? CafeTheme.Soft : CafeTheme.Surface);
            args.Graphics.FillRectangle(brush, args.Bounds);
            using var font = selected ? CafeTheme.Strong(9) : CafeTheme.Body(9);
            TextRenderer.DrawText(args.Graphics, tabs.TabPages[args.Index].Text, font, Rectangle.Inflate(args.Bounds, -8, 0), selected ? CafeTheme.Accent : CafeTheme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (selected) { using var pen = new Pen(CafeTheme.Accent, 3); args.Graphics.DrawLine(pen, args.Bounds.Left + 12, args.Bounds.Bottom - 2, args.Bounds.Right - 12, args.Bounds.Bottom - 2); }
        };
        // Native TabControl tự cung cấp mũi tên khi thiếu chỗ; không gán ItemSize trong Resize vì gây vòng lặp native.
        return tabs;
    }

    /// <summary>Chỉ hiển thị KPI nhận từ caller; tự chuyển 4 cột thành 2×2 khi không đủ chiều ngang.</summary>
    public static Control Kpis(params (string Title, string Value, string Hint)[] values)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Top, Height = 138, Margin = new Padding(0, 0, 0, 16) };
        var cards = values.Select((value, index) => new MetricCard(value.Title, value.Value, value.Hint, index) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 12) }).ToArray();
        int previous = -1;
        void Arrange()
        {
            var columns = row.Width < 950 && cards.Length > 2 ? 2 : Math.Max(1, cards.Length);
            if (columns == previous) return; previous = columns;
            row.SuspendLayout(); row.Controls.Clear(); row.ColumnStyles.Clear(); row.RowStyles.Clear();
            row.ColumnCount = columns; row.RowCount = (cards.Length + columns - 1) / columns;
            row.Height = Math.Max(1, row.RowCount) * 138;
            for (var i = 0; i < columns; i++) row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            for (var i = 0; i < row.RowCount; i++) row.RowStyles.Add(new RowStyle(SizeType.Absolute, 138));
            for (var i = 0; i < cards.Length; i++) row.Controls.Add(cards[i], i % columns, i / columns);
            row.ResumeLayout(true);
        }
        row.Resize += (_, _) => Arrange(); Arrange(); return row;
    }

    /// <summary>Hai cột tự xếp dọc ở cửa sổ hẹp, giữ nguyên các control đang nhập và handler của chúng.</summary>
    public static Control Columns(Control left, Control right, float rightPercent = 34)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Margin = Padding.Empty };
        bool? previous = null;
        void Arrange()
        {
            var stacked = panel.Width < 840;
            if (previous == stacked) return; previous = stacked;
            panel.SuspendLayout(); panel.Controls.Clear(); panel.ColumnStyles.Clear(); panel.RowStyles.Clear();
            panel.ColumnCount = stacked ? 1 : 2; panel.RowCount = stacked ? 2 : 1;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, stacked ? 100 : 100 - rightPercent));
            if (!stacked) panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, rightPercent));
            left.Dock = right.Dock = DockStyle.Top;
            left.Margin = stacked ? new Padding(0, 0, 0, 16) : new Padding(0, 0, 18, 0); right.Margin = Padding.Empty;
            panel.Controls.Add(left, 0, 0); panel.Controls.Add(right, stacked ? 0 : 1, stacked ? 1 : 0);
            panel.ResumeLayout(true);
        }
        panel.Resize += (_, _) => Arrange(); Arrange(); return panel;
    }

    /// <summary>Giữ điểm gọi cũ cho các bộ lọc; cách tự xuống dòng do Row quản lý.</summary>
    public static Control Filters(params Control[] controls) => Row(controls);
}
