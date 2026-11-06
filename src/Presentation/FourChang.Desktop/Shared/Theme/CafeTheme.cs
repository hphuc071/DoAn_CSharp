using System.Drawing.Drawing2D;
namespace FourChang.Desktop.Shared.Theme;

public static class CafeTheme
{
    // Một palette cố định: không đọc theme, hệ điều hành hoặc cấu hình màu cũ.
    public static Color Coffee => ColorTranslator.FromHtml("#805033");
    public static Color Dark => ColorTranslator.FromHtml("#3A2E25");
    public static Color Cream => ColorTranslator.FromHtml("#FBF8F2");
    public static Color Surface => ColorTranslator.FromHtml("#FFFDF9");
    public static Color Soft => ColorTranslator.FromHtml("#F4ECE0");
    public static Color InputBorder => ColorTranslator.FromHtml("#A28D77");
    public static Color Line => ColorTranslator.FromHtml("#E1D5C5");
    public static Color Muted => ColorTranslator.FromHtml("#716252");
    public static Color Ink => ColorTranslator.FromHtml("#302922");
    public static Color Accent => ColorTranslator.FromHtml("#805033");
    public static Color Sage => ColorTranslator.FromHtml("#E8F1E5");
    public static Color SuccessInk => ColorTranslator.FromHtml("#345F37");
    public static Color Amber => ColorTranslator.FromHtml("#FFF0D5");
    public static Color AmberInk => ColorTranslator.FromHtml("#80561E");
    public static Color Danger => ColorTranslator.FromHtml("#A53E33");
    public static Font Body(float size = 10) => new("Segoe UI", size);
    public static Font Strong(float size = 10) => new("Segoe UI", size, FontStyle.Bold);
    public const string Brand = "4CHANG LINH NGU LAM";
    public static GraphicsPath Rounded(RectangleF rect, float radius = 12)
    {
        var p = new GraphicsPath();
        // AutoSize có thể tạo kích thước 0 trong lúc sắp layout; không gửi hình học âm/rỗng vào GDI+.
        if (rect.Width <= 0 || rect.Height <= 0) return p;
        float d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        p.AddArc(rect.Left, rect.Top, d, d, 180, 90); p.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        p.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); p.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
}

public sealed class SurfacePanel : Panel
{
    public SurfacePanel() { BackColor = CafeTheme.Surface; DoubleBuffered = true; Padding = new Padding(18); }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (Width < 2 || Height < 2) return; e.Graphics.Clear(Parent?.BackColor ?? CafeTheme.Cream); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = CafeTheme.Rounded(new RectangleF(0, 0, Width - 1, Height - 1)); using var fill = new SolidBrush(BackColor); e.Graphics.FillPath(fill, path);
    }
    protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); if (Width < 2 || Height < 2) return; e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using var pen = new Pen(CafeTheme.Line); using var path = CafeTheme.Rounded(new RectangleF(.5f, .5f, Width - 1, Height - 1)); e.Graphics.DrawPath(pen, path); }
}


/// <summary>Nút dùng chung POS/Admin: chuyển màu nhẹ, giữ nguyên kích thước và sự kiện nghiệp vụ.</summary>
public sealed class CafeButton : Button
{
    public float CornerRadius { get; set; } = 10;
    private readonly System.Windows.Forms.Timer animation = new() { Interval = 16 };
    private bool hovered;
    private bool pressed;
    private float hoverProgress;

    /// <summary>Bật vẽ đệm và chỉ chạy timer trong khoảng thời gian nút đang đổi màu.</summary>
    public CafeButton()
    {
        DoubleBuffered = true;
        animation.Tick += (_, _) =>
        {
            var target = hovered && Enabled ? 1f : 0f;
            hoverProgress = Math.Clamp(hoverProgress + (target > hoverProgress ? .1f : -.1f), 0f, 1f);
            Invalidate();
            if (Math.Abs(hoverProgress - target) < .01f) animation.Stop();
        };
    }

