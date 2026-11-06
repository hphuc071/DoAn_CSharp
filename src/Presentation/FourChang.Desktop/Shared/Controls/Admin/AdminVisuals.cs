using System.Drawing.Drawing2D;
using FourChang.Desktop.Shared.Theme;

namespace FourChang.Desktop.Shared.Controls.Admin;

// Bảng màu riêng cho màn hình khách hàng: nâu–kem cũ, tăng độ sáng ở nền và điểm nhấn.
// Bộ control dùng chung cho UC-ADM-01 và UC-ADM-02, đồng bộ với theme nâu–kem của ứng dụng.
internal static class AdminColors
{
    internal static Color Accent => CafeTheme.Coffee;
    internal static Color Hover => ControlPaint.Light(CafeTheme.Coffee, .1f);
    internal static Color Surface => CafeTheme.Surface;
    internal static Color Soft => CafeTheme.Soft;
    internal static Color Border => CafeTheme.Line;
    internal static Color Muted => CafeTheme.Muted;
}

internal enum AdminIcon { Search, Refresh, Add, View, Edit, Lock, Key, Delete, Previous, Next }

internal sealed class AdminActionButton : Button
{
    private readonly System.Windows.Forms.Timer animation = new() { Interval = 16 };
    private readonly AdminIcon icon;
    private readonly bool primary;
    private readonly bool danger;
    private bool hovered;
    private bool pressed;
    private float blend;

    internal AdminActionButton(string caption, AdminIcon icon, bool primary = false, bool danger = false)
    {
        this.icon = icon; this.primary = primary; this.danger = danger;
        Text = caption; Font = CafeTheme.Strong(9); Cursor = Cursors.Hand;
        Size = new Size(TextRenderer.MeasureText(caption, Font).Width + 54, 44);
        MinimumSize = new Size(86, 44); Margin = new Padding(0, 0, 8, 8);
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
        DoubleBuffered = true; SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        animation.Tick += (_, _) =>
        {
            var target = hovered && Enabled ? 1f : 0f;
            blend = Math.Clamp(blend + (target > blend ? .1f : -.1f), 0, 1);
            Invalidate(); if (Math.Abs(blend - target) < .01f) animation.Stop();
        };
    }
    // Chỉ đổi màu trong ~100 ms, không dịch chuyển layout hoặc cản trở thao tác liên tiếp.
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; if (CafeVisuals.MotionEnabled) animation.Start(); else { blend = 1; Invalidate(); } }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; pressed = false; if (CafeVisuals.MotionEnabled) animation.Start(); else { blend = 0; Invalidate(); } }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); pressed = e.Button == MouseButtons.Left; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); if (!Enabled) { blend = 0; hovered = false; animation.Stop(); } Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics; graphics.Clear(Parent?.BackColor ?? AdminColors.Surface);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var fill = Enabled ? Mix(primary ? AdminColors.Accent : AdminColors.Surface,
            primary ? AdminColors.Hover : AdminColors.Soft, blend) : CafeTheme.Soft;
        if (pressed && Enabled) fill = Mix(fill, Color.FromArgb(95, 53, 31), .12f);
        var ink = !Enabled ? CafeTheme.Muted : primary ? Color.White : danger ? CafeTheme.Danger : CafeTheme.Ink;
        using var shape = CafeTheme.Rounded(new RectangleF(1, 1, Width - 3, Height - 3), 10);
        using var brush = new SolidBrush(fill); graphics.FillPath(brush, shape);
        using var border = new Pen(Focused && Enabled ? AdminColors.Accent : primary ? fill : AdminColors.Border, Focused && Enabled ? 1.8f : 1);
        graphics.DrawPath(border, shape);
        using var pen = new Pen(ink, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        // Căn giữa cả cụm icon + chữ; nhấn chỉ đổi màu, không đẩy icon/chữ lệch xuống.
        var textWidth = Math.Min(Width - 48, TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding).Width);
        var left = Math.Max(12, (Width - textWidth - 24) / 2);
        var state = graphics.Save(); graphics.TranslateTransform(left, Height / 2f - 8);
        DrawIcon(graphics, pen, icon); graphics.Restore(state);
        TextRenderer.DrawText(graphics, Text, Font, new Rectangle(left + 24, 0, textWidth, Height), ink,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
    }
    private static Color Mix(Color first, Color second, float amount) => Color.FromArgb(
        (int)(first.R + (second.R - first.R) * amount), (int)(first.G + (second.G - first.G) * amount), (int)(first.B + (second.B - first.B) * amount));
    internal static void DrawIcon(Graphics g, Pen p, AdminIcon icon)
    {
        switch (icon)
        {
            case AdminIcon.Search: g.DrawEllipse(p, 1, 1, 10, 10); g.DrawLine(p, 10, 10, 15, 15); break;
            case AdminIcon.Refresh: g.DrawArc(p, 1, 2, 13, 12, 35, 285); g.DrawLines(p, new Point[] { new(10, 1), new(15, 2), new(14, 7) }); break;
            case AdminIcon.Add: g.DrawLine(p, 8, 2, 8, 14); g.DrawLine(p, 2, 8, 14, 8); break;
            case AdminIcon.View: g.DrawEllipse(p, 1, 4, 14, 9); g.DrawEllipse(p, 6, 6, 4, 4); break;
            case AdminIcon.Edit: g.DrawLines(p, new Point[] { new(2, 11), new(11, 2), new(14, 5), new(5, 14), new(1, 15), new(2, 11) }); break;
            case AdminIcon.Lock: g.DrawArc(p, 4, 1, 8, 10, 180, 180); g.DrawRectangle(p, 2, 7, 12, 8); g.DrawLine(p, 8, 10, 8, 12); break;
            case AdminIcon.Key: g.DrawEllipse(p, 1, 1, 7, 7); g.DrawLine(p, 7, 7, 14, 14); g.DrawLine(p, 11, 11, 13, 9); break;
            case AdminIcon.Delete: g.DrawLine(p, 2, 4, 14, 4); g.DrawLine(p, 6, 1, 10, 1); g.DrawLines(p, new Point[] { new(3, 6), new(4, 15), new(12, 15), new(13, 6) }); g.DrawLine(p, 6, 7, 6, 12); g.DrawLine(p, 10, 7, 10, 12); break;
            case AdminIcon.Previous: g.DrawLines(p, new Point[] { new(11, 2), new(5, 8), new(11, 14) }); break;
            case AdminIcon.Next: g.DrawLines(p, new Point[] { new(5, 2), new(11, 8), new(5, 14) }); break;
        }
    }
    protected override void Dispose(bool disposing) { if (disposing) animation.Dispose(); base.Dispose(disposing); }
}

