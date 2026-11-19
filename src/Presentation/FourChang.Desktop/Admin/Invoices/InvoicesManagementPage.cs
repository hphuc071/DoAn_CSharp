using System.Drawing.Printing;
using System.Globalization;
using FourChang.Desktop.Admin.Orders;
using FourChang.Desktop.Shared.Invoices;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Admin.Invoices;

/// <summary>UC-ADM-06: tra cứu hóa đơn, xem/in/tải bản đã phát hành, không sửa chứng từ gốc.</summary>
public sealed class InvoicesManagementPage : AdminWorkspacePage<AdminInvoiceView>
{
    private readonly DateTimePicker from = new() { Value = DateTime.Today.AddMonths(-1), Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 135 };
    private readonly DateTimePicker to = new() { Value = DateTime.Today, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 135 };
    private readonly NumericUpDown shift = new() { Maximum = 999999999, Width = 120 };
    private string? boundSnapshot;
    private readonly System.Windows.Forms.Timer refresh = new() { Interval = 5000 };
    private bool quiet;
    private (string Search, DateTime From, DateTime To, long? Shift, int Page)? applied;
    private int page = 1;
    private readonly Label pagination = Ui.Label("", 9);

    /// <summary>Bộ lọc ngày không có ô tích; chỉ xem/in/tải hóa đơn, không nhập mã nguồn điều chỉnh.</summary>
    public InvoicesManagementPage() : base("Hóa đơn bán hàng", "Tra cứu hóa đơn • Xem chi tiết • In hoặc tải PDF", true, "TÀI CHÍNH / HÓA ĐƠN", fitTableToRows: false)
    {
        Name = "InvoicesManagementPage"; SearchBox.PlaceholderText = "Số hóa đơn, mã đơn hoặc tên khách";
        from.Name = "InvoiceFrom"; to.Name = "InvoiceTo";
        CommerceWorkspaceVisuals.AdminFilters(Filters, SearchBox, from, to, ("Ca (0: tất cả)", shift));
        AddAction(Filters, "Tìm kiếm", async () => { page = 1; await Reload(); }); AddAction(Filters, "Tải lại", async () => { page = 1; SearchBox.Clear(); await Reload(); });
        Column("SoHoaDon", "Số hóa đơn", 190); Column("MaDon", "Mã đơn", 100); Column("NguoiMua", "Khách hàng", 185);
        Column("SoTien", "Số tiền", 115); Column("PhatHanhLuc", "Ngày phát hành", 165);
        AddAction(Actions, "Xem hóa đơn", Details, true); AddAction(Actions, "In hóa đơn", Print); AddAction(Actions, "Tải PDF", Export);
        AddAction(Actions, "Trang trước", async () => { if (page > 1) { page--; await Reload(); } });
        AddAction(Actions, "Trang sau", async () => { page++; await Reload(); }); Actions.Controls.Add(pagination);
        OperationsPresentation.FitRows(Filters); OperationsPresentation.FitRows(Actions); OperationsPresentation.Grid(Grid);
        CommerceWorkspaceVisuals.Apply(Actions, Counter, pagination, Grid);
        Grid.RowTemplate.Height = 56; Grid.ColumnHeadersHeight = 48;
        Grid.DefaultCellStyle.Padding = new Padding(12, 10, 12, 10);
        foreach (DataGridViewColumn column in Grid.Columns)
        {
            if (column.DataPropertyName is "SoHoaDon" or "MaDon" or "SoTien") column.DefaultCellStyle.Font = CafeTheme.Strong(10);
            if (column.DataPropertyName == "SoTien") column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        Grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0 && !Busy) await Details(); };
        SearchBox.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter || Busy) return;
            e.SuppressKeyPress = true; page = 1; await Reload();
        };
        Grid.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex < 0) return;
            if (Grid.Columns[e.ColumnIndex].DataPropertyName == "SoTien" && e.Value is decimal money) { e.Value = money.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + "đ"; e.FormattingApplied = true; }
            if (Grid.Columns[e.ColumnIndex].DataPropertyName == "PhatHanhLuc" && e.Value is DateTime at) { e.Value = at.ToLocalTime().ToString("dd/MM/yyyy HH:mm"); e.FormattingApplied = true; }
        };
        refresh.Tick += async (_, _) =>
        {
            if (Busy || !Visible || FindForm()?.OwnedForms.Length != 0) return;
            quiet = true; try { await RefreshQuietly(); } finally { quiet = false; }
        };
        CommerceWorkspaceVisuals.AdminStyle(Filters, Actions, Grid);
        refresh.Start();
    }
    /// <summary>Tra tìm theo số/đơn/người mua và thời điểm phát hành ở server.</summary>
    protected override async Task<IReadOnlyList<AdminInvoiceView>> Fetch()
    {
        var filter = quiet && applied is { } saved ? saved : (SearchBox.Text.Trim(), from.Value.Date.ToUniversalTime(), to.Value.Date.AddDays(1).ToUniversalTime(), shift.Value == 0 ? (long?)null : (long)shift.Value, page);
        applied = filter;
        var result = await LiveSession.Api.GetAdminInvoicesAsync(filter.Item1, filter.Item2, filter.Item3, filter.Item4, filter.Item5);
        pagination.Text = $"Trang {page} · {result.TongSo} chứng từ"; return result.DuLieu;
    }
    /// <summary>Không lọc lại tại UI làm sai phân trang đã trả từ máy chủ.</summary>
    protected override bool Match(AdminInvoiceView item, string query) => true;
    /// <summary>Đọc JSON phát hành và đối chiếu tiền ở vùng riêng, không lấy menu mới dựng hóa đơn.</summary>
    private async Task Details()
    {
        var selected = Selected(); if (selected is null) return;
        var data = await LiveSession.Api.GetAdminInvoiceAsync(selected.MaHoaDon);
        var order = await LiveSession.Api.GetAdminOrderAsync(selected.MaDonHang);
        using var form = new InvoiceDetailsForm(data.HoaDon, data.SnapshotJson, order);
        form.ShowDialog(FindForm());
    }
    /// <summary>Gửi job tới máy in đã chọn; in lại có BẢN SAO, không phát hành số mới khi in lỗi.</summary>
    private async Task Print()
    {
        var selected = Selected(); if (selected is null) return;
        var data = await LiveSession.Api.GetAdminInvoiceAsync(selected.MaHoaDon);
        using var dialog = new PrintDialog { UseEXDialog = true };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        var job = await LiveSession.Api.LogInvoiceDocumentAsync(selected.MaHoaDon, new("PRINT_STARTED", dialog.PrinterSettings.PrinterName, null, Guid.NewGuid()));
        using var document = PrintDocument(data, job.BanSao); document.PrinterSettings = dialog.PrinterSettings;
        try { document.Print(); await LiveSession.Api.LogInvoiceDocumentAsync(selected.MaHoaDon, new("PRINT_SENT", job.MaCongViec.ToString(), null, Guid.NewGuid())); Message.Text = "Đã gửi lệnh in; kiểm tra kết quả tại máy in."; }
        catch (Exception ex) when (ex is InvalidPrinterException or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.ExternalException)
        { await LiveSession.Api.LogInvoiceDocumentAsync(selected.MaHoaDon, new("PRINT_FAILED", job.MaCongViec.ToString(), "Không gửi được tới máy in", Guid.NewGuid())); Message.Text = "Máy in chưa xử lý được. Thử lại in cùng hóa đơn."; }
    }
    /// <summary>Tải PDF hoàn chỉnh rồi mới lưu bằng temp + rename; hủy SaveDialog không ghi sự kiện đã xuất.</summary>
    private async Task Export()
    {
        var selected = Selected(); if (selected is null) return;
        using var dialog = new SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf", FileName = "HoaDon-" + selected.MaHoaDon + ".pdf" };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        try
        {
            var bytes = await LiveSession.Api.GetAdminInvoicePdfAsync(selected.MaHoaDon); var temp = dialog.FileName + ".tmp";
            await File.WriteAllBytesAsync(temp, bytes); File.Move(temp, dialog.FileName, true);
            await LiveSession.Api.LogInvoiceDocumentAsync(selected.MaHoaDon, new("PDF_SAVED", "PDF", null, Guid.NewGuid())); Message.Text = "Đã lưu PDF từ snapshot hóa đơn.";
        }
        catch (IOException) { Message.Text = "Không lưu được file. Chọn thư mục có quyền ghi và thử lại xuất."; }
    }
    /// <summary>Nội dung lấy duy nhất từ snapshot, giữ dấu Unicode; không tra giá/menu.</summary>
    private static string SnapshotText(AdminInvoiceDetail data)
    {
        var culture = CultureInfo.GetCultureInfo("vi-VN");
        var lines = InvoiceSnapshotReader.Read(data.SnapshotJson);
        var text = new System.Text.StringBuilder($"4CHANG LINH NGU LAM\n{data.HoaDon.SoHoaDon}\nKhách hàng: {data.HoaDon.NguoiMua ?? "Khách lẻ"}\nPhát hành: {data.HoaDon.PhatHanhLuc?.ToLocalTime():dd/MM/yyyy HH:mm}\n\n");
        if (lines.Count == 0) text.AppendLine("Hóa đơn cũ chưa lưu đầy đủ danh sách món.");
        foreach (var line in lines)
        {
            text.AppendLine($"{line.Mon} · {line.Size} · {line.SoLuong} ly × {line.DonGia.ToString("N0", culture)}đ = {line.ThanhTien.ToString("N0", culture)}đ");
            if (!string.IsNullOrWhiteSpace(line.Topping)) text.AppendLine("  " + line.Topping);
        }
        text.AppendLine($"\nTổng giá trị hóa đơn: {data.HoaDon.SoTien.ToString("N0", culture)}đ");
        return text.ToString();
    }
    /// <summary>Phân trang theo vùng in thực; nhãn bản sao áp dụng mọi trang in lại.</summary>
    private static PrintDocument PrintDocument(AdminInvoiceDetail data, bool copy)
    {
        var document = new PrintDocument { DocumentName = data.HoaDon.SoHoaDon }; var remaining = "";
        document.BeginPrint += (_, _) => remaining = (copy ? "BẢN SAO\n" : "") + SnapshotText(data);
        document.PrintPage += (_, e) =>
        {
            if (e.Graphics is null) { e.Cancel = true; return; }
            using var font = CafeTheme.Body(10); var bounds = e.MarginBounds;
            e.Graphics.MeasureString(remaining, font, bounds.Size, StringFormat.GenericDefault, out var chars, out var linesFilled);
            e.Graphics.DrawString(remaining[..chars], font, Brushes.Black, bounds, StringFormat.GenericDefault);
            remaining = remaining[chars..]; e.HasMorePages = remaining.Length > 0;
        };
        return document;
    }
    /// <summary>Snapshot không đổi thì không bind lại; giữ dòng/chỗ cuộn khi có hóa đơn mới.</summary>
    protected override void BindRows(IReadOnlyList<AdminInvoiceView> rows)
    {
        var snapshot = System.Text.Json.JsonSerializer.Serialize(rows); if (snapshot == boundSnapshot) return;
        var selected = Selected()?.MaHoaDon; var top = Grid.FirstDisplayedScrollingRowIndex;
        Grid.DataSource = rows.ToList(); boundSnapshot = snapshot;
        if (Grid.Rows.Count > 0)
        {
            var index = rows.Select((row, i) => (row, i)).FirstOrDefault(x => x.row.MaHoaDon == selected).i;
            Grid.CurrentCell = Grid.Rows[index].Cells[0];
            if (top >= 0) Grid.FirstDisplayedScrollingRowIndex = Math.Min(top, Grid.RowCount - 1);
        }
    }
    protected override void Dispose(bool disposing) { if (disposing) { refresh.Stop(); refresh.Dispose(); } base.Dispose(disposing); }

}
