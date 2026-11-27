// UC: UC-ADM-06
// Chức năng: Theo dõi khoản cần hoàn và kết quả khách nhận tiền tại quầy.
// Cần nhớ: Trạng thái được duyệt không được trình bày thành đã chi tiền.

using System.Globalization;
using System.Text.Json;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Orders;
using FourChang.Desktop.Shared.Session;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Admin.Refunds;

/// <summary>UC-ADM-06: Quản lý theo dõi; khoản hoàn đã duyệt khi hủy không cần quyết định lần hai.</summary>
public sealed class RefundsManagementPage : AdminWorkspacePage<RefundView>
{
    private readonly DateTimePicker from = new() { Value = DateTime.Today.AddMonths(-1), Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 135, Name = "RefundFrom" };
    private readonly DateTimePicker to = new() { Value = DateTime.Today, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 135, Name = "RefundTo" };
    private readonly ComboBox state = Ui.Select("Tất cả", "WAITING", "PAID", "PARTIAL", "CHECK");
    private readonly Label summary = Ui.Label("", 10, true);
    private readonly System.Windows.Forms.Timer refresh = new() { Interval = 5000 };
    private (string Search, DateTime From, DateTime To, string? Status)? applied;
    private bool quiet;
    private string? boundSnapshot;
    private readonly Button details;
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Chỉ đọc, một nút xem chi tiết; tiền đã trả lấy từ lần chi thành công của Nhân viên.</summary>
    public RefundsManagementPage() : base("Theo dõi hoàn tiền", "Duyệt hủy → Khách đến quầy → Nhân viên trả tiền", true, "TÀI CHÍNH / HOÀN TIỀN", fitTableToRows: false)
    {
        Name = "RefundsManagementPage"; SearchBox.PlaceholderText = "Mã đơn hoặc tên người nhận";
        state.Name = "RefundStatusFilter"; state.Width = 210; state.FormattingEnabled = true;
        state.Format += (_, e) => e.Value = Caption(e.ListItem?.ToString());
        CommerceWorkspaceVisuals.AdminFilters(Filters, SearchBox, from, to, ("Kết quả", state));
        AddAction(Filters, "Tìm kiếm", Reload); AddAction(Filters, "Tải lại", Reload);
        Column("MaDon", "Mã đơn", 100); Column("NguoiNhan", "Người nhận", 160);
        Column("DuocDuyet", "Cần hoàn", 100); Column("DaHoan", "Đã trả", 100); Column("ConHoan", "Còn phải trả", 110);
        Column("TrangThaiTheoDoi", "Kết quả", 180); Column("TaoLuc", "Ngày phát sinh", 130);
        details = AddAction(Actions, "Xem chi tiết", Details, true); details.Name = "RefundDetails";
        summary.Name = "RefundSummary"; summary.Margin = new Padding(12, 12, 0, 8); Actions.Controls.Add(summary);
        OperationsPresentation.FitRows(Filters); OperationsPresentation.FitRows(Actions); OperationsPresentation.Grid(Grid);
        CommerceWorkspaceVisuals.FormatGrid(Grid);
        foreach (DataGridViewColumn column in Grid.Columns)
            if (column.DataPropertyName is "DuocDuyet" or "DaHoan" or "ConHoan") column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        Grid.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex < 0 || e.RowIndex < 0) return;
            var property = Grid.Columns[e.ColumnIndex].DataPropertyName;
            if (property is "DuocDuyet" or "DaHoan" or "ConHoan") { e.Value = e.Value is decimal money ? Money(money) : "Chưa duyệt"; e.FormattingApplied = true; }
            if (property == "TrangThaiTheoDoi") { e.Value = Caption(e.Value?.ToString()); e.FormattingApplied = true; }
            if (property == "TaoLuc") { e.Value = e.Value is DateTime at ? at.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "—"; e.FormattingApplied = true; }
        };
        Grid.CurrentCellChanged += (_, _) => details.Enabled = Selected() is not null;
        refresh.Tick += async (_, _) =>
        {
            if (Busy || !Visible || FindForm()?.OwnedForms.Length != 0) return;
            quiet = true; try { await RefreshQuietly(); } finally { quiet = false; }
        };
        CommerceWorkspaceVisuals.AdminStyle(Filters, Actions, Grid);
        // Chốt min/weight khi chưa bật Fill để WinForms không tự làm lệch tỷ lệ khi tăng cột tên.
        Grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        foreach (DataGridViewColumn column in Grid.Columns)
        {
            (column.MinimumWidth, column.FillWeight) = column.DataPropertyName switch
            {
                "MaDon" => (90, 90), "NguoiNhan" => (170, 185), "DuocDuyet" => (90, 95),
                "DaHoan" => (80, 80), "ConHoan" => (105, 110), "TrangThaiTheoDoi" => (160, 165),
                "TaoLuc" => (120, 125), _ => (column.MinimumWidth, column.FillWeight)
            };
        }
        Grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        Grid.CellPainting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || Grid.Columns[e.ColumnIndex].DataPropertyName != "TrangThaiTheoDoi" || Grid.Rows[e.RowIndex].DataBoundItem is not RefundView row) return;
            CommerceWorkspaceVisuals.StatusBadge(Grid, e, Caption(row.TrangThaiTheoDoi), row.TrangThaiTheoDoi == "PAID", row.TrangThaiTheoDoi == "CHECK");
        };
        Grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0 && !Busy) await Details(); };
        SearchBox.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter && !Busy) { e.SuppressKeyPress = true; await Reload(); } };
        refresh.Start();
    }

    /// <summary>Cập nhật nền dùng bộ lọc đã áp dụng, không gửi nội dung khách đang gõ dở.</summary>
    protected override async Task<IReadOnlyList<RefundView>> Fetch()
    {
        var filter = quiet && applied is { } saved ? saved : (SearchBox.Text.Trim(), from.Value.Date.ToUniversalTime(), to.Value.Date.AddDays(1).ToUniversalTime(), state.SelectedIndex > 0 ? state.SelectedItem?.ToString() : null);
        applied = filter;
        return await LiveSession.Api.GetAdminRefundsAsync(filter.Item1, filter.Item2, filter.Item3, filter.Item4);
    }
    protected override bool Match(RefundView item, string query) => true;

    /// <summary>Chỉ gắn lại khi dữ liệu thực đổi, giữ dòng/chỗ cuộn và không chớp khi polling.</summary>
    protected override void BindRows(IReadOnlyList<RefundView> rows)
    {
        var snapshot = JsonSerializer.Serialize(rows); if (snapshot == boundSnapshot) return;
        var selected = Selected()?.MaHoanTien; var top = Grid.FirstDisplayedScrollingRowIndex;
        Grid.DataSource = rows.ToList(); boundSnapshot = snapshot;
        if (Grid.Rows.Count > 0)
        {
            var index = rows.Select((row, i) => (row, i)).FirstOrDefault(x => x.row.MaHoanTien == selected).i;
            Grid.CurrentCell = Grid.Rows[index].Cells[0];
            if (top >= 0) Grid.FirstDisplayedScrollingRowIndex = Math.Min(top, Grid.RowCount - 1);
        }
        details.Enabled = rows.Count > 0;
        summary.Text = $"Còn phải trả: {Money(rows.Sum(x => x.ConHoan))}  •  Đã trả: {Money(rows.Sum(x => x.DaHoan))}";
    }

    /// <summary>Đọc lại khi mở chi tiết; chỉ hiện thông tin quầy cần dùng, không hiện mã kỹ thuật hay nhật ký JSON.</summary>
    private async Task Details()
    {
        if (Selected() is not { } selected) return;
        var data = await LiveSession.Api.GetAdminRefundAsync(selected.MaHoanTien); var record = data.HoSo;
        using var form = AdminUi.Dialog("Hoàn tiền · " + record.MaDon, 1020, 720); form.Name = "RefundTrackingDetails"; form.MinimumSize = new Size(900, 620);
        var history = Ui.Grid(new([], []), 210); history.Name = "RefundPayoutHistory"; history.AutoGenerateColumns = false;
        foreach (var field in new[] { ("CoKetQuaLuc", "Thời gian"), ("NguoiChi", "Nhân viên"), ("MaCa", "Ca làm"), ("SoTien", "Số tiền"), ("TrangThai", "Kết quả") })
            history.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = field.Item1, HeaderText = field.Item2 });
        history.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; CommerceWorkspaceVisuals.FormatGrid(history);
        history.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex < 0 || e.RowIndex < 0) return;
            var property = history.Columns[e.ColumnIndex].DataPropertyName;
            e.Value = property switch { "SoTien" when e.Value is decimal money => Money(money), "CoKetQuaLuc" when e.Value is DateTime at => at.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), "TrangThai" => e.Value?.ToString() switch { "SUCCEEDED" => "Đã trả", "FAILED" => "Thất bại", _ => "Chờ kiểm tra" }, _ => e.Value ?? "—" };
            e.FormattingApplied = true;
        };
        history.DataSource = record.LanChi.ToList();
        var body = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, Padding = new Padding(16) };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var sections = new[] {
            OrderDetailsForm.Section("Thông tin hoàn tiền", OrderDetailsForm.Value("Mã đơn", record.MaDon), OrderDetailsForm.Value("Người nhận", record.NguoiNhan, 260), OrderDetailsForm.Value("Kết quả", Caption(record.TrangThaiTheoDoi), 260)),
            OrderDetailsForm.Section("Số tiền", OrderDetailsForm.Value("Cần hoàn", record.DuocDuyet is { } amount ? Money(amount) : "Chưa duyệt"), OrderDetailsForm.Value("Đã trả", Money(record.DaHoan)), OrderDetailsForm.Value("Còn phải trả", Money(record.ConHoan))),
            OrderDetailsForm.Section("Nội dung", OrderDetailsForm.Value("Lý do", record.LyDo, 850)),
            OrderDetailsForm.Section("Lịch sử trả tiền", history, Ui.Label(record.LanChi.Count == 0 ? "Chưa ghi nhận lần trả tiền nào." : "Chỉ lần trả thành công mới tính vào số tiền đã hoàn.")) };
        foreach (var section in sections) { section.Dock = DockStyle.Top; body.RowStyles.Add(new RowStyle(SizeType.AutoSize)); body.Controls.Add(section); }
        if (record.TrangThaiTheoDoi == "WAITING") body.Controls.Add(Ui.Label("Khách đến quầy nhận tiền. Nhân viên kiểm tra và xác nhận sau khi đã giao tiền."));
        if (record.TrangThaiTheoDoi == "CHECK") body.Controls.Add(Ui.Label("Hồ sơ/lần chi cần kiểm tra; chưa xác nhận khách đã nhận đủ tiền."));
        var close = Ui.Button("Đóng", () => form.Close()); AdminUi.Compose(form, body, close); form.CancelButton = close;
        // Giữ đầu nội dung khi mở, không để grid lịch sử tự nhận focus và kéo trang xuống.
        form.Shown += (_, _) => { close.Focus(); if (body.Parent is ScrollableControl viewport) viewport.AutoScrollPosition = Point.Empty; };
        form.ShowDialog(FindForm());
    }
    private static string Money(decimal value) => value.ToString("N0", Vietnamese) + "đ";
    private static string Caption(string? value) => value switch { "WAITING" => "Chờ khách nhận", "PAID" => "Đã hoàn tiền", "PARTIAL" => "Đã hoàn một phần", "CHECK" => "Cần kiểm tra", _ => value ?? "—" };
    protected override void Dispose(bool disposing) { if (disposing) { refresh.Stop(); refresh.Dispose(); } base.Dispose(disposing); }
}
