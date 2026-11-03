using System.Drawing.Drawing2D;
using FourChang.Desktop.Shared.Theme;
namespace FourChang.Desktop.Shared.Controls;

/// <summary>Nút điều hướng: rail gọn cho POS, hàng biểu tượng/chữ cho quản lý; không đổi route nghiệp vụ.</summary>
internal sealed class NavigationButton : Button
{
    private readonly bool compact;
    private readonly string route;
    private bool hovered;

    /// <summary>Giữ Button thật để Enter/Tab và accessibility hoạt động như trước.</summary>
    internal NavigationButton(string caption, string route, bool compact)
    {
        this.compact = compact;
        this.route = route;
        Text = caption;
        AccessibleName = caption;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        Font = CafeTheme.Strong(compact ? 8.5f : 9);
        BackColor = CafeTheme.Dark;
        ForeColor = Color.FromArgb(246, 236, 220);
        DoubleBuffered = true;
    }

    /// <summary>Tô sáng nhẹ khi rê chuột, không đổi kích thước nút hoặc vị trí nhãn.</summary>
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }

    /// <summary>Trả về trạng thái route hiện tại khi chuột rời nút.</summary>
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; Invalidate(); }

    /// <summary>Vẽ các hình học đơn giản thay font icon để không lỗi ký tự trên máy khác.</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? CafeTheme.Dark);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var fill = hovered ? ControlPaint.Light(BackColor, .08f) : BackColor;
        using var background = new SolidBrush(fill);
        using var shape = CafeTheme.Rounded(new RectangleF(1, 1, Width - 2, Height - 2), 5);
        e.Graphics.FillPath(background, shape);
        var x = compact ? (Width - 20) / 2 : 14;
        var y = compact ? 13 : (Height - 20) / 2;
        using var pen = new Pen(ForeColor, 1.7f);
        if (route == "sale")
        {
            e.Graphics.DrawArc(pen, x, y + 3, 16, 12, 0, 180);
            e.Graphics.DrawLine(pen, x + 1, y + 5, x + 15, y + 5);
            e.Graphics.DrawArc(pen, x + 13, y + 5, 7, 7, -90, 180);
            e.Graphics.DrawLine(pen, x, y + 18, x + 18, y + 18);
        }
        else if (route is "customers" or "employees")
        {
            e.Graphics.DrawEllipse(pen,x+6,y+1,8,8);
            e.Graphics.DrawArc(pen,x+1,y+10,18,17,180,180);
        }
        else if (route=="dashboard")
        {
            e.Graphics.DrawLine(pen,x+2,y+19,x+19,y+19);
            e.Graphics.DrawRectangle(pen,x+3,y+11,3,7);
            e.Graphics.DrawRectangle(pen,x+9,y+6,3,12);
            e.Graphics.DrawRectangle(pen,x+15,y+1,3,17);
        }
        else if (route is "inventory" or "ingredients" or "stock")
        {
            e.Graphics.DrawRectangle(pen,x+2,y+3,17,15);
            e.Graphics.DrawLine(pen,x+2,y+7,x+19,y+7);
            e.Graphics.DrawLine(pen,x+10,y+3,x+10,y+12);
        }
        else
        {
            e.Graphics.DrawRectangle(pen, x + 3, y + 2, 14, 17);
            e.Graphics.DrawLine(pen, x + 6, y + 7, x + 14, y + 7);
            e.Graphics.DrawLine(pen, x + 6, y + 11, x + 14, y + 11);
            e.Graphics.DrawLine(pen, x + 6, y + 15, x + 11, y + 15);
        }
        var textBounds = compact ? new Rectangle(3, 39, Width - 6, Height - 43) : new Rectangle(42, 0, Width - 46, Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, ForeColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix |
            (compact ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left));
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4));
    }
}
