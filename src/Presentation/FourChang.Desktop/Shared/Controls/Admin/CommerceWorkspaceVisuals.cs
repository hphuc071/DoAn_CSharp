using FourChang.Desktop.Shared.Theme;
namespace FourChang.Desktop.Shared.Controls.Admin;

/// <summary>Chuẩn hóa màn hình đơn/hóa đơn, chỉ căn chỉnh control và định dạng dữ liệu, không sửa nghiệp vụ.</summary>
internal static class CommerceWorkspaceVisuals
{
    /// <summary>Đưa phân trang xuống cuối danh sách, giữ nguyên button/event hiện có và vị trí dữ liệu.</summary>
    internal static void Apply(FlowLayoutPanel actions, Label counter, Label pagination, DataGridView grid)
    {
        var buttons = actions.Controls.OfType<Button>().Where(x => x.Text is "Trang trước" or "Trang sau").ToArray();
        if (counter.Parent is TableLayoutPanel parent)
        {
            var row = parent.GetRow(counter);
            parent.Controls.Remove(counter);
            var footer = Ui.Row(); footer.Name = "CommercePagination";
            foreach (var button in buttons) { actions.Controls.Remove(button); footer.Controls.Add(button); }
            actions.Controls.Remove(pagination); footer.Controls.Add(pagination); footer.Controls.Add(counter);
            parent.Controls.Add(footer,0,row);
        }
        foreach (var button in actions.Controls.OfType<Button>())
        {
            button.MinimumSize = new Size(106,44); button.Margin = new Padding(0,0,8,8);
            if (button.Text.StartsWith("Hủy", StringComparison.Ordinal)) button.ForeColor = CafeTheme.Danger;
        }
        var end = actions.Controls.OfType<Button>().FirstOrDefault(x => x.Text == "Bàn giao / hoàn tất");
        if (end is not null) actions.SetFlowBreak(end,true);
        FormatGrid(grid);
    }

    /// <summary>Tiền/ngày/trạng thái giữ giá trị gốc; bảng có hàng cao, chữ không sát viền và selected state rõ.</summary>
    internal static void FormatGrid(DataGridView grid)
    {
        grid.BorderStyle=BorderStyle.None;grid.BackgroundColor=CafeTheme.Surface;
        grid.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;
        grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersHeight=46;grid.RowTemplate.Height=48;
        grid.DefaultCellStyle.Padding=new Padding(10,8,10,8);
        grid.ColumnHeadersDefaultCellStyle.Padding=new Padding(10,8,10,8);
        grid.DefaultCellStyle.SelectionBackColor=CafeTheme.Selection;grid.DefaultCellStyle.SelectionForeColor=CafeTheme.Ink;
        grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(251,248,243);
        grid.ShowCellToolTips=true;
        foreach(DataGridViewColumn column in grid.Columns)
        {
            column.MinimumWidth=Math.Max(column.MinimumWidth,TextRenderer.MeasureText(column.HeaderText,CafeTheme.Strong(9)).Width+28);
            if(column.DataPropertyName is "SoTien" or "TienHang" or "PhiGiaoHang")
            {column.DefaultCellStyle.Format="#,##0'đ'";column.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleRight;}
        }
    }
}
