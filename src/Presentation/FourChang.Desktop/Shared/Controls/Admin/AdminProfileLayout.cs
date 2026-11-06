using System.Drawing.Drawing2D;
using FourChang.Desktop.Shared.Theme;

namespace FourChang.Desktop.Shared.Controls.Admin;

/// <summary>Khung hồ sơ dùng chung: header rõ danh tính, thẻ thông tin và thanh hành động luôn nhìn thấy.</summary>
internal static class AdminProfileLayout
{
    /// <summary>Lắp ba vùng cố định/co giãn; nội dung cuộn riêng, không để nút bị trôi mất ở cuối cửa sổ.</summary>
    internal static void Mount(Form form, Control header, Control body, params Control[] actions)
    {
        form.BackColor = CafeTheme.Cream; form.Font = CafeTheme.Body(); form.AutoScroll = false;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 126)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.Dock = DockStyle.Fill; header.Margin = Padding.Empty;
        var content = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(22, 20, 22, 12), Margin = Padding.Empty, BackColor = CafeTheme.Cream };
        body.Dock = DockStyle.Top; content.Controls.Add(body);
        var footer = Ui.Row(actions.Reverse().ToArray()); footer.Dock = DockStyle.Fill; footer.WrapContents = false; footer.FlowDirection = FlowDirection.RightToLeft;
        footer.Padding = new Padding(20, 13, 20, 10); footer.BackColor = AdminColors.Surface; footer.Margin = Padding.Empty;
        foreach (var action in actions) action.Margin = new Padding(8, 0, 0, 0);
        root.Controls.Add(header, 0, 0); root.Controls.Add(content, 0, 1); root.Controls.Add(footer, 0, 2); form.Controls.Add(root);
    }

    /// <summary>Hiển thị tên, mã và trạng thái bằng chữ Việt, không lộ mã trạng thái kỹ thuật trên giao diện.</summary>
    internal static Control Header(string name, string subtitle, string status)
    {
        var panel = new Panel { BackColor = CafeTheme.Coffee, Padding = new Padding(22), Dock = DockStyle.Top, Height = 126 };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
        var avatar = new InitialAvatar(name) { Width = 68, Height = 68, Margin = new Padding(0, 0, 14, 0) };
        var title = Ui.Label(name, 18, true, Color.White); title.AutoSize = false; title.Height = 38; title.Dock = DockStyle.Top; title.AutoEllipsis = true; title.Margin = Padding.Empty;
        var caption = Ui.Label(subtitle, 9, false, Color.FromArgb(246, 225, 204)); caption.AutoSize = false; caption.Height = 30; caption.AutoEllipsis = true; caption.Dock = DockStyle.Top;
        var text = AdminListLayout.Column(title, caption);
        var badge = new Label
        {
            Text = status,
            Dock = DockStyle.Top,
            Height = 32,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = CafeTheme.Soft,
            ForeColor = CafeTheme.Accent,
            Font = CafeTheme.Strong(9),
            Margin = new Padding(0, 15, 0, 0)
        };
        layout.Controls.Add(avatar, 0, 0); layout.Controls.Add(text, 1, 0); layout.Controls.Add(badge, 2, 0); panel.Controls.Add(layout); return panel;
    }

    /// <summary>Thông tin chia hai cột có nhãn nhỏ; tooltip giữ đầy đủ giá trị dài như địa chỉ hoặc email.</summary>
    internal static Control Info(params (string Label, string? Value)[] fields)
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = (fields.Length + 1) / 2 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var tooltip = new ToolTip(); table.Disposed += (_, _) => tooltip.Dispose();
        for (var i = 0; i < fields.Length; i++)
        {
            var label = Ui.Label(fields[i].Label.ToUpperInvariant(), 8, true, AdminColors.Muted); label.Margin = new Padding(0, 0, 0, 6);
            var value = Ui.Label(string.IsNullOrWhiteSpace(fields[i].Value) ? "Chưa cập nhật" : fields[i].Value!, 10, true);
            value.AutoSize = false; value.AutoEllipsis = true; value.Dock = DockStyle.Top; value.Height = 36; value.Margin = Padding.Empty; tooltip.SetToolTip(value, fields[i].Value);
            var cell = AdminListLayout.Column(label, value); label.Margin = new Padding(0, 0, 0, 4); value.Margin = Padding.Empty; value.Height = 30; cell.Margin = new Padding(0, 0, 18, 12); cell.Dock = DockStyle.Fill;
            table.Controls.Add(cell, i % 2, i / 2);
        }
        return table;
    }

    /// <summary>Thẻ nền sáng, tiêu đề đồng nhất cho thông tin và lịch sử.</summary>
    internal static Control Card(string title, params Control[] content)
    {
        var card = new AdminSurface { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(20), Margin = new Padding(0, 0, 0, 16) };
        card.Controls.Add(AdminListLayout.Column([Ui.Label(title, 11, true), .. content])); return card;
    }


    /// <summary>Thẻ điều hướng hồ sơ rõ vùng chọn, nội dung từng thẻ cuộn độc lập để tránh một trang kéo quá dài.</summary>
    internal static TabControl Tabs(params (string Title, Control Body)[] pages)
    {
        var tabs = Ui.Tabs(pages); tabs.DrawMode = TabDrawMode.OwnerDrawFixed; tabs.SizeMode = TabSizeMode.Fixed;
        tabs.ItemSize = new Size(190, 42); tabs.Padding = new Point(14, 10);
        tabs.DrawItem += (_, e) =>
        {
            var selected = e.Index == tabs.SelectedIndex;
            using var fill = new SolidBrush(selected ? CafeTheme.Soft : CafeTheme.Surface);
            e.Graphics.FillRectangle(fill, e.Bounds);
            using var font = selected ? CafeTheme.Strong(9) : CafeTheme.Body(9);
            TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, font, e.Bounds, selected ? CafeTheme.Accent : AdminColors.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (selected) { using var pen = new Pen(CafeTheme.Accent, 3); e.Graphics.DrawLine(pen, e.Bounds.Left + 12, e.Bounds.Bottom - 2, e.Bounds.Right - 12, e.Bounds.Bottom - 2); }
        };
        return tabs;
    }

    /// <summary>Nội dung rỗng gọn, mô tả đúng trạng thái thay vì một bảng trống chiếm toàn cửa sổ.</summary>
    internal static Control Empty(string title, string detail) => Ui.EmptyState(title, detail, 138);
}

/// <summary>Avatar bằng chữ cái đầu, hỗ trợ tên tiếng Việt và không phụ thuộc ảnh/font biểu tượng bên ngoài.</summary>
internal sealed class InitialAvatar(string name) : Control
{
    /// <summary>Vẽ avatar tĩnh bằng GDI+; không tải ảnh hay làm lộ thông tin ra dịch vụ ngoài.</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); if (Width < 4 || Height < 4) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var fill = new SolidBrush(CafeTheme.Soft); e.Graphics.FillEllipse(fill, 1, 1, Width - 3, Height - 3);
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var text = parts.Length == 0 ? "?" : string.Concat(parts[0][0], parts[^1][0]).ToUpperInvariant();
        using var font = CafeTheme.Strong(20);
        TextRenderer.DrawText(e.Graphics, text, font, ClientRectangle, CafeTheme.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}
