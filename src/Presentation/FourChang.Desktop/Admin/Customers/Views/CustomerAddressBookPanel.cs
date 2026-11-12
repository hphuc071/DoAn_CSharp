using FourChang.Desktop.Admin.Employees;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Customer;

namespace FourChang.Desktop.Admin.Customers.Views;

/// <summary>Sổ địa chỉ thật trong hồ sơ khách hàng; mỗi thao tác là transaction riêng có receipt/version.</summary>
internal sealed class CustomerAddressBookPanel : UserControl
{
    private readonly long owner;
    private readonly bool editable;
    private readonly Panel content = new() { Dock = DockStyle.Top, AutoSize = true };
    private readonly Label error = Ui.Label("", 9, false, CafeTheme.Danger);
    private readonly Button retry = Ui.Button("Thử lại yêu cầu", () => { });
    private readonly Label count = Ui.Label("Đang tải địa chỉ…", 9, false, AdminColors.Muted);
    private CustomerAddressBook? book;
    private Func<Task<CustomerAddressBook>>? pending;
    private Guid pendingId;
    private bool busy;
    internal bool HasUnresolvedMutation => pending is not null;
    internal bool HasPending => busy || pending is not null;
    internal event Action<CustomerAccount>? Changed;

    /// <summary>Chỉ đọc ở hồ sơ xem; chế độ sửa mới có thêm/sửa/mặc định/ngừng. Quyền cuối cùng do API kiểm tra.</summary>
    internal CustomerAddressBookPanel(CustomerAccount customer, bool editable)
    {
        owner = customer.MaKhachHang; this.editable = editable; Dock = DockStyle.Top; AutoSize = true;
        var add = Ui.Button("Thêm địa chỉ giao hàng", async () => { if (!HasPending) await Edit(null); }, true); add.Name = "CustomerAddressAdd";
        var reload = Ui.Button("Tải lại địa chỉ", async () => { if (!HasPending) await LoadBook(); });
        retry.Visible = false; retry.Click += async (_, _) => await Run(null, Guid.Empty);
        var buttons = Ui.Row(add, reload, retry); add.Visible = editable; count.Name = "AddressBookCount";
        Controls.Add(AdminListLayout.Column(buttons, count, error, content));
        HandleCreated += async (_, _) => await LoadBook();
    }

    /// <summary>Lấy version và sổ địa chỉ mới nhất; không tự thông báo thành công khi mất kết nối.</summary>
    private async Task LoadBook()
    {
        busy = true;
        try { var data = await LiveSession.Api.GetAdminAddressesAsync(owner); if (!IsDisposed) Apply(data, false); }
        catch (CafeApiException ex) { if (!IsDisposed) error.Text = ex.Message; }
        finally { busy = false; }
    }

    /// <summary>Vẽ danh sách thẻ với người nhận, số liên hệ, địa chỉ và trạng thái; không dùng dữ liệu mẫu trong UI.</summary>
    private void Apply(CustomerAddressBook data, bool changed)
    {
        book = data; error.Text = ""; count.Text = $"{data.DiaChi.Count(x => x.DangSuDung)} địa chỉ đang sử dụng";
        foreach (Control child in content.Controls) child.Dispose(); content.Controls.Clear();
        var rows = new List<Control>();
        foreach (var address in data.DiaChi)
        {
            var badge = address.DangSuDung ? address.LaDiaChiMacDinh ? "Mặc định" : "Đang sử dụng" : "Đã ngừng sử dụng";
            var details = Ui.Label(address.DiaChiChiTiet, 10); details.AutoSize = false; details.Height = 50; details.Dock = DockStyle.Top;
            var parts = new List<Control> { Ui.Label($"{address.TenNguoiNhan}  ·  {address.DienThoaiNguoiNhan}", 10, true), details,
                Ui.Label($"{address.NhanDiaChi ?? "Địa chỉ giao hàng"}  ·  {badge}", 9, false, AdminColors.Muted) };
            if (!string.IsNullOrWhiteSpace(address.GhiChuGiaoHang)) parts.Add(Ui.Label(address.GhiChuGiaoHang, 9, false, AdminColors.Muted));
            if (editable && address.DangSuDung)
            {
                var edit = Ui.Button("Sửa địa chỉ", async () => { if (!HasPending) await Edit(address); }); edit.Name = "CustomerAddressEdit";
                var select = Ui.Button("Đặt mặc định", async () => await Act(address, "default")); select.Enabled = !address.LaDiaChiMacDinh;
                var remove = Ui.Button("Ngừng sử dụng", async () => await Act(address, "deactivate"), danger: true);
                parts.Add(Ui.Row(edit, select, remove));
            }
            rows.Add(AdminProfileLayout.Card(address.NhanDiaChi ?? $"Địa chỉ #{address.MaDiaChi}", parts.ToArray()));
        }
        if (rows.Count == 0) rows.Add(AdminProfileLayout.Empty("Chưa có địa chỉ giao hàng", editable ? "Thêm địa chỉ nhận hàng cho khách tại đây." : "Khách hàng chưa lưu địa chỉ nhận hàng."));
        content.Controls.Add(AdminListLayout.Column(rows.ToArray()));
        if (changed) Changed?.Invoke(data.Account);
    }

    /// <summary>Lưu hành động nhanh bằng GUID giữ nguyên qua timeout; thao tác chưa rõ chỉ được thử lại cùng yêu cầu.</summary>
    private async Task Run(Func<Task<CustomerAddressBook>>? send, Guid operation)
    {
        if (busy) return;
        if (pending is null) { pending = send; pendingId = operation; }
        if (pending is null) return;
        busy = true;
        try
        {
            var result = await AdminUi.Recover(pending, () => LiveSession.Api.AdminAddressReceiptAsync(pendingId));
            pending = null; if (!IsDisposed) Apply(result, true);
        }
        catch (CafeApiException ex) { if (!IsDisposed) error.Text = ex.Message; if (ex.StatusCode is not null) pending = null; }
        finally { busy = false; if (!retry.IsDisposed) retry.Visible = pending is not null; }
    }

