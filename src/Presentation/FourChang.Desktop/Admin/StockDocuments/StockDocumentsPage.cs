using System.Globalization;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Admin.StockDocuments;

/// <summary>UC-ADM-04: phiếu nháp sửa được; xác nhận thủ công cập nhật kho và khóa nội dung.</summary>
public sealed class StockDocumentsPage : AdminWorkspacePage<StockDocumentView>
{
    private readonly Dictionary<string, StockDocumentAction> pending = [];
    private readonly ComboBox state = Ui.Select("Tất cả", "Phiếu nháp", "Thành công", "Đã hủy", "Đang xử lý", "Cần kiểm tra");
    private readonly DateTimePicker from = DateFilter("StockDocumentFrom");
    private readonly DateTimePicker until = DateFilter("StockDocumentUntil");
    private readonly Button edit;
    private readonly Button post;
    private readonly Button delete;
    private readonly Button cancelSchedule;
    private readonly System.Windows.Forms.Timer refresh = new() { Interval = 5000 };
    private string? rowSnapshot;
    private readonly Label postHint = Ui.Label("", 9, false, CafeTheme.AmberInk);

    /// <summary>Bộ lọc ngày nghiệp vụ và nhóm nút theo trạng thái, không cho chỉnh chứng từ thành công.</summary>
    public StockDocumentsPage() : base("Chứng từ kho", "Nhập kho • Xuất hỏng / hết hạn • Lịch sử biến động", true, "KHO / CHỨNG TỪ", fitTableToRows: false)
    {
        Column("SoPhieuKho", "Số phiếu", 150); Column("LoaiPhieu", "Loại phiếu", 90);
        Column("NgayThucHien", "Ngày phiếu", 100); Column("TrangThai", "Trạng thái", 100);
        Column("Nguon", "Nguồn nhập", 140); Column("GhiSoLuc", "Xác nhận lúc", 120);
        Filters.Controls.AddRange([Ui.Field("Trạng thái", state), Ui.Field("Từ ngày", from), Ui.Field("Đến ngày", until)]);
        state.SelectedIndexChanged += (_, _) => Render();
        from.ValueChanged += (_, _) => Render(); until.ValueChanged += (_, _) => Render();
        AddAction(Actions, "Lập phiếu", () => Edit(null), true);
        AddAction(Actions, "Chi tiết", Details);
        edit = AddAction(Actions, "Sửa nháp", () => Edit(Selected()));
        edit.Name = "StockDocumentEditButton";
        post = AddAction(Actions, "Xác nhận nhập / xuất", () => Act("post"), true);
        delete = AddAction(Actions, "Xóa nháp", Delete);
        cancelSchedule = AddAction(Actions, "Hủy phiếu chờ", () => Act("cancel"));
        cancelSchedule.Name = "StockDocumentCancelButton";
        postHint.MaximumSize = new Size(520, 0);
        // CurrentCellChanged chạy sau khi CurrentRow đã đổi; SelectionChanged có thể còn trả dòng cũ.
        Actions.Controls.Add(postHint);
        Grid.CurrentCellChanged += (_, _) => UpdateActions();
        Grid.DataBindingComplete += (_, _) => UpdateActions();
        Grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await Details(); };
        Grid.CellFormatting += FormatCell;
        UpdateActions();
        refresh.Tick += async (_, _) =>
        {
            if (!Busy && !Application.OpenForms.Cast<Form>().Any(f => f.Owner == FindForm())) await RefreshQuietly();
        };
        refresh.Start();
    }

    /// <summary>Chỉ bind khi nội dung đổi; giữ phiếu đang chọn khi hệ thống chuyển trạng thái đến hạn.</summary>
    protected override void BindRows(IReadOnlyList<StockDocumentView> rows)
    {
        var next = System.Text.Json.JsonSerializer.Serialize(new { Rows = rows, Reversals = Items.Where(x => x.MaPhieuBiDao is not null).Select(x => x.MaPhieuBiDao) });
        if (next == rowSnapshot) return;
        var id = Selected()?.MaPhieuKho; var top = Grid.FirstDisplayedScrollingRowIndex;
        Grid.DataSource = rows.ToList();
        foreach (DataGridViewRow row in Grid.Rows)
            if (row.DataBoundItem is StockDocumentView item && item.MaPhieuKho == id) Grid.CurrentCell = row.Cells[0];
        if (top >= 0 && Grid.RowCount > 0) Grid.FirstDisplayedScrollingRowIndex = Math.Min(top, Grid.RowCount - 1);
        rowSnapshot = next;
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { refresh.Stop(); refresh.Dispose(); }
        base.Dispose(disposing);
    }

    /// <summary>Khoảng ngày lọc luôn áp dụng, không cần tích chọn; mặc định 30 ngày gần nhất.</summary>
    private static DateTimePicker DateFilter(string name) => new() { Name = name, Format = DateTimePickerFormat.Custom,
        CustomFormat = "dd/MM/yyyy", ShowCheckBox = false, Width = 180,
        Value = DateTime.UtcNow.AddHours(7).Date.AddDays(name == "StockDocumentFrom" ? -30 : 0) };
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
    private static DateOnly DocumentDate(StockDocumentView item) => item.NgayThucHien ?? DateOnly.FromDateTime(item.TaoLuc.AddHours(7));
    private bool Reversed(StockDocumentView item) => Items.Any(x => x.MaPhieuBiDao == item.MaPhieuKho);

    /// <summary>Chỉ đọc chứng từ; việc đến ngày không phát sinh bất cứ lệnh ghi sổ nào.</summary>
    protected override Task<IReadOnlyList<StockDocumentView>> Fetch() => LiveSession.Api.GetStockDocumentsAsync();
    protected override bool Match(StockDocumentView item, string query)
    {
        var date = DocumentDate(item);
        return (date >= DateOnly.FromDateTime(from.Value)) && (date <= DateOnly.FromDateTime(until.Value))
            && (state.SelectedIndex switch { 1 => item.TrangThai == "DRAFT", 2 => item.TrangThai == "POSTED" && !Reversed(item), 3 => Reversed(item) || item.TrangThai == "CANCELLED", 4 => item.TrangThai == "SCHEDULED", 5 => item.TrangThai == "FAILED", _ => true })
            && (query.Length == 0 || $"{item.MaPhieuKho} {item.SoPhieuKho} {item.Nguon} {item.LyDo} {TypeName(item.LoaiPhieu)}".Contains(query, StringComparison.CurrentCultureIgnoreCase));
    }

    /// <summary>Chỉ đưa ra thao tác hợp lệ cho phiếu đang chọn; chặn xác nhận trước ngày nghiệp vụ.</summary>
    private void UpdateActions()
    {
        if (edit is null) return;
        var item = Selected();
        var editable = item is not null && item.TrangThai is "DRAFT" or "FAILED" && item.LoaiPhieu is "RECEIPT" or "ISSUE";
        edit.Visible = post.Visible = editable;
        edit.Text = item?.TrangThai == "FAILED" ? "Sửa phiếu lỗi" : "Sửa nháp";
        delete.Visible = editable && item?.TrangThai == "DRAFT";
        post.Enabled = editable;
        cancelSchedule.Visible = item?.TrangThai is "SCHEDULED" or "FAILED";
        postHint.Visible = item?.TrangThai == "FAILED";
        postHint.Text = postHint.Visible ? item!.LoiXuLy ?? "Kiểm tra phiếu trước khi xác nhận lại." : "";
    }

    private string StatusName(StockDocumentView item) => Reversed(item) ? "Đã hủy" : item.TrangThai switch
    { "DRAFT" => "Nháp", "SCHEDULED" => "Đang xử lý", "FAILED" => "Cần kiểm tra", "POSTED" => "Thành công", "CANCELLED" => "Đã hủy", _ => item.TrangThai };
    private static string TypeName(string code) => code switch { "RECEIPT" => "Nhập kho", "ISSUE" => "Xuất kho", "COUNT" => "Kiểm kê", "REVERSAL" => "Hủy phiếu", _ => "Điều chỉnh (cũ)" };
    private void FormatCell(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || Grid.Rows[e.RowIndex].DataBoundItem is not StockDocumentView item) return;
        switch (Grid.Columns[e.ColumnIndex].DataPropertyName)
        {
            case "LoaiPhieu": e.Value = TypeName(item.LoaiPhieu); break;
            case "NgayThucHien": e.Value = DocumentDate(item).ToString("dd/MM/yyyy"); break;
            case "GhiSoLuc": e.Value = item.GhiSoLuc?.AddHours(7).ToString("dd/MM/yyyy HH:mm") ?? "—"; break;
            case "TrangThai":
                e.Value = StatusName(item);
                e.CellStyle!.ForeColor = item.TrangThai == "FAILED" ? CafeTheme.Danger : item.TrangThai == "POSTED" ? CafeTheme.SuccessInk : CafeTheme.AmberInk;
                break;
            default: return; // Cột không tùy biến vẫn cần formatter chuẩn xử lý null thành chuỗi rỗng.
        }
        e.FormattingApplied = true;
    }

    /// <summary>Editor chỉ làm việc với nháp; API tiếp tục kiểm tra trạng thái và rowversion khi lưu.</summary>
    private async Task Edit(StockDocumentView? item)
    {
        if (item is not null && (item.TrangThai is not ("DRAFT" or "FAILED") || item.LoaiPhieu is not ("RECEIPT" or "ISSUE"))) return;
        using var form = new StockDocumentEditorForm(await LiveSession.Api.GetInventoryAsync(), item);
        if (form.ShowDialog(FindForm()) != DialogResult.OK) return;
        if (form.SavedDocumentId is long id) await ShowSavedDraft(id);
    }

    /// <summary>Nạp lại sau lưu, mở khoảng lọc đến ngày phiếu và cuộn tới đúng ID, kể cả phiếu nhập lùi ngày.</summary>
    private async Task ShowSavedDraft(long id)
    {
        Busy = true;
        try { Items = await Fetch(); }
        catch (FourChang.Presentation.Shared.ApiClients.CafeApiException)
        {
            Message.Text = "Đã lưu phiếu nháp nhưng chưa tải lại được danh sách. Bấm Tải lại để xem phiếu.";
            return;
        }
        finally { Busy = false; }
        var saved = Items.SingleOrDefault(x => x.MaPhieuKho == id);
        if (saved is null)
        {
            Message.Text = "Đã lưu phiếu nháp nhưng danh sách chưa trả về phiếu này. Bấm Tải lại để kiểm tra.";
            return;
        }
        var date = DocumentDate(saved).ToDateTime(TimeOnly.MinValue);
        // Bộ lọc phải phản ánh đúng khoảng đang hiển thị, không bỏ qua ngày bằng một cờ ẩn.
        if (date < from.Value.Date) from.Value = date;
        if (date > until.Value.Date) until.Value = date;
        if (from.Value.Date > until.Value.Date) { from.Value = date; until.Value = date; }
        SearchBox.Clear(); state.SelectedIndex = 0; Render();
        foreach (DataGridViewRow row in Grid.Rows)
        {
            if (row.DataBoundItem is not StockDocumentView item || item.MaPhieuKho != id) continue;
            Grid.ClearSelection(); Grid.CurrentCell = row.Cells[0]; row.Selected = true;
            Grid.FirstDisplayedScrollingRowIndex = row.Index;
            break;
        }
        UpdateActions();
        Message.Text = saved.TrangThai switch
        {
            "POSTED" => "Thành công. Đã cập nhật kho; phiếu đang chọn đã khóa sửa/xóa.",
            "SCHEDULED" => $"Đã xác nhận. Đang xử lý, hệ thống tự cập nhật kho ngày {DocumentDate(saved):dd/MM/yyyy}.",
            "FAILED" => "Cần kiểm tra: " + saved.LoiXuLy,
            _ => "Đã lưu phiếu nháp đang chọn. Chưa đổi tồn kho; có thể sửa hoặc xác nhận phiếu."
        };
    }

    /// <summary>Chi tiết chỉ đọc cho cả nháp và phiếu đã xác nhận, giữ nguyên số lượng/giá đã chốt.</summary>
    private Task Details()
    {
        var item = Selected(); if (item is null) return Task.CompletedTask;
        using var form = new StockDocumentDetailsForm(item, StatusName(item));
        form.ShowDialog(FindForm()); return Task.CompletedTask;
    }

    /// <summary>Xác nhận có chủ đích; timeout giữ nguyên mã thao tác để không cộng/trừ kho hai lần.</summary>
    private async Task Act(string action)
    {
        var item = Selected(); if (item is null) return;
        if (action == "post" && item.TrangThai is not ("DRAFT" or "FAILED")) return;
        var key = action + ":" + item.MaPhieuKho;
        if (!pending.TryGetValue(key, out var input))
        {
            string? reason;
            if (action == "post")
            {
                if (!StockDocumentPrompts.Confirm(this, item)) return;
                reason = item.LyDo ?? "Xác nhận phiếu kho";
            }
            else reason = StockDocumentPrompts.Reason(this, "Hủy phiếu đang chờ xử lý", "");
            if (reason is null) return;
            input = new(reason, item.PhienBanDuLieu, Guid.NewGuid()); pending[key] = input;
        }
        try
        {
            await AdminUi.Recover(() => LiveSession.Api.ActStockDocumentAsync(item.MaPhieuKho, action, input), () => LiveSession.Api.StockDocumentReceiptAsync(input.MaThaoTac));
            pending.Remove(key); await Reload(); UpdateActions();
            if (action == "post") await ShowSavedDraft(item.MaPhieuKho);
            else Message.Text = "Đã hủy phiếu chờ; tồn kho không thay đổi.";
        }
        catch (FourChang.Presentation.Shared.ApiClients.CafeApiException ex)
        { if (ex.StatusCode is not null && (int)ex.StatusCode < 500) pending.Remove(key); throw; }
    }

    /// <summary>Chỉ xóa nháp sau xác nhận; phiếu đã ghi sổ được bảo vệ ở API và SQL.</summary>
    private async Task Delete()
    {
        var item = Selected(); if (item is null || item.TrangThai != "DRAFT") return;
        if (MessageBox.Show(this, "Xóa phiếu nháp " + item.SoPhieuKho + "?", "Xác nhận", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        var operation = Guid.NewGuid();
        await AdminUi.Recover(() => LiveSession.Api.DeleteStockDocumentAsync(item.MaPhieuKho, new(item.PhienBanDuLieu, operation)), () => LiveSession.Api.StockDocumentReceiptAsync(operation));
        await Reload(); UpdateActions();
    }
}

/// <summary>Xác nhận ghi sổ hoặc hủy phiếu chờ; không có thao tác hủy phiếu đã thành công.</summary>
internal static class StockDocumentPrompts
{
    /// <summary>Đối chiếu phiếu đang chọn trước khi ghi sổ, có nút xác nhận rõ ràng và không yêu cầu nhập lại lý do.</summary>
    internal static bool Confirm(Control owner, StockDocumentView item)
    {
        using var form = AdminUi.Dialog("Xác nhận phiếu kho", 660, 360);
        form.Name = "StockDocumentPostDialog";
        var confirm = Ui.Button("Xác nhận cập nhật kho", () => form.DialogResult = DialogResult.OK, true);
        confirm.Name = "StockDocumentPostConfirm";
        AdminUi.Compose(form, Ui.Panel(item.LoaiPhieu == "RECEIPT" ? "Xác nhận nhập kho" : "Xác nhận xuất kho",
            Ui.Label(item.SoPhieuKho), Ui.Label($"Số nguyên liệu: {item.ThanhPhan.Count}"),
            Ui.Label(item.NgayThucHien > DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7))
                ? $"Đang xử lý: tự cập nhật kho ngày {item.NgayThucHien:dd/MM/yyyy}. Phiếu sẽ được khóa sửa."
                : "Xác nhận sẽ cập nhật kho ngay và khóa sửa/xóa phiếu.", 9, false, CafeTheme.Muted)),
            confirm, Ui.Button("Quay lại", () => form.Close()));
        return form.ShowDialog(owner.FindForm()) == DialogResult.OK;
    }

    internal static string? Reason(Control owner, string title, string initial)
    {
        using var form = AdminUi.Dialog(title, 620, 320); form.MinimumSize = new(560, 300);
        var input = Ui.Input("Lý do hủy phiếu", initial, 480);
        var confirm = Ui.Button("Xác nhận hủy phiếu", () => { if (!string.IsNullOrWhiteSpace(input.Text)) form.DialogResult = DialogResult.OK; }, true);
        AdminUi.Compose(form, Ui.Panel(title, Ui.Field("Lý do bắt buộc", input)), confirm, Ui.Button("Đóng", () => form.Close()));
        return form.ShowDialog(owner.FindForm()) == DialogResult.OK ? input.Text.Trim() : null;
    }
}

