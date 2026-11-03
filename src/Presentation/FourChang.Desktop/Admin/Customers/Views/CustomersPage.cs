using FourChang.Desktop.Admin.Customers.Controllers;
using System.Drawing.Drawing2D;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Preview;

namespace FourChang.Desktop.Admin.Customers.Views;

// UC-ADM-01: chỉ tổ chức lại cách hiển thị; mọi lệnh vẫn qua API và giữ nguyên kiểm tra nghiệp vụ.
public sealed class CustomersPage : UserControl
{
    private readonly AdminSearchBox searchBox = new();
    private TextBox Search => searchBox.Input;
    private readonly DataGridView grid = Ui.Grid(new PreviewTable([], []), 400);
    private readonly Label feedback = Ui.Label("", 9, false, CafeTheme.Danger);
    private readonly Label paging = Ui.Label("", 9);
    private readonly Label count = Ui.Label("Đang tải…", 10, true, AdminColors.Accent);
    private readonly Label description = Ui.Label("Hồ sơ, thông tin liên hệ và trạng thái truy cập của khách hàng.", 9, false, AdminColors.Muted);
    private readonly Label selection = Ui.Label("Chọn khách hàng để xem hoặc cập nhật hồ sơ", 9, false, AdminColors.Muted);
    private readonly AdminLoadingStrip progress = new();
    private readonly Panel list = new() { Height = 420, Dock = DockStyle.Top, BackColor = AdminColors.Surface };
    private readonly List<Button> commands = [];
    private readonly List<Button> selectionCommands = [];
    private readonly Button previous;
    private readonly Button next;
    private Form? host;
    private readonly CustomerListController controller = new(LiveSession.Api);
    private int hoveredRow = -1;
    private bool loading;