internal sealed class AdminSurface : Panel
{
    internal AdminSurface() { DoubleBuffered = true; BackColor = AdminColors.Surface; Padding = new Padding(22); }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? CafeTheme.Cream); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = CafeTheme.Rounded(new RectangleF(0, 0, Width - 1, Height - 1), 15);
        using var fill = new SolidBrush(BackColor); e.Graphics.FillPath(fill, shape);
        using var border = new Pen(AdminColors.Border); e.Graphics.DrawPath(border, shape);
    }
}

internal sealed class AdminSearchBox : Panel
{
    internal TextBox Input { get; } = new()
    {
        BorderStyle = BorderStyle.None,
        Font = CafeTheme.Body(10),
        PlaceholderText = "Tìm mã, tên, tài khoản, email hoặc điện thoại",
        Name = "CustomerSearch",
        AccessibleName = "Tìm kiếm khách hàng"
    };
    internal AdminSearchBox()
    {
        Height = 44; DoubleBuffered = true; BackColor = AdminColors.Surface;
        Controls.Add(Input); Input.GotFocus += (_, _) => Invalidate(); Input.LostFocus += (_, _) => Invalidate();
    }
    protected override void OnResize(EventArgs e)
    { base.OnResize(e); Input.SetBounds(40, Math.Max(8, (Height - Input.PreferredHeight) / 2), Math.Max(20, Width - 55), Input.PreferredHeight); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? AdminColors.Surface); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = CafeTheme.Rounded(new RectangleF(.8f, .8f, Width - 2, Height - 2), 9);
        using var fill = new SolidBrush(CafeTheme.Surface); e.Graphics.FillPath(fill, shape);
        using var border = new Pen(Input.Focused ? CafeTheme.Accent : CafeTheme.InputBorder, Input.Focused ? 1.6f : 1);
        e.Graphics.DrawPath(border, shape);
        using var icon = new Pen(AdminColors.Muted, 1.5f);
        var state = e.Graphics.Save(); e.Graphics.TranslateTransform(14, Height / 2f - 8);
        AdminActionButton.DrawIcon(e.Graphics, icon, AdminIcon.Search); e.Graphics.Restore(state);
    }
}

internal sealed class AdminLoadingStrip : Control
{
    private readonly System.Windows.Forms.Timer animation = new() { Interval = 25 };
    private float position;
    private bool running;
    internal bool Running
    {
        get => running;
        set { running = value; if (value) animation.Start(); else animation.Stop(); Invalidate(); }
    }
    internal AdminLoadingStrip()
    {
        Height = 3; Dock = DockStyle.Top; DoubleBuffered = true;
        animation.Tick += (_, _) => { position = (position + .025f) % 1.3f; Invalidate(); };
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(AdminColors.Surface);
        if (!running || Width < 1) return;
        var band = new RectangleF((position - .3f) * Width, 0, Width * .3f, Height);
        using var fill = new SolidBrush(Color.FromArgb(203, 153, 105)); e.Graphics.FillRectangle(fill, band);
    }
    protected override void Dispose(bool disposing) { if (disposing) animation.Dispose(); base.Dispose(disposing); }
}