    /// <summary>Ngừng địa chỉ có xác nhận; đặt mặc định giữ duy nhất tại máy chủ và không sửa đơn cũ.</summary>
    private async Task Act(AdminCustomerAddress address, string action)
    {
        if (HasPending || book is null) return;
        if (action == "deactivate" && MessageBox.Show(FindForm(), "Ngừng sử dụng địa chỉ này? Địa chỉ trên đơn đã đặt vẫn được giữ nguyên.", "Ngừng địa chỉ", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        var input = new AdminAddressAction(Guid.NewGuid(), book.Account.PhienBanDuLieu, address.PhienBanDuLieu);
        await Run(() => LiveSession.Api.ActAdminAddressAsync(owner, address.MaDiaChi, action, input), input.MaThaoTac);
    }

    /// <summary>Biểu mẫu địa chỉ riêng; giữ nháp và GUID khi lỗi, không ghép kết quả chưa lưu vào hồ sơ.</summary>
    private async Task Edit(AdminCustomerAddress? current)
    {
        if (book is null) return;
        await Task.Yield();
        using var form = AdminUi.Dialog(current is null ? "Thêm địa chỉ giao hàng" : "Sửa địa chỉ giao hàng", 780, 760);
        var label = Ui.Input("Nhà riêng, văn phòng…", current?.NhanDiaChi ?? "", 640); label.MaxLength = 50;
        var recipient = Ui.Input("Tên người nhận", current?.TenNguoiNhan ?? book.Account.HoTen, 640); recipient.MaxLength = 120;
        var phone = Ui.Input("Điện thoại người nhận", current?.DienThoaiNguoiNhan ?? book.Account.SoDienThoai ?? "", 640); phone.MaxLength = 10;
        phone.KeyPress += (_, e) => e.Handled = !char.IsControl(e.KeyChar) && (e.KeyChar < '0' || e.KeyChar > '9');
        var address = Ui.Input("Số nhà, đường, phường/xã, tỉnh/thành", current?.DiaChiChiTiet ?? "", 640); address.MaxLength = 500;
        address.Multiline = true; address.Height = 70;
        var note = Ui.Input("Hướng dẫn giao hàng (nếu có)", current?.GhiChuGiaoHang ?? "", 640); note.MaxLength = 500;
        var standard = new CheckBox { Text = "Dùng làm địa chỉ mặc định", Checked = current?.LaDiaChiMacDinh ?? false, AutoSize = true, Font = CafeTheme.Body() };
        recipient.Name = "AddressRecipient"; phone.Name = "AddressPhone"; address.Name = "AddressText"; standard.Name = "AddressDefault";
        var addressField = (TableLayoutPanel)AdminListLayout.Field("Địa chỉ giao hàng", address); addressField.Height = 110; addressField.RowStyles[1].Height = 82;
        var message = Ui.Label("", 9, false, CafeTheme.Danger);
        var save = Ui.Button("Lưu địa chỉ", () => { }, true); save.Name = "AddressSave";
        var cancel = Ui.Button("Hủy", form.Close);
        AdminProfileLayout.Mount(form, AdminProfileLayout.Header(book.Account.HoTen, "Sổ địa chỉ giao hàng", "Khách hàng"),
            AdminProfileLayout.Card("Thông tin nhận hàng", AdminListLayout.Field("Nhãn địa chỉ", label),
                AdminListLayout.Fields(AdminListLayout.Field("Người nhận", recipient), AdminListLayout.Field("Điện thoại", phone)),
                addressField, AdminListLayout.Field("Ghi chú giao hàng", note), standard, message), cancel, save);
        SaveSelfAddress? request = null; var running = false; var uncertain = false; var done = false;
        save.Click += async (_, _) =>
        {
            if (running) return;
            var changedText = current is null || address.Text.Trim() != current.DiaChiChiTiet.Trim();
            request ??= new(Guid.NewGuid(), book.Account.PhienBanDuLieu, current?.PhienBanDuLieu, label.Text, recipient.Text, phone.Text,
                address.Text, changedText ? null : current?.ViDo, changedText ? null : current?.KinhDo, note.Text, standard.Checked);
            running = true; save.Enabled = cancel.Enabled = false;
            foreach (var control in new Control[] { label, recipient, phone, address, note, standard }) control.Enabled = false;
            try
            {
                var result = await AdminUi.Recover(() => LiveSession.Api.SaveAdminAddressAsync(owner, current?.MaDiaChi, request), () => LiveSession.Api.AdminAddressReceiptAsync(request.MaThaoTac));
                Apply(result, true); done = true; form.DialogResult = DialogResult.OK; form.Close();
            }
            catch (CafeApiException ex)
            {
                message.Text = ex.Message; uncertain = ex.StatusCode is null;
                if (!uncertain) { request = null; foreach (var control in new Control[] { label, recipient, phone, address, note, standard }) control.Enabled = true; }
                save.Text = uncertain ? "Thử lại yêu cầu" : "Lưu địa chỉ";
            }
            finally { running = false; if (!save.IsDisposed) { save.Enabled = true; cancel.Enabled = !uncertain; } }
        };
        form.FormClosing += (_, e) => { if (!done && (running || uncertain)) e.Cancel = true; };
        form.ShowDialog(FindForm());
        await Task.CompletedTask;
    }
}