    /// <summary>Bắt đầu chuyển màu khi chuột đi vào; chế độ tương phản cao đổi màu ngay.</summary>
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        hovered = true;
        AnimateHover();
    }

    /// <summary>Trở về màu gốc khi rời nút, đồng thời xóa trạng thái đang nhấn.</summary>
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hovered = false;
        pressed = false;
        AnimateHover();
    }

    /// <summary>Hiển thị phản hồi nhấn chuột trái mà không di chuyển chữ hoặc bố cục.</summary>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        pressed = e.Button == MouseButtons.Left;
        Invalidate();
    }

    /// <summary>Bỏ hiệu ứng nhấn khi thả chuột; việc thực thi Click vẫn do Button gốc xử lý.</summary>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        pressed = false;
        Invalidate();
    }

    /// <summary>Dừng animation khi nút bị khóa để timer không chạy nền trong lúc gọi API.</summary>
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        if (!Enabled)
        {
            animation.Stop();
            hovered = false;
            pressed = false;
            hoverProgress = 0;
        }
        Invalidate();
    }

    /// <summary>Chuyển màu khoảng 120 ms; không thêm hiệu ứng khi Windows bật tương phản cao.</summary>
    private void AnimateHover()
    {
        if (!CafeVisuals.MotionEnabled)
        {
            hoverProgress = hovered ? 1f : 0f;
            Invalidate();
        }
        else animation.Start();
    }

    /// <summary>Vẽ nút bo góc, màu hover/nhấn và viền focus; giữ nguyên nội dung tiếng Việt.</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 2 || Height < 2) return;
        e.Graphics.Clear(Parent?.BackColor ?? CafeTheme.Surface);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = CafeTheme.Rounded(new RectangleF(.5f, .5f, Width - 1, Height - 1), CornerRadius);
        var background = Enabled ? BackColor : CafeTheme.Soft;
        var target = ControlPaint.Light(background, .12f);
        background = Color.FromArgb(
            (int)(background.R + (target.R - background.R) * hoverProgress),
            (int)(background.G + (target.G - background.G) * hoverProgress),
            (int)(background.B + (target.B - background.B) * hoverProgress));
        if (Enabled && pressed) background = ControlPaint.Dark(background, .07f);
        using var fill = new SolidBrush(background);
        e.Graphics.FillPath(fill, path);
        if (FlatAppearance.BorderSize > 0 || Focused)
        {
            using var pen = new Pen(!Enabled ? CafeTheme.Line : Focused ? CafeTheme.Coffee : FlatAppearance.BorderColor, Focused ? 2 : 1);
            e.Graphics.DrawPath(pen, path);
        }
        var rect = Rectangle.Inflate(ClientRectangle, -Padding.Horizontal / 2, -Padding.Vertical / 2);
        TextRenderer.DrawText(e.Graphics, Text, Font, rect, Enabled ? ForeColor : CafeTheme.Muted,
            TextFormatFlags.VerticalCenter |
            (TextAlign == ContentAlignment.MiddleLeft ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter) |
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
    }

    /// <summary>Giải phóng timer cùng control để điều hướng qua nhiều màn hình không rò rỉ tài nguyên.</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing) animation.Dispose();
        base.Dispose(disposing);
    }
}

public sealed class ProductImage : Control
{
    public int ImageIndex { get; set; }
    private static readonly Lazy<Image?> Sheet = new(() =>
    {
        using var stream = typeof(ProductImage).Assembly.GetManifestResourceStream("FourChang.Desktop.CoffeeSheet.png");
        if (stream is null) return null;
        using var source = Image.FromStream(stream); return new Bitmap(source);
    });
    public ProductImage() { DoubleBuffered = true; BackColor = CafeTheme.Cream; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var sheet = Sheet.Value;
        if (sheet is null || Width <= 0 || Height <= 0) return;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        int w = sheet.Width / 3, h = sheet.Height / 2; int index = Math.Clamp(ImageIndex, 0, 5);
        int size = Math.Min(Width, Height);
        e.Graphics.DrawImage(sheet, new Rectangle((Width - size) / 2, (Height - size) / 2, size, size), new Rectangle(index % 3 * w, index / 3 * h, w, h), GraphicsUnit.Pixel);
    }
}
