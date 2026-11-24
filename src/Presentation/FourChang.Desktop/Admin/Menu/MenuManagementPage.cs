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
        var definitions = new[] {
            ("dishes", "Món", "Danh sách món", "Thông tin món, hiển thị và bật/tắt bán."),
            ("variants", "Cấu hình món", "Cấu hình và giá bán", "Chọn riêng size/nóng-lạnh và giá dùng chung Web/POS."),
            ("toppings", "Topping", "Topping dùng chung", "Giá/phần, giới hạn và trạng thái sử dụng."),
            ("recipes", "Công thức", "Công thức pha chế", "Lưu áp dụng ngay, giữ nguyên định mức đơn cũ.") };

        var navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, Padding = new Padding(0, 0, 0, 12), Name = "MenuNavigation" };
        foreach (var (kind, caption, title, description) in definitions)
        {
            var page = new TabPage(caption) { BackColor = CafeTheme.Cream, Padding = Padding.Empty, Margin = Padding.Empty, AutoScroll = false };
            if (kind == "recipes")
            {
                page.AutoScroll = true;
                page.Controls.Add(new FourChang.Desktop.Admin.Recipes.RecipesPage());
            }
            else page.Controls.Add(List(kind, title, description));
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
            "variants" => "Giá dùng chung Web/POS. Khai báo nguyên liệu tại tab Công thức.",
            "toppings" => "Topping dùng chung; ngừng sử dụng để bảo toàn lịch sử.",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var pane = new MenuListLayout(kind, title, description, note, () => Edit(kind, null), () => Selected(kind, id => Edit(kind, id)), () => _ = LoadData());
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
            "dishes" => data.Mon.OrderBy(x => x.ThuTuHienThi).ThenBy(x => x.MaMon).Select(x => new[] { x.MaMon.ToString(), x.TenMon, data.DanhMuc.FirstOrDefault(c => c.MaDanhMuc == x.MaDanhMuc)?.TenDanhMuc ?? "", x.DaCongBo ? "Hiển thị" : "Ẩn", x.DangBan ? "Bật bán" : "Tạm hết" }),
            "variants" => data.CauHinh.Select(x => new[] { x.MaCauHinhMon.ToString(), data.Mon.FirstOrDefault(m => m.MaMon == x.MaMon)?.TenMon ?? "", x.KichCo, x.NhietDo == "HOT" ? "Nóng" : "Lạnh", x.GiaBan.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")), x.DangBan ? "Bật" : "Tắt", x.LaMacDinh ? "Mặc định" : "" }),
            "toppings" => data.MonThem.Select(x => new[] { x.MaMonThem.ToString(), x.TenMonThem, x.GiaMoiPhan.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")), x.GioiHanMacDinhMoiLy.ToString(), x.DangSuDung ? "Sử dụng" : "Ngừng" }),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        string[] columns = kind switch
        {
            "dishes" => ["STT", "Tên món", "Loại món", "Hiển thị", "Bán"],
            "variants" => ["Mã", "Món", "Size", "Nhiệt độ", "Giá (đ)", "Bán", "Mặc định"],
            "toppings" => ["Mã", "Topping", "Giá/phần (đ)", "Giới hạn/ly", "Trạng thái"],
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var matches = rows.Where(r => term.Length == 0 || r.Any(s => s.Contains(term, StringComparison.CurrentCultureIgnoreCase))).ToArray();
        // Cấu hình cột trước khi bật Fill: tab chưa hiển thị có thể chưa đủ layout native của DataGridView.
        grid.SuspendLayout();
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        DatabaseViews.SetGrid(grid, new(columns, matches));
        if (kind == "dishes")
        {
            // STT chỉ để trình bày; Tag giữ khóa thật để sửa đúng món sau khi lọc/sắp xếp.
            for (var index = 0; index < grid.Rows.Count; index++)
            {
                grid.Rows[index].Tag = Convert.ToInt64(matches[index][0]);
                grid.Rows[index].Cells[0].Value = (index + 1).ToString();
            }
        }
        var unit = kind switch
        {
            "dishes" => "món",
            "variants" => "cấu hình",
            "toppings" => "topping",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        count.Text = $"{matches.Length} {unit}";
        var weights = kind switch
        {
            "dishes" => new float[] { 8, 38, 18, 18, 18 },
            "variants" => [8, 28, 9, 13, 16, 12, 14],
            "toppings" => [8, 32, 20, 16, 24],
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
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
        action(kind == "dishes" ? (long)grid.CurrentRow.Tag! : Convert.ToInt64(grid.CurrentRow.Cells[0].Value));
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
            if (kind == "variants" && id is null)
            {
                using var batch = new MenuVariantBatchForm(data);
                if (batch.ShowDialog(FindForm()) == DialogResult.OK && batch.SavedDishId is { } dish)
                {
                    await LoadData();
                    using var recipes = new FourChang.Desktop.Admin.Recipes.DishRecipeEditorForm(await LiveSession.Api.GetIngredientsAsync(),
                        await LiveSession.Api.GetAdminMenuAsync(), await LiveSession.Api.GetRecipesAsync(), dish);
                    recipes.ShowDialog(FindForm());
                    await LoadData();
                }
                return;
            }
            using var form = kind == "toppings"
                ? new MenuEditorForm(kind, id, data, await LiveSession.Api.GetIngredientsAsync(), await LiveSession.Api.GetRecipesAsync())
                : new MenuEditorForm(kind, id, data);
            if (kind == "dishes" && id is null) form.SetRecipeTemplates(await LiveSession.Api.GetRecipesAsync());
            if (form.ShowDialog(FindForm()) == DialogResult.OK)
                await LoadData();
        }
        catch (CafeApiException ex) { AdminUi.Error(this, ex); }
        catch (InvalidOperationException) { MessageBox.Show(FindForm(), "Dòng không còn tồn tại. Tải lại danh sách."); }
    }

}
