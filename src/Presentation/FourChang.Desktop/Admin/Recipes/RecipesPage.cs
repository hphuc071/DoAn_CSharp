using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Admin.Ingredients;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Admin.Recipes;

/// <summary>ADM-04: danh sách phiên bản BOM, hành động rõ ràng thay vì sửa bảng SQL thô.</summary>
public sealed class RecipesPage : AdminWorkspacePage<RecipeView>
{
    /// <summary>Nhóm nút theo vòng đời nháp, hiệu lực và mô phỏng; cùng chiều cao, tự xuống dòng khi hẹp.</summary>
    public RecipesPage() : base("Công thức pha chế", "Quản lý phiên bản BOM · Đơn đã gửi giữ nguyên định mức đã chốt", true, "THỰC ĐƠN / CÔNG THỨC")
    {
        Column("MaCongThuc", "Mã", 40); Column("TenCongThuc", "Công thức", 180); Column("SoPhienBan", "Phiên bản", 60);
        Column("TrangThai", "Trạng thái", 90); Column("HieuLucTu", "Hiệu lực từ", 115); Column("HieuLucDen", "Đến", 115);
        AddAction(Actions, "Tạo nháp", () => Edit(null), true);
        AddAction(Actions, "Sửa nháp", async () => { if (Selected() is { } item) await Edit(item); });
        AddAction(Actions, "Tạo phiên bản mới", () => Action("clone"));
        AddAction(Actions, "Công bố / lên lịch", Publish);
        AddAction(Actions, "Mô phỏng", Simulate);
        AddAction(Actions, "Ngừng", Stop);
        AddAction(Actions, "Xóa nháp", () => Action("delete"));
        Grid.CellFormatting += FormatCell;
    }
    /// <summary>Hiển thị nhãn tiếng Việt và giờ địa phương; dữ liệu/hiệu lực lưu trong database vẫn là UTC.</summary>
    private void FormatCell(object? sender, DataGridViewCellFormattingEventArgs args)
    {
        if (args.RowIndex < 0 || args.ColumnIndex < 0) return;
        var property = Grid.Columns[args.ColumnIndex].DataPropertyName;
        if (property == "TrangThai" && args.Value is string status)
        {
            args.Value = status switch { "DRAFT" => "Nháp", "SCHEDULED" => "Lên lịch", "ACTIVE" => "Hiệu lực", "RETIRED" => "Đã ngừng", _ => status };
            args.FormattingApplied = true;
        }
        if (property is "HieuLucTu" or "HieuLucDen" && args.Value is DateTime time)
        {
            // datetime2 do EF đọc không có Kind; gắn UTC trước khi hiển thị để không lệch múi giờ.
            args.Value = DateTime.SpecifyKind(time, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            args.FormattingApplied = true;
        }
    }

    /// <summary>Đọc các phiên bản thực từ API.</summary>
    protected override Task<IReadOnlyList<RecipeView>> Fetch() => LiveSession.Api.GetRecipesAsync();
    /// <summary>Tìm theo mã hoặc tên công thức, giữ nguyên số phiên bản và đối tượng.</summary>
    protected override bool Match(RecipeView item, string query) => query.Length == 0 || item.MaCongThuc.ToString() == query || item.TenCongThuc.Contains(query, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>Đọc danh mục hiện hành trước mở editor; API vẫn kiểm tra lại khi lưu.</summary>
    private async Task Edit(RecipeView? item)
    {
        var ingredients = await LiveSession.Api.GetIngredientsAsync(); var menu = await LiveSession.Api.GetAdminMenuAsync();
        using var form = new RecipeEditorForm(ingredients, menu, item);
        if (form.ShowDialog(FindForm()) == DialogResult.OK) await Reload();
    }

    /// <summary>Gửi clone/xóa theo version sau xác nhận, không xóa bản công bố trên UI trước phản hồi.</summary>
    private async Task Action(string action)
    {
        var item = Selected(); if (item is null) return;
        if (MessageBox.Show(this, action == "delete" ? "Xóa nháp chưa được sử dụng?" : "Sao chép thành phiên bản nháp mới?", "Xác nhận", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        var op = Guid.NewGuid(); var input = new AdminDeleteInput(item.PhienBanDuLieu, op);
        await AdminUi.Recover(() => LiveSession.Api.RecipeActionAsync(item.MaCongThuc, action, input), () => LiveSession.Api.AdminReceiptAsync("recipes", op));
        await Reload();
    }

    /// <summary>Chọn mốc hiệu lực theo giờ máy, chuyển UTC trước gửi; không công bố hồi tố.</summary>
    private async Task Publish()
    {
        var item = Selected(); if (item is null) return;
        using var form = AdminUi.Dialog("Công bố công thức", 640, 380);
        var start = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy HH:mm:ss", Width = 230, Value = DateTime.Now };
        var end = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy HH:mm:ss", Width = 230, ShowCheckBox = true, Checked = false, Value = DateTime.Now.AddDays(1) };
        var confirm = Ui.Button("Xác nhận công bố", () => { }, true); var op = Guid.NewGuid();
        confirm.Click += async (_, _) =>
        {
            confirm.Enabled = false;
            try
            {
                var input = new RecipePublishInput(start.Value.ToUniversalTime(), end.Checked ? end.Value.ToUniversalTime() : null, item.PhienBanDuLieu, op);
                await AdminUi.Recover(() => LiveSession.Api.RecipeActionAsync(item.MaCongThuc, "publish", input), () => LiveSession.Api.AdminReceiptAsync("recipes", op));
                form.DialogResult = DialogResult.OK; form.Close();
            }
            catch (FourChang.Presentation.Shared.ApiClients.CafeApiException ex) { AdminUi.Error(form, ex); }
            finally { if (!confirm.IsDisposed) confirm.Enabled = true; }
        };
        form.Controls.Add(Ui.Panel(item.TenCongThuc, Ui.Field("Hiệu lực từ", start), Ui.Field("Kết thúc nếu có", end), confirm));
        if (form.ShowDialog(FindForm()) == DialogResult.OK) await Reload();
    }

    /// <summary>Ngừng có lý do, không xóa công thức đang được đơn tham chiếu.</summary>
    private async Task Stop()
    {
        var item = Selected(); if (item is null) return;
        using var form = AdminUi.Dialog("Ngừng công thức", 640, 300);
        var reason = Ui.Input("Nhập lý do bắt buộc", "", 520); reason.MaxLength = 500;
        var confirm = Ui.Button("Ngừng công thức", () => { }, true); var op = Guid.NewGuid();
        confirm.Click += async (_, _) =>
        {
            confirm.Enabled = false;
            try
            {
                var input = new RecipeStopInput(reason.Text, item.PhienBanDuLieu, op);
                await AdminUi.Recover(() => LiveSession.Api.RecipeActionAsync(item.MaCongThuc, "stop", input), () => LiveSession.Api.AdminReceiptAsync("recipes", op));
                form.DialogResult = DialogResult.OK; form.Close();
            }
            catch (FourChang.Presentation.Shared.ApiClients.CafeApiException ex) { AdminUi.Error(form, ex); }
            finally { if (!confirm.IsDisposed) confirm.Enabled = true; }
        };
        form.Controls.Add(Ui.Panel(item.TenCongThuc, reason, confirm));
        if (form.ShowDialog(FindForm()) == DialogResult.OK) await Reload();
    }

    /// <summary>Mô phỏng theo số ly/đường/đá và topping hỗ trợ; kết quả chỉ đọc không sửa tồn.</summary>
    private async Task Simulate()
    {
        var item = Selected(); if (item is null) return;
        var menu = await LiveSession.Api.GetAdminMenuAsync();
        using var form = AdminUi.Dialog("Mô phỏng nguyên liệu", 940, 680);
        var quantity = new NumericUpDown { Minimum = 1, Maximum = 999, Value = 1, Width = 120 };
        var sugar = Ui.Select("0", "25", "50", "75", "100"); sugar.SelectedItem = "50";
        var ice = Ui.Select("0", "25", "50", "75", "100"); ice.SelectedItem = "50";
        var selectedToppings = new Dictionary<long, int>();
        var topping = new ComboBox { Width = 230, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "TenMonThem", ValueMember = "MaMonThem", DataSource = menu.MonThem.ToList() };
        var parts = new NumericUpDown { Minimum = 1, Maximum = 99, Value = 1, Width = 100 };
        var topSummary = Ui.Label("Không thêm topping", 9);
        var add = Ui.Button("Thêm / sửa topping", () => { if (topping.SelectedValue is long id) selectedToppings[id] = (int)parts.Value; topSummary.Text = string.Join(" · ", selectedToppings.Select(x => menu.MonThem.Single(t => t.MaMonThem == x.Key).TenMonThem + " × " + x.Value)); });
        var clear = Ui.Button("Bỏ topping", () => { selectedToppings.Clear(); topSummary.Text = "Không thêm topping"; });
        var grid = Ui.Grid(new([], []), 330); var result = Ui.Label("Chọn thông số rồi bấm Mô phỏng", 9);
        var run = Ui.Button("Mô phỏng", () => { }, true);
        run.Click += async (_, _) =>
        {
            run.Enabled = false;
            try
            {
                var input = new RecipeSimulationInput(item.MaCongThuc, (int)quantity.Value, int.Parse(sugar.Text), int.Parse(ice.Text), selectedToppings.Select(x => new RecipeToppingInput(x.Key, x.Value)).ToList());
                var data = await LiveSession.Api.SimulateRecipeAsync(input);
                grid.DataSource = data.Select(x => new { Ma = x.MaNguyenLieu, NguyenLieu = x.TenNguyenLieu, DonVi = x.DonVi, CanDung = x.SoLuongCan, KhaDung = x.KhaDung, DuNguyenLieu = x.DuNguyenLieu }).ToList();
                result.Text = "Kết quả mô phỏng · Không giữ hoặc trừ kho";
            }
            catch (FourChang.Presentation.Shared.ApiClients.CafeApiException ex) { AdminUi.Error(form, ex); }
            finally { if (!run.IsDisposed) run.Enabled = true; }
        };
        form.Controls.Add(Ui.Panel(item.TenCongThuc, Ui.Row(Ui.Field("Số ly", quantity), Ui.Field("Đường %", sugar), Ui.Field("Đá %", ice)),
            Ui.Row(topping, parts, add, clear), topSummary, run, result, grid));
        form.ShowDialog(FindForm());
    }
}