    public CustomersPage()
    {
        Name = "CustomersPage"; Dock = DockStyle.Top; AutoSize = true; DoubleBuffered = true;
        BackColor = CafeTheme.Cream;
        feedback.Visible = false; feedback.MaximumSize = new Size(1000, 0);
        count.Name = "CustomerResultCount"; count.AutoSize = false; count.Width = 180; count.Height = 34;
        count.TextAlign = ContentAlignment.MiddleRight; count.Anchor = AnchorStyles.Right;
        paging.Name = "CustomerPaging"; paging.AutoSize = false; paging.Width = 155; paging.Height = 42; paging.TextAlign = ContentAlignment.MiddleCenter;
        selection.AutoSize = false; selection.AutoEllipsis = true; selection.Height = 42; selection.Dock = DockStyle.Fill; selection.TextAlign = ContentAlignment.MiddleLeft;

        // Tìm và tải lại đều đọc nội dung hiện tại của ô tìm, không tái sử dụng từ khóa đã bị xóa.
        var find = Command("Tìm kiếm", AdminIcon.Search, ApplySearch, true); find.Name = "CustomerFind";
        var reload = Command("Tải lại", AdminIcon.Refresh, ApplySearch); reload.Name = "CustomerReload";
        var add = Command("Thêm khách hàng", AdminIcon.Add, () => OpenEditor(null, "create"), true);
        var details = SelectionCommand("Xem hồ sơ", AdminIcon.View, ShowDetails);
        var edit = SelectionCommand("Sửa hồ sơ", AdminIcon.Edit, () => OpenSelected("update"));
        var status = SelectionCommand("Khóa / mở / ngừng", AdminIcon.Lock, () => OpenSelected("status"));
        var reset = SelectionCommand("Đặt lại mật khẩu", AdminIcon.Key, () => OpenSelected("reset-password"));
        var delete = SelectionCommand("Xóa có điều kiện", AdminIcon.Delete, () => OpenSelected("delete"), true);
        previous = new AdminActionButton("Trước", AdminIcon.Previous) { Name = "CustomerPrevious" };
        next = new AdminActionButton("Sau", AdminIcon.Next) { Name = "CustomerNext" };
        previous.Click += async (_, _) => { if (!loading && controller.Page > 1) { controller.PreviousPage(); await LoadPage(); } };
        next.Click += async (_, _) => { if (!loading && controller.Page * 20 < controller.Total) { controller.NextPage(); await LoadPage(); } };
        Search.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; find.PerformClick(); } };

        var title = Ui.Label("Danh sách khách hàng", 15, true);
        title.Margin = new Padding(0, 0, 0, 5);
        var headingText = Ui.Stack(title, description); headingText.Dock = DockStyle.Fill;
        var heading = new TableLayoutPanel { Dock = DockStyle.Top, Height = 65, ColumnCount = 2, Margin = Padding.Empty };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        heading.Controls.Add(headingText, 0, 0); heading.Controls.Add(count, 1, 0);

        var searchRow = new TableLayoutPanel { Dock = DockStyle.Top, Height = 56, ColumnCount = 3, Margin = Padding.Empty };
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, find.Width + 10));
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, reload.Width));
        searchBox.Dock = DockStyle.Fill; searchBox.Margin = new Padding(0, 0, 12, 12);
        find.Margin = new Padding(0, 0, 10, 12); reload.Margin = new Padding(0, 0, 0, 12);
        searchRow.Controls.Add(searchBox, 0, 0); searchRow.Controls.Add(find, 1, 0); searchRow.Controls.Add(reload, 2, 0);

        var toolbar = Ui.Row(add, details, edit, status, reset, delete);
        toolbar.Dock = DockStyle.Fill; toolbar.Margin = new Padding(0, 2, 0, 10);
        ConfigureGrid();
        grid.Dock = DockStyle.Fill; list.Controls.Add(grid); list.Controls.Add(progress);
        var footer = new TableLayoutPanel { Dock = DockStyle.Top, Height = 54, ColumnCount = 2, Margin = new Padding(0, 10, 0, 0) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var pagination = Ui.Row(previous, paging, next); pagination.WrapContents = false; pagination.Dock = DockStyle.Fill;
        footer.Controls.Add(selection, 0, 0); footer.Controls.Add(pagination, 1, 0);
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 6, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var item in new Control[] { heading, searchRow, toolbar, feedback, list, footer }) layout.Controls.Add(item);
        var card = new AdminSurface { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        card.Controls.Add(layout); Controls.Add(card);
        var initialized = false;
        HandleCreated += async (_, _) =>
        {
            if (initialized) return; initialized = true;
            host = FindForm(); if (host is not null) host.Resize += ResizeList;
            ResizeList(this, EventArgs.Empty); await LoadPage();
        };
    }

    private void ConfigureGrid()
    {
        grid.Name = "CustomersGrid"; grid.AutoGenerateColumns = false;
        grid.BackgroundColor = AdminColors.Surface; grid.GridColor = Color.FromArgb(243, 233, 221);
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.ColumnHeadersHeight = 46; grid.RowTemplate.Height = 54; grid.ShowCellToolTips = true;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(252, 243, 231);
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = grid.ColumnHeadersDefaultCellStyle.BackColor;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(130, 94, 65);
        grid.ColumnHeadersDefaultCellStyle.Font = CafeTheme.Strong(9);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 6, 0);
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.DefaultCellStyle.BackColor = AdminColors.Surface;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(249, 230, 209);
        grid.DefaultCellStyle.SelectionForeColor = CafeTheme.Ink;
        grid.DefaultCellStyle.Padding = new Padding(10, 0, 6, 0);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(255, 250, 244);
        AddColumn("MaKhachHang", "Mã KH", 80, 8);
        AddColumn("HoTen", "Họ tên", 150, 23);
        AddColumn("TenDangNhap", "Tên đăng nhập", 135, 19);
        AddColumn("Email", "Email", 170, 27);
        AddColumn("SoDienThoai", "Điện thoại", 125, 18);
        AddColumn("TrangThai", "Trạng thái", 126, 17);
        grid.Columns[0].ToolTipText = "Mã khách hàng";
        grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            if (e.RowIndex == hoveredRow && !grid.Rows[e.RowIndex].Selected)
                e.CellStyle!.BackColor = Color.FromArgb(255, 241, 222);
            if (e.ColumnIndex == 5 && e.Value is string value)
                e.Value = StatusText(value);
        };
        grid.CellPainting += PaintStatus;
        grid.CellMouseEnter += (_, e) => { if (hoveredRow >= 0 && hoveredRow < grid.Rows.Count) grid.InvalidateRow(hoveredRow); hoveredRow = e.RowIndex; if (hoveredRow >= 0) grid.InvalidateRow(hoveredRow); };
        grid.MouseLeave += (_, _) => { var old = hoveredRow; hoveredRow = -1; if (old >= 0 && old < grid.Rows.Count) grid.InvalidateRow(old); };
        grid.SelectionChanged += (_, _) => UpdateSelection();
        grid.CellDoubleClick += async (_, e) =>
        {
            if (e.RowIndex < 0 || loading) return;
            try { await ShowDetails(); } catch (CafeApiException ex) { ShowError(ex.Message); }
        };
    }

    private static string StatusText(string status) => status switch { "ACTIVE" => "Hoạt động", "LOCKED" => "Đã khóa", "INACTIVE" => "Đã ngừng", _ => status };
    private void PaintStatus(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != 5 || grid.Rows[e.RowIndex].DataBoundItem is not CustomerAccount account) return;
        e.PaintBackground(e.ClipBounds, true);
        var (background, ink) = account.TrangThai switch
        {
            "ACTIVE" => (Color.FromArgb(234, 245, 225), Color.FromArgb(76, 119, 53)),
            "LOCKED" => (Color.FromArgb(255, 237, 207), Color.FromArgb(162, 101, 33)),
            _ => (Color.FromArgb(239, 235, 229), Color.FromArgb(129, 116, 103))
        };
        var text = StatusText(account.TrangThai); var width = Math.Min(e.CellBounds.Width - 12, TextRenderer.MeasureText(text, grid.Font).Width + 24);
        var box = new Rectangle(e.CellBounds.Left + 8, e.CellBounds.Top + (e.CellBounds.Height - 28) / 2, width, 28);
        e.Graphics!.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = CafeTheme.Rounded(box, 12); using var fill = new SolidBrush(background); e.Graphics.FillPath(fill, shape);
        TextRenderer.DrawText(e.Graphics, text, grid.Font, box, ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        e.Handled = true;
    }

    private void AddColumn(string property, string title, int minimumWidth, float weight) => grid.Columns.Add(new DataGridViewTextBoxColumn
    { DataPropertyName = property, Name = property, HeaderText = title, MinimumWidth = minimumWidth, FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable });

    private Button Command(string caption, AdminIcon icon, Func<Task> action, bool primary = false, bool danger = false)
    {
        var button = new AdminActionButton(caption, icon, primary, danger); commands.Add(button);
        button.Click += async (_, _) =>
        {
            if (loading) return;
            try { await action(); } catch (CafeApiException ex) { ShowError(ex.Message); }
        };
        return button;
    }
    private Button SelectionCommand(string caption, AdminIcon icon, Func<Task> action, bool danger = false)
    { var button = Command(caption, icon, action, danger: danger); selectionCommands.Add(button); return button; }

    private async Task ApplySearch()
    {
        // Sửa lỗi tải lại: từ khóa nguồn phải là ô hiện tại, đồng thời bỏ trang cũ của bộ lọc trước.
        controller.ApplySearch(Search.Text); await LoadPage();
    }
    private async Task LoadPage()
    {
        if (IsDisposed || loading) return;
        loading = true; progress.Running = true; SetCommands(false);
        try
        {
            var result = await controller.LoadPageAsync();
            if (IsDisposed) return;
            hoveredRow = -1; grid.DataSource = result.Items;
            count.Text = $"{controller.Total:N0} khách hàng";
            description.Text = controller.SearchTerm.Length == 0 ? "Hồ sơ, thông tin liên hệ và trạng thái truy cập của khách hàng." : $"Kết quả tìm kiếm cho “{controller.SearchTerm}”";
            description.AutoEllipsis = true;
            paging.Text = $"Trang {controller.Page} / {Math.Max(1, (controller.Total + 19) / 20)}";
            feedback.Visible = false; UpdateSelection();
        }
        catch (CafeApiException ex) { if (!IsDisposed) ShowError(ex.Message + " Danh sách hiện tại chưa được xác nhận cập nhật."); }
        finally { if (!IsDisposed) { loading = false; progress.Running = false; SetCommands(true); } }
    }
    private void SetCommands(bool enabled)
    {
        foreach (var button in commands) button.Enabled = enabled;
        foreach (var button in selectionCommands) button.Enabled = enabled && grid.CurrentRow?.DataBoundItem is CustomerAccount;
        previous.Enabled = enabled && controller.Page > 1; next.Enabled = enabled && controller.Page * 20 < controller.Total;
    }
    private void UpdateSelection()
    {
        var customer = grid.CurrentRow?.DataBoundItem as CustomerAccount;
        selection.Text = customer is null ? "Chưa có khách hàng được chọn" : $"Đã chọn: {customer.HoTen}";
        foreach (var button in selectionCommands) button.Enabled = !loading && customer is not null;
    }
    private CustomerAccount? Selected()
    {
        var selected = grid.CurrentRow?.DataBoundItem as CustomerAccount;
        if (selected is null) ShowError("Chọn một khách hàng trong danh sách."); return selected;
    }
    private async Task OpenSelected(string action)
    {
        var selected = Selected(); if (selected is null) return;
        var detail = await controller.GetDetailsAsync(selected.MaKhachHang);
        if (!IsDisposed) await OpenEditor(detail, action);
    }
    private async Task OpenEditor(CustomerDetails? detail, string action)
    {
        using var editor = new CustomerEditorForm(detail, action);
        if (editor.ShowDialog(FindForm()) == DialogResult.OK) await LoadPage();
    }
    private async Task ShowDetails()
    {
        var selected = Selected(); if (selected is null) return;
        var detail = await controller.GetDetailsAsync(selected.MaKhachHang); if (IsDisposed) return;
        using var form = new CustomerDetailsForm(detail); form.ShowDialog(FindForm());
    }
    private void ResizeList(object? sender, EventArgs e) => list.Height = Math.Clamp((host?.ClientSize.Height ?? 920) - 500, 260, 420);
    private void ShowError(string message) { feedback.Text = message; feedback.Visible = true; }
    protected override void Dispose(bool disposing)
    { if (disposing && host is not null) host.Resize -= ResizeList; base.Dispose(disposing); }
}
