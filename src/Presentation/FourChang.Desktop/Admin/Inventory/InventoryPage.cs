// UC: UC-ADM-03
// Chức năng: Hiển thị số tồn và nguồn biến động của nguyên liệu.
// Cần nhớ: Đây là màn hình tra cứu; số kho thay đổi qua nghiệp vụ nhập/xuất hoặc pha chế.

using System.Globalization;
using FourChang.Desktop.Shared.Theme;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shell;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Admin.Inventory;

/// <summary>UC-ADM-04: theo dõi tồn và lịch sử kho chỉ đọc; không sửa số tồn trực tiếp.</summary>
public sealed class InventoryPage : AdminWorkspacePage<StockView>
{
    private readonly ComboBox state = Ui.Select("Tất cả", "Còn", "Sắp hết", "Hết", "Chưa xác định");
    private readonly System.Windows.Forms.Timer refresh = new() { Interval = 3000 };

    /// <summary>Giữ bố cục Admin, phân biệt cảnh báo bằng nhãn và màu nền nhẹ.</summary>
    public InventoryPage() : base("Tồn kho và cảnh báo", "Tồn thực tế − Giữ chỗ = Khả dụng", true, "KHO / TỒN NGUYÊN LIỆU", fitTableToRows: false)
    {
        Column("MaNguyenLieu", "Mã", 35); Column("TenNguyenLieu", "Nguyên liệu", 150); Column("DonVi", "Đơn vị", 50);
        Column("TonThucTe", "Thực tế", 80); Column("GiuCho", "Giữ chỗ", 75);
        Column("KhaDung", "Khả dụng", 80); Column("CanhBao", "Cảnh báo", 100);
        Filters.Controls.Add(Ui.Field("Trạng thái", state)); state.SelectedIndexChanged += async (_, _) => await Reload();
        AddAction(Actions, "Lịch sử kho", Details, true);
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
        refresh.Tick += async (_, _) => await RefreshQuietly(); refresh.Start();
    }
    private string? boundSnapshot;
    /// <summary>Không bind lại cùng dữ liệu khi polling, giữ dòng đang chọn và vị trí đọc.</summary>
    protected override void BindRows(IReadOnlyList<StockView> rows)
    {
        var snapshot = System.Text.Json.JsonSerializer.Serialize(rows);
        if (snapshot == boundSnapshot) return;
        var id = Selected()?.MaNguyenLieu;
        var top = Grid.FirstDisplayedScrollingRowIndex;
        Grid.DataSource = rows.ToList();
        foreach (DataGridViewRow row in Grid.Rows)
            if (row.DataBoundItem is StockView item && item.MaNguyenLieu == id) Grid.CurrentCell = row.Cells[0];
        if (top >= 0 && Grid.RowCount > 0) Grid.FirstDisplayedScrollingRowIndex = Math.Min(top, Grid.RowCount - 1);
        boundSnapshot = snapshot;
    }
    /// <summary>Lấy mốc và số liệu máy chủ; không tính lại số dư trên WinForms.</summary>
    protected override async Task<IReadOnlyList<StockView>> Fetch()
    {
        var code = state.SelectedIndex switch { 1 => "AVAILABLE", 2 => "LOW", 3 => "OUT", 4 => "UNKNOWN", _ => "" };
        var snapshot = await LiveSession.Api.GetInventoryAsync(status: code); return snapshot.NguyenLieu;
    }
    /// <summary>Lọc theo mã hoặc tên trên snapshot vừa tải.</summary>
    protected override bool Match(StockView item, string query) => query.Length == 0 || item.MaNguyenLieu.ToString() == query || item.TenNguyenLieu.Contains(query, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>Đọc snapshot từ API hiện hữu và mở lịch sử đơn giản; không thay đổi dữ liệu kho/giữ chỗ/công thức.</summary>
    private async Task Details()
    {
        var selected = Selected(); if (selected is null) return;
        var data = await LiveSession.Api.GetStockDetailsAsync(selected.MaNguyenLieu);
        using var form = new InventoryHistoryForm(data);
        form.ShowDialog(FindForm());
    }
    /// <summary>Dừng timer khi rời trang để không truy vấn nền và không cập nhật control đã dispose.</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing) { refresh.Stop(); refresh.Dispose(); }
        base.Dispose(disposing);
    }
}

