using System.Text.Json;
using System.Globalization;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Admin.Ingredients;

/// <summary>Danh mục nguyên liệu theo hai đơn vị cố định, không còn tác vụ quản lý đơn vị/quy đổi.</summary>
public sealed class IngredientsPage : AdminWorkspacePage<IngredientView>
{
    private IngredientSnapshot snapshot = new([], []);
    private readonly BindingSource visibleRows = new();
    private string boundSnapshot = "";
    private bool viewportRestorePending;

    /// <summary>Danh sách gọn với tìm kiếm, thêm/sửa; đơn vị và ngưỡng được đọc cùng nhau, không nhập tồn kho.</summary>
    public IngredientsPage() : base("Danh sách nguyên liệu", "Quản lý tên, đơn vị g/ml, ngưỡng cảnh báo và trạng thái sử dụng.", polished: true, fitTableToRows: false)
    {
        Name = "IngredientsPage"; Grid.Name = "IngredientsGrid";
        Column("TenNguyenLieu", "Nguyên liệu", 42); Column("MaDonViCoSo", "Đơn vị", 22);
        Column("NguongCanhBao", "Ngưỡng cảnh báo", 24); Column("DangSuDung", "Trạng thái", 22);
        Grid.Columns[0].MinimumWidth = 180; Grid.Columns[1].MinimumWidth = 130;
        Grid.Columns[2].MinimumWidth = 165; Grid.Columns[3].MinimumWidth = 140;
        Grid.Columns[2].DefaultCellStyle.Format = "#,##0.####";
        Grid.Columns[2].DefaultCellStyle.FormatProvider = CultureInfo.GetCultureInfo("vi-VN");
        SearchBox.Name = "IngredientSearch";
        AddAction(Actions, "Thêm nguyên liệu", () => Edit(null), true);
        AddAction(Actions, "Sửa nguyên liệu", async () => { if (Selected() is { } item) await Edit(item); });
        Grid.CellFormatting += FormatCell;
    }

    /// <summary>Đọc danh mục và nhãn đơn vị từ API, không khai báo ID riêng trong giao diện.</summary>
    protected override async Task<IReadOnlyList<IngredientView>> Fetch()
    { snapshot = await LiveSession.Api.GetIngredientsAsync(); return snapshot.NguyenLieu; }

    /// <summary>Tìm theo mã hoặc tên, không lấy tên làm khóa ghi dữ liệu.</summary>
    protected override bool Match(IngredientView item, string query) => query.Length == 0 || item.MaNguyenLieu.ToString() == query || item.TenNguyenLieu.Contains(query, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>Giữ nguồn bảng khi lọc, chỉ cập nhật khi kết quả đổi và giữ lựa chọn/cuộn nếu còn hợp lệ.</summary>
    protected override void BindRows(IReadOnlyList<IngredientView> rows)
    {
        // Rowversion là byte[]: so sánh nội dung thay vì tham chiếu của bản ghi vừa tải.
        var next = JsonSerializer.Serialize(rows);
        if (next == boundSnapshot) return;
        var selected = Selected()?.MaNguyenLieu;
        var top = Grid.FirstDisplayedScrollingRowIndex;
        var column = Grid.CurrentCell?.ColumnIndex ?? 0;
        // CurrencyManager có thể cuộn các vùng cha khi đổi dòng. Giữ nguyên khung nhìn trong lúc gõ.
        var scrollPositions = new List<(ScrollableControl Control, Point Position)>();
        if (SearchBox.ContainsFocus && !viewportRestorePending)
            for (Control? parent = this; parent is not null; parent = parent.Parent)
                if (parent is ScrollableControl scroll && scroll.AutoScroll)
                    scrollPositions.Add((scroll, scroll.AutoScrollPosition));
        Grid.SuspendLayout();
        try
        {
            visibleRows.DataSource = rows.ToList();
            if (!ReferenceEquals(Grid.DataSource, visibleRows)) Grid.DataSource = visibleRows;
            if (rows.Count > 0)
            {
                var index = rows.Select((item, i) => (item, i)).FirstOrDefault(x => x.item.MaNguyenLieu == selected).i;
                Grid.CurrentCell = Grid.Rows[index].Cells[Math.Clamp(column, 0, Grid.ColumnCount - 1)];
                Grid.Rows[index].Selected = true;
                if (top >= 0) Grid.FirstDisplayedScrollingRowIndex = Math.Min(top, Grid.RowCount - 1);
            }
            boundSnapshot = next;
        }
        finally
        {
            Grid.ResumeLayout();
            if (scrollPositions.Count > 0)
            {
                // TextBox cuộn con trỏ sau TextChanged. Khôi phục sau thông điệp nhập, trước lần vẽ tiếp theo.
                viewportRestorePending = true;
                BeginInvoke(new Action(() =>
                {
                    try
                    {
                        foreach (var (control, position) in scrollPositions)
                            if (!control.IsDisposed) control.AutoScrollPosition = new Point(-position.X, -position.Y);
                    }
                    finally { viewportRestorePending = false; }
                }));
            }
        }
    }

    /// <summary>Giải phóng nguồn binding do riêng trang này sở hữu.</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing) { Grid.DataSource = null; visibleRows.Dispose(); }
        base.Dispose(disposing);
    }

    /// <summary>Chuyển mã đơn vị/trạng thái sang chữ dễ đọc; không thay giá trị gốc của DTO.</summary>
    private void FormatCell(object? sender, DataGridViewCellFormattingEventArgs args)
    {
        if (args.RowIndex < 0 || args.ColumnIndex < 0) return;
        var property = Grid.Columns[args.ColumnIndex].DataPropertyName;
        if (property == "DangSuDung") { args.Value = args.Value is true ? "Đang dùng" : "Ngừng dùng"; args.FormattingApplied = true; }
        if (property == "MaDonViCoSo" && args.Value is long id)
        {
            var unit = snapshot.DonVi.SingleOrDefault(x => x.MaDonVi == id);
            args.Value = unit is null ? "Chưa xác định" : IngredientUnits.IsSupported(unit.KyHieu) ? IngredientUnits.Label(unit.KyHieu) : unit.TenDonVi;
            args.FormattingApplied = true;
        }
    }

    /// <summary>Mở form tạo/sửa; trạng thái ngừng vẫn chỉnh trong form, không bỏ chức năng này.</summary>
    private async Task Edit(IngredientView? item)
    {
        using var form = new IngredientEditorForm(snapshot, item);
        if (form.ShowDialog(FindForm()) == DialogResult.OK) await Reload();
    }

}
