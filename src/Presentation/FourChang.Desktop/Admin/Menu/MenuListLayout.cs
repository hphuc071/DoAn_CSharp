using System.Drawing.Drawing2D;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Theme;
namespace FourChang.Desktop.Admin.Menu;

/// <summary>Tab nội dung không có khung nổi Windows; TabPages/SelectedIndex gốc vẫn giữ cho điều hướng và test.</summary>
internal sealed class MenuContentTabs : TabControl
{
    /// <summary>Dùng toàn vùng chứa cho trang; thanh điều hướng được vẽ bằng các nút có focus ở phía trên.</summary>
    public override Rectangle DisplayRectangle => ClientRectangle;
    /// <summary>Ẩn header native dư thừa, không tạo lại page hoặc sự kiện mỗi lần chọn tab.</summary>
    internal MenuContentTabs()
    {
        Appearance = TabAppearance.FlatButtons;
        ItemSize = new Size(1, 1);
        SizeMode = TabSizeMode.Fixed;
        Multiline = true;
        TabStop = false;
        Dock = DockStyle.Fill;
        Margin = System.Windows.Forms.Padding.Empty;
    }
}

/// <summary>Thẻ danh sách: tiêu đề, tìm kiếm/action, bảng co giãn và tổng số; không chứa lệnh nghiệp vụ.</summary>
internal sealed class MenuListLayout : Panel
{
    private readonly TableLayoutPanel toolbar;
    private readonly Control searchField;
    private readonly Control actions;
    private readonly AdminLoadingStrip loading = new();
    private readonly ToolTip tooltip = new() { AutoPopDelay = 12000 };
    private readonly Button edit;
    private readonly Button delete;
    private readonly Button add;
    private readonly Button reload;
    private readonly Label empty = Ui.Label("Chưa có dữ liệu phù hợp", 10, false, CafeTheme.Muted);
    private bool loadingData;
    private bool arrangingGrid;
    internal DataGridView Grid
    {
        get;
    }
    internal TextBox Search
    {
        get;
    }
    internal Label Count
    {
        get;
    }

