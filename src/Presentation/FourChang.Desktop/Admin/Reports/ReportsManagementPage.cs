using System.Data;
using System.Globalization;
using FourChang.Desktop.Shell;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Admin.Reports;

/// <summary>UC-ADM-07: ba chỉ tiêu, ba bảng và xuất Excel từ đúng snapshot đang xem.</summary>
public sealed class ReportsManagementPage : UserControl
{
    private readonly DateTimePicker from = new() { Value = DateTime.Today.AddDays(-30), Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 150, Name = "ReportFrom" };
    private readonly DateTimePicker to = new() { Value = DateTime.Today, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 150, Name = "ReportTo" };
    private readonly ComboBox channel = Ui.Select("Tất cả", "WEB", "POS");
    private readonly ComboBox fulfillment = Ui.Select("Tất cả", "DINE_IN", "TAKEAWAY", "DELIVERY");
    private readonly NumericUpDown shift = new() { Maximum = 999999999, Width = 150 };
    private readonly Label status = Ui.Label("", 9, false, CafeTheme.Muted);
    private readonly Label definition = Ui.Label("", 9, false, CafeTheme.Muted);
    private readonly TableLayoutPanel cards = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, RowCount = 1, Name = "ReportMetrics" };
    private readonly Panel tables = new() { Dock = DockStyle.Top, Height = 350 };
    private readonly Button load;
    private readonly Button excel;
    private readonly Button relatedPage;
    private ReportSnapshot? snapshot;
    private ReportExportInput? exportDraft;
    private Form? host;
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Giữ màu nâu/kem và card/bảng cùng phong cách các trang giao dịch; không tạo số minh họa.</summary>
    public ReportsManagementPage()
    {
        Dock = DockStyle.Top; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; Padding = new Padding(16); BackColor = CafeTheme.Cream; Name = "ReportsManagementPage";
        for (var i = 0; i < 3; i++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        status.Visible = false; status.TextChanged += (_, _) => status.Visible = !string.IsNullOrWhiteSpace(status.Text);
        channel.Width = fulfillment.Width = 150; fulfillment.FormattingEnabled = true;
        fulfillment.Format += (_, e) => e.Value = OperationsPresentation.Label(e.ListItem?.ToString() ?? "");
        load = Ui.Button("Xem báo cáo", () => { }, true); load.Name = "ReportLoad"; load.Click += async (_, _) => await Reload();
        excel = Ui.Button("Xuất Excel", () => { }); excel.Name = "ReportExcel"; excel.Enabled = false; excel.Click += async (_, _) => await Export();
        var audit = Ui.Button("Nhật ký hệ thống", OpenAudit); audit.Name = "ReportAudit";
        relatedPage = Ui.Button("Đến Đơn hàng", OpenRelatedPage); relatedPage.Name = "ReportRelatedPage"; relatedPage.Enabled = false;
        var filters = Ui.Row(Ui.Field("Từ ngày", from), Ui.Field("Đến ngày", to), Ui.Field("Nguồn đơn", channel), Ui.Field("Nhận món", fulfillment), Ui.Field("Ca (0: tất cả)", shift));
        var actions = Ui.Row(load, excel, audit, relatedPage);
        OperationsPresentation.FitRows(filters); OperationsPresentation.FitRows(actions);
        var header = Ui.Stack(Ui.Label("4CHANG  /  BÁO CÁO VÀ NHẬT KÝ", 8, true, CafeTheme.Coffee), Ui.Label("Báo cáo hoạt động", 18, true), Ui.Label("Đơn hoàn thành • Món bán chạy • Tiền đã hoàn", 9));
        var filterCard = new AdminSurface { AutoSize = true, Padding = new Padding(18, 16, 18, 8), Name = "AdminFilterSurface" };
        filters.SetFlowBreak(filters.Controls[filters.Controls.Count - 1], true); actions.Dock = DockStyle.None; filters.Controls.Add(actions);
        filterCard.Controls.Add(filters);
        var tableCard = new AdminSurface { AutoSize = true, Padding = new Padding(16) };
        tableCard.Controls.Add(AdminListLayout.Column(definition, tables));
        Controls.Add(AdminListLayout.Column(header, filterCard, status, cards, tableCard));
        HandleCreated += async (_, _) => { host = FindForm(); if (host is not null) host.Resize += ResizeTables; await Reload(); };
    }

    /// <summary>Đọc kỳ có cận cuối loại trừ; lỗi giữ nguyên báo cáo cũ, không thay bằng số 0.</summary>
    private async Task Reload()
    {
        if (!load.Enabled) return;
        load.Enabled = excel.Enabled = false; status.Text = "Đang tính báo cáo…";
        try
        {
            var result = await LiveSession.Api.GetReportAsync(new(from.Value.Date.ToUniversalTime(), to.Value.Date.AddDays(1).ToUniversalTime(),
                channel.SelectedIndex == 0 ? null : channel.SelectedItem?.ToString(), fulfillment.SelectedIndex == 0 ? null : fulfillment.SelectedItem?.ToString(), shift.Value == 0 ? null : (long)shift.Value));
            if (IsDisposed) return;
            snapshot = result; exportDraft = null; Render(result); status.Text = "";
        }
        catch (CafeApiException error) { if (!IsDisposed) status.Text = error.Message + (snapshot is null ? " Chưa có báo cáo." : " Đang giữ báo cáo đã tải trước đó."); }
        finally { if (!IsDisposed) { load.Enabled = true; excel.Enabled = snapshot is not null; } }
    }

    /// <summary>Không cộng chồng tổng đơn với tiền thu/tiền hoàn; món chỉ lấy ly thuộc đơn đã hoàn thành.</summary>
    private void Render(ReportSnapshot data)
    {
        foreach (Control child in cards.Controls.Cast<Control>().ToArray()) child.Dispose(); cards.Controls.Clear();
        var values = new[] {
            ("Tổng tiền đơn hoàn thành", data.ChiSo.DoanhThuThuan.ToString("N0", Vietnamese) + "đ", "Gồm phí giao và giảm trừ hợp lệ"),
            ("Số đơn hoàn thành", data.ChiSo.HoanTat.ToString("N0", Vietnamese), "Theo ngày hoàn thành trong kỳ"),
            ("Tiền đã hoàn", (data.ChiSo.HoanTienMat + data.ChiSo.HoanChuyenKhoan).ToString("N0", Vietnamese) + "đ", "Chỉ khoản đã trả thành công") };
        for (var i = 0; i < values.Length; i++) cards.Controls.Add(new MetricCard(values[i].Item1, values[i].Item2, values[i].Item3, i) { Dock = DockStyle.Fill, Height = 116, Margin = new Padding(0, 0, i == 2 ? 0 : 12, 12) }, i, 0);
        foreach (Control child in tables.Controls.Cast<Control>().ToArray()) child.Dispose(); tables.Controls.Clear();
        var tabs = Ui.Tabs(data.Bang.Select(table => (table.Ten, (Control)Table(table))).ToArray()); tabs.Name = "ReportTables"; tabs.Dock = DockStyle.Fill;
        tabs.SelectedIndexChanged += (_, _) => UpdateRelatedPage(); tables.Controls.Add(tabs); UpdateRelatedPage();
        definition.MaximumSize = new Size(Math.Max(500, Width - 100), 0);
        definition.Text = $"Kỳ báo cáo: {data.BoLoc.TuLuc.ToLocalTime():dd/MM/yyyy} – {data.BoLoc.DenLuc.ToLocalTime().AddDays(-1):dd/MM/yyyy}. Tổng đơn và tiền hoàn là hai chỉ tiêu riêng.";
        ResizeTables(this, EventArgs.Empty);
    }

    /// <summary>Giá trị số vẫn là decimal để định dạng/tính đúng; tên món/khách là text, không công thức.</summary>
    internal static DataGridView Table(ReportTable source)
    {
        var data = new DataTable();
        foreach (var column in source.Cot) data.Columns.Add(column.Ten, column.Kieu == "number" ? typeof(decimal) : column.Kieu == "datetime" ? typeof(DateTime) : typeof(string));
        foreach (var row in source.Dong) data.Rows.Add(row.Select((value, index) => value is null ? DBNull.Value : source.Cot[index].Kieu switch
        { "number" => (object)decimal.Parse(value, CultureInfo.InvariantCulture), "datetime" => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), _ => value }).ToArray());
        var grid = Ui.Grid(new([], []), 320); grid.DataSource = data; grid.Dock = DockStyle.Fill; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        CommerceWorkspaceVisuals.FormatGrid(grid); grid.RowTemplate.Height = 56; grid.ColumnHeadersHeight = 48;
        grid.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex < 0) return;
            if (e.Value is decimal number) { e.Value = number.ToString("N0", Vietnamese) + (source.Cot[e.ColumnIndex].Ten.Contains("tiền", StringComparison.OrdinalIgnoreCase) ? "đ" : ""); e.FormattingApplied = true; }
            if (e.Value is DateTime at) { e.Value = at.ToLocalTime().ToString("dd/MM/yyyy HH:mm"); e.FormattingApplied = true; }
        };
        return grid;
    }

    /// <summary>Tái sử dụng worker/receipt chống gửi trùng; chỉ Excel, không có màn hình quản lý tệp đã xuất.</summary>
    private async Task Export()
    {
        if (snapshot is null) return;
        excel.Enabled = false;
        try
        {
            exportDraft ??= new(snapshot.MaSnapshot, "EXCEL", Guid.NewGuid());
            var input = exportDraft;
            var job = await AdminUi.Recover(() => LiveSession.Api.StartReportExportAsync(input), () => LiveSession.Api.ReportExportReceiptAsync(input.MaThaoTac));
            exportDraft = null; using var dialog = new ReportExportDialog(job); dialog.ShowDialog(FindForm());
        }
        catch (CafeApiException error) { status.Text = error.Message; if (error.StatusCode is not null && (int)error.StatusCode < 500) exportDraft = null; }
        finally { if (!IsDisposed) excel.Enabled = snapshot is not null; }
    }
    private (string Route, string Caption)? RelatedDestination() => tables.Controls.OfType<TabControl>().FirstOrDefault()?.SelectedTab?.Text switch
    { "Đơn hoàn thành" => ("manage-orders", "Đơn hàng"), "Món bán chạy" => ("menu", "Thực đơn"), "Tiền đã hoàn" => ("refunds", "Hoàn tiền"), _ => null };
    private void UpdateRelatedPage() { var target = RelatedDestination(); relatedPage.Enabled = target is not null; relatedPage.Text = target is { } item ? "Đến " + item.Caption : "Đến trang liên quan"; }
    private void OpenRelatedPage() { if (RelatedDestination() is { } target && FindForm() is MainForm shell) shell.Navigate(target.Route); }
    private void OpenAudit() { using var form = AdminUi.Dialog("Nhật ký hệ thống", 1220, 820); form.Controls.Add(new AuditManagementPage()); form.ShowDialog(FindForm()); }
    private void ResizeTables(object? sender, EventArgs args) { if (host is not null && tables.IsHandleCreated) tables.Height = Math.Clamp(host.ClientSize.Height - host.PointToClient(tables.PointToScreen(Point.Empty)).Y - 55, 180, 520); }
    protected override void Dispose(bool disposing) { if (disposing && host is not null) host.Resize -= ResizeTables; base.Dispose(disposing); }
}