using System.Drawing.Text;

namespace FourChang.Desktop.Shared.Theme;

/// <summary>Font và vùng chữ dùng chung; không đổi ngôn ngữ nhập, dữ liệu hoặc quy tắc tính toán.</summary>
public static class CafeTypography
{
    private static readonly Lazy<string> family = new(FindFamily);
    public static string FamilyName => family.Value;

    /// <summary>Ưu tiên font Windows hỗ trợ tiếng Việt; máy thiếu Segoe UI dùng Tahoma/Arial thay vì font thay thế ngẫu nhiên.</summary>
    private static string FindFamily()
    {
        using var installed = new InstalledFontCollection();
        foreach (var name in new[] { "Segoe UI", "Tahoma", "Arial" })
            if (installed.Families.Any(font => font.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) return name;
        return SystemFonts.MessageBoxFont?.FontFamily.Name ?? FontFamily.GenericSansSerif.Name;
    }

    /// <summary>Dùng đơn vị point để Windows scale font theo DPI, giữ rõ chữ và dấu tiếng Việt.</summary>
    public static Font Create(float size, FontStyle style = FontStyle.Regular) => new(FamilyName, size, style, GraphicsUnit.Point);

    /// <summary>Chừa khoảng trống cho dấu phía trên/dưới; không dựa vào chiều cao ASCII cố định.</summary>
    internal static int LineHeight(Control control, int padding = 6) =>
        TextRenderer.MeasureText("ẮỄộỵ", control.Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Height
        + (int)Math.Ceiling(padding * control.DeviceDpi / 96f);

    /// <summary>Nhãn dùng renderer Unicode native; hàng tuyệt đối quá thấp được nới thay vì cắt dấu.</summary>
    internal static void PrepareLabel(Label label)
    {
        label.UseCompatibleTextRendering = false;
        label.UseMnemonic = false;
        if (!label.AutoSize) label.AutoEllipsis = true;
        if (label.Parent is not TableLayoutPanel table || table.GetRowSpan(label) != 1) return;
        var row = table.GetRow(label);
        if (row < 0 || row >= table.RowStyles.Count || table.RowStyles[row].SizeType != SizeType.Absolute) return;
        var lines = label.Text.Count(c => c == '\n') + 1;
        var needed = LineHeight(label, 4) * lines + label.Margin.Vertical + label.Padding.Vertical;
        if (table.RowStyles[row].Height < needed) table.RowStyles[row].Height = needed;
    }

    /// <summary>Không để cửa sổ vượt vùng làm việc khi Windows dùng 125–200% DPI; không di chuyển worker kiểm thử off-screen.</summary>
    internal static void FitWindow(Form form)
    {
        if (form.IsDisposed || form.WindowState != FormWindowState.Normal ||
            (form.StartPosition == FormStartPosition.Manual && (form.Left < -5000 || form.Top < -5000))) return;
        var area = Screen.FromControl(form).WorkingArea;
        form.MinimumSize = new Size(Math.Min(form.MinimumSize.Width, area.Width), Math.Min(form.MinimumSize.Height, area.Height));
        var width = Math.Min(form.Width, area.Width); var height = Math.Min(form.Height, area.Height);
        var left = Math.Clamp(form.Left, area.Left, Math.Max(area.Left, area.Right - width));
        var top = Math.Clamp(form.Top, area.Top, Math.Max(area.Top, area.Bottom - height));
        if (form.Bounds != new Rectangle(left, top, width, height)) form.Bounds = new Rectangle(left, top, width, height);
    }
}