    /// <summary>Tái sử dụng control, mã/tên và handler hiện có; chỉ cải tiến bố cục, trạng thái và trình bày.</summary>
    internal MenuListLayout(string kind, string title, string subtitle, string note, Action create, Action change, Action remove, Action refresh)
    {
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = new Padding(22, 18, 22, 8);
        DoubleBuffered = true;
        BackColor = CafeTheme.Surface;
        var heading = Ui.Label(title, 17, true);
        heading.Margin = Padding.Empty;
        heading.Dock = DockStyle.Fill;
        heading.AccessibleDescription = note;
        tooltip.SetToolTip(heading, note);
        var caption = Ui.Label(subtitle, 9, false, CafeTheme.Muted);
        caption.Dock = DockStyle.Fill;
        caption.Margin = Padding.Empty;
        Count = new Label { AutoSize = false, Text = "Đang tải…", TextAlign = ContentAlignment.MiddleRight, Font = CafeTheme.Strong(10), ForeColor = CafeTheme.Coffee, Dock = DockStyle.Fill, Margin = Padding.Empty };
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
        header.ColumnStyles.Add(new(SizeType.Percent, 100));
        header.ColumnStyles.Add(new(SizeType.Absolute, 150));
        header.RowStyles.Add(new(SizeType.Absolute, 34));
        header.RowStyles.Add(new(SizeType.Absolute, 24));
        header.Controls.Add(heading, 0, 0);
        header.Controls.Add(Count, 1, 0);
        header.Controls.Add(caption, 0, 1);
        header.SetColumnSpan(caption, 2);

        var search = new AdminSearchBox { Dock = DockStyle.Top, Height = 44, Margin = Padding.Empty };
        Search = search.Input;
        Search.Name = "MenuSearch_" + kind;
        Search.AccessibleName = "Tìm kiếm " + title.ToLowerInvariant();
        Search.PlaceholderText = "Tìm theo mã hoặc tên…";
        var label = Ui.Label("Tìm kiếm", 9, true);
        label.Margin = Padding.Empty;
        searchField = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 0, 16, 0) };
        ((TableLayoutPanel)searchField).ColumnStyles.Add(new(SizeType.Percent, 100));
        ((TableLayoutPanel)searchField).RowStyles.Add(new(SizeType.Absolute, 24));
        ((TableLayoutPanel)searchField).RowStyles.Add(new(SizeType.Absolute, 44));
        searchField.Controls.Add(label);
        searchField.Controls.Add(search);
        add = new AdminActionButton("Thêm", AdminIcon.Add, true);
        edit = new AdminActionButton("Sửa", AdminIcon.Edit);
        delete = new AdminActionButton("Xóa có điều kiện", AdminIcon.Delete, danger: true);
        reload = new AdminActionButton("Tải lại", AdminIcon.Refresh);
        add.Click += (_, _) => create();
        edit.Click += (_, _) => change();
        delete.Click += (_, _) => remove();
        reload.Click += (_, _) => refresh();
        foreach (var button in new[] { add, edit, delete, reload })
            button.Margin = new Padding(0, 0, 8, 0);
        reload.Margin = Padding.Empty;
        actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoSize = false, Margin = Padding.Empty, Padding = new Padding(0, 24, 0, 0) };
        actions.Controls.AddRange([add, edit, delete, reload]);
        toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        toolbar.ColumnStyles.Add(new(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new(SizeType.Absolute, ActionWidth()));
        toolbar.RowStyles.Add(new(SizeType.Percent, 100));
        toolbar.Controls.Add(searchField, 0, 0);
        toolbar.Controls.Add(actions, 1, 0);
        toolbar.SizeChanged += (_, _) => ArrangeToolbar();

        Grid = Ui.Grid(new([], []), 350);
        Grid.Name = "MenuGrid_" + kind;
        Grid.Dock = DockStyle.Fill;
        Grid.Margin = Padding.Empty;
        StyleGrid(Grid);
        Grid.CellPainting += PaintBadge;
        var table = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = Padding.Empty };
        table.Controls.Add(Grid);
        table.Controls.Add(loading);
        empty.Dock = DockStyle.Bottom;
        empty.Height = 42;
        empty.AutoSize = false;
        empty.TextAlign = ContentAlignment.MiddleCenter;
        empty.Visible = false;
        table.Controls.Add(empty);
        var footer = Ui.Label("Chọn một dòng để chỉnh sửa hoặc xem đầy đủ thông tin.", 9, false, CafeTheme.Muted);
        footer.Dock = DockStyle.Fill;
        footer.TextAlign = ContentAlignment.MiddleLeft;
        footer.Margin = Padding.Empty;
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
        body.ColumnStyles.Add(new(SizeType.Percent, 100));
        body.RowStyles.Add(new(SizeType.Absolute, 76));
        body.RowStyles.Add(new(SizeType.Absolute, 84));
        body.RowStyles.Add(new(SizeType.Percent, 100));
        body.RowStyles.Add(new(SizeType.Absolute, 40));
        body.Controls.Add(header, 0, 0);
        body.Controls.Add(toolbar, 0, 1);
        body.Controls.Add(table, 0, 2);
        body.Controls.Add(footer, 0, 3);
        Controls.Add(body);
        Grid.DataBindingComplete += (_, _) => { UpdateSelection(); ArrangeGrid(); };
        Grid.ClientSizeChanged += (_, _) => ArrangeGrid();
        Grid.VisibleChanged += (_, _) => ArrangeGrid();
    }

    /// <summary>Tính từ độ rộng chữ/nút thật để action không xuống hàng hoặc bị cắt tại DPI khác nhau.</summary>
    private int ActionWidth() => new[] { add, edit, delete, reload }.Sum(b => b.Width + b.Margin.Horizontal) + 4;

    /// <summary>Khi vùng nội dung quá hẹp, xếp toolbar thành hai hàng có khoảng cách 8px; bảng vẫn cuộn riêng.</summary>
    private void ArrangeToolbar()
    {
        if (toolbar.Parent is not TableLayoutPanel body)
            return;
        var compact = toolbar.Width < ActionWidth() + 260;
        if ((toolbar.RowCount == 2) == compact)
            return;
        toolbar.SuspendLayout();
        toolbar.ColumnStyles.Clear();
        toolbar.RowStyles.Clear();
        if (compact)
        {
            toolbar.ColumnCount = 1;
            toolbar.RowCount = 2;
            toolbar.ColumnStyles.Add(new(SizeType.Percent, 100));
            toolbar.RowStyles.Add(new(SizeType.Absolute, 68));
            toolbar.RowStyles.Add(new(SizeType.Absolute, 52));
            toolbar.SetCellPosition(searchField, new(0, 0));
            toolbar.SetCellPosition(actions, new(0, 1));
            actions.Padding = new Padding(0, 8, 0, 0);
            body.RowStyles[1].Height = 136;
        }
        else
        {
            toolbar.ColumnCount = 2;
            toolbar.RowCount = 1;
            toolbar.ColumnStyles.Add(new(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new(SizeType.Absolute, ActionWidth()));
            toolbar.RowStyles.Add(new(SizeType.Percent, 100));
            toolbar.SetCellPosition(searchField, new(0, 0));
            toolbar.SetCellPosition(actions, new(1, 0));
            actions.Padding = new Padding(0, 24, 0, 0);
            body.RowStyles[1].Height = 84;
        }
        toolbar.ResumeLayout(true);
    }

    /// <summary>Chia độ rộng từ vùng thực còn lại sau scrollbar; tránh Fill mode tạo cuộn ngang ở tab ẩn hoặc khi thu cửa sổ.</summary>
    internal void ArrangeGrid()
    {
        if (arrangingGrid || !Grid.IsHandleCreated || !Grid.Visible || Grid.Columns.Count == 0 || Grid.ClientSize.Width < 200)
            return;
        arrangingGrid = true;
        Grid.SuspendLayout();
        try
        {
            var vertical = Grid.Controls.OfType<VScrollBar>().Any(s => s.Visible) || Grid.Rows.Count * Grid.RowTemplate.Height + Grid.ColumnHeadersHeight > Grid.ClientSize.Height;
            var available = Grid.ClientSize.Width - (vertical ? SystemInformation.VerticalScrollBarWidth : 0) - 2;
            var total = Grid.Columns.Cast<DataGridViewColumn>().Sum(c => c.FillWeight);
            var used = 0;
            for (int i = 0; i < Grid.Columns.Count; i++)
            {
                var column = Grid.Columns[i];
                var width = i == Grid.Columns.Count - 1 ? available - used : (int)(available * column.FillWeight / total);
                column.Width = Math.Max(column.MinimumWidth, width);
                used += column.Width;
            }
        }
        finally { Grid.ResumeLayout(true); arrangingGrid = false; }
    }

    /// <summary>Khóa action trong lúc tải và chỉ bật sửa/xóa khi có dòng; không thay điều kiện xóa ở backend.</summary>
    internal bool Busy
    {
        set
        {
            loadingData = value;
            loading.Running = value && CafeVisuals.MotionEnabled;
            add.Enabled = reload.Enabled = Search.Enabled = !value;
            UpdateSelection();
        }
    }
    /// <summary>Empty state và action theo dữ liệu thật, không dựng dòng mẫu để lấp bảng.</summary>
    private void UpdateSelection()
    {
        empty.Visible = !loadingData && Grid.Rows.Count == 0;
        edit.Enabled = delete.Enabled = !loadingData && Grid.Rows.Count > 0;
    }

    /// <summary>Giảm viền ô, ưu tiên tên/giá; trạng thái vẫn có chữ để không chỉ dựa vào màu.</summary>
    private static void StyleGrid(DataGridView grid)
    {
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersHeight = 44;
        grid.RowTemplate.Height = 54;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersDefaultCellStyle.Font = CafeTheme.Strong(9);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(12, 0, 8, 0);
        grid.DefaultCellStyle.Padding = new Padding(12, 0, 8, 0);
        grid.DefaultCellStyle.Font = CafeTheme.Body(10);
        grid.AlternatingRowsDefaultCellStyle.BackColor = CafeTheme.Surface;
        grid.DefaultCellStyle.SelectionBackColor = grid.RowsDefaultCellStyle.SelectionBackColor = grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = CafeTheme.Selection;
        grid.DefaultCellStyle.SelectionForeColor = CafeTheme.Ink;
        grid.ShowCellToolTips = true;
        grid.CellToolTipTextNeeded += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex >= 0) e.ToolTipText = grid[e.ColumnIndex, e.RowIndex].Value?.ToString(); };
    }

    /// <summary>Vẽ badge từ nội dung ô hiện có; không chuyển đổi hoặc ghi lại trạng thái nghiệp vụ.</summary>
    private static void PaintBadge(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || sender is not DataGridView grid)
            return;
        var header = grid.Columns[e.ColumnIndex].HeaderText;
        if (header is not ("Công bố" or "Bán" or "Trạng thái" or "Sử dụng" or "Mặc định"))
            return;
        var selected = (e.State & DataGridViewElementStates.Selected) != 0;
        // Tô toàn ô, kể cả padding, để màu chọn hàng không bị đứt ở cột badge.
        var background = selected ? CafeTheme.Selection : grid.Rows[e.RowIndex].InheritedStyle.BackColor;
        using var cellFill = new SolidBrush(background);
        e.Graphics!.FillRectangle(cellFill, e.CellBounds);
        using var separator = new Pen(CafeTheme.Line);
        e.Graphics.DrawLine(separator, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
        var savedClip = e.Graphics.Save();
        e.Graphics.SetClip(Rectangle.Intersect(e.CellBounds, e.ClipBounds));
        var text = e.FormattedValue?.ToString() ?? "";
        if (text.Length > 0)
        {
            var on = text is "Bật bán" or "Bật" or "Sử dụng";
            var draft = text == "Nháp";
            var published = text is "Công bố" or "Mặc định";
            var fill = on ? CafeTheme.Sage : draft ? CafeTheme.Amber : selected ? CafeTheme.Surface : CafeTheme.Soft;
            var ink = on ? CafeTheme.SuccessInk : draft ? CafeTheme.AmberInk : published ? CafeTheme.Coffee : CafeTheme.Muted;
            using var font = CafeTheme.Body(9);
            var width = Math.Min(e.CellBounds.Width - 12, TextRenderer.MeasureText(text, font).Width + 16);
            var box = new Rectangle(e.CellBounds.X + 8, e.CellBounds.Y + (e.CellBounds.Height - 28) / 2, Math.Max(20, width), 28);
            e.Graphics!.SmoothingMode = SmoothingMode.AntiAlias;
            using var shape = CafeTheme.Rounded(box, 9);
            using var brush = new SolidBrush(fill);
            e.Graphics.FillPath(brush, shape);
            TextRenderer.DrawText(e.Graphics, text, font, box, ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
        e.Graphics.Restore(savedClip);
        e.Handled = true;
    }

    /// <summary>Vẽ thẻ bo góc cục bộ, không sửa lớp theme dùng chung của những màn hình khác.</summary>
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? CafeTheme.Cream);
        if (Width < 2 || Height < 2)
            return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = CafeTheme.Rounded(new RectangleF(.5f, .5f, Width - 1, Height - 1), 14);
        using var fill = new SolidBrush(CafeTheme.Surface);
        using var border = new Pen(CafeTheme.Line);
        e.Graphics.FillPath(fill, shape);
        e.Graphics.DrawPath(border, shape);
    }
    /// <summary>Giải phóng tooltip cùng trang; animation dùng component có dispose sẵn.</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            tooltip.Dispose();
        base.Dispose(disposing);
    }
}
