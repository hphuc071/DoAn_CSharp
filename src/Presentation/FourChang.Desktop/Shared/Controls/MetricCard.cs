using System.Drawing.Drawing2D;
using FourChang.Desktop.Shared.Theme;

namespace FourChang.Desktop.Shared.Controls;

/// <summary>Thẻ KPI chỉ trình bày giá trị được truyền vào; không tính toán, nội suy hoặc tạo xu hướng giả.</summary>
internal sealed class MetricCard : Control
{
    private readonly string title;
    private readonly string value;
    private readonly string hint;
    private readonly int icon;
    private readonly ToolTip tooltip = new();

    /// <summary>Giữ nguyên nội dung và cung cấp đầy đủ cho trợ năng/tooltip khi giá trị dài.</summary>
    internal MetricCard(string title, string value, string hint, int icon)
    {
        this.title = title; this.value = value; this.hint = hint; this.icon = icon;
        DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true);
        AccessibleName = $"{title}: {value}. {hint}"; tooltip.SetToolTip(this, AccessibleName);
    }

    /// <summary>Vẽ khung, nhãn, giá trị và một biểu tượng hình học đồng nhất, không dùng font icon ngoài.</summary>
    protected override void OnPaint(PaintEventArgs args)
    {
        base.OnPaint(args); if (Width < 8 || Height < 8) return;
        var graphics = args.Graphics; graphics.Clear(Parent?.BackColor ?? CafeTheme.Cream); graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = CafeTheme.Rounded(new RectangleF(.5f, .5f, Width - 2, Height - 2), 12);
        using var fill = new SolidBrush(CafeTheme.Surface); graphics.FillPath(fill, path);
        using var border = new Pen(CafeTheme.Line); graphics.DrawPath(border, path);
        using var small = CafeTheme.Body(9); using var large = CafeTheme.Strong(21);
        TextRenderer.DrawText(graphics, title, small, new Rectangle(18, 14, Math.Max(0, Width - 62), 25), CafeTheme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(graphics, value, large, new Rectangle(18, 43, Width - 32, 40), CafeTheme.Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(graphics, hint, small, new Rectangle(18, 89, Width - 32, 24), CafeTheme.Muted, TextFormatFlags.EndEllipsis);
        using var pen = new Pen(CafeTheme.Accent, 1.7f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        var state = graphics.Save(); graphics.TranslateTransform(Width - 36, 17);
        if (icon % 4 == 0) { graphics.DrawLine(pen, 1, 18, 18, 18); graphics.DrawRectangle(pen, 3, 9, 3, 8); graphics.DrawRectangle(pen, 10, 2, 3, 15); }
        else if (icon % 4 == 1) { graphics.DrawRectangle(pen, 2, 1, 14, 18); graphics.DrawLines(pen, new Point[] {new Point(5, 10), new Point(8, 13), new Point(13, 7)}); }
        else if (icon % 4 == 2) { graphics.DrawArc(pen, 2, 3, 15, 15, 210, 285); graphics.DrawLines(pen, new Point[] {new Point(1, 9), new Point(2, 2), new Point(9, 3)}); }
        else { graphics.DrawPolygon(pen, new Point[] {new Point(9, 1), new Point(18, 18), new Point(0, 18)}); graphics.DrawLine(pen, 9, 7, 9, 11); graphics.DrawEllipse(pen, 8.5f, 14, 1, 1); }
        graphics.Restore(state);
    }

    /// <summary>Hủy tooltip cùng thẻ; điều hướng nhiều lần không giữ tài nguyên native cũ.</summary>
    protected override void Dispose(bool disposing) { if (disposing) tooltip.Dispose(); base.Dispose(disposing); }
}
