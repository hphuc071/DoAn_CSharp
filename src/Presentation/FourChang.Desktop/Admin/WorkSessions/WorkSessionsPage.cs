using FourChang.Desktop.Admin.Employees;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Database;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Preview;

namespace FourChang.Desktop.Admin.WorkSessions;

/// <summary>UC-ADM-13: danh sách/lọc/đối chiếu và các xác nhận; giữ bộ lọc khi trở lại từ chi tiết.</summary>
public sealed class WorkSessionsPage : UserControl
{
    private readonly TextBox employee = Ui.Input("Mã nội bộ nhân viên", "", 165);
    private readonly TextBox machine = Ui.Input("Mã máy", "", 150);
    private readonly ComboBox name = Ui.Select("Tất cả ca", "Sáng", "Chiều", "Tối");
    private readonly ComboBox status = Ui.Select("Tất cả trạng thái", "Đang mở", "Đang khóa", "Đã chốt", "Đã bàn giao");
    private readonly DateTimePicker from = new() { Width = 145, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", ShowCheckBox = false };
    private readonly DateTimePicker to = new() { Width = 145, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", ShowCheckBox = false };
    private readonly DataGridView grid = Ui.Grid(new PreviewTable([], []), 370);
    private readonly Label summary = Ui.Label("Đang tải…", 10);
    private readonly Label error = Ui.Label("", 9, false, CafeTheme.Danger);
    private readonly List<Control> actions = [];
    private readonly AdminListLayout workspace;
    private int page = 1;
    private int total;
    private bool busy;

    /// <summary>Nhận MaNhanVien từ ADM-12, không dùng tên làm khóa; trang mở độc lập vẫn lọc được tất cả.</summary>
    public WorkSessionsPage(long? employeeId = null)
    {
        AutoSize = true; Dock = DockStyle.Top;
        employee.Text = employeeId?.ToString() ?? ""; employee.Name = "WorkSessionEmployeeFilter";
        name.Width = 125; status.Width = 170; grid.Name = "WorkSessionsGrid";
        from.Value = DateTime.Today.AddDays(-30); to.Value = DateTime.Today;
        var filterTop = AdminListLayout.Fields(AdminListLayout.Field("Mã nhân viên", employee), AdminListLayout.Field("Tên ca", name),
            AdminListLayout.Field("Trạng thái", status), AdminListLayout.Field("Mã máy", machine));
        var filterBottom = AdminListLayout.Fields(AdminListLayout.Field("Từ ngày", from), AdminListLayout.Field("Đến ngày", to),
            Ui.Stack(Ui.Label("Thao tác", 9, true), Ui.Row(Button("Tìm kiếm", async () => { page = 1; await LoadPage(); }, true), Button("Tải lại", async () => { page = 1; await LoadPage(); }))));
        var tableTitle = Ui.Label("DANH SÁCH CA", 9, true, AdminColors.Accent);
        tableTitle.AutoSize = false; tableTitle.Dock = DockStyle.Fill; tableTitle.TextAlign = ContentAlignment.MiddleLeft;
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Top, Height = 46, ColumnCount = 2, Margin = Padding.Empty };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.Controls.Add(tableTitle, 0, 0); toolbar.Controls.Add(Button("Xem đối chiếu", Details), 1, 0);
        workspace = new AdminListLayout("Nhật ký & đối chiếu ca", "Tra cứu nhân viên, kiểm tra quỹ tiền mặt và bàn giao có truy vết.",
            AdminListLayout.Column(filterTop, filterBottom), toolbar, error, grid,
            Ui.Row(Button("Trang trước", async () => { if (page > 1) { page--; await LoadPage(); } }), summary,
                Button("Trang sau", async () => { if (page * 20 < total) { page++; await LoadPage(); } })));
        Controls.Add(workspace); AdminListLayout.StyleGrid(grid, 8);
        grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 1 && e.Value is string code && code.Length > 13)
                e.Value = code[..11] + "…";
        };
        Load += async (_, _) => { SetBusy(true); try { await LoadPage(); } finally { if (!IsDisposed) SetBusy(false); } };

    }

    /// <summary>Khóa thao tác khi chạy API và xử lý lỗi; không gửi các lệnh xác nhận song song.</summary>
    private Control Button(string text, Func<Task> action, bool primary = false)
    {
        var result = AdminListLayout.Action(text, primary); actions.Add(result);
        result.Click += async (_, _) =>
        {
            if (busy) return;
            SetBusy(true);
            try { await action(); }
            catch (CafeApiException ex) { if (!IsDisposed) WorkforceUi.Error(this, ex); }
            finally { if (!IsDisposed) SetBusy(false); }
        };
        return result;
    }

    /// <summary>Đồng bộ trạng thái toolbar và bộ lọc để không có phản hồi cũ đè truy vấn mới.</summary>
    private void SetBusy(bool value)
    {
        busy = value; workspace.Busy = value;
        foreach (var action in actions) action.Enabled = !value && (action.Text != "Trang trước" || page > 1) && (action.Text != "Trang sau" || page * 20 < total);
        employee.Enabled = machine.Enabled = name.Enabled = status.Enabled = from.Enabled = to.Enabled = !value;

    }

    /// <summary>Kiểm tra đầu vào và gửi ngày kết thúc bao gồm toàn ngày theo múi giờ máy khách.</summary>
    private async Task LoadPage()
    {
        if (!string.IsNullOrWhiteSpace(employee.Text) && (!long.TryParse(employee.Text, out var id) || id <= 0))
        { error.Text = "Mã nội bộ nhân viên phải là số nguyên dương."; return; }
        try
        {
            var data = await LiveSession.Api.GetWorkSessionsAsync(string.IsNullOrWhiteSpace(employee.Text) ? null : long.Parse(employee.Text),
                from.Value.Date, to.Value.Date.AddDays(1).AddTicks(-1),
                name.SelectedIndex == 0 ? "" : name.Text, status.SelectedIndex switch { 1 => "OPEN", 2 => "LOCKED", 3 => "CLOSED", 4 => "HANDED_OVER", _ => "" }, machine.Text.Trim(), page);
            if (IsDisposed) return;
            total = data.Total;
            if (data.Items.Count == 0 && total > 0 && page > 1) { page = Math.Max(1, (total + 19) / 20); await LoadPage(); return; }
            DatabaseViews.SetGrid(grid, new(["Mã nội bộ ca", "Mã ca", "Mã nhân viên", "Nhân viên", "Tên ca", "Máy", "Bắt đầu", "Kết thúc", "Trạng thái"],
                data.Items.Select(x => new[] { x.MaCaLamViec.ToString(), x.MaCaHienThi, x.MaNhanVien.ToString(), x.HoTen, x.TenCa, x.MaMay,
                    Local(x.BatDauLuc), x.KetThucLuc is { } ended ? Local(ended) : "", WorkforceUi.Status(x.TrangThai) }).ToArray()));
            grid.Columns[0].Visible = false; grid.Columns[2].Visible = false;
            var widths = new[] { 0f, 14f, 0f, 25f, 7f, 12f, 16f, 14f, 12f };
            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (!column.Visible) continue;
                column.MinimumWidth = column.Index == 3 ? 150 : 60; column.FillWeight = widths[column.Index];
            }
            grid.Columns[1].HeaderText = "Mã ca"; grid.Columns[3].HeaderText = "Nhân viên";
            foreach (DataGridViewRow row in grid.Rows)
            {
                row.Cells[3].Value = $"{row.Cells[3].Value}\n#{Convert.ToInt64(row.Cells[2].Value):0000}";
                row.Cells[3].Style.WrapMode = DataGridViewTriState.True;
                row.Cells[1].ToolTipText = row.Cells[1].Value?.ToString();
            }
            // Sau khi ẩn cột khóa nội bộ, chọn ô hiển thị để CurrentRow vẫn hợp lệ cho nút Xem đối chiếu.
            if (grid.Rows.Count > 0) grid.CurrentCell = grid.Rows[0].Cells[3];
            summary.Text = $"Trang {page}/{Math.Max(1, (total + 19) / 20)} · {total} ca · Cập nhật {DateTime.Now:HH:mm:ss}"; error.Text = "";
        }
        catch (CafeApiException ex) { if (!IsDisposed) { error.Text = "Chưa tải được dữ liệu mới. " + ex.Message; WorkforceUi.Error(this, ex); } }
    }

    /// <summary>SQL trả DateTime không kèm Kind; xác định UTC theo hợp đồng rồi mới hiển thị giờ địa phương.</summary>
    private static string Local(DateTime date) => DateTime.SpecifyKind(date, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm");

    /// <summary>Chi tiết chỉ đọc; xác nhận dùng phiên bản mới tải và số đếm đã được POS gửi.</summary>
    private async Task Details()
    {
        if (grid.CurrentRow is null) { MessageBox.Show(FindForm(), "Chọn ca cần đối chiếu."); return; }
        var id = Convert.ToInt64(grid.CurrentRow.Cells[0].Value);
        var data = await LiveSession.Api.GetWorkSessionAsync(id);
        using var form = WorkforceUi.Dialog($"Đối chiếu ca · {data.Ca.MaCaHienThi}", 1060, 760);
        var ca = data.Ca;
        var cash = Ui.Panel("Đối chiếu tiền mặt",
            Ui.Label($"Đầu ca: {ca.TienMatDauCa:N0}đ    ·    Thu CASH đã xác minh: {data.ThuTienMat:N0}đ    ·    Hoàn CASH thành công: {data.HoanTienMat:N0}đ"),
            Ui.Label($"Dự kiến từ nguồn hiện tại: {data.TienMatDuKienHienTai:N0}đ", 15, true),
            Ui.Label($"Thực đếm: {(ca.TienMatThucDem is { } counted ? counted.ToString("N0") + "đ" : "Chưa được nhân viên gửi")}"),
            Ui.Label($"Chênh lệch: {(data.ChenhLech is { } diff ? diff.ToString("N0") + "đ" : "Chưa xác định")}"),
            Ui.Label($"Lý do: {ca.LyDoChenhLech ?? "Chưa có"}"),
            Ui.Label($"Dự kiến đã chốt: {(ca.TienMatDuKienCuoiCa is { } frozen ? frozen.ToString("N0") + "đ" : "Chưa chốt")} · Quản lý xác nhận: {ca.MaQuanLyDuyet?.ToString() ?? "Chưa có"}"),
            Ui.Label($"Ca trước: {ca.MaCaBanGiaoTruoc?.ToString() ?? "Không có"} · Dữ liệu tại {Local(data.DuLieuTaiLuc)}", 9, false, CafeTheme.Muted));
        var transactions = Ui.Grid(new PreviewTable(["Loại", "Mã nguồn", "Đơn", "Tham chiếu", "Phương thức", "Tiền đã xác minh / chi", "Trạng thái"],
            data.GiaoDich.Select(x => new[] { x.Loai, x.Ma.ToString(), x.MaDonHang?.ToString() ?? "", x.ThamChieu, x.PhuongThuc, x.SoTien.ToString("N0"), x.TrangThai }).ToArray()), 210);
        var orders = Ui.Grid(new PreviewTable(["Mã đơn", "Đơn", "Trạng thái", "Ca nguồn"],
            data.DonDangXuLy.Select(x => new[] { x.MaDonHang.ToString(), x.MaDonHienThi, x.TrangThai, x.MaCaNguon?.ToString() ?? "" }).ToArray()), 170);
        var audit = Ui.Grid(new PreviewTable(["Thời điểm", "Người thực hiện", "Hành động", "Lý do"],
            data.NhatKy.Select(x => new[] { Local(x.ThoiDiem), x.NguoiThucHien, x.HanhDong, x.LyDo ?? "" }).ToArray()), 200);
        var message = Ui.Label("", 9, false, CafeTheme.Danger);
        var close = Ui.Button("Xác nhận chốt ca", () => { }, true);
        var handover = Ui.Button("Xác nhận bàn giao", () => { });
        close.Enabled = handover.Enabled = ca.KetThucLuc is null;
        var resume = Ui.Button("Cho tiếp tục ca", () => { });
        resume.Enabled = ca.TrangThai == "LOCKED";
        var reload = Ui.Button("Tải lại đối chiếu", () => { form.DialogResult = DialogResult.Retry; form.Close(); });
        var tabs = Ui.Tabs(("Thu / hoàn · CASH / CK", transactions), ("Đơn còn dở", orders), ("Nhật ký ca", audit),
            ("Bàn giao", Ui.Panel("Biên bản", Ui.Label(data.BanGiao is { } h ? $"Nhận: #{h.MaNhanVienNhan} · Máy: {h.MaMayNhan}\nLý do: {h.LyDo}\nĐơn: {string.Join(", ", h.MaDonBanGiao)}\nKhoản chưa rõ: {string.Join(", ", h.KhoanChuaRo)}" : "Chưa có bàn giao từ ca này."))));
        form.Controls.Add(Ui.Panel($"{ca.HoTen} · {ca.TenCa} · {ca.MaMay} · {WorkforceUi.Status(ca.TrangThai)}", cash,
            Ui.Label("Khoản chưa rõ: " + (data.KhoanChuaRo.Count == 0 ? "Không có" : string.Join(", ", data.KhoanChuaRo)), 10),
            Ui.Row(close, handover, resume, reload), message, tabs));
        var operation = Guid.NewGuid();
        close.Click += async (_, _) =>
        {
            if (MessageBox.Show(form, "Xác nhận chốt từ số thực đếm đã gửi? Ca sẽ không nhận giao dịch mới.", "Chốt ca", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            close.Enabled = handover.Enabled = resume.Enabled = reload.Enabled = false;
            try
            {
                var input = new CloseWorkSessionInput(ca.PhienBanDuLieu, operation, data.TienMatDuKienHienTai);
                await WorkforceUi.Recover(async () => { await LiveSession.Api.ConfirmWorkSessionAsync(id, input); return true; },
                    async () => { await LiveSession.Api.WorkSessionReceiptAsync(operation); return true; });
                form.DialogResult = DialogResult.OK; form.Close();
            }
            catch (CafeApiException ex) { message.Text = ex.Message; if (ex.StatusCode is not null) operation = Guid.NewGuid(); }
            finally { if (!close.IsDisposed) { close.Enabled = handover.Enabled = reload.Enabled = true; resume.Enabled = ca.TrangThai == "LOCKED"; } }
        };
        handover.Click += async (_, _) => { if (await Handover(form, data)) { form.DialogResult = DialogResult.OK; form.Close(); } };

        resume.Click += (_, _) =>
        {
            using var prompt = WorkforceUi.Dialog("Cho tiếp tục ca hiện hữu", height: 350);
            var reason = Ui.Input("Lý do tiếp tục / yêu cầu kiểm đếm lại", "", 440); reason.MaxLength = 500;
            var message = Ui.Label("", 9, false, CafeTheme.Danger);
            var confirm = Ui.Button("Xác nhận tiếp tục", () => { }, true);
            prompt.Controls.Add(Ui.Panel("Khôi phục đúng ca đang khóa", Ui.Field("Lý do", reason), message, confirm));
            ResumeWorkSessionInput? pending = null;
            confirm.Click += async (_, _) =>
            {
                pending ??= new(reason.Text, ca.PhienBanDuLieu, Guid.NewGuid());
                confirm.Enabled = reason.Enabled = false;
                try
                {
                    await WorkforceUi.Recover(async () => { await LiveSession.Api.ResumeWorkSessionAsync(id, pending); return true; },
                        async () => { await LiveSession.Api.WorkSessionReceiptAsync(pending.MaThaoTac); return true; });
                    prompt.DialogResult = DialogResult.OK; prompt.Close();
                }
                catch (CafeApiException ex) { message.Text = ex.Message; if (ex.StatusCode is not null) { pending = null; reason.Enabled = true; } }
                finally { if (!confirm.IsDisposed) confirm.Enabled = true; }
            };
            if (prompt.ShowDialog(form) == DialogResult.OK) { form.DialogResult = DialogResult.Retry; form.Close(); }
        };
        var result = form.ShowDialog(FindForm());
        if (result == DialogResult.Retry) await Details();
        await LoadPage();
    }

    /// <summary>Chọn nhân viên/máy từ nguồn được phép và xác nhận chính xác danh sách cần bàn giao.</summary>
    private async Task<bool> Handover(Form owner, WorkSessionDetails data)
    {
        var employees = (await LiveSession.Api.GetEmployeesAsync(true)).Where(x => x.TrangThai == "ACTIVE").ToList();
        var devices = (await LiveSession.Api.GetPosDevicesAsync()).Where(x => x.TrangThai == "ACTIVE").ToList();
        using var form = WorkforceUi.Dialog("Xác nhận bàn giao", 650, 550);
        var receiver = new ComboBox { Width = 500, DropDownStyle = ComboBoxStyle.DropDownList, DataSource = employees.Select(x => new { Id = x.MaNhanVien, Text = $"#{x.MaNhanVien} · {x.HoTen} · {x.SoDienThoai}" }).ToList(), DisplayMember = "Text", ValueMember = "Id" };
        var machine = new ComboBox { Width = 500, DropDownStyle = ComboBoxStyle.DropDownList, DataSource = devices, DisplayMember = "MaMay", ValueMember = "MaMay" };
        var reason = Ui.Input("Lý do bàn giao", "", 500); reason.MaxLength = 500;
        var message = Ui.Label("", 9, false, CafeTheme.Danger);
        var save = Ui.Button("Xác nhận bàn giao", () => { }, true);
        form.Controls.Add(Ui.Panel("Người và máy nhận", Ui.Field("Nhân viên đang làm", receiver), Ui.Field("Thiết bị được phép", machine), Ui.Field("Lý do", reason),
            Ui.Label("Đơn cần bàn giao: " + string.Join(", ", data.DonDangXuLy.Select(x => x.MaDonHienThi))),
            Ui.Label("Khoản chưa rõ: " + string.Join(", ", data.KhoanChuaRo)), message, save));
        HandoverWorkSessionInput? pending = null;
        save.Click += async (_, _) =>
        {
            if (receiver.SelectedValue is null || machine.SelectedValue is null) { message.Text = "Chọn nhân viên và thiết bị nhận hợp lệ."; return; }
            pending ??= new(Convert.ToInt64(receiver.SelectedValue), machine.SelectedValue.ToString()!, reason.Text,
                data.DonDangXuLy.Select(x => x.MaDonHang).ToArray(), data.KhoanChuaRo.ToArray(), data.Ca.PhienBanDuLieu, Guid.NewGuid(), data.TienMatDuKienHienTai);
            save.Enabled = receiver.Enabled = machine.Enabled = reason.Enabled = false;
            try
            {
                await WorkforceUi.Recover(async () => { await LiveSession.Api.HandoverWorkSessionAsync(data.Ca.MaCaLamViec, pending); return true; },
                    async () => { await LiveSession.Api.WorkSessionReceiptAsync(pending.MaThaoTac); return true; });
                form.DialogResult = DialogResult.OK; form.Close();
            }
            catch (CafeApiException ex) { message.Text = ex.Message; if (ex.StatusCode is not null) { pending = null; receiver.Enabled = machine.Enabled = reason.Enabled = true; } }
            finally { if (!save.IsDisposed) save.Enabled = true; }
        };
        return form.ShowDialog(owner) == DialogResult.OK;
    }
}
