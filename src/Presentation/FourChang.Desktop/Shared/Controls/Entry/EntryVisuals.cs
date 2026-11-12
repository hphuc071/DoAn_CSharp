using System.Drawing.Drawing2D;
using FourChang.Desktop.Shared.Theme;
namespace FourChang.Desktop.Shared.Controls.Entry;

// Viền và khoảng đệm cho input mở ca/đăng nhập; giữ TextBox thật để nhập tiếng Việt và hỗ trợ bàn phím.
internal sealed class EntryInputFrame : Panel
{
    internal EntryInputFrame(Control input)
    {
        Size = new Size(442, 50); BackColor = CafeTheme.Surface; DoubleBuffered = true;
        input.Margin = Padding.Empty; Controls.Add(input);
        if (input is TextBox text) { text.BorderStyle = BorderStyle.None; text.BackColor = CafeTheme.Surface; text.Font = CafeTheme.Body(11); }
        if (input is ComboBox combo) { combo.FlatStyle = FlatStyle.Flat; combo.Font = CafeTheme.Body(11); }
        if (input is NumericUpDown number) { number.BorderStyle = BorderStyle.None; number.Font = CafeTheme.Body(11); }
        void Position() { input.SetBounds(14, (Height - input.PreferredSize.Height) / 2, Math.Max(80, Width - 28), input.PreferredSize.Height); }
        Resize += (_, _) => Position(); input.FontChanged += (_, _) => Position(); input.GotFocus += (_, _) => Invalidate(); input.LostFocus += (_, _) => Invalidate(); Position();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = CafeTheme.Rounded(new RectangleF(.8f, .8f, Width - 2, Height - 2), 10);
        using var pen = new Pen(ContainsFocus ? CafeTheme.Coffee : CafeTheme.Line, ContainsFocus ? 1.8f : 1f); e.Graphics.DrawPath(pen, path);
    }
}

// Nền thương hiệu vẽ trực tiếp bằng GDI+, không tải ảnh hoặc font từ bên ngoài.
internal sealed class EntryBrandPanel : Panel
{
    internal EntryBrandPanel() { DoubleBuffered = true; BackColor = CafeTheme.Dark; }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(CafeTheme.Dark);
        var lower = new Rectangle(0, Math.Min(280, Height - 1), Width, Math.Max(1, Height - 280));
        using var gradient = new LinearGradientBrush(lower, CafeTheme.Dark, Color.FromArgb(90, 61, 42), 70f); e.Graphics.FillRectangle(gradient, lower);
        using var ring = new Pen(Color.FromArgb(34, 242, 205, 156), 1.5f);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.DrawEllipse(ring, -160, Height - 280, 420, 420); e.Graphics.DrawEllipse(ring, -190, Height - 310, 480, 480);
    }
}
