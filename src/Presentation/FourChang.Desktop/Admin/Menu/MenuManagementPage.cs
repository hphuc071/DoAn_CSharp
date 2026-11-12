using FourChang.Desktop.Admin.Employees;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Database;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;
namespace FourChang.Desktop.Admin.Menu;

/// <summary>ADM-02: màn hình CRUD thật, tái sử dụng bảng màu/khung chuẩn; mã và giá luôn lấy từ API.</summary>
public sealed class MenuManagementPage : UserControl
{
    private MenuAdminSnapshot data = new([], [], [], [], []);
    private readonly Dictionary<string, (DataGridView Grid, TextBox Search, Label Count)> lists = [];
    private readonly Label error = Ui.Label("", 9, false, CafeTheme.Danger);
    private bool busy;
    private readonly Dictionary<string, MenuListLayout> panes = [];
    private readonly List<Button> tabButtons = [];
    private readonly MenuContentTabs tabs = new() { Name = "MenuTabs" };
    private Form? host;


    /// <summary>Giữ nhóm Thực đơn trong sidebar hiện có; chia tab theo đúng dữ liệu nghiệp vụ để dễ thao tác.</summary>
    public MenuManagementPage()
    {
        Name = "MenuManagementPage";
        AutoSize = false;
        Dock = DockStyle.Top;
        Height = 680;
        BackColor = CafeTheme.Cream;
        DoubleBuffered = true;
        var definitions = new[] { ("dishes", "Món", "Danh sách món", "Quản lý thông tin và trạng thái hiển thị của món."), ("variants", "Cấu hình món", "Cấu hình và giá bán", "Size, nóng/lạnh và lựa chọn pha chế dùng chung Web/POS."), ("categories", "Danh mục", "Danh mục thực đơn", "Sắp xếp nhóm món và nội dung hiển thị trên thực đơn."), ("toppings", "Topping", "Topping và giá mỗi phần", "Quản lý giá, giới hạn và trạng thái sử dụng."), ("banners", "Banner", "Nội dung công bố", "Quản lý hình ảnh và thời gian hiển thị banner.") };
        var navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, Padding = new Padding(0, 0, 0, 12), Name = "MenuNavigation" };
        foreach (var (kind, caption, title, description) in definitions)
        {
            var page = new TabPage(caption) { BackColor = CafeTheme.Cream, Padding = Padding.Empty, Margin = Padding.Empty, AutoScroll = false };
            page.Controls.Add(List(kind, title, description));
            tabs.TabPages.Add(page);
            var index = tabButtons.Count;
            var button = Ui.Button(caption, () => tabs.SelectedIndex = index);
            button.Name = "MenuTab_" + kind;
            button.AccessibleName = caption;
            button.Margin = new Padding(0, 0, 8, 0);
            button.Height = 44;
            tabButtons.Add(button);
            navigation.Controls.Add(button);
        }
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.Absolute, 60));
        layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.RowStyles.Add(new(SizeType.Percent, 100));
        error.Dock = DockStyle.Top;
        error.Visible = false;
        error.Margin = new Padding(0, 0, 0, 12);
        error.TextChanged += (_, _) => error.Visible = !string.IsNullOrWhiteSpace(error.Text);
        layout.Controls.Add(navigation, 0, 0);
        layout.Controls.Add(error, 0, 1);
        layout.Controls.Add(tabs, 0, 2);
        Controls.Add(layout);
        tabs.SelectedIndexChanged += (_, _) => SelectTab();
        tabs.SelectedIndex = 0;
        SelectTab();
        HandleCreated += async (_, _) => { host = FindForm(); if (host is not null) host.Resize += Fit; BeginInvoke(new Action(() => Fit(this, EventArgs.Empty))); await LoadData(); };
    }

    /// <summary>Chỉ đổi trạng thái nút điều hướng; các TabPage và dữ liệu đang nhập không được tạo lại.</summary>
    private void SelectTab()
    {
        for (int i = 0; i < tabButtons.Count; i++)
        {
            var selected = i == tabs.SelectedIndex;
            tabButtons[i].BackColor = selected ? CafeTheme.Coffee : CafeTheme.Surface;
            tabButtons[i].ForeColor = selected ? Color.White : CafeTheme.Muted;
            tabButtons[i].AccessibleDescription = selected ? "Đang chọn" : "Mở tab";
        }
    }

    /// <summary>Co vùng danh sách theo cửa sổ thật để chỉ bảng cuộn, tránh ba lớp scrollbar như giao diện cũ.</summary>
    private void Fit(object? sender, EventArgs e)
    {
        if (host is null || IsDisposed)
            return;
        var top = host.PointToClient(PointToScreen(Point.Empty)).Y;
        Height = Math.Max(460, host.ClientSize.Height - Math.Max(90, top) - 24);
    }

    /// <summary>Dừng theo dõi resize khi rời màn hình để không giữ lại các trang đã đóng.</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing && host is not null)
            host.Resize -= Fit;
        base.Dispose(disposing);
    }

    /// <summary>Khung danh sách tái sử dụng handler CRUD cũ, chỉ thay cách sắp xếp và trình bày.</summary>
    private Control List(string kind, string title, string description)
    {
        var note = kind switch
        {
            "dishes" => "Món mới cần cấu hình và BOM hợp lệ trước khi công bố. Thiếu nguyên liệu vẫn tạm hết.",
            "variants" => "Giá dùng chung Web/POS. BOM tại ADM-05; ghi chú không tự đổi định mức.",
            "categories" => "Ẩn/ngừng danh mục ảnh hưởng các món thuộc nhóm; đơn cũ giữ nguyên.",
            "toppings" => "Giá theo phần; topping đã có tham chiếu chỉ được ngừng.",
            _ => "Chỉ banner hợp lệ, công bố và trong thời gian hiệu lực mới hiển thị."
        };
        var pane = new MenuListLayout(kind, title, description, note, () => Edit(kind, null), () => Selected(kind, id => Edit(kind, id)), () => Selected(kind, id => _ = Delete(kind, id)), () => _ = LoadData());
        panes[kind] = pane;
        lists[kind] = (pane.Grid, pane.Search, pane.Count);
        pane.Search.TextChanged += (_, _) => Render(kind);
        return pane;
    }

    /// <summary>Chỉ thay snapshot sau khi tải thành công; lỗi giữ dữ liệu cũ và không khẳng định đó là dữ liệu mới.</summary>
    private async Task LoadData()
    {
        if (busy)
            return;
        busy = true;
        foreach (var pane in panes.Values)
            pane.Busy = true;
        try
        {
            var snapshot = await LiveSession.Api.GetAdminMenuAsync();
            if (IsDisposed)
                return;
            data = snapshot;
            foreach (var kind in lists.Keys)
                Render(kind);
            error.Text = "";
        }
        catch (CafeApiException ex) { if (!IsDisposed) { error.Text = "Chưa tải được dữ liệu mới. " + ex.Message; AdminUi.Error(this, ex); } }
        finally { busy = false; foreach (var pane in panes.Values) if (!pane.IsDisposed) pane.Busy = false; }
    }

    /// <summary>Lọc snapshot mới nhất theo từ khóa hiện tại; xóa từ khóa trả lại đầy đủ số dòng.</summary>
    private void Render(string kind)
    {
        var (grid, search, count) = lists[kind];
        var term = search.Text.Trim();
        var rows = kind switch
        {
            "categories" => data.DanhMuc.Select(x => new[] { x.MaDanhMuc.ToString(), x.TenDanhMuc, x.ThuTuHienThi.ToString(), x.SoMon.ToString(), x.DaCongBo ? "Công bố" : "Ẩn", x.DangSuDung ? "Sử dụng" : "Ngừng" }),
            "dishes" => data.Mon.Select(x => new[] { x.MaMon.ToString(), x.TenMon, data.DanhMuc.FirstOrDefault(c => c.MaDanhMuc == x.MaDanhMuc)?.TenDanhMuc ?? "", x.DaCongBo ? "Công bố" : "Nháp", x.DangBan ? "Bật bán" : "Tắt bán", x.CongBoTuLuc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "Ngay khi công bố" }),
            "variants" => data.CauHinh.Select(x => new[] { x.MaCauHinhMon.ToString(), data.Mon.FirstOrDefault(m => m.MaMon == x.MaMon)?.TenMon ?? "", x.KichCo, x.NhietDo == "HOT" ? "Nóng" : "Lạnh", x.GiaBan.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")), x.DangBan ? "Bật" : "Tắt", x.LaMacDinh ? "Mặc định" : "" }),
            "toppings" => data.MonThem.Select(x => new[] { x.MaMonThem.ToString(), x.TenMonThem, x.GiaMoiPhan.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")), x.GioiHanMacDinhMoiLy.ToString(), x.DangSuDung ? "Sử dụng" : "Ngừng" }),
            _ => data.Banner.Select(x => new[] { x.MaBanner.ToString(), x.TenBanner, x.BatDauLuc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), x.KetThucLuc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "Không giới hạn", x.DaCongBo ? "Công bố" : "Nháp" })
        };
        var columns = kind switch
        {
            "categories" => new[] { "Mã", "Danh mục", "Thứ tự", "Số món", "Công bố", "Sử dụng" },
            "dishes" => ["Mã", "Tên món", "Danh mục", "Công bố", "Bán", "Công bố từ"],
            "variants" => ["Mã", "Món", "Size", "Nhiệt độ", "Giá (đ)", "Bán", "Mặc định"],
            "toppings" => ["Mã", "Topping", "Giá/phần (đ)", "Giới hạn/ly", "Trạng thái"],
            _ => ["Mã", "Banner", "Bắt đầu", "Kết thúc", "Trạng thái"]
        };
        var matches = rows.Where(r => term.Length == 0 || r.Any(s => s.Contains(term, StringComparison.CurrentCultureIgnoreCase))).ToArray();
        // Cấu hình cột trước khi bật Fill: tab chưa hiển thị có thể chưa đủ layout native của DataGridView.
        grid.SuspendLayout();
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        DatabaseViews.SetGrid(grid, new(columns, matches));
        var unit = kind switch
        {
            "dishes" => "món",
            "variants" => "cấu hình",
            "categories" => "danh mục",
            "toppings" => "topping",
            _ => "banner"
        };
        count.Text = $"{matches.Length} {unit}";
        var weights = kind switch
        {
            "dishes" => new float[] { 8, 26, 15, 14, 14, 23 },
            "variants" => [8, 28, 9, 13, 16, 12, 14],
            "categories" => [8, 30, 10, 12, 20, 20],
            "toppings" => [8, 32, 20, 16, 24],
            _ => [8, 30, 22, 22, 18]
        };
        for (int i = 0; i < grid.Columns.Count; i++)
        {
            grid.Columns[i].FillWeight = weights[i];
            grid.Columns[i].MinimumWidth = i == 0 ? 56 : i == 1 ? 130 : 60;
        }
        if (kind == "variants")
            grid.Columns[4].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        if (kind == "toppings")
            grid.Columns[2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        grid.Columns[0].MinimumWidth = 56;
        grid.Columns[0].FillWeight = 8;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        grid.ResumeLayout(true);
        panes[kind].ArrangeGrid();
    }

    /// <summary>Luôn dùng khóa dòng đang chọn, không suy mã từ tên món hoặc vị trí hiển thị.</summary>
    private void Selected(string kind, Action<long> action)
    {
        var grid = lists[kind].Grid;
        if (grid.CurrentRow is null)
        {
            MessageBox.Show(FindForm(), "Chọn một dòng trước.");
            return;
        }
        action(Convert.ToInt64(grid.CurrentRow.Cells[0].Value));
    }

    /// <summary>Mở editor có snapshot/version hiện tại; lỗi tải không được tạo form sửa từ dữ liệu thiếu.</summary>
    private async void Edit(string kind, long? id)
    {
        if (busy)
            return;
        try
        {
            data = await LiveSession.Api.GetAdminMenuAsync();
            if (IsDisposed)
                return;
            using var form = new MenuEditorForm(kind, id, data);
            if (form.ShowDialog(FindForm()) == DialogResult.OK)
                await LoadData();
        }
        catch (CafeApiException ex) { AdminUi.Error(this, ex); }
        catch (InvalidOperationException) { MessageBox.Show(FindForm(), "Dòng không còn tồn tại. Tải lại danh sách."); }
    }

    /// <summary>Xác nhận xóa; giữ nguyên mã thao tác khi timeout và tra receipt trước khi cho thử lại.</summary>
    private async Task Delete(string kind, long id)
    {
        if (busy)
            return;
        try
        {
            data = await LiveSession.Api.GetAdminMenuAsync();
            byte[] version = kind switch
            {
                "categories" => data.DanhMuc.Single(x => x.MaDanhMuc == id).PhienBanDuLieu,
                "dishes" => data.Mon.Single(x => x.MaMon == id).PhienBanDuLieu,
                "variants" => data.CauHinh.Single(x => x.MaCauHinhMon == id).PhienBanDuLieu,
                "toppings" => data.MonThem.Single(x => x.MaMonThem == id).PhienBanDuLieu,
                _ => data.Banner.Single(x => x.MaBanner == id).PhienBanDuLieu
            };
            if (MessageBox.Show(FindForm(), "Xóa dữ liệu chưa tham chiếu? Dữ liệu đang dùng phải ẩn/ngừng thay vì xóa.", "Xác nhận xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            var input = new MenuDeleteInput(version, Guid.NewGuid());
            bool retry;
            do
            {
                retry = false;
                try
                {
                    await AdminUi.Recover(() => LiveSession.Api.DeleteMenuAsync(kind, id, input), () => LiveSession.Api.MenuReceiptAsync(input.MaThaoTac));
                }
                catch (CafeApiException ex) when (ex.StatusCode is null) { retry = MessageBox.Show(FindForm(), ex.Message + "\nThử lại cùng mã thao tác?", "Chưa rõ kết quả", MessageBoxButtons.RetryCancel) == DialogResult.Retry; }
            } while (retry);
            await LoadData();
        }
        catch (CafeApiException ex) { AdminUi.Error(this, ex); }
        catch (InvalidOperationException) { MessageBox.Show(FindForm(), "Dữ liệu đã thay đổi. Tải lại danh sách."); }
    }
}
