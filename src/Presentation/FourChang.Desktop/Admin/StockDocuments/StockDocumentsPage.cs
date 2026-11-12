using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Admin.StockDocuments;

/// <summary>UC-ADM-06: quản lý nháp, ghi sổ và đảo; không sửa tồn trực tiếp tại bảng.</summary>
public sealed class StockDocumentsPage : AdminWorkspacePage<StockDocumentView>
{
    private readonly Dictionary<string, StockDocumentAction> pending = [];

    /// <summary>Danh sách nguồn–đảo rõ ràng, nhóm hành động cùng kích thước và tìm theo số/loại/trạng thái.</summary>
    public StockDocumentsPage() : base("Chứng từ kho", "Nhập • Xuất • Kiểm kê • Điều chỉnh — chỉ phiếu đã ghi sổ ảnh hưởng tồn", true, "KHO / CHỨNG TỪ")
    {
        Column("SoPhieuKho", "Số phiếu", 160); Column("LoaiPhieu", "Loại", 80); Column("TrangThai", "Trạng thái", 80);
        Column("Nguon", "Nguồn nhập", 120); Column("LyDo", "Lý do", 160); Column("MaPhieuBiDao", "Phiếu gốc", 70); Column("GhiSoLuc", "Ghi sổ lúc", 120);
        AddAction(Actions, "Lập phiếu", () => Edit(null), true);
        AddAction(Actions, "Sửa nháp", () => Edit(Selected()));
        AddAction(Actions, "Ghi sổ", () => Act("post")); AddAction(Actions, "Đảo phiếu", () => Act("reverse"));
        AddAction(Actions, "Xóa nháp", Delete);
        OperationsPresentation.FitRows(Filters); OperationsPresentation.FitRows(Actions); OperationsPresentation.Grid(Grid);
    }
    /// <summary>Đọc phiếu từ dịch vụ có quyền Admin.</summary>
    protected override Task<IReadOnlyList<StockDocumentView>> Fetch() => LiveSession.Api.GetStockDocumentsAsync();
    /// <summary>Tìm theo số, mã, loại, trạng thái hoặc lý do trên danh sách đã tải.</summary>
    protected override bool Match(StockDocumentView item, string query) => query.Length == 0 || $"{item.MaPhieuKho} {item.SoPhieuKho} {item.LoaiPhieu} {item.TrangThai} {item.LyDo}".Contains(query, StringComparison.CurrentCultureIgnoreCase);
    /// <summary>Mở editor với snapshot tồn hiện tại; không mở sửa một phiếu đã ghi sổ.</summary>
    private async Task Edit(StockDocumentView? item)
    {
        if (item is not null && item.TrangThai != "DRAFT") { Message.Text = "Phiếu đã ghi sổ: dùng Đảo phiếu."; return; }
        using var form = new StockDocumentEditorForm(await LiveSession.Api.GetInventoryAsync(), item);
        if (form.ShowDialog(FindForm()) != DialogResult.OK) return;
        // Hiển thị và chọn đúng phiếu vừa lưu để người dùng có thể kiểm tra rồi ghi sổ.
        SearchBox.Clear();
        await Reload();
        foreach (DataGridViewRow row in Grid.Rows)
            if (row.DataBoundItem is StockDocumentView saved && saved.MaPhieuKho == form.SavedDocumentId)
                Grid.CurrentCell = row.Cells[0];
        Message.Text = "Đã lưu nháp, chưa thay đổi tồn kho. Kiểm tra phiếu đang chọn rồi bấm Ghi sổ để nhập/xuất hàng.";
    }
    /// <summary>Xác nhận rõ tác động trước lệnh; timeout giữ mã và tra kết quả cũ trước khi gửi lại.</summary>
    private async Task Act(string action)
    {
        var item = Selected(); if (item is null) return;
        var key = action + ":" + item.MaPhieuKho;
        if (!pending.TryGetValue(key, out var input))
        {
            var reason = StockDocumentPrompts.Reason(this, action == "post" ? "Ghi sổ phiếu" : "Đảo phiếu", item.LyDo ?? "");
            if (reason is null) return;
            input = new(reason, item.PhienBanDuLieu, Guid.NewGuid()); pending[key] = input;
        }
        try
        {
            await AdminUi.Recover(() => LiveSession.Api.ActStockDocumentAsync(item.MaPhieuKho, action, input), () => LiveSession.Api.StockDocumentReceiptAsync(input.MaThaoTac));
            pending.Remove(key);
            await Reload();
            Message.Text = action == "post"
                ? "Đã ghi sổ phiếu. Tồn kho đã được cập nhật; có thể kiểm tra tại trang Tồn kho."
                : "Đã đảo phiếu và cập nhật tồn kho. Lịch sử phiếu gốc được giữ nguyên.";
        }
        catch (FourChang.Presentation.Shared.ApiClients.CafeApiException ex) { if (ex.StatusCode is not null && (int)ex.StatusCode < 500) pending.Remove(key); throw; }
    }
    /// <summary>Xóa nháp có xác nhận; không âm thầm xóa một chứng từ đã phát sinh.</summary>
    private async Task Delete()
    {
        var item = Selected(); if (item is null || item.TrangThai != "DRAFT") return;
        if (MessageBox.Show(this, "Xóa phiếu nháp " + item.SoPhieuKho + "?", "Xác nhận", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        var operation = Guid.NewGuid();
        await AdminUi.Recover(() => LiveSession.Api.DeleteStockDocumentAsync(item.MaPhieuKho, new(item.PhienBanDuLieu, operation)), () => LiveSession.Api.StockDocumentReceiptAsync(operation));
        await Reload();
    }
}

/// <summary>Hộp nhập lý do dùng chung, không ghi nghiệp vụ trước khi xác nhận.</summary>
internal static class StockDocumentPrompts
{
    /// <summary>Trả null nếu hủy; bắt buộc lý do và không dùng MessageBox như bằng chứng giao dịch đã thành công.</summary>
    internal static string? Reason(Control owner, string title, string initial)
    {
        using var form = AdminUi.Dialog(title, 620, 320); form.MinimumSize = new(560, 300);
        var input = FourChang.Desktop.Shared.Controls.Ui.Input("Lý do", initial, 480);
        var confirm = FourChang.Desktop.Shared.Controls.Ui.Button("Xác nhận", () => { if (!string.IsNullOrWhiteSpace(input.Text)) form.DialogResult = DialogResult.OK; }, true);
        AdminUi.Compose(form, FourChang.Desktop.Shared.Controls.Ui.Panel(title, FourChang.Desktop.Shared.Controls.Ui.Field("Lý do bắt buộc", input)), confirm,
            FourChang.Desktop.Shared.Controls.Ui.Button("Hủy", () => form.Close()));
        return form.ShowDialog(owner.FindForm()) == DialogResult.OK ? input.Text.Trim() : null;
    }
}
