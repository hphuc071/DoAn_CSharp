using System.Drawing.Drawing2D;
using FourChang.Desktop.Shared.Theme;

namespace FourChang.Desktop.Shared.Controls.Admin;

/// <summary>Khung danh sách dùng chung: lưới lọc thẳng hàng, bảng giãn theo màn hình và hiệu ứng tải nhẹ.</summary>
internal sealed class AdminListLayout : UserControl
{
    private readonly AdminLoadingStrip loading = new();
    private readonly Panel tableArea;
    private readonly DataGridView grid;
    private Form? host;

    /// <summary>Dùng số liệu thật từ API; khung chỉ trình bày, không tạo KPI hoặc thay đổi nghiệp vụ.</summary>
    internal AdminListLayout(string title, string description, Control filters, Control toolbar, Label error,
        DataGridView grid, Control footer)
    {
        this.grid = grid; AutoSize = true; Dock = DockStyle.Top; DoubleBuffered = true;
        BackColor = CafeTheme.Cream;
        var eyebrow = Ui.Label("4CHANG  /  QUẢN TRỊ NHÂN SỰ", 8, true, AdminColors.Accent);
        eyebrow.Margin = new Padding(0, 0, 0, 6);
        var heading = Ui.Label(title, 18, true); heading.Margin = new Padding(0, 0, 0, 7);
        var sub = Ui.Label(description, 9, false, AdminColors.Muted); sub.Margin = new Padding(0, 0, 0, 0);
        var header = Ui.Stack(eyebrow, heading, sub);
        var filterCard = new AdminSurface { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(18, 16, 18, 8) };
        filterCard.Controls.Add(filters);
        tableArea = new Panel { Dock = DockStyle.Top, Height = 360, BackColor = AdminColors.Surface };
        grid.Dock = DockStyle.Fill;
        tableArea.Controls.Add(grid); tableArea.Controls.Add(loading);
        error.AutoSize = true; error.MaximumSize = new Size(1100, 0); error.Visible = false;
        error.TextChanged += (_, _) => error.Visible = !string.IsNullOrWhiteSpace(error.Text);
        var list = new AdminSurface { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(16) };
        var body = Column(toolbar, error, tableArea, footer); list.Controls.Add(body);
        var layout = Column(header, filterCard, list);
        header.Margin = new Padding(2, 0, 0, 20); filterCard.Margin = new Padding(0, 0, 0, 16); list.Margin = Padding.Empty;
        Controls.Add(layout);
        HandleCreated += (_, _) =>
        {
            host = FindForm(); if (host is not null) host.Resize += Fit;
            BeginInvoke(new Action(() => Fit(this, EventArgs.Empty)));
        };
    }

    /// <summary>Chạy thanh sáng trong khi tải; timer dừng khi không có tác vụ để tránh tốn CPU.</summary>
    internal bool Busy { set => loading.Running = value && CafeVisuals.MotionEnabled; }

    /// <summary>Đặt chiều cao bảng theo diện tích thực còn lại, không kéo cả trang dài vô hạn.</summary>
    private void Fit(object? sender, EventArgs e)
    {
        var top = host is null ? 0 : host.PointToClient(tableArea.PointToScreen(Point.Empty)).Y;
        var available = host is null ? 340 : host.ClientSize.Height - top - 90;
        tableArea.Height = Math.Clamp(available, 220, 650);
    }

