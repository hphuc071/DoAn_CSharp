// UC: UC-ADM-07
// Chức năng: Tra cứu nhật ký thao tác, không sửa dữ liệu lịch sử.
// Cần nhớ: Đọc hàm từ bước kiểm tra dữ liệu đến bước trả kết quả; các quyết định nghiệp vụ do Backend kiểm tra.

using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Orders;
using FourChang.Desktop.Shared.Session;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Admin.Reports;

/// <summary>UC-ADM-07: nhật ký chỉ đọc với người/thời gian/thao tác/kết quả, không có sửa hoặc xóa.</summary>
public sealed class AuditManagementPage : AdminWorkspacePage<AdminTraceRow>
{
    private readonly DateTimePicker from = new() { Value = DateTime.Today.AddDays(-30), Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 150 };
    private readonly DateTimePicker to = new() { Value = DateTime.Today, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 150 };
    private readonly ComboBox action = Ui.Select("Tất cả", "CANCELLATION_APPROVE", "CANCELLATION_REJECT", "COUNTER_REFUND", "ORDER_", "STOCK_", "MENU_", "INGREDIENT_", "EMPLOYEE_", "SHIFT");
    private readonly Label pagination = Ui.Label("", 9);
    private int page = 1;
    private string? boundSnapshot;

    public AuditManagementPage() : base("Nhật ký hệ thống", "Tra cứu lịch sử thao tác quan trọng • Chỉ đọc", true, "BÁO CÁO / NHẬT KÝ", fitTableToRows: false)
    {
        Name = "AuditManagementPage"; SearchBox.PlaceholderText = "Tên người thực hiện";
        action.FormattingEnabled = true; action.Format += (_, e) => e.Value = ActionName(e.ListItem?.ToString() ?? "");
        CommerceWorkspaceVisuals.AdminFilters(Filters, SearchBox, from, to, ("Thao tác", action));
        AddAction(Filters, "Tìm kiếm", async () => { page = 1; await Reload(); }); AddAction(Filters, "Tải lại", Reload);
        Column("Luc", "Thời gian", 150); Column("Nguoi", "Người thực hiện", 150); Column("HanhDong", "Thao tác", 180); Column("LoaiDoiTuong", "Đối tượng", 120); Column("KetQua", "Kết quả", 100);
        AddAction(Actions, "Xem chi tiết", Details, true); AddAction(Actions, "Trang trước", async () => { if (page > 1) { page--; await Reload(); } }); AddAction(Actions, "Trang sau", async () => { page++; await Reload(); }); Actions.Controls.Add(pagination);
        OperationsPresentation.Grid(Grid); CommerceWorkspaceVisuals.Apply(Actions, Counter, pagination, Grid); CommerceWorkspaceVisuals.AdminStyle(Filters, Actions, Grid);
        Grid.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex < 0) return;
            e.Value = Grid.Columns[e.ColumnIndex].DataPropertyName switch { "Luc" when e.Value is DateTime at => at.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), "HanhDong" => ActionName(e.Value?.ToString() ?? ""), "LoaiDoiTuong" => EntityName(e.Value?.ToString()), "KetQua" => ResultName(e.Value?.ToString()), _ => e.Value };
            e.FormattingApplied = true;
        };
    }
    protected override async Task<IReadOnlyList<AdminTraceRow>> Fetch()
    {
        var result = await LiveSession.Api.GetAuditAsync(new(from.Value.Date.ToUniversalTime(), to.Value.Date.AddDays(1).ToUniversalTime(),
            HanhDong: action.SelectedIndex == 0 ? null : action.SelectedItem?.ToString(), Nguoi: SearchBox.Text.Trim(), Trang: page));
        pagination.Text = $"Trang {page} · {result.TongSo} thao tác"; return result.DuLieu;
    }
    protected override bool Match(AdminTraceRow item, string query) => true;
    /// <summary>Nhập tên không bind lại cùng dữ liệu, tránh chớp bảng khi người dùng đang gõ.</summary>
    protected override void BindRows(IReadOnlyList<AdminTraceRow> rows)
    {
        var value = System.Text.Json.JsonSerializer.Serialize(rows);
        if (value == boundSnapshot) return;
        base.BindRows(rows); boundSnapshot = value;
    }
    /// <summary>Chỉ hiện thông tin dễ đọc; snapshot/audit bất biến vẫn lưu đầy đủ ở máy chủ.</summary>
    private Task Details()
    {
        if (Selected() is not { } row) return Task.CompletedTask;
        using var form = AdminUi.Dialog("Chi tiết thao tác", 900, 560); form.Name = "AuditDetails";
        var info = OrderDetailsForm.Section("Thông tin thao tác", OrderDetailsForm.Value("Thời gian", row.Luc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), 220), OrderDetailsForm.Value("Người thực hiện", row.Nguoi, 280),
            OrderDetailsForm.Value("Thao tác", ActionName(row.HanhDong), 280), OrderDetailsForm.Value("Đối tượng", EntityName(row.LoaiDoiTuong) + " · " + row.MaDoiTuong, 280), OrderDetailsForm.Value("Kết quả", ResultName(row.KetQua)), OrderDetailsForm.Value("Nội dung / lý do", row.LyDo, 760));
        info.Dock = DockStyle.Top; var close = Ui.Button("Đóng", () => form.Close()); AdminUi.Compose(form, info, close); form.CancelButton = close; form.ShowDialog(FindForm()); return Task.CompletedTask;
    }
    private static string ActionName(string code) => code switch { "Tất cả" => code, "ORDER_CANCEL" => "Xử lý yêu cầu hủy", "OPEN_SHIFT" => "Mở ca", "CLOSE_SHIFT" => "Kết thúc ca", "EMPLOYEE_SAVE" => "Cập nhật nhân viên", "MENU_SAVE" => "Cập nhật thực đơn", "SEED_CUSTOMER_SAMPLE" => "Khởi tạo dữ liệu mẫu", "ORDER_ACCEPT" => "Tiếp nhận đơn", "ORDER_START" => "Bắt đầu pha", "ORDER_FINISH" => "Pha xong", "ORDER_COMPLETE" => "Hoàn tất đơn", "ORDER_DELIVER" => "Bắt đầu giao hàng", "CANCELLATION_FORWARD" => "Chuyển yêu cầu hủy", "CANCELLATION_APPROVE" => "Duyệt hủy đơn", "CANCELLATION_REJECT" => "Từ chối hủy đơn", "COUNTER_REFUND" => "Trả tiền tại quầy", "ORDER_" => "Xử lý đơn hàng", "STOCK_" => "Nhập / xuất kho", "MENU_" => "Thực đơn", "INGREDIENT_" => "Nguyên liệu", "EMPLOYEE_" => "Nhân viên", "SHIFT" => "Ca làm việc", _ => code.Replace('_', ' ') };
    private static string EntityName(string? code) => code switch { "TaiKhoanKhachHang" => "Khách hàng", "Mon" => "Món", "MonThem" => "Topping", "CongThuc" => "Công thức", "DonHang" => "Đơn hàng", "HoSoHoanTien" => "Hoàn tiền", "PhieuKho" => "Phiếu kho", "NhanVien" => "Nhân viên", "CaLamViec" => "Ca làm việc", "HoaDon" => "Hóa đơn", "NguyenLieu" => "Nguyên liệu", "NhatKyHeThong" => "Nhật ký", _ => code ?? "—" };
    private static string ResultName(string? code) => code switch { "SUCCESS" or "SUCCEEDED" or "OK" => "Thành công", "FAILED" or "ERROR" => "Thất bại", _ => code ?? "—" };
}