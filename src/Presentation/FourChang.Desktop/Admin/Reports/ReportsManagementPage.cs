using System.Data;
using FourChang.Desktop.Shell;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Admin.Reports;

/// <summary>UC-ADM-10: chỉ hiển thị số server đã tính, giữ mốc khi lỗi và xuất đúng snapshot đang xem.</summary>
public sealed class ReportsManagementPage : UserControl
{
    private readonly DateTimePicker from = new() { Value = DateTime.Today.AddDays(-30), Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 140 };
    private readonly DateTimePicker to = new() { Value = DateTime.Today, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 140 };
    private readonly ComboBox channel = Ui.Select("Tất cả", "WEB", "POS");
    private readonly ComboBox fulfillment = Ui.Select("Tất cả", "DINE_IN", "TAKEAWAY", "DELIVERY");
    private readonly NumericUpDown shift = new() { Maximum = 999999999, Width = 120 };
    private readonly Label status = Ui.Label("", 9, false, CafeTheme.Muted);
    private readonly Label definition = Ui.Label("", 9, false, CafeTheme.Muted);
    private readonly TableLayoutPanel cards = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, RowCount = 2, Height = 240 };
    private readonly Panel tables = new() { Dock = DockStyle.Top, Height = 510 };
    private readonly Button load;
    private readonly Button excel;
    private readonly Button pdf;
    private readonly Button relatedPage;
    private ReportSnapshot? snapshot;
    private readonly Dictionary<string, ReportExportInput> exportDrafts = [];

    /// <summary>Giữ khu vực lọc, KPI và bảng chi tiết; không dùng biểu đồ/trend giả khi chưa có dữ liệu.</summary>
    public ReportsManagementPage()
    {
        status.Visible = false; status.TextChanged += (_, _) => status.Visible = !string.IsNullOrWhiteSpace(status.Text);
        Dock = DockStyle.Top; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; AutoScroll = false; Padding = new Padding(16); BackColor = CafeTheme.Cream; Name = "ReportsManagementPage";
        for (var i = 0; i < 4; i++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        load = Ui.Button("Xem báo cáo", () => { }, true); load.Click += async (_, _) => await Reload();
        excel = Ui.Button("Xuất Excel", () => { }); excel.Click += async (_, _) => await Export("EXCEL");
        pdf = Ui.Button("Xuất PDF", () => { }); pdf.Click += async (_, _) => await Export("PDF");
        var audit = Ui.Button("Nhật ký hệ thống", OpenAudit);
        var filterRow = Ui.Row(Ui.Field("Từ ngày", from), Ui.Field("Đến ngày", to), Ui.Field("Nguồn", channel), Ui.Field("Nhận món", fulfillment), Ui.Field("Mã ca (0: tất cả)", shift));
        var jobs = Ui.Button("Tệp đã xuất", () => { }); jobs.Click += async (_, _) => await OpenJobs();
        relatedPage = Ui.Button("Đến trang liên quan", OpenRelatedPage);
        relatedPage.Name = "ReportRelatedPage";
        relatedPage.Enabled = false;
        var actionRow = Ui.Row(load, excel, pdf, audit, jobs, relatedPage); OperationsPresentation.FitRows(filterRow); OperationsPresentation.FitRows(actionRow);
        Controls.Add(Ui.Panel("Báo cáo hoạt động", filterRow, actionRow, status, cards, definition, tables));
        HandleCreated += async (_, _) => await Reload();
    }

    /// <summary>Không dùng 0 thay lỗi; bảng cũ giữ lại với thời điểm và nhãn dữ liệu chưa cập nhật.</summary>
    private async Task Reload()
    {
        load.Enabled = false; status.Text = "Đang tính dữ liệu…";
        try
        {
            var result = await LiveSession.Api.GetReportAsync(new(from.Value.Date.ToUniversalTime(), to.Value.Date.AddDays(1).ToUniversalTime(),
                channel.SelectedIndex == 0 ? null : channel.Text, fulfillment.SelectedIndex == 0 ? null : fulfillment.Text, shift.Value == 0 ? null : (long)shift.Value));
            if (IsDisposed) return; snapshot = result; Render(result);
        }
        catch (CafeApiException ex) { if (!IsDisposed) status.Text = ex.Message + (snapshot is null ? " Chưa có báo cáo." : " · Đang hiển thị báo cáo đã tải trước đó."); }
        finally { if (!load.IsDisposed) load.Enabled = true; }
    }
    /// <summary>KPI có định nghĩa riêng; không cộng doanh thu với thực thu hoặc dùng cùng ngày cho cả thu và hoàn.</summary>
    private void Render(ReportSnapshot data)
    {
        foreach (Control child in cards.Controls.Cast<Control>().ToArray()) child.Dispose(); cards.Controls.Clear();
        var x = data.ChiSo;
        var values = new (string, string, string, int)[] { ("Doanh thu thuần", $"{x.DoanhThuThuan:N0}đ", "Đơn hoàn tất, điều chỉnh tại mốc", 0),
            ("Thu tiền mặt", $"{x.ThuTienMat:N0}đ", "Theo ngày thu đã xác minh", 1), ("Thu chuyển khoản", $"{x.ThuChuyenKhoan:N0}đ", "Theo ngày thu đã xác minh", 1),
            ("Hoàn thành công", $"{x.HoanTienMat + x.HoanChuyenKhoan:N0}đ", "Theo ngày chi thành công", 2),
            ("Dòng tiền ròng", $"{x.DongTienRong:N0}đ", "Thực thu trừ thực hoàn trong kỳ", 0), ("Đơn hoàn tất", x.HoanTat.ToString("N0"), "Theo ngày hoàn tất", 1),
            ("Đơn hết hạn", x.HetHan.ToString("N0"), "Tách khỏi hủy chủ động", 3), ("Tỷ lệ hoàn tất", x.TyLeHoanTat?.ToString("P1") ?? "Không áp dụng", "Mẫu số: đơn tạo trong kỳ", 1) };
        for (var i = 0; i < values.Length; i++)
        {
            var v = values[i]; cards.Controls.Add(new MetricCard(v.Item1, v.Item2, v.Item3, v.Item4) { Dock = DockStyle.Fill, Height = 116, Margin = new Padding(0, 0, 12, 12) }, i % 4, i / 4);
        }
        foreach (Control child in tables.Controls.Cast<Control>().ToArray()) child.Dispose(); tables.Controls.Clear();
        var tabs = Ui.Tabs(data.Bang.Select(t => (t.Ten, (Control)Table(t))).ToArray());
        tabs.Name = "ReportTables";
        tabs.Dock = DockStyle.Fill;
        tabs.SelectedIndexChanged += (_, _) => UpdateRelatedPage();
        tables.Controls.Add(tabs);
        UpdateRelatedPage();
        status.Text = "";
        definition.MaximumSize = new Size(Math.Max(600, Width - 100), 0); definition.Text = data.DinhNghia;
    }
    /// <summary>Tạo bảng đúng số dòng/cột của snapshot, không thay dữ liệu thật bằng số minh họa.</summary>
    internal static DataGridView Table(ReportTable source)
    {
        var table = new DataTable(); foreach (var column in source.Cot) table.Columns.Add(column.Ten);
        foreach (var row in source.Dong) table.Rows.Add(row.Select(x => (object?)x ?? DBNull.Value).ToArray());
        var grid = Ui.Grid(new([], []), 440); grid.DataSource = table; grid.Dock = DockStyle.Fill; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells; return grid;
    }
    /// <summary>Tạo job xuất đúng mốc; dialog cho xem tiến độ, hủy và chỉ lưu file READY.</summary>
    private async Task Export(string format)
    {
        if (snapshot is null) return; excel.Enabled = pdf.Enabled = false;
        var key = snapshot.MaSnapshot + ":" + format;
        try
        {
            if (!exportDrafts.TryGetValue(key, out var input)) exportDrafts[key] = input = new(snapshot.MaSnapshot, format, Guid.NewGuid());
            var job = await AdminUi.Recover(() => LiveSession.Api.StartReportExportAsync(input), () => LiveSession.Api.ReportExportReceiptAsync(input.MaThaoTac));
            exportDrafts.Remove(key); using var form = new ReportExportDialog(job); form.ShowDialog(FindForm());
        }
        catch (CafeApiException ex) { status.Text = ex.Message; if (ex.StatusCode is not null && (int)ex.StatusCode < 500) exportDrafts.Remove(key); }
        finally { if (!IsDisposed) excel.Enabled = pdf.Enabled = true; }
    }
    /// <summary>Ánh xạ tab báo cáo đến trang hiện có; không lấy số thứ tự dòng làm mã bản ghi.</summary>
    private (string Route, string Caption)? RelatedDestination()
    {
        var tab = tables.Controls.OfType<TabControl>().FirstOrDefault()?.SelectedTab;
        return tab?.Text switch
        {
            "Món đã phục vụ" => ("menu", "Thực đơn"),
            "Tỷ lệ đơn theo ngày tạo" => ("manage-orders", "Đơn hàng"),
            "Nguyên liệu cảnh báo" => ("inventory", "Tồn kho"),
            "Nguồn thu trong kỳ" => ("manage-orders", "Đơn hàng"),
            "Hoàn thành công trong kỳ" => ("refunds", "Hoàn tiền"),
            "Hủy món trong kỳ" => ("manage-orders", "Đơn hàng"),
            _ => null
        };
    }

    /// <summary>Cho biết trang sẽ mở ngay trên nút; tab chưa có đích được vô hiệu thay vì bấm không phản hồi.</summary>
    private void UpdateRelatedPage()
    {
        var destination = RelatedDestination();
        relatedPage.Enabled = destination is not null;
        relatedPage.Text = destination is { } target ? "Đến " + target.Caption : "Đến trang liên quan";
        relatedPage.AccessibleDescription = destination is { } page
            ? "Chuyển sang trang " + page.Caption + ". Không yêu cầu chọn dòng trong báo cáo."
            : "Chưa có trang liên quan cho tab hiện tại.";
    }

    /// <summary>Chuyển trang bằng router và sidebar hiện có, không mở popup hoặc sửa dữ liệu nguồn.</summary>
    private void OpenRelatedPage()
    {
        var destination = RelatedDestination();
        if (destination is null)
        {
            status.Text = "Chọn một tab báo cáo có trang liên quan.";
            return;
        }
        if (FindForm() is not MainForm shell)
        {
            status.Text = "Hãy mở báo cáo từ cửa sổ Quản lý để chuyển trang.";
            return;
        }
        // Navigate tự cập nhật tiêu đề/sidebar và giải phóng trang cũ. Không đọc control sau lệnh này.
        shell.Navigate(destination.Value.Route);
    }

    /// <summary>Mở lại công việc đã tạo, không xuất lại số liệu mới hoặc tạo thêm job khi người dùng chỉ muốn tải tệp cũ.</summary>
    private async Task OpenJobs()
    {
        try
        {
            var jobs = await LiveSession.Api.GetReportExportsAsync();
            using var form = AdminUi.Dialog("Tệp đã xuất", 1000, 650);
            var grid = Ui.Grid(new([], []), 440); grid.DataSource = jobs.ToList();
            var open = Ui.Button("Mở tác vụ đã chọn", () =>
            {
                if (grid.CurrentRow?.DataBoundItem is not ReportExportJob selected) return;
                using var dialog = new ReportExportDialog(selected); dialog.ShowDialog(form);
            }, true);
            form.Controls.Add(Ui.Panel("Công việc xuất theo snapshot", grid, Ui.Row(open))); form.ShowDialog(FindForm());
        }
        catch (CafeApiException ex) { status.Text = ex.Message; }
    }

    /// <summary>Audit có màn hình/bộ lọc và quyền riêng, không buộc có quyền báo cáo mới được tra cứu.</summary>
    private void OpenAudit()
    {
        using var form = AdminUi.Dialog("Nhật ký hệ thống", 1240, 840); form.Controls.Add(new AuditManagementPage()); form.ShowDialog(FindForm());
    }
}
