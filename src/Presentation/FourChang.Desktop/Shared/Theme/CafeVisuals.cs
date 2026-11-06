using System.Runtime.CompilerServices;

namespace FourChang.Desktop.Shared.Theme;

/// <summary>Chuẩn hóa control native theo một bảng màu nâu–kem. Không đọc/ghi thiết lập theme hoặc dữ liệu người dùng.</summary>
public static class CafeVisuals
{
    private static readonly ConditionalWeakTable<Control, object> prepared = new();
    private static readonly ConditionalWeakTable<Form, object> windows = new();
    private static bool initialized;

    /// <summary>Giữ quyền giảm chuyển động của Windows; đây không phải lựa chọn màu hay theme.</summary>
    public static bool MotionEnabled => SystemInformation.IsMenuAnimationEnabled && !SystemInformation.HighContrast;

    /// <summary>Áp dụng style cho modal mới mở; không đăng ký sự kiện chuyển theme hoặc đọc LocalAppData.</summary>
    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        Application.Idle += (_, _) =>
        {
            foreach (Form form in Application.OpenForms.Cast<Form>().ToArray())
            {
                if (windows.TryGetValue(form, out _)) continue;
                windows.Add(form, new object()); Apply(form);
            }
        };
    }

    /// <summary>Giữ nguyên instance, binding và handler; chỉ căn màu nền, chữ và trạng thái chọn của control.</summary>
    public static void Apply(Control root)
    {
        if (root.IsDisposed) return;
        Prepare(root);
        if (root is DataGridView grid)
        {
            grid.BackgroundColor = CafeTheme.Surface; grid.GridColor = CafeTheme.Line;
            grid.EnableHeadersVisualStyles = false;
            foreach (var style in new[] { grid.DefaultCellStyle, grid.RowsDefaultCellStyle, grid.AlternatingRowsDefaultCellStyle, grid.ColumnHeadersDefaultCellStyle })
            {
                style.BackColor = CafeTheme.Surface; style.ForeColor = CafeTheme.Ink;
                style.SelectionBackColor = CafeTheme.Soft; style.SelectionForeColor = CafeTheme.Ink;
            }
            grid.ColumnHeadersDefaultCellStyle.BackColor = CafeTheme.Soft;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = CafeTheme.Muted;
            grid.AlternatingRowsDefaultCellStyle.BackColor = CafeTheme.Cream;
        }
        if (root is TextBoxBase or ComboBox or UpDownBase)
        {
            root.BackColor = CafeTheme.Surface; root.ForeColor = CafeTheme.Ink;
        }
        foreach (Control child in root.Controls) Apply(child);
        root.Invalidate();
    }

    /// <summary>Đăng ký vẽ/ControlAdded đúng một lần, tránh trùng event khi tải bảng hoặc điều hướng nhiều lần.</summary>
    private static void Prepare(Control control)
    {
        if (prepared.TryGetValue(control, out _)) return;
        prepared.Add(control, new object());
        if (control is ComboBox combo && combo.DrawMode == DrawMode.Normal)
        {
            combo.DrawMode = DrawMode.OwnerDrawFixed; combo.ItemHeight = Math.Max(22, combo.ItemHeight);
            combo.DrawItem += (_, args) =>
            {
                using var background = new SolidBrush(args.State.HasFlag(DrawItemState.Selected) ? CafeTheme.Soft : CafeTheme.Surface);
                args.Graphics.FillRectangle(background, args.Bounds);
                var text = args.Index >= 0 ? combo.GetItemText(combo.Items[args.Index]) : combo.Text;
                TextRenderer.DrawText(args.Graphics, text, combo.Font, Rectangle.Inflate(args.Bounds, -4, 0), CafeTheme.Ink,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                args.DrawFocusRectangle();
            };
        }
        control.ControlAdded += (_, args) =>
        {
            if (control.IsHandleCreated && !control.IsDisposed)
                control.BeginInvoke(new Action(() => { if (args.Control is { IsDisposed: false } child) Apply(child); }));
        };
    }
}
