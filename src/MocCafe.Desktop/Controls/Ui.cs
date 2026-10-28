using System.Data;
using MocCafe.Desktop.DesignSystem;
using MocCafe.Desktop.Preview;
using MocCafe.Presentation.Shared.Models;
namespace MocCafe.Desktop.Controls;

public static class Ui
{
    public static Label Label(string text, float size = 10, bool bold = false, Color? color = null) => new() { Text = text, AutoSize = true, Font = bold ? CafeTheme.Strong(size) : CafeTheme.Body(size), ForeColor = color ?? CafeTheme.Ink, Margin = new Padding(0, 0, 0, 9) };
    public static Button Button(string text, Action? action = null, bool primary = false, bool danger = false)
    {
        var b = new CafeButton { Text = text, AutoSize = true, MinimumSize = new Size(95, 40), Padding = new Padding(14, 7, 14, 7), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = CafeTheme.Strong(9), BackColor = primary ? CafeTheme.Coffee : CafeTheme.Surface, ForeColor = primary ? Color.White : danger ? CafeTheme.Danger : CafeTheme.Ink, Margin = new Padding(0, 0, 8, 8) };
        b.FlatAppearance.BorderColor = danger ? CafeTheme.Danger : primary ? CafeTheme.Coffee : CafeTheme.Line;
        b.Click += (_, _) => { if(action != null) action(); else PreviewDialog.ShowMessage(b.FindForm(), text); };
        return b;
    }
    public static Button Unavailable(string text){var b=Button(text);b.Enabled=false;return b;}
    public static Control EmptyState(string title,string detail,int height=220)
    {
        var p=new Panel{Dock=DockStyle.Top,Height=height,BackColor=CafeTheme.Surface};
        p.Paint+=(_,e)=>{var r=new Rectangle(16,height/2-35,Math.Max(0,p.Width-32),40);TextRenderer.DrawText(e.Graphics,title,CafeTheme.Strong(11),r,CafeTheme.Ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);r.Y+=42;r.Height=55;TextRenderer.DrawText(e.Graphics,detail,CafeTheme.Body(9),r,CafeTheme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.WordBreak);};
        return p;
    }
    public static FlowLayoutPanel Row(params Control[] children) { var p = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, WrapContents = true, Margin = new Padding(0), Padding = new Padding(0) };p.Controls.AddRange(children);return p; }
    public static FlowLayoutPanel Stack(params Control[] children) { var p = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };p.Controls.AddRange(children);return p; }
    public static ComboBox Select(params string[] values) { var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170, Font = CafeTheme.Body(9), Margin = new Padding(0, 0, 10, 10) };c.Items.AddRange(values);if(values.Length > 0)c.SelectedIndex = 0;return c; }
    public static TextBox Input(string placeholder, string value = "", int width = 250) => new() { PlaceholderText = placeholder, Text = value, Width = width, Font = CafeTheme.Body(10), Margin = new Padding(0, 0, 10, 10), BorderStyle = BorderStyle.FixedSingle };
    public static Control Field(string name, Control input) { var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 14, 8) };p.Controls.Add(Label(name, 9, true));p.Controls.Add(input);return p; }
    public static SurfacePanel Panel(string title, params Control[] children)
    {
        var p = new SurfacePanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 0, 16) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = children.Length + 1, Margin = new Padding(0), Padding = new Padding(0) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var heading = Label(title, 12, true);heading.Margin = new Padding(0, 0, 0, 18);layout.Controls.Add(heading);
        foreach(var c in children) { c.Margin = new Padding(0, 0, 0, 12);layout.Controls.Add(c); }
        p.Controls.Add(layout);return p;
    }
    public static DataGridView Grid(PreviewTable sample, int height = 230)
    {
        var data = new DataTable();foreach(var c in sample.Columns)data.Columns.Add(c);foreach(var row in sample.Rows)data.Rows.Add(row.Cast<object>().ToArray());
        var grid = new DataGridView { DataSource = data, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, Dock = DockStyle.Top, Height = height, BackgroundColor = CafeTheme.Surface, BorderStyle = BorderStyle.None, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, GridColor = CafeTheme.Line, EnableHeadersVisualStyles = false, Font = CafeTheme.Body(9), ColumnHeadersHeight = 38, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing };
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(242,237,229), ForeColor = CafeTheme.Muted, Font = CafeTheme.Strong(9), Padding = new Padding(7), SelectionBackColor = Color.FromArgb(242,237,229), SelectionForeColor = CafeTheme.Ink };
        grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = CafeTheme.Surface, ForeColor = CafeTheme.Ink, SelectionBackColor = Color.FromArgb(238,224,205), SelectionForeColor = CafeTheme.Ink, Padding = new Padding(7) };
        grid.RowTemplate.Height = 42;grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250,247,242);
        grid.Paint+=(_,e)=>{if(grid.Rows.Count==0){var r=new Rectangle(12,grid.ColumnHeadersHeight+20,Math.Max(0,grid.Width-24),Math.Max(0,grid.Height-grid.ColumnHeadersHeight-30));TextRenderer.DrawText(e.Graphics,"Chưa có dữ liệu",CafeTheme.Body(10),r,CafeTheme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}};
        return grid;
    }
    public static TabControl Tabs(params (string Title, Control Body)[] pages)
    {
        var tabs = new TabControl { Dock = DockStyle.Top, Height = 560, Font = CafeTheme.Body(10), Padding = new Point(15, 9) };
        foreach(var (title, body) in pages) { var page = new TabPage(title) { BackColor = CafeTheme.Cream, Padding = new Padding(16), AutoScroll = true };body.Dock = DockStyle.Top;page.Controls.Add(body);tabs.TabPages.Add(page); }
        return tabs;
    }
    public static Control Kpis(params (string Title, string Value, string Hint)[] values)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Top, Height = 136, ColumnCount = values.Length, RowCount = 1, Margin = new Padding(0,0,0,16) };
        foreach(var (title,value,hint) in values) { row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f/values.Length));var card = new SurfacePanel { Dock = DockStyle.Fill, Margin = new Padding(0,0,12,0), Padding = new Padding(14) };var stack = Stack(Label(title,9,false,CafeTheme.Muted),Label(value,21,true),Label(hint,8,false,CafeTheme.Muted));card.Controls.Add(stack);row.Controls.Add(card); }
        return row;
    }
    public static Control Columns(Control left, Control right, float rightPercent = 34)
    {
        var p = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100-rightPercent));p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,rightPercent));left.Dock=DockStyle.Top;right.Dock=DockStyle.Top;left.Margin=new Padding(0,0,18,0);right.Margin=new Padding(0);p.Controls.Add(left,0,0);p.Controls.Add(right,1,0);return p;
    }
    public static Control Filters(params Control[] controls) => Row(controls);
}

