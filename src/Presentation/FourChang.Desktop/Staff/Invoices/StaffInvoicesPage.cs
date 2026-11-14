using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Invoices;
using FourChang.Desktop.Shared.Session;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Staff.Invoices;

/// <summary>Tra cứu hóa đơn có bộ lọc và phân trang server, không giới hạn vào bảng thô 100 bản ghi.</summary>
public sealed class StaffInvoicesPage : AdminWorkspacePage<AdminInvoiceView>
{
    private readonly DateTimePicker from = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 155, Value = DateTime.Today.AddDays(-30) };
    private readonly DateTimePicker to = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 155, Value = DateTime.Today };
    private readonly Label pagination = Ui.Label("", 9);
    private int page = 1;
    private int total;

    /// <summary>Giữ tab Hóa đơn/Hoàn tiền, chuẩn hóa hàng lọc và hiển thị mã đơn ngắn, tiền đồng, khách hàng.</summary>
    public StaffInvoicesPage() : base("Hóa đơn", "Tra cứu hóa đơn theo số, mã đơn, tên khách và ngày phát hành.", true, "POS / HÓA ĐƠN")
    {
        Name = "StaffInvoicesPage"; Grid.Name = "StaffDocumentsGrid"; SearchBox.Name = "StaffDocumentSearch";
        SearchBox.PlaceholderText = "Số hóa đơn, mã đơn hoặc tên khách…"; SearchBox.Width = 340;
        from.Name = "StaffInvoiceFrom"; to.Name = "StaffInvoiceTo";
        Filters.Controls.Clear(); Filters.Controls.AddRange([Ui.Field("Tìm hóa đơn", SearchBox), Ui.Field("Từ ngày", from), Ui.Field("Đến ngày", to)]);
        var find = AddAction(Filters, "Tìm kiếm", Find, true); find.Name = "StaffInvoiceSearch";
        AddAction(Filters, "Tải lại", Reload);
        SearchBox.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await Find(); } };
        Column("SoHoaDon", "Số hóa đơn", 165); Column("MaDon", "Mã đơn", 90); Column("NguoiMua", "Khách hàng", 145);
        Column("SoTien", "Tổng tiền", 100); Column("TrangThai", "Trạng thái", 105); Column("PhatHanhLuc", "Phát hành", 135);
        AddAction(Actions, "Xem hóa đơn", Details, true);
        AddAction(Actions, "Trang trước", async () => { if (page > 1) { page--; await Reload(); } });
        AddAction(Actions, "Trang sau", async () => { if (page * 50 < total) { page++; await Reload(); } }); Actions.Controls.Add(pagination);
        OperationsPresentation.FitRows(Filters); OperationsPresentation.FitRows(Actions); CommerceWorkspaceVisuals.Apply(Actions, Counter, pagination, Grid);
        Grid.Columns[0].MinimumWidth = 175; Grid.Columns[1].MinimumWidth = 95;
        Grid.Columns[3].DefaultCellStyle.Format = "#,##0'đ'"; Grid.Columns[3].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        Grid.CellFormatting += (_, e) =>
        {
            if (e.Value is DateTime date) e.Value = date.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            else if (e.ColumnIndex >= 0 && Grid.Columns[e.ColumnIndex].DataPropertyName == "TrangThai" && e.Value is string state) e.Value = OperationsPresentation.Label(state);
        };
        Grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0 && !Busy) await Details(); };
    }

    /// <summary>Áp dụng từ khóa/ngày mới từ trang đầu, không thay đổi dữ liệu hóa đơn.</summary>
    private async Task Find() { page = 1; await Reload(); }

    /// <summary>Đến ngày bao gồm hết ngày địa phương; cận trên gửi UTC loại trừ ngày kế tiếp.</summary>
    protected override async Task<IReadOnlyList<AdminInvoiceView>> Fetch()
    {
        if (from.Value.Date > to.Value.Date) throw new FourChang.Presentation.Shared.ApiClients.CafeApiException("Từ ngày không được sau đến ngày.", System.Net.HttpStatusCode.BadRequest);
        var data = await LiveSession.Api.GetStaffInvoicesAsync(SearchBox.Text.Trim(), from.Value.Date.ToUniversalTime(), to.Value.Date.AddDays(1).ToUniversalTime(), page);
        total = data.TongSo; pagination.Text = $"Trang {page}/{Math.Max(1, (total + 49) / 50)} · {total} hóa đơn";
        return data.DuLieu;
    }

    /// <summary>Server đã lọc trước Count/Skip/Take; không lọc lại trang đã tải bằng từ khóa đang nhập.</summary>
    protected override bool Match(AdminInvoiceView item, string query) => true;

    /// <summary>Mở hóa đơn theo ID gốc bằng quyền Staff, giữ form năm phần đã thống nhất.</summary>
    private async Task Details()
    {
        if (Selected() is not { } selected) return;
        var invoice = await LiveSession.Api.GetStaffInvoiceAsync(selected.MaHoaDon);
        var order = await LiveSession.Api.GetStaffOrderAsync(selected.MaDonHang);
        using var dialog = new InvoiceDetailsForm(invoice.HoaDon, invoice.SnapshotJson, order); dialog.ShowDialog(FindForm());
    }
}