/// <summary>Lịch sử kho chỉ đọc: số tồn theo API và một bảng biến động, không lộ các cột khóa kỹ thuật.</summary>
public sealed class InventoryHistoryForm : Form
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    public InventoryHistoryForm(StockDetail detail)
    {
        var stock = detail.NguyenLieu;
        Name = "InventoryHistoryForm"; Text = "Lịch sử kho · " + stock.TenNguyenLieu;
        AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(1080, 670); MinimumSize = new Size(800, 520);
        StartPosition = FormStartPosition.CenterParent; BackColor = CafeTheme.Surface; Font = CafeTheme.Body();
        var summary = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = Padding.Empty };
        var values = new[] { ("Đơn vị", stock.DonVi), ("Tồn thực tế", Amount(stock.TonThucTe, stock.DonVi)),
            ("Đang giữ", Amount(stock.GiuCho, stock.DonVi)), ("Khả dụng", Amount(stock.KhaDung, stock.DonVi)) };
        for (var i = 0; i < values.Length; i++)
        {
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            var cell = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = new Padding(0, 0, 12, 0) };
            cell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            cell.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); cell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var label = Ui.Label(values[i].Item1, 9, false, CafeTheme.Muted); label.Dock = DockStyle.Fill;
            var value = Ui.Label(values[i].Item2, 13, true); value.Name = "InventoryHistoryValue" + i;
            value.AutoSize = false; value.AutoEllipsis = true; value.Dock = DockStyle.Fill;
            cell.Controls.Add(label, 0, 0); cell.Controls.Add(value, 0, 1); summary.Controls.Add(cell, i, 0);
        }
        var grid = Ui.Grid(new([], [])); grid.Name = "InventoryHistoryGrid"; grid.Dock = DockStyle.Fill;
        grid.AutoGenerateColumns = false; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        foreach (var (name, caption, weight, minimum) in new[]
        {
            ("ThoiGian", "Thời gian", 155f, 145), ("HoatDong", "Hoạt động", 120f, 100),
            ("SoLuong", "Số lượng thay đổi", 150f, 140), ("Nguon", "Nguồn", 145f, 120), ("GhiChu", "Ghi chú", 250f, 150)
        })
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = name, DataPropertyName = name, HeaderText = caption,
                FillWeight = weight, MinimumWidth = minimum, SortMode = DataGridViewColumnSortMode.NotSortable });
        grid.Columns["ThoiGian"].DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";
        grid.Columns["SoLuong"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        grid.Columns["SoLuong"].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        grid.Columns["SoLuong"].HeaderCell.Style.Padding = new Padding(10, 0, 14, 0);
        grid.Columns["SoLuong"].DefaultCellStyle.Padding = new Padding(10, 4, 14, 4);
        grid.Columns["GhiChu"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.RowTemplate.MinimumHeight = 48; grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex].Name != "SoLuong" || e.Value is not decimal quantity) return;
            e.Value = (quantity > 0 ? "+" : "") + Amount(quantity, stock.DonVi);
            e.CellStyle!.ForeColor = quantity < 0 ? CafeTheme.Danger : CafeTheme.SuccessInk; e.FormattingApplied = true;
        };
        grid.DataSource = detail.BienDong.OrderByDescending(x => x.PhatSinhLuc).ThenByDescending(x => x.MaBienDong).Select(x => new
        {
            ThoiGian = x.PhatSinhLuc.AddHours(7), HoatDong = Activity(x.Loai), x.SoLuong,
            Nguon = x.MaPhieuKho is long voucher ? "Phiếu kho #" + voucher : x.MaDonHang is long order ? "Đơn #" + order : "—",
            GhiChu = string.IsNullOrWhiteSpace(x.GhiChu) ? "—" : x.GhiChu
        }).ToList();
        var table = new Panel { Dock = DockStyle.Fill, BackColor = CafeTheme.Surface, Margin = Padding.Empty };
        var empty = Ui.Label("Chưa phát sinh nhập, xuất hoặc pha chế.", 10, false, CafeTheme.Muted);
        empty.Name = "InventoryHistoryEmpty"; empty.AutoSize = false; empty.Dock = DockStyle.Fill; empty.TextAlign = ContentAlignment.MiddleCenter;
        table.Controls.Add(grid); table.Controls.Add(empty);
        grid.Visible = detail.BienDong.Count > 0; empty.Visible = detail.BienDong.Count == 0;
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(22), BackColor = CafeTheme.Surface };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var title = Ui.Label(stock.TenNguyenLieu, 18, true); title.Dock = DockStyle.Fill;
        body.Controls.Add(title, 0, 0); body.Controls.Add(summary, 0, 1);
        body.Controls.Add(Ui.Label("Biến động nhập, xuất và pha chế", 10, true), 0, 2); body.Controls.Add(table, 0, 3);
        var close = Ui.Button("Đóng", () => Close()); close.Name = "InventoryHistoryClose";
        AdminUi.Compose(this, body, close);
    }

    private static string Amount(decimal? quantity, string unit) => quantity is decimal value ? value.ToString("#,##0.####", Vietnamese) + " " + unit : "Chưa xác định";
    private static string Activity(string type) => type switch
    {
        "RECEIPT" => "Nhập kho", "ISSUE" => "Xuất kho", "ORDER_CONSUMPTION" => "Pha chế", "REVERSAL" => "Hủy phiếu kho",
        "COUNT" => "Kiểm kê (cũ)", "ADJUSTMENT" => "Điều chỉnh (cũ)", _ => "Biến động khác"
    };
}