    /// <summary>Ngắt đăng ký resize khi rời trang để không giữ các trang cũ trong bộ nhớ.</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing && host is not null) host.Resize -= Fit;
        base.Dispose(disposing);
    }

    /// <summary>Xếp các phần theo cột co giãn; không dùng vị trí tuyệt đối cho các khối giao diện.</summary>
    internal static TableLayoutPanel Column(params Control[] items)
    {
        var result = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = items.Length, Margin = Padding.Empty };
        result.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var item in items) { item.Dock = DockStyle.Top; item.Margin = new Padding(0, 0, 0, 12); result.Controls.Add(item); }
        return result;
    }

    /// <summary>Lưới các trường bằng nhau theo chiều ngang; từng ô nhập được căn giữa trong khung 44px.</summary>
    internal static TableLayoutPanel Fields(params Control[] items)
    {
        var result = new TableLayoutPanel { Dock = DockStyle.Top, Height = 84, ColumnCount = items.Length, RowCount = 1, Margin = Padding.Empty };
        for (var i = 0; i < items.Length; i++)
        {
            result.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / items.Length));
            items[i].Dock = DockStyle.Fill; items[i].Margin = new Padding(0, 0, i == items.Length - 1 ? 0 : 16, 6);
            result.Controls.Add(items[i], i, 0);
        }
        return result;
    }

    /// <summary>Nhãn 22px và ô nhập 44px dùng cho TextBox, ComboBox, DateTimePicker.</summary>
    internal static Control Field(string label, Control input)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Top, Height = 76, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var title = Ui.Label(label, 9, true); title.Margin = Padding.Empty;
        panel.Controls.Add(title, 0, 0); panel.Controls.Add(new AdminInputFrame(input) { Dock = DockStyle.Fill, Margin = Padding.Empty }, 0, 1);
        return panel;
    }

    /// <summary>Checkbox lọc ngày nằm trong nhãn thay vì dính vào ô ngày; không chọn thì không gửi bộ lọc ngày.</summary>
    internal static Control DateField(string label, DateTimePicker input, CheckBox enabled)
    {
        var panel = (TableLayoutPanel)Field(label, input);
        var old = panel.GetControlFromPosition(0, 0)!; panel.Controls.Remove(old); old.Dispose();
        enabled.Text = label; enabled.AutoSize = true; enabled.Font = CafeTheme.Body(9); enabled.Margin = Padding.Empty;
        panel.Controls.Add(enabled, 0, 0); input.ShowCheckBox = false; input.Enabled = enabled.Checked;
        enabled.CheckedChanged += (_, _) => input.Enabled = enabled.Checked;
        return panel;
    }

    /// <summary>Phân biệt hành động chính bằng màu nâu, nhóm còn lại dùng nền trắng và icon đồng nhất.</summary>
    internal static Button Action(string text, bool primary = false)
    {
        var icon = text switch
        {
            "Tìm kiếm" => AdminIcon.Search,
            "Tải lại" => AdminIcon.Refresh,
            "Trang trước" => AdminIcon.Previous,
            "Trang sau" => AdminIcon.Next,
            "Thêm nhân viên" => AdminIcon.Add,
            "Sửa hồ sơ" => AdminIcon.Edit,
            "Ngừng / kích hoạt lại" => AdminIcon.Lock,
            _ => AdminIcon.View
        };
        return new AdminActionButton(text, icon, primary) { Height = 44, Margin = new Padding(0, 0, 8, 0) };
    }

    /// <summary>Trang trí bảng, tooltip và trạng thái dạng nhãn; không sửa giá trị gốc của các ô.</summary>
    internal static void StyleGrid(DataGridView grid, int stateColumn)
    {
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.RowTemplate.Height = 58; grid.ColumnHeadersHeight = 42;
        grid.BackgroundColor = AdminColors.Surface; grid.GridColor = CafeTheme.Line;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersDefaultCellStyle.BackColor = CafeTheme.Soft;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = grid.ColumnHeadersDefaultCellStyle.BackColor;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = CafeTheme.Muted;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(12, 0, 6, 0);
        grid.DefaultCellStyle.Padding = new Padding(12, 0, 8, 0);
        grid.DefaultCellStyle.SelectionBackColor = CafeTheme.Soft;
        grid.DefaultCellStyle.SelectionForeColor = CafeTheme.Ink;
        grid.AlternatingRowsDefaultCellStyle.BackColor = CafeTheme.Surface;
        grid.ShowCellToolTips = true;
        grid.CellToolTipTextNeeded += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0) e.ToolTipText = grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString();
        };
        grid.CellPainting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex != stateColumn) return;
            e.PaintBackground(e.ClipBounds, true);
            var text = e.FormattedValue?.ToString() ?? "";
            var green = text is "Đang làm" or "Đang mở" or "Hoạt động";
            var amber = text == "Đang khóa";
            var fill = green ? CafeTheme.Sage : amber ? CafeTheme.Amber : CafeTheme.Soft;
            var ink = green ? CafeTheme.SuccessInk : amber ? CafeTheme.AmberInk : CafeTheme.Muted;
            var width = Math.Min(e.CellBounds.Width - 12, TextRenderer.MeasureText(text, grid.Font).Width + 20);
            var box = new Rectangle(e.CellBounds.X + 6, e.CellBounds.Y + (e.CellBounds.Height - 28) / 2, width, 28);
            e.Graphics!.SmoothingMode = SmoothingMode.AntiAlias;
            using var shape = CafeTheme.Rounded(box, 10); using var brush = new SolidBrush(fill); e.Graphics.FillPath(brush, shape);
            TextRenderer.DrawText(e.Graphics, text, grid.Font, box, ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            e.Handled = true;
        };
    }
}

