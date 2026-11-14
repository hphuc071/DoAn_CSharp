using System.Drawing.Printing;
using System.Text.Json;
using FourChang.Desktop.Admin.Orders;
using FourChang.Desktop.Shared.Invoices;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Admin.Invoices;

/// <summary>UC-ADM-08: tra snapshot, điều chỉnh có nguồn và in/xuất riêng, không sửa hoặc xóa hóa đơn gốc.</summary>
public sealed class InvoicesManagementPage : AdminWorkspacePage<AdminInvoiceView>
{
    private readonly DateTimePicker from = new() { Value = DateTime.Today.AddMonths(-1), Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 135 };
    private readonly DateTimePicker to = new() { Value = DateTime.Today, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 135 };
    private readonly NumericUpDown shift = new() { Maximum = 999999999, Width = 120 };
    private readonly Dictionary<long, CommandDraft<InvoiceAdjustmentInput>> drafts = [];
    private int page = 1;
    private readonly Label pagination = Ui.Label("", 9);

    /// <summary>Bộ lọc ngày không có ô tích; nút điều chỉnh tách khỏi xem/in/xuất để tránh phát hành nhầm.</summary>
    public InvoicesManagementPage() : base("Hóa đơn và điều chỉnh", "Bản gốc bất biến • Đối chiếu thu/hoàn • In lại có dấu bản sao", true, "TÀI CHÍNH / HÓA ĐƠN")
    {
        Filters.Controls.Clear(); Filters.Controls.AddRange([Ui.Field("Tìm kiếm", SearchBox), Ui.Field("Từ ngày", from), Ui.Field("Đến ngày", to), Ui.Field("Mã ca (0: tất cả)", shift)]);
        AddAction(Filters, "Tìm kiếm", async () => { page = 1; await Reload(); }); AddAction(Filters, "Tải lại", async () => { page = 1; SearchBox.Clear(); await Reload(); });
        Column("SoHoaDon", "Số hóa đơn", 140); Column("MaDonHang", "Mã đơn", 65); Column("Loai", "Loại", 80); Column("NguoiMua", "Người mua", 140);
        Column("SoTien", "Giá trị", 90); Column("TrangThai", "Trạng thái", 75); Column("PhatHanhLuc", "Phát hành", 130); Column("MaHoaDonGoc", "Hóa đơn gốc", 85);
        AddAction(Actions, "Chi tiết / đối chiếu", Details, true); AddAction(Actions, "Phát hành điều chỉnh", Adjust);
        AddAction(Actions, "Xem trước", Preview); AddAction(Actions, "In / in lại", Print); AddAction(Actions, "Xuất PDF", Export);
        AddAction(Actions, "Trang trước", async () => { if (page > 1) { page--; await Reload(); } });
        AddAction(Actions, "Trang sau", async () => { page++; await Reload(); }); Actions.Controls.Add(pagination);
        OperationsPresentation.FitRows(Filters); OperationsPresentation.FitRows(Actions); OperationsPresentation.Grid(Grid);
        CommerceWorkspaceVisuals.Apply(Actions, Counter, pagination, Grid);
    }
    /// <summary>Tra tìm theo số/đơn/người mua và thời điểm phát hành ở server.</summary>
    protected override async Task<IReadOnlyList<AdminInvoiceView>> Fetch()
    {
        var result = await LiveSession.Api.GetAdminInvoicesAsync(SearchBox.Text, from.Value.Date.ToUniversalTime(), to.Value.Date.AddDays(1).ToUniversalTime(), shift.Value == 0 ? null : (long)shift.Value, page);
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
    /// <summary>Chọn nguồn hủy hoặc hoàn khiếu nại đã duyệt; hoàn thừa/muộn không được giảm hóa đơn.</summary>
    private async Task Adjust()
    {
        var selected = Selected(); if (selected is null) return;
        if (!drafts.TryGetValue(selected.MaHoaDon, out var draft)) drafts[selected.MaHoaDon] = draft = new();
        using var form = new AdminCommandDialog<InvoiceAdjustmentInput>("Phát hành điều chỉnh " + selected.SoHoaDon, draft);
        var kind = form.ChoiceField("Nguồn điều chỉnh", "Hủy món hợp lệ", "Hồ sơ hoàn khiếu nại đã duyệt");
        var source = form.NumberField("Mã nguồn"); var reason = form.TextField("Lý do phát hành");
        form.Submit(op => new(kind.SelectedIndex == 0 ? (long)source.Value : null, reason.Text, selected.PhienBanDuLieu, op, kind.SelectedIndex == 1 ? (long)source.Value : null),
            async input => { await LiveSession.Api.AdjustInvoiceAsync(selected.MaHoaDon, input); }, async op => { await LiveSession.Api.InvoiceReceiptAsync(op); });
        if (form.ShowDialog(FindForm()) == DialogResult.OK) await Reload();
    }
    /// <summary>Xem trước chỉ dựng trang in, không hiện nút in tự do bỏ qua log nghiệp vụ.</summary>
    private async Task Preview()
    {
        var selected = Selected(); if (selected is null) return;
        var data = await LiveSession.Api.GetAdminInvoiceAsync(selected.MaHoaDon);
        await LiveSession.Api.LogInvoiceDocumentAsync(selected.MaHoaDon, new("PREVIEW", null, null, Guid.NewGuid()));
        using var document = PrintDocument(data, false); using var form = AdminUi.Dialog("Xem trước " + selected.SoHoaDon, 1000, 800);
        form.Controls.Add(new PrintPreviewControl { Dock = DockStyle.Fill, Document = document, Zoom = .9 }); form.ShowDialog(FindForm());
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
        using var json = JsonDocument.Parse(data.SnapshotJson);
        return $"4CHANG LINH NGU LAM\n{data.HoaDon.SoHoaDon}\nGiá trị: {data.HoaDon.SoTien:N0}đ\nPhát hành: {data.HoaDon.PhatHanhLuc:dd/MM/yyyy HH:mm}\n\n" + JsonSerializer.Serialize(json.RootElement, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }
    /// <summary>Phân trang theo vùng in thực; nhãn bản sao áp dụng cả xem/print nhiều trang.</summary>
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
}
