using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
namespace FourChang.Desktop.Shared.Controls;

// Logo vector của quán: bốn hạt cà phê, tách cà phê và vòng tròn. Cùng hình học với brand-mark.svg trên Web.
// Vẽ theo hệ tọa độ 100x100 nên không vỡ nét khi Windows thay đổi DPI, không tải ảnh/font ngoài.
public sealed class BrandMark : Control
{
    public BrandMark() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); AccessibleName = "Logo 4CHANG LINH NGU LAM"; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); Draw(e.Graphics, Width, Height);
    }
    // Cùng logo cho biểu tượng cửa sổ; giải phóng handle Windows sau khi clone để không rò tài nguyên GDI.
    public static Icon CreateWindowIcon()
    {
        using var bitmap = new Bitmap(64, 64, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap)) { graphics.Clear(Color.Transparent); Draw(graphics, 64, 64); }
        var handle = bitmap.GetHicon();
        try { using var borrowed = Icon.FromHandle(handle); return (Icon)borrowed.Clone(); }
        finally { DestroyIcon(handle); }
    }
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(IntPtr handle);
    private static void Draw(Graphics g, int width, int height)
    {
        if (width < 2 || height < 2) return; g.SmoothingMode = SmoothingMode.AntiAlias; var size = Math.Min(width, height) - 4;
        var state = g.Save(); g.TranslateTransform((width - size) / 2f, (height - size) / 2f); g.ScaleTransform(size / 100f, size / 100f);
        using var paper = new SolidBrush(ColorTranslator.FromHtml("#FFF6E8")); using var coffee = new SolidBrush(ColorTranslator.FromHtml("#845034"));
        using var rim = new Pen(ColorTranslator.FromHtml("#C99B70"), 1.4f); using var inner = new Pen(ColorTranslator.FromHtml("#E8D3B9"), .8f);
        using var stroke = new Pen(ColorTranslator.FromHtml("#845034"), 2.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.FillEllipse(paper, 4, 4, 92, 92); g.DrawEllipse(rim, 4, 4, 92, 92); g.DrawEllipse(inner, 11, 11, 78, 78);
        for (var i = 0; i < 4; i++)
        {
            var bean = g.Save(); g.TranslateTransform(33 + i * 11, 31 - (i is 1 or 2 ? 5 : 0)); g.RotateTransform(i * 12 - 18);
            g.FillEllipse(coffee, -3.4f, -7, 6.8f, 14); using var seam = new Pen(ColorTranslator.FromHtml("#F8E5CC"), .9f);
            g.DrawBezier(seam, 0, -5, -3, -1, 3, 1, 0, 5); g.Restore(bean);
        }
        using var cup = new GraphicsPath(); cup.AddBezier(29, 47, 31, 76, 62, 76, 65, 47); cup.CloseFigure(); g.DrawPath(stroke, cup);
        g.DrawLine(stroke, 29, 47, 65, 47); g.DrawBezier(stroke, 66, 51, 83, 45, 82, 65, 66, 63); g.DrawArc(stroke, 24, 67, 49, 13, 10, 160);
        g.Restore(state);
    }
}