/// <summary>Khung nhập bo góc và chuyển viền focus êm; vẫn giữ control native cho bàn phím và trợ năng.</summary>
internal sealed class AdminInputFrame : Panel
{
    private readonly Control input;
    private readonly System.Windows.Forms.Timer animation = new() { Interval = 16 };
    private float progress;

    /// <summary>Giữ nguyên dữ liệu/sự kiện của ô nhập được truyền vào, chỉ bổ sung lớp trình bày.</summary>
    internal AdminInputFrame(Control input)
    {
        this.input = input; DoubleBuffered = true; Height = 44; BackColor = CafeTheme.Surface; Controls.Add(input);
        input.Margin = Padding.Empty; input.Font = CafeTheme.Body(10); input.BackColor = CafeTheme.Surface;
        if (input is TextBox text) text.BorderStyle = BorderStyle.None;
        if (input is ComboBox combo) combo.FlatStyle = FlatStyle.Flat;
        input.GotFocus += FocusChanged; input.LostFocus += FocusChanged;
        animation.Tick += (_, _) =>
        {
            var target = input.ContainsFocus ? 1f : 0f;
            progress = Math.Clamp(progress + (target > progress ? .18f : -.18f), 0, 1); Invalidate();
            if (Math.Abs(target - progress) < .01f) animation.Stop();
        };
        Resize += (_, _) => Arrange(); Arrange();
    }

    /// <summary>Căn giữa control native theo chiều dọc, tránh combobox và nút bị lệch hàng.</summary>
    private void Arrange()
    {
        if (input is TextBox { Multiline: true }) input.SetBounds(12, 9, Math.Max(20, Width - 24), Math.Max(24, Height - 18));
        else input.SetBounds(12, Math.Max(5, (Height - input.PreferredSize.Height) / 2), Math.Max(20, Width - 24), input.PreferredSize.Height);
    }

    /// <summary>Chỉ chạy animation khi cần; tương phản cao đổi viền ngay.</summary>
    private void FocusChanged(object? sender, EventArgs e)
    {
        if (!CafeVisuals.MotionEnabled) { progress = input.ContainsFocus ? 1 : 0; Invalidate(); } else animation.Start();
    }

    /// <summary>Vẽ nền trắng và viền màu caramel chuyển nhẹ theo focus, không thay đổi layout.</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); if (Width < 4 || Height < 4) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var border = ContainsFocus ? CafeTheme.Accent : CafeTheme.InputBorder;
        using var pen = new Pen(border, 1 + progress * .5f); using var shape = CafeTheme.Rounded(new RectangleF(1, 1, Width - 3, Height - 3), 9);
        e.Graphics.DrawPath(pen, shape);
    }

    /// <summary>Giải phóng timer khi chuyển trang.</summary>
    protected override void Dispose(bool disposing) { if (disposing) animation.Dispose(); base.Dispose(disposing); }
}
