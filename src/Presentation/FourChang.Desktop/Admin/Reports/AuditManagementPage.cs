using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Admin.Reports;

/// <summary>Tra audit độc lập với báo cáo, phân trang và xem trước/sau; không có chức năng sửa/xóa.</summary>
public sealed class AuditManagementPage : AdminWorkspacePage<AdminTraceRow>
{
    private readonly DateTimePicker from = new() { Value = DateTime.Today.AddDays(-30), Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 130 };
    private readonly DateTimePicker to = new() { Value = DateTime.Today, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 130 };
    private readonly TextBox entity = Ui.Input("Loại đối tượng", "", 170);
    private readonly TextBox objectId = Ui.Input("Mã đối tượng", "", 150);
    private readonly TextBox person = Ui.Input("Người thực hiện", "", 180);
    private readonly TextBox machine = Ui.Input("Mã máy", "", 150);
    private readonly TextBox correlation = Ui.Input("Correlation ID", "", 270);
    private readonly NumericUpDown shift = new() { Maximum = 999999999, Width = 110 };
    private int page = 1;

    /// <summary>Bộ lọc ca/máy/người tách biệt; không coi tên trùng là cùng danh tính nhân viên.</summary>
    public AuditManagementPage() : base("Nhật ký hệ thống", "Chỉ đọc • Trước / sau • Mã ca và correlation ID để truy vết", true, "HỆ THỐNG / NHẬT KÝ")
    {
        Filters.Controls.Clear(); Filters.Controls.AddRange([Ui.Field("Hành động", SearchBox), Ui.Field("Từ ngày", from), Ui.Field("Đến ngày", to),
            Ui.Field("Loại đối tượng", entity), Ui.Field("Mã đối tượng", objectId), Ui.Field("Người thực hiện", person), Ui.Field("Mã ca (0: tất cả)", shift), Ui.Field("Mã máy", machine), Ui.Field("Correlation ID", correlation)]);
        AddAction(Filters, "Tìm kiếm", async () => { page = 1; await Reload(); }); AddAction(Filters, "Tải lại", async () => { page = 1; SearchBox.Clear(); await Reload(); });
        Column("Ma", "Mã log", 55); Column("Luc", "Thời điểm UTC", 130); Column("Nguoi", "Người / dịch vụ", 140); Column("MaNhanVien", "Mã nhân viên", 75); Column("MaCa", "Ca", 60); Column("MaMay", "Máy (lịch sử)", 90);
        Column("HanhDong", "Hành động", 140); Column("KetQua", "Kết quả", 75); Column("LyDo", "Lý do", 160);
        AddAction(Actions, "Xem trước / sau", Details, true); AddAction(Actions, "Xuất Excel", () => Export("EXCEL")); AddAction(Actions, "Xuất PDF", () => Export("PDF"));
        AddAction(Actions, "Trang trước", async () => { if (page > 1) { page--; await Reload(); } }); AddAction(Actions, "Trang sau", async () => { page++; await Reload(); });
        OperationsPresentation.FitRows(Filters); OperationsPresentation.FitRows(Actions); OperationsPresentation.Grid(Grid);
    }
    /// <summary>Chuẩn hóa bộ lọc, không cho mã correlation sai định dạng biến thành tìm tất cả.</summary>
    private AuditFilter Filter()
    {
        Guid? link = null;
        if (!string.IsNullOrWhiteSpace(correlation.Text))
        { if (!Guid.TryParse(correlation.Text, out var id)) throw new FourChang.Presentation.Shared.ApiClients.CafeApiException("Correlation ID không hợp lệ."); link = id; }
        return new(from.Value.Date.ToUniversalTime(), to.Value.Date.AddDays(1).ToUniversalTime(), SearchBox.Text.Trim(), entity.Text.Trim(), objectId.Text.Trim(), person.Text.Trim(),
            shift.Value == 0 ? null : (long)shift.Value, machine.Text.Trim(), link, page);
    }
    /// <summary>Máy chủ tự kiểm tra quyền audit và che JSON; UI không nhận bản chứa mật khẩu/token.</summary>
    protected override async Task<IReadOnlyList<AdminTraceRow>> Fetch() => (await LiveSession.Api.GetAuditAsync(Filter())).DuLieu;
    /// <summary>Bộ lọc chạy tại server để không làm sai phân trang.</summary>
    protected override bool Match(AdminTraceRow item, string query) => true;
    /// <summary>Trình bày hai snapshot song song và mã liên kết; chỉ đọc, không cho lưu ngược lại audit.</summary>
    private Task Details()
    {
        var selected = Selected(); if (selected is null) return Task.CompletedTask;
        using var form = AdminUi.Dialog("Truy vết log " + selected.Ma, 1100, 730);
        var before = new RichTextBox { ReadOnly = true, Height = 450, Dock = DockStyle.Fill, Font = CafeTheme.Body(), BackColor = CafeTheme.Surface, Text = selected.Truoc ?? "Không có snapshot trước" };
        var after = new RichTextBox { ReadOnly = true, Height = 450, Dock = DockStyle.Fill, Font = CafeTheme.Body(), BackColor = CafeTheme.Surface, Text = selected.Sau ?? "Không có snapshot sau" };
        form.Controls.Add(Ui.Panel(selected.HanhDong, Ui.Label($"{selected.Nguoi} · Nhân viên {selected.MaNhanVien} · Ca {selected.MaCa} · Máy {selected.MaMay} · UTC {selected.Luc:O}"),
            Ui.Label("Correlation: " + selected.CorrelationId + " · " + selected.LoaiDoiTuong + ": " + selected.MaDoiTuong), Ui.Label("Nhân viên theo hồ sơ/ca khai báo, không phải tài khoản xác thực cá nhân.", 9), Ui.Tabs(("Trước thay đổi", before), ("Sau thay đổi", after))));
        form.ShowDialog(FindForm()); return Task.CompletedTask;
    }
    /// <summary>Chụp toàn bộ bộ lọc và mốc trước khi tạo job; server kiểm tra quyền xuất audit riêng.</summary>
    private async Task Export(string format)
    {
        var snapshot = await LiveSession.Api.GetAuditExportSnapshotAsync(Filter());
        var job = await LiveSession.Api.StartReportExportAsync(new(snapshot.MaSnapshot, format, Guid.NewGuid()));
        using var form = new ReportExportDialog(job); form.ShowDialog(FindForm());
    }
}
