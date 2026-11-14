using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;

namespace FourChang.Desktop.Shared.Controls.Admin;

/// <summary>Khung danh sách Admin dùng chung: tìm kiếm, tải lại, trạng thái cũ và nhóm nút thẳng hàng.</summary>
public abstract class AdminWorkspacePage<T> : UserControl
{
    protected readonly TextBox SearchBox = Ui.Input("Tìm theo mã hoặc tên…", "", 320);
    protected readonly DataGridView Grid = Ui.Grid(new([], []), 420);
    protected readonly Label Message = Ui.Label("", 9, false, CafeTheme.Muted);
    protected readonly FlowLayoutPanel Actions = Ui.Row();
    protected readonly FlowLayoutPanel Filters = Ui.Row();
    protected readonly Label Counter = Ui.Label("", 9, false, CafeTheme.Muted);
    protected IReadOnlyList<T> Items = [];
    protected bool Busy;
    private DateTime? lastLoaded;
    private readonly AdminListLayout? listLayout;

    /// <summary>Thiết lập layout Dock/Anchor thay vì tọa độ cố định để không tràn khi resize.</summary>
    protected AdminWorkspacePage(string title, string description, bool polished = false, string section = "KHO / NGUYÊN LIỆU")
    {
        Message.Visible = false; Message.TextChanged += (_, _) => Message.Visible = !string.IsNullOrWhiteSpace(Message.Text);
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; Dock = DockStyle.Top; BackColor = CafeTheme.Cream; Padding = new Padding(16); AutoScroll = true;
        Grid.Name = "AdminDataGrid"; Grid.AutoGenerateColumns = false; Grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        Grid.MinimumSize = new Size(0, 260); Grid.Height = 440;
        Filters.Name = "WorkspaceFilters"; Actions.Name = "WorkspaceActions";
        if (polished)
            Filters.Controls.Add(Ui.Field("Tìm kiếm", SearchBox));
        else Filters.Controls.Add(SearchBox);
        AddAction(Filters, "Tìm kiếm", async () => { await Task.CompletedTask; Render(); });
        AddAction(Filters, "Tải lại", async () => { SearchBox.Clear(); await Reload(); });
        // Danh sách nguyên liệu dùng khung đã chuẩn hóa; các trang khác giữ layout hiện có.
        if (polished)
        {
            listLayout = new AdminListLayout(title, description, Filters, Actions, Message, Grid, Counter, section: section);
            Controls.Add(listLayout);
        }
        else Controls.Add(Ui.Panel(title, Ui.Label(description, 9, false, CafeTheme.Muted), Filters, Actions, Message, Grid, Counter));
        OperationsPresentation.FitRows(Filters); OperationsPresentation.FitRows(Actions);
        SearchBox.TextChanged += (_, _) => Render();
        HandleCreated += async (_, _) => await Reload();
    }

    /// <summary>Thêm cột có tên tiếng Việt, ẩn ID kỹ thuật theo quyết định của từng trang.</summary>
    protected void Column(string property, string caption, float weight = 100)
    {
        Grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = property, HeaderText = caption, FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable });
    }

    /// <summary>Đọc bản ghi đang chọn mà không suy danh tính từ vị trí/số thứ tự trên màn hình.</summary>
    protected T? Selected() => Grid.CurrentRow?.DataBoundItem is T item ? item : default;

    /// <summary>Tạo nút cùng chiều cao và bao lỗi bất đồng bộ; không thêm sự kiện giữ click vào dialog mẫu.</summary>
    protected Button AddAction(FlowLayoutPanel row, string title, Func<Task> action, bool primary = false)
    {
        var button = Ui.Button(title, () => { }, primary, danger: title.StartsWith("Xóa", StringComparison.Ordinal));
        // Bộ lọc có nhãn ở phía trên: căn nút với ô nhập, không căn với dòng nhãn.

        button.Click += async (_, _) =>
        {
            if (Busy) return;
            button.Enabled = false;
            try { await action(); }
            catch (CafeApiException ex) { AdminUi.Error(this, ex); }
            finally { if (!button.IsDisposed) button.Enabled = true; }
        };
        if (row == Filters)
        {
            // Hai nút lọc luôn xuống hàng cùng nhau, tránh “Tìm kiếm” ở hàng trên, “Tải lại” ở hàng dưới.
            var group = row.Controls.OfType<FlowLayoutPanel>().FirstOrDefault(x => x.Name == "FilterActionGroup");
            if (group is null)
            {
                group = Ui.Row();
                group.Name = "FilterActionGroup";
                group.Dock = DockStyle.None;
                group.WrapContents = false;
                group.Margin = new Padding(0, 30, 0, 8);
                row.Controls.Add(group);
            }
            button.Margin = new Padding(0, 0, 8, 0);
            group.Controls.Add(button);
        }
        else row.Controls.Add(button);
        return button;
    }

    /// <summary>Tải từ API, giữ bảng cũ có nhãn thời điểm khi mất mạng thay vì xóa hoặc giả số 0.</summary>
    public Task Reload() => ReloadCore(false);

    /// <summary>Cập nhật nền khi polling: không bật overlay tải hoặc xóa nội dung đang đọc.</summary>
    protected Task RefreshQuietly() => ReloadCore(true);

    /// <summary>Chỉ hiển thị trạng thái tải khi người dùng yêu cầu; cả hai nhánh vẫn giữ dữ liệu cũ khi lỗi mạng.</summary>
    private async Task ReloadCore(bool quiet)
    {
        if (Busy || IsDisposed) return;
        Busy = true; if (!quiet && listLayout is not null) listLayout.Busy = true; if (!quiet) Message.Text = "Đang tải dữ liệu…";
        try
        {
            Items = await Fetch();
            if (IsDisposed) return;
            lastLoaded = DateTime.Now; Grid.Tag = lastLoaded; Message.Text = ""; Render();
        }
        catch (CafeApiException ex)
        {
            if (IsDisposed) return;
            Message.Text = ex.Message + (lastLoaded is null ? " Chưa tải được dữ liệu." : " · Đang hiển thị dữ liệu đã tải trước đó.");
        }
        finally { Busy = false; if (!quiet && listLayout is not null && !IsDisposed) listLayout.Busy = false; }
    }

    /// <summary>Lọc tại danh sách đã lấy; API tiếp tục kiểm tra điều kiện hiện tại khi ghi.</summary>
    protected void Render()
    {
        if (Grid.IsDisposed) return;
        var rows = Items.Where(x => Match(x, SearchBox.Text.Trim())).ToList();
        BindRows(rows); Counter.Text = $"{rows.Count} / {Items.Count} bản ghi";
    }
    /// <summary>Mặc định gắn danh sách như trước; trang có polling được phép giữ binding khi snapshot không đổi.</summary>
    protected virtual void BindRows(IReadOnlyList<T> rows) => Grid.DataSource = rows.ToList();

    /// <summary>Lớp con lấy đúng DTO của chức năng, không đọc bảng thô thay nghiệp vụ.</summary>
    protected abstract Task<IReadOnlyList<T>> Fetch();
    /// <summary>Lớp con định nghĩa các trường tìm kiếm hiển thị.</summary>
    protected abstract bool Match(T item, string query);
}