/// <summary>Chi tiết chỉ đọc: phân nhóm thông tin, căn đơn vị/số tiền và không hiển thị trường không áp dụng.</summary>
public sealed class StockDocumentDetailsForm : Form
{
    public StockDocumentDetailsForm(StockDocumentView item, string status)
    {
        var receipt = item.LoaiPhieu == "RECEIPT";
        Name = "StockDocumentDetailsForm"; Text = "Chi tiết phiếu kho";
        AutoScaleMode = AutoScaleMode.Dpi; BackColor = CafeTheme.Surface; Font = CafeTheme.Body();
        StartPosition = FormStartPosition.CenterParent; MinimumSize = new Size(800, 460);
        ClientSize = new Size(1000, Math.Clamp(350 + item.ThanhPhan.Count * 48 + (receipt ? 56 : 0), 460, 720));
        var title = Ui.Label(item.LoaiPhieu switch { "RECEIPT" => "Phiếu nhập kho", "ISSUE" => "Phiếu xuất kho", "COUNT" => "Kiểm kê (lịch sử)", "REVERSAL" => "Phiếu hủy (lịch sử)", _ => "Phiếu kho" }, 18, true);
        title.Dock = DockStyle.Fill;
        var state = Ui.Label(status, 10, true, status == "Thành công" ? CafeTheme.SuccessInk : item.TrangThai == "FAILED" ? CafeTheme.Danger : CafeTheme.AmberInk);
        state.Name = "StockDocumentDetailsStatus"; state.Padding = new Padding(12, 6, 12, 6);
        state.BackColor = status == "Thành công" ? CafeTheme.Sage : CafeTheme.Soft;
        var header = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 0, 0, 14) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(title, 0, 0); header.Controls.Add(state, 1, 0);
        var code = Ui.Label("Mã phiếu: " + item.SoPhieuKho, 9, false, CafeTheme.Muted); code.AutoSize = false;
        code.AutoEllipsis = true; code.Height = 26; code.Dock = DockStyle.Top;
        var date = item.NgayThucHien ?? DateOnly.FromDateTime(item.TaoLuc.AddHours(7));
        var metadata = new List<Control>
        {
            Ui.Label("Ngày thực hiện: " + date.ToString("dd/MM/yyyy") + "    ·    Ghi sổ: " + (item.GhiSoLuc?.AddHours(7).ToString("dd/MM/yyyy HH:mm") ?? "Chưa ghi sổ"), 10)
        };
        if (receipt && !string.IsNullOrWhiteSpace(item.Nguon))
        {
            var source = Ui.Label("Nguồn nhập: " + item.Nguon, 10); source.Name = "StockDocumentDetailsSource"; metadata.Add(source);
        }
        if (!string.IsNullOrWhiteSpace(item.LyDo)) metadata.Add(Ui.Label("Ghi chú: " + item.LyDo, 10));
        if (!string.IsNullOrWhiteSpace(item.LoiXuLy)) metadata.Add(Ui.Label("Cần kiểm tra: " + item.LoiXuLy, 10, false, CafeTheme.Danger));
        var info = AdminListLayout.Column(metadata.ToArray());
        var grid = Ui.Grid(new([], [])); grid.Name = "StockDocumentDetailsLines"; grid.Dock = DockStyle.Fill;
        grid.AutoGenerateColumns = false; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        foreach (var (name, caption, weight, minimum) in new[]
        {
            ("NguyenLieu", "Nguyên liệu", 210f, 160), ("DonVi", "Đơn vị", 70f, 75), ("SoLuong", "Số lượng", 130f, 110),
            ("DonGiaNhap", "Đơn giá (đ)", 150f, 140), ("ThanhTien", "Thành tiền (đ)", 150f, 140)
        })
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = name, DataPropertyName = name, HeaderText = caption, FillWeight = weight, MinimumWidth = minimum, SortMode = DataGridViewColumnSortMode.NotSortable });
        grid.Columns["DonGiaNhap"].Visible = grid.Columns["ThanhTien"].Visible = receipt;
        grid.Columns["DonVi"].DefaultCellStyle.Alignment = grid.Columns["DonVi"].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        foreach (var name in new[] { "SoLuong", "DonGiaNhap", "ThanhTien" })
        {
            grid.Columns[name].DefaultCellStyle.Format = "#,##0.####";
            grid.Columns[name].DefaultCellStyle.FormatProvider = CultureInfo.GetCultureInfo("vi-VN");
            grid.Columns[name].DefaultCellStyle.Alignment = grid.Columns[name].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.Columns[name].DefaultCellStyle.Padding = new Padding(10, 4, 14, 4);
            grid.Columns[name].HeaderCell.Style.Padding = new Padding(10, 0, 14, 0);
        }
        grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex].Name != "DonGiaNhap" || e.Value is not decimal amount) return;
            e.Value = amount.ToString("#,##0.##", CultureInfo.GetCultureInfo("vi-VN")) + " đ/" + grid.Rows[e.RowIndex].Cells["DonVi"].Value;
            e.FormattingApplied = true;
        };
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.DataSource = item.ThanhPhan.OrderBy(x => x.MaDong).Select(x => new { NguyenLieu = x.TenNguyenLieu, x.DonVi, x.SoLuong, x.DonGiaNhap, ThanhTien = x.SoLuong * x.DonGiaNhap }).ToList();
        var total = Ui.Label("Tổng tiền nhập: " + item.ThanhPhan.Sum(x => x.SoLuong * (x.DonGiaNhap ?? 0)).ToString("#,##0.##", CultureInfo.GetCultureInfo("vi-VN")) + " đ", 13, true, CafeTheme.Coffee);
        total.Name = "StockDocumentDetailsTotal"; total.Visible = receipt; total.Dock = DockStyle.Top; total.Padding = new Padding(0, 12, 0, 0);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 6, BackColor = CafeTheme.Surface };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var sizing in new[] { SizeType.AutoSize, SizeType.AutoSize, SizeType.AutoSize, SizeType.AutoSize, SizeType.Percent, SizeType.AutoSize }) body.RowStyles.Add(new RowStyle(sizing, sizing == SizeType.Percent ? 100 : 0));
        body.Controls.Add(header, 0, 0); body.Controls.Add(code, 0, 1); body.Controls.Add(info, 0, 2);
        body.Controls.Add(Ui.Label($"Nguyên liệu trong phiếu ({item.ThanhPhan.Count})", 11, true), 0, 3);
        body.Controls.Add(grid, 0, 4); body.Controls.Add(total, 0, 5);
        body.SizeChanged += (_, _) =>
        {
            foreach (var label in metadata.OfType<Label>()) label.MaximumSize = new Size(Math.Max(200, body.ClientSize.Width - 44), 0);
        };
        var close = Ui.Button("Đóng", () => Close()); close.Name = "StockDocumentDetailsClose";
        AdminUi.Compose(this, body, close);
    }
}
