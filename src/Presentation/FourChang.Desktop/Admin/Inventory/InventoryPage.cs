using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shell;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Admin.Inventory;

/// <summary>ADM-05: bảng theo dõi tự làm mới, drill-down giữ chỗ/sổ kho/BOM; không có nút sửa số tồn.</summary>
public sealed class InventoryPage : AdminWorkspacePage<StockView>
{
    private readonly ComboBox state = Ui.Select("Tất cả", "Còn", "Sắp hết", "Hết", "Chưa xác định");
    private readonly System.Windows.Forms.Timer refresh = new() { Interval = 3000 };

    /// <summary>Giữ bố cục Admin, phân biệt cảnh báo bằng nhãn và màu nền nhẹ.</summary>
    public InventoryPage() : base("Tồn kho và cảnh báo", "Tồn thực tế − Giữ chỗ − Tồn an toàn = Khả dụng", true, "KHO / TỒN NGUYÊN LIỆU")
    {
        Column("MaNguyenLieu", "Mã", 35); Column("TenNguyenLieu", "Nguyên liệu", 150); Column("DonVi", "Đơn vị", 50);
        Column("TonThucTe", "Thực tế", 80); Column("GiuCho", "Giữ chỗ", 75); Column("TonAnToan", "An toàn", 75);
        Column("KhaDung", "Khả dụng", 80); Column("CanhBao", "Cảnh báo", 100);
        Filters.Controls.Add(Ui.Field("Trạng thái", state)); state.SelectedIndexChanged += async (_, _) => await Reload();
        AddAction(Actions, "Xem truy vết", Details, true);
        AddAction(Actions, "Chứng từ kho", () => { (FindForm() as MainForm)?.Navigate("stock"); return Task.CompletedTask; });
        foreach (DataGridViewColumn column in Grid.Columns)
            if (column.DataPropertyName is "TonThucTe" or "GiuCho" or "TonAnToan" or "KhaDung")
            {
                column.DefaultCellStyle.Format = "#,##0.####";
                column.DefaultCellStyle.FormatProvider = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
                column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
        Grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (Grid.Columns[e.ColumnIndex].DataPropertyName == "CanhBao" && e.Value is string code)
                e.Value = code switch { "OUT" => "Hết", "LOW" => "Sắp hết", "AVAILABLE" => "Còn", _ => "Chưa xác định" };
            if (Grid.Rows[e.RowIndex].DataBoundItem is StockView item && item.CanhBao is "LOW" or "OUT")
                e.CellStyle!.BackColor = item.CanhBao == "OUT" ? Color.FromArgb(255, 235, 227) : Color.FromArgb(255, 246, 218);
        };
        refresh.Tick += async (_, _) => await Reload(); refresh.Start();
    }
    /// <summary>Lấy mốc và số liệu máy chủ; không tính lại số dư trên WinForms.</summary>
    protected override async Task<IReadOnlyList<StockView>> Fetch()
    {
        var code = state.SelectedIndex switch { 1 => "AVAILABLE", 2 => "LOW", 3 => "OUT", 4 => "UNKNOWN", _ => "" };
        var snapshot = await LiveSession.Api.GetInventoryAsync(status: code); return snapshot.NguyenLieu;
    }
    /// <summary>Lọc theo mã hoặc tên trên snapshot vừa tải.</summary>
    protected override bool Match(StockView item, string query) => query.Length == 0 || item.MaNguyenLieu.ToString() == query || item.TenNguyenLieu.Contains(query, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>Hiển thị nguồn đơn/phiếu/ca, số giữ/tiêu hao/giải phóng và công thức ảnh hưởng.</summary>
    private async Task Details()
    {
        var selected = Selected(); if (selected is null) return;
        var data = await LiveSession.Api.GetStockDetailsAsync(selected.MaNguyenLieu);
        using var form = AdminUi.Dialog("Truy vết " + selected.TenNguyenLieu, 1100, 690);
        var movements = Ui.Grid(new([], []), 420); movements.DataSource = data.BienDong.ToList();
        var held = Ui.Grid(new([], []), 420); held.DataSource = data.GiuCho.ToList();
        var affected = Ui.Grid(new([], []), 420); affected.DataSource = data.AnhHuong.ToList();
        foreach (var grid in new[] { movements, held, affected }) { grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells; }
        form.Controls.Add(Ui.Panel(selected.TenNguyenLieu,
            Ui.Tabs(("Sổ kho và nguồn phát sinh", movements), ("Giữ chỗ / tiêu hao / giải phóng", held), ("BOM và cấu hình ảnh hưởng", affected))));
        form.ShowDialog(FindForm());
    }
    /// <summary>Dừng timer khi rời trang để không truy vấn nền và không cập nhật control đã dispose.</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing) { refresh.Stop(); refresh.Dispose(); }
        base.Dispose(disposing);
    }
}
