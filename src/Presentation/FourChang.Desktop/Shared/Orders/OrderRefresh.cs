using System.Text.Json;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Shared.Orders;

public partial class OrdersWorkspacePage
{
    /// <summary>Polling không che màn hình, không đổi bộ lọc đang nhập và không chạy khi dialog/menu/lệnh còn mở.</summary>
    private async Task RefreshOrdersInBackground()
    {
        if (Busy || commandRunning || IsDisposed || !IsHandleCreated || !Visible || actionMenu.Visible || FindForm()?.OwnedForms.Length != 0) return;
        refreshingAutomatically = true;
        try { await RefreshQuietly(); }
        finally { refreshingAutomatically = false; }
    }

    /// <summary>Không gắn lại DataSource nếu dữ liệu giống nhau; khi có thay đổi, giữ mã đơn chọn, vị trí cuộn và cột hiện hành.</summary>
    protected override void BindRows(IReadOnlyList<AdminOrderRow> rows)
    {
        // Record chứa byte[] rowversion nên so sánh giá trị bằng nội dung JSON, không dùng so sánh tham chiếu mảng.
        var snapshot = JsonSerializer.Serialize(rows);
        if (snapshot == boundSnapshot) return;
        var selected = Selected()?.MaDonHang;
        var top = Grid.FirstDisplayedScrollingRowIndex;
        var horizontal = Grid.HorizontalScrollingOffset;
        var column = Grid.CurrentCell?.ColumnIndex ?? 0;
        Grid.SuspendLayout();
        try
        {
            Grid.DataSource = rows.ToList();
            if (Grid.Rows.Count > 0)
            {
                var row = rows.Select((value, index) => (value, index)).FirstOrDefault(x => x.value.MaDonHang == selected).index;
                Grid.CurrentCell = Grid.Rows[row].Cells[Math.Clamp(column, 0, Grid.ColumnCount - 1)];
                Grid.Rows[row].Selected = true;
                if (top >= 0) Grid.FirstDisplayedScrollingRowIndex = Math.Min(top, Grid.RowCount - 1);
                Grid.HorizontalScrollingOffset = horizontal;
            }
            boundSnapshot = snapshot;
        }
        finally { Grid.ResumeLayout(); }
    }
}
