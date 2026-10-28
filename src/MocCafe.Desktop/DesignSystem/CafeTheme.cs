using System.Drawing.Drawing2D;
namespace MocCafe.Desktop.DesignSystem;

public static class CafeTheme
{
    public static readonly Color Coffee = ColorTranslator.FromHtml("#75452D");
    public static readonly Color Dark = ColorTranslator.FromHtml("#30241D");
    public static readonly Color Cream = ColorTranslator.FromHtml("#F7F2EA");
    public static readonly Color Surface = ColorTranslator.FromHtml("#FFFDF9");
    public static readonly Color Line = ColorTranslator.FromHtml("#E5DCD1");
    public static readonly Color Muted = ColorTranslator.FromHtml("#81756A");
    public static readonly Color Ink = ColorTranslator.FromHtml("#2C2621");
    public static readonly Color Sage = ColorTranslator.FromHtml("#E4EFDF");
    public static readonly Color Amber = ColorTranslator.FromHtml("#FFF0D5");
    public static readonly Color Danger = ColorTranslator.FromHtml("#AD493E");
    public static Font Body(float size = 10) => new("Segoe UI", size);
    public static Font Strong(float size = 10) => new("Segoe UI", size, FontStyle.Bold);
    public const string Brand = "4CHANG LINH NGU LAM";
    public static GraphicsPath Rounded(RectangleF rect, float radius = 12)
    {
        var p = new GraphicsPath(); float d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        p.AddArc(rect.Left,rect.Top,d,d,180,90);p.AddArc(rect.Right-d,rect.Top,d,d,270,90);
        p.AddArc(rect.Right-d,rect.Bottom-d,d,d,0,90);p.AddArc(rect.Left,rect.Bottom-d,d,d,90,90);p.CloseFigure();return p;
    }
}

public sealed class SurfacePanel : Panel
{
    public SurfacePanel() { BackColor = CafeTheme.Surface; DoubleBuffered = true; Padding = new Padding(18); }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? CafeTheme.Cream);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var path=CafeTheme.Rounded(new RectangleF(0,0,Width-1,Height-1));using var fill=new SolidBrush(BackColor);e.Graphics.FillPath(fill,path);
    }
    protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using var pen=new Pen(CafeTheme.Line);using var path=CafeTheme.Rounded(new RectangleF(.5f,.5f,Width-1,Height-1));e.Graphics.DrawPath(pen,path); }
}

public sealed class CafeButton : Button
{
    public CafeButton(){DoubleBuffered=true;}
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor??CafeTheme.Surface);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var path=CafeTheme.Rounded(new RectangleF(.5f,.5f,Width-1,Height-1),7);
        var background=Enabled?BackColor:Color.FromArgb(235,228,219);
        using var fill=new SolidBrush(background);e.Graphics.FillPath(fill,path);
        if(FlatAppearance.BorderSize>0){using var pen=new Pen(Focused?CafeTheme.Coffee:FlatAppearance.BorderColor,Focused?2:1);e.Graphics.DrawPath(pen,path);}
        var rect=Rectangle.Inflate(ClientRectangle,-Padding.Horizontal/2,-Padding.Vertical/2);
        TextRenderer.DrawText(e.Graphics,Text,Font,rect,Enabled?ForeColor:CafeTheme.Muted,TextFormatFlags.VerticalCenter|(TextAlign==ContentAlignment.MiddleLeft?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter)|TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
    }
}

public sealed class ProductImage : Control
{
    public int ImageIndex { get; set; }
    private static readonly Lazy<Image?> Sheet = new(() => {
        using var stream=typeof(ProductImage).Assembly.GetManifestResourceStream("MocCafe.Desktop.CoffeeSheet.png");
        if(stream is null)return null;
        using var source=Image.FromStream(stream);return new Bitmap(source);
    });
    public ProductImage() { DoubleBuffered = true; BackColor = CafeTheme.Cream; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var sheet = Sheet.Value;
        if (sheet is null || Width<=0 || Height<=0) return;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        int w = sheet.Width / 3, h = sheet.Height / 2; int index=Math.Clamp(ImageIndex,0,5);
        int size=Math.Min(Width,Height);
        e.Graphics.DrawImage(sheet, new Rectangle((Width-size)/2,(Height-size)/2,size,size), new Rectangle(index % 3 * w, index / 3 * h, w, h), GraphicsUnit.Pixel);
    }
}
