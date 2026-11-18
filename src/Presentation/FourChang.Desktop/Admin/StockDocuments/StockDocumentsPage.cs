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
    private readonly ComboBox state = Ui.Select("Tất cả", "Phiếu nháp", "Thành công", "Đã hủy");
    private readonly DateTimePicker from = DateFilter("StockDocumentFrom");
    private readonly DateTimePicker until = DateFilter("StockDocumentUntil");
    private readonly Button edit;
    private readonly Button post;
    private readonly Button reverse;
    private readonly Button delete;

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
        post = AddAction(Actions, "Xác nhận nhập / xuất", () => Act("post"), true);
        reverse = AddAction(Actions, "Hủy phiếu đã ghi sổ", () => Act("reverse"));
        delete = AddAction(Actions, "Xóa nháp", Delete);
        // CurrentCellChanged chạy sau khi CurrentRow đã đổi; SelectionChanged có thể còn trả dòng cũ.
        Grid.CurrentCellChanged += (_, _) => UpdateActions();
        Grid.DataBindingComplete += (_, _) => UpdateActions();
        Grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await Details(); };
        Grid.CellFormatting += FormatCell;
        UpdateActions();
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
            && (state.SelectedIndex switch { 1 => item.TrangThai == "DRAFT", 2 => item.TrangThai == "POSTED" && !Reversed(item), 3 => Reversed(item), _ => true })
            && (query.Length == 0 || $"{item.MaPhieuKho} {item.SoPhieuKho} {item.Nguon} {item.LyDo} {TypeName(item.LoaiPhieu)}".Contains(query, StringComparison.CurrentCultureIgnoreCase));
    }

    /// <summary>Chỉ đưa ra thao tác hợp lệ cho phiếu đang chọn; chặn xác nhận trước ngày nghiệp vụ.</summary>
    private void UpdateActions()
    {
        if (edit is null) return;
        var item = Selected();
        var draft = item?.TrangThai == "DRAFT" && item.LoaiPhieu is "RECEIPT" or "ISSUE";
        edit.Visible = post.Visible = delete.Visible = draft;
        post.Enabled = draft && DocumentDate(item!) <= Today;
        reverse.Visible = item?.TrangThai == "POSTED" && item.LoaiPhieu != "REVERSAL" && !Reversed(item);
    }

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
                e.Value = Reversed(item) ? "Đã hủy" : item.TrangThai == "POSTED" ? "Thành công" : "Nháp";
                e.CellStyle!.ForeColor = item.TrangThai == "POSTED" ? CafeTheme.SuccessInk : CafeTheme.AmberInk;
                break;
            default: return; // Cột không tùy biến vẫn cần formatter chuẩn xử lý null thành chuỗi rỗng.
        }
        e.FormattingApplied = true;
    }

    /// <summary>Editor chỉ làm việc với nháp; API tiếp tục kiểm tra trạng thái và rowversion khi lưu.</summary>
    private async Task Edit(StockDocumentView? item)
    {
        if (item is not null && (item.TrangThai != "DRAFT" || item.LoaiPhieu is not ("RECEIPT" or "ISSUE"))) return;
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
        Message.Text = "Đã lưu phiếu nháp đang chọn. Chưa đổi tồn kho; sau xác nhận nhập / xuất phiếu sẽ được khóa.";
    }

    /// <summary>Chi tiết chỉ đọc cho cả nháp và phiếu đã xác nhận, giữ nguyên số lượng/giá đã chốt.</summary>
    private Task Details()
    {
        var item = Selected(); if (item is null) return Task.CompletedTask;
        using var form = AdminUi.Dialog("Chi tiết phiếu kho", 1000, 660);
        var lines = Ui.Grid(new([], []), 300);
        lines.AutoGenerateColumns = false;
        foreach (var (property, caption) in new[] { ("TenNguyenLieu", "Nguyên liệu"), ("DonVi", "Đơn vị"), ("SoLuong", "Số lượng"), ("DonGiaNhap", "Đơn giá (đ)"), ("ThanhTien", "Thành tiền (đ)") })
            lines.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = property, HeaderText = caption, DefaultCellStyle = new() { Format = "#,##0.####", FormatProvider = CultureInfo.GetCultureInfo("vi-VN") } });
        lines.DataSource = item.ThanhPhan.Select(x => new { x.TenNguyenLieu, x.DonVi, x.SoLuong, x.DonGiaNhap, ThanhTien = x.SoLuong * x.DonGiaNhap }).ToList();
        lines.Columns[3].Visible = lines.Columns[4].Visible = item.LoaiPhieu == "RECEIPT";
        AdminUi.Compose(form, Ui.Panel(TypeName(item.LoaiPhieu) + " · " + item.SoPhieuKho,
            Ui.Label("Ngày: " + DocumentDate(item).ToString("dd/MM/yyyy") + " · " + (item.TrangThai == "POSTED" ? "Thành công — chỉ xem" : "Nháp — chưa cập nhật kho")),
            Ui.Label("Nguồn nhập: " + (item.Nguon ?? "—")), Ui.Label("Ghi chú: " + item.LyDo), lines,
            Ui.Label(item.LoaiPhieu == "RECEIPT" ? "Tổng tiền nhập: " + item.ThanhPhan.Sum(x => x.SoLuong * (x.DonGiaNhap ?? 0)).ToString("#,##0.##", CultureInfo.GetCultureInfo("vi-VN")) + " đ" : "")),
            Ui.Button("Đóng", () => form.Close()));
        form.ShowDialog(FindForm()); return Task.CompletedTask;
    }

    /// <summary>Xác nhận có chủ đích; timeout giữ nguyên mã thao tác để không cộng/trừ kho hai lần.</summary>
    private async Task Act(string action)
    {
        var item = Selected(); if (item is null) return;
        if (action == "post" && (item.TrangThai != "DRAFT" || DocumentDate(item) > Today)) return;
        var key = action + ":" + item.MaPhieuKho;
        if (!pending.TryGetValue(key, out var input))
        {
            string? reason;
            if (action == "post")
            {
                if (MessageBox.Show(this, "Hàng đã thực nhập / xuất đúng theo phiếu? Sau xác nhận, tồn kho được cập nhật và phiếu không được sửa.", "Xác nhận phiếu kho", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                reason = item.LyDo ?? "Xác nhận phiếu kho";
            }
            else reason = StockDocumentPrompts.Reason(this, "Hủy phiếu đã ghi sổ", "");
            if (reason is null) return;
            input = new(reason, item.PhienBanDuLieu, Guid.NewGuid()); pending[key] = input;
        }
        try
        {
            await AdminUi.Recover(() => LiveSession.Api.ActStockDocumentAsync(item.MaPhieuKho, action, input), () => LiveSession.Api.StockDocumentReceiptAsync(input.MaThaoTac));
            pending.Remove(key); await Reload(); UpdateActions();
            Message.Text = action == "post" ? "Thành công. Đã cập nhật tồn kho; phiếu được khóa và chỉ cho xem."
                : "Đã hủy bằng chứng từ ngược. Phiếu gốc và lịch sử được giữ nguyên.";
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

/// <summary>Chỉ yêu cầu lý do khi hủy phiếu đã ghi sổ, không tạo chứng từ trước khi xác nhận.</summary>
internal static class StockDocumentPrompts
{
    internal static string? Reason(Control owner, string title, string initial)
    {
        using var form = AdminUi.Dialog(title, 620, 320); form.MinimumSize = new(560, 300);
        var input = Ui.Input("Lý do hủy phiếu", initial, 480);
        var confirm = Ui.Button("Xác nhận hủy phiếu", () => { if (!string.IsNullOrWhiteSpace(input.Text)) form.DialogResult = DialogResult.OK; }, true);
        AdminUi.Compose(form, Ui.Panel(title, Ui.Field("Lý do bắt buộc", input)), confirm, Ui.Button("Đóng", () => form.Close()));
        return form.ShowDialog(owner.FindForm()) == DialogResult.OK ? input.Text.Trim() : null;
    }
}
