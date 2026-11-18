using FourChang.Desktop.Admin.Ingredients;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.ApiClients;

namespace FourChang.Desktop.Admin.Recipes;

/// <summary>Editor công thức dùng control có cấu trúc, không nhập định mức bằng JSON hoặc ghi chú tự do.</summary>
public sealed class RecipeEditorForm : Form
{
    private sealed record Target(long Id, string Label);
    private sealed record LineType(string Code, string Label);
    private static readonly LineType[] LineTypes = [new("FIXED", "Lượng cố định"), new("SUGAR", "Theo % đường"), new("ICE", "Theo % đá")];
    private readonly RecipeView? existing;
    private readonly IngredientSnapshot ingredients;
    private readonly List<RecipeLineInput> lines = [];
    private readonly TextBox name;
    private readonly TextBox note;
    private readonly ComboBox mode;
    private readonly ComboBox target;
    private readonly DataGridView grid;
    private readonly Button save;
    private readonly ComboBox defaultSugar;
    private readonly CheckedListBox ingredientChoices;
    private bool rendering;
    private sealed record IngredientChoice(IngredientView Item)
    {
        public override string ToString() => $"{Item.TenNguyenLieu} ({(Item.MaDonViCoSo == 1 ? "g" : "ml")})";
    }
    private Guid operation;
    private RecipeInput? pending;
    private bool busy;
    private bool uncertain;
    private readonly Label feedback = Ui.Label("", 9, false, CafeTheme.Danger);

    /// <summary>Chọn đúng cấu hình hoặc topping, đơn vị và loại FIXED/SUGAR/ICE; lưu áp dụng ngay, server tự bảo toàn công thức cũ.</summary>
    public RecipeEditorForm(IngredientSnapshot ingredients, MenuAdminSnapshot menu, RecipeView? item, IReadOnlyList<RecipeView>? recipes = null, RecipeView? initialTarget = null)
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        this.ingredients = ingredients; existing = item;
        Name = "RecipeEditorForm"; Text = item is null ? "Thêm công thức" : "Sửa công thức"; ClientSize = new Size(980, 900); MinimumSize = new Size(760, 600);
        Font = CafeTheme.Body(); BackColor = CafeTheme.Cream; Padding = new Padding(16); AutoScroll = true; StartPosition = FormStartPosition.CenterParent;
        name = Ui.Input("Tên công thức", item?.TenCongThuc ?? "", 420); note = Ui.Input("Ghi chú quản trị", item?.GhiChu ?? "", 360);
        name.MaxLength = 150; note.MaxLength = 500;
        mode = Ui.Select("Món theo size / nóng–lạnh", "Topping"); mode.Width = 350; mode.SelectedIndex = (item?.MaMonThem ?? initialTarget?.MaMonThem) is not null ? 1 : 0;
        target = new ComboBox { Width = 480, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Label", ValueMember = "Id", BindingContext = new BindingContext() };
        var targetHint = Ui.Label("", 9, false, CafeTheme.Muted); targetHint.MaximumSize = new Size(850, 0);
        // Khi thêm chỉ liệt kê đối tượng chưa có công thức, tránh báo trùng sau khi nhập xong.
        void Targets()
        {
            target.DataSource = mode.SelectedIndex == 0
                ? menu.CauHinh.Where(v => item is not null || recipes is null || !recipes.Any(x => x.MaCauHinhMon == v.MaCauHinhMon)).Select(v => new Target(v.MaCauHinhMon, $"{menu.Mon.Single(m => m.MaMon == v.MaMon).TenMon} · Size {v.KichCo} · {(v.NhietDo == "HOT" ? "Nóng" : "Lạnh")}")).ToList()
                : menu.MonThem.Where(t => item is not null || recipes is null || !recipes.Any(x => x.MaMonThem == t.MaMonThem)).Select(t => new Target(t.MaMonThem, t.TenMonThem)).ToList();
            targetHint.Text = item is not null ? "Chỉnh sửa áp dụng cho đơn mới; đơn cũ giữ công thức đã chốt."
                : target.Items.Count == 0 ? "Không có cấu hình còn thiếu. Chọn Thêm size / nóng-lạnh cho món mới, hoặc sửa công thức đã có."
                : "Chỉ hiện đối tượng chưa có công thức. Muốn thay công thức cũ, chọn Sửa công thức ở danh sách.";
            if (save is not null) save.Enabled = target.Items.Count > 0;
        }
        Targets();
        if (item is null && initialTarget is not null) target.SelectedValue = initialTarget.MaCauHinhMon ?? initialTarget.MaMonThem;
        mode.SelectedIndexChanged += (_, _) => Targets();
        if (item is not null) { target.SelectedValue = item.MaCauHinhMon ?? item.MaMonThem; mode.Enabled = target.Enabled = false; }
        if (item is not null)
            lines.AddRange(item.ThanhPhan.Select(x => new RecipeLineInput(x.MaNguyenLieu, ingredients.NguyenLieu.Single(n => n.MaNguyenLieu == x.MaNguyenLieu).MaDonViCoSo, x.DinhMucCoSo, x.LoaiDinhMuc, 0)));
        defaultSugar = new ComboBox { Name = "RecipeDefaultSugar", Width = 220, DropDownStyle = ComboBoxStyle.DropDownList,
            DisplayMember = "Label", ValueMember = "Id", BindingContext = new BindingContext(),
            DataSource = new[] { 0,25,50,75,100 }.Select(x => new Target(x, x + "%")).ToList() };
        defaultSugar.SelectedValue = (long)(item?.PhanTramDuongMacDinh ?? 50);
        ingredientChoices = new CheckedListBox { Name = "RecipeIngredientChoices", Width = 420, Height = 130, CheckOnClick = true,
            Font = CafeTheme.Body(10), BorderStyle = BorderStyle.FixedSingle };
        foreach (var n in ingredients.NguyenLieu.Where(n => n.DangSuDung)) ingredientChoices.Items.Add(new IngredientChoice(n));
        grid = Ui.Grid(new([], []), 220); grid.Name = "RecipeLines";
        ConfigureGrid();
        name.Name = "RecipeName"; target.Name = "RecipeTarget"; mode.Name = "RecipeMode";
        var add = Ui.Button("Thêm nguyên liệu đã chọn", () =>
        {
            foreach (var choice in ingredientChoices.CheckedItems.Cast<IngredientChoice>().ToList())
            {
                var n = choice.Item;
                if (lines.Any(l => l.MaNguyenLieu == n.MaNguyenLieu)) continue;
                var key = n.TenNguyenLieu.Trim().ToLowerInvariant();
                var type = mode.SelectedIndex == 1 ? "FIXED" : key is "đường" or "duong" ? "SUGAR" : key is "đá" or "da" ? "ICE" : "FIXED";
                lines.Add(new(n.MaNguyenLieu, n.MaDonViCoSo, 1, type, 0));
            }
            feedback.Text = ""; Render(lines.Count - 1);
        });
        add.Name = "RecipeLineAdd";
        var remove = Ui.Button("Bỏ dòng đang chọn", () => { if (grid.CurrentRow is { } row && row.Index < lines.Count) lines.RemoveAt(row.Index); Render(); });
        remove.Name = "RecipeLineRemove";
        save = Ui.Button("Lưu công thức", () => { }, true); save.Name = "RecipeSave"; save.Click += async (_, _) => await Save();
        save.Enabled = target.Items.Count > 0;
        var availableTemplates = (recipes ?? []).Where(r => r.TrangThai == "ACTIVE" && r.ThanhPhan.Count > 0 &&
            r.ThanhPhan.All(l => ingredients.NguyenLieu.Any(n => n.MaNguyenLieu == l.MaNguyenLieu && n.DangSuDung))).ToList();
        var source = new ComboBox { Name = "RecipeTemplate", Width = 620, DropDownStyle = ComboBoxStyle.DropDownList,
            DisplayMember = "Label", ValueMember = "Id", BindingContext = new BindingContext(), DataSource = new[] { new Target(0, "Tự nhập nguyên liệu") }
                .Concat(availableTemplates.Select(r => new Target(r.MaCongThuc, $"{r.TenDoiTuong ?? r.TenCongThuc} · {r.KichCo} · {(r.NhietDo == "HOT" ? "Nóng" : r.NhietDo == "ICED" ? "Lạnh" : "Topping")}"))).ToList() };
        var apply = Ui.Button("Lấy nguyên liệu", () =>
        {
            if (source.SelectedValue is not long selected || selected == 0) return;
            if (lines.Count > 0 && MessageBox.Show(this, "Thay nguyên liệu đang nhập bằng công thức được chọn?", "Lấy công thức mẫu", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            var template = availableTemplates.Single(r => r.MaCongThuc == selected);
            lines.Clear();
            lines.AddRange(template.ThanhPhan.Select(l => new RecipeLineInput(l.MaNguyenLieu,
                ingredients.NguyenLieu.Single(n => n.MaNguyenLieu == l.MaNguyenLieu).MaDonViCoSo, l.DinhMucCoSo, l.LoaiDinhMuc, 0)));
            defaultSugar.SelectedValue = (long)template.PhanTramDuongMacDinh;
            feedback.Text = ""; Render();
        });
        apply.Margin = new Padding(0, 30, 0, 8);
        apply.Name = "RecipeApplyTemplate";
        apply.Enabled = source.SelectedValue is long sourceId && sourceId != 0;
        source.SelectedValueChanged += (_, _) => apply.Enabled = source.SelectedValue is long sourceId && sourceId != 0;
        if (item is null)
        {
            string generatedName = "";
            void SuggestName()
            {
                if (name.Text.Length == 0 || name.Text == generatedName)
                { generatedName = target.Text[..Math.Min(target.Text.Length, 150)]; name.Text = generatedName; }
            }
            target.SelectedValueChanged += (_, _) => SuggestName(); SuggestName();
        }
        var guide = Ui.Label("Nhập lượng ngay trong bảng (g/ml). Lượng đường/đá khai báo ở mức 100%; hệ thống nhân mức khách chọn. Trừ kho đúng lượng dùng khi bắt đầu pha, không cộng hao hụt.", 9, false, CafeTheme.Muted);
        guide.MaximumSize = new Size(850, 0);
        AdminUi.Compose(this, Ui.Panel(Text,
            Ui.Row(Ui.Field("1. Công thức cho", mode), Ui.Field("Áp dụng cho", target)), targetHint,
            Ui.Row(Ui.Field("Tên công thức", name), Ui.Field("Ghi chú (không bắt buộc)", note)),
            Ui.Row(Ui.Field("Lấy nguyên liệu từ công thức có sẵn (không bắt buộc)", source), apply),
            Ui.Label("2. Chọn nhiều nguyên liệu và nhập lượng cho một ly / một phần", 11, true),
            Ui.Row(Ui.Field("Nguyên liệu (có thể chọn nhiều)", ingredientChoices), Ui.Stack(Ui.Field("Đường mặc định của công thức", defaultSugar), add, remove)),
            guide, grid, feedback), save, Ui.Button("Hủy", () => Close()));
        feedback.Name = "RecipeFeedback";
        FormClosing += (_, e) => { if (busy || uncertain) { e.Cancel = true; feedback.Text = "Kết quả chưa rõ. Chọn Kiểm tra / thử lại trước khi đóng."; } };
        Render();
    }

    /// <summary>Bảng nhập nhiều nguyên liệu: đơn vị chỉ đọc, lượng decimal4 và cách tính chọn bằng tiếng Việt.</summary>
    private void ConfigureGrid()
    {
        grid.DataSource = null; grid.Columns.Clear(); grid.ReadOnly = false;
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ingredient", HeaderText = "Nguyên liệu", ReadOnly = true, FillWeight = 35 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Unit", HeaderText = "Đơn vị", ReadOnly = true, FillWeight = 10 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "Lượng / ly", ValueType = typeof(decimal), FillWeight = 20, DefaultCellStyle = new() { Format = "0.####" } });
        grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "LineType", HeaderText = "Cách tính lượng", FillWeight = 35,
            DataSource = LineTypes.ToList(), DisplayMember = "Label", ValueMember = "Code", DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton });
        grid.CellValidating += (_, e) =>
        {
            if (rendering || e.RowIndex < 0 || e.ColumnIndex != 2) return;
            if (!TryAmount(e.FormattedValue, out var value) || value <= 0 || value != decimal.Round(value, 4) || value > 999999999999m)
            { e.Cancel = true; feedback.Text = "Lượng phải lớn hơn 0 và tối đa 4 số thập phân."; }
        };
        grid.CellParsing += (_, e) =>
        {
            if (e.ColumnIndex == 2 && TryAmount(e.Value, out var amount)) { e.Value = amount; e.ParsingApplied = true; }
        };
        grid.CurrentCellDirtyStateChanged += (_, _) => { if (grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewComboBoxCell) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        grid.CellValueChanged += (_, e) =>
        {
            if (rendering || e.RowIndex < 0 || e.RowIndex >= lines.Count) return;
            if (e.ColumnIndex == 2 && TryAmount(grid.Rows[e.RowIndex].Cells[2].Value, out var value) && value > 0 && value == decimal.Round(value,4))
                lines[e.RowIndex] = lines[e.RowIndex] with { DinhMuc = value };
            if (e.ColumnIndex == 3 && grid.Rows[e.RowIndex].Cells[3].Value is string type)
                lines[e.RowIndex] = lines[e.RowIndex] with { LoaiDinhMuc = type };
            feedback.Text = "";
        };
        grid.DataError += (_, e) => { e.ThrowException = false; e.Cancel = true; feedback.Text = "Kiểm tra lại lượng hoặc cách tính của dòng nguyên liệu."; };
    }

    /// <summary>Nhận dấu chấm hoặc phẩy thập phân, không hiểu nhầm 2,5 thành 25 theo thiết lập Windows.</summary>
    private static bool TryAmount(object? input, out decimal amount)
    {
        var text = Convert.ToString(input)?.Trim() ?? "";
        if (text.Contains(',') && text.Contains('.')) { amount = 0; return false; }
        return decimal.TryParse(text.Replace(',', '.'), System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture, out amount);
    }

    /// <summary>Giữ lượng có thể sửa trực tiếp, bỏ số0 thừa và đồng bộ các nguyên liệu đang chọn.</summary>
    private void Render(int? selectedIndex = null)
    {
        rendering = true;
        try
        {
            grid.Rows.Clear();
            foreach (var line in lines)
            {
                var n = ingredients.NguyenLieu.Single(n => n.MaNguyenLieu == line.MaNguyenLieu);
                var unit = ingredients.DonVi.Single(u => u.MaDonVi == line.MaDonVi).KyHieu;
                grid.Rows.Add(n.TenNguyenLieu, unit, line.DinhMuc, line.LoaiDinhMuc);
            }
            for (var i = 0; i < ingredientChoices.Items.Count; i++)
                ingredientChoices.SetItemChecked(i, lines.Any(l => l.MaNguyenLieu == ((IngredientChoice)ingredientChoices.Items[i]).Item.MaNguyenLieu));
            if (selectedIndex is >= 0 && selectedIndex < grid.Rows.Count) grid.CurrentCell = grid.Rows[selectedIndex.Value].Cells[2];
        }
        finally { rendering = false; }
    }

    /// <summary>Lưu hoặc tra receipt cùng payload; lỗi chưa rõ giữ nháp và không tạo mã mới.</summary>
    private async Task Save()
    {
        if (busy || target.SelectedValue is not long id || !grid.EndEdit()) return;
        pending ??= new RecipeInput(mode.SelectedIndex == 0 ? id : null, mode.SelectedIndex == 1 ? id : null,
            name.Text, note.Text, lines.ToList(), existing?.PhienBanDuLieu, operation = Guid.NewGuid(), (int)(long)defaultSugar.SelectedValue!);
        busy = true; save.Enabled = false; SetFields(false);
        try
        {
            await AdminUi.Recover(() => LiveSession.Api.SaveRecipeAsync(existing?.MaCongThuc, pending),
                () => LiveSession.Api.AdminReceiptAsync("recipes", operation));
            uncertain = false; busy = false; DialogResult = DialogResult.OK; Close();
        }
        catch (CafeApiException ex)
        {
            uncertain = ex.StatusCode is null || (int)ex.StatusCode >= 500;
            feedback.Text = ex.Message;
            if (!uncertain) { pending = null; SetFields(true); }
            save.Text = uncertain ? "Kiểm tra / thử lại" : "Lưu công thức";
        }
        finally { busy = false; if (!save.IsDisposed) save.Enabled = true; }
    }

    /// <summary>Khóa ô nhập/nút sửa dòng trong lúc ghi hoặc chưa rõ kết quả, vẫn cho dùng nút thử lại.</summary>
    private void SetFields(bool enabled)
    {
        foreach (var control in Descendants(this).Where(x => x is TextBox or ComboBox or NumericUpDown or Button))
            if (control != save) control.Enabled = enabled;
        grid.Enabled = ingredientChoices.Enabled = enabled;
        if (enabled && existing is not null) mode.Enabled = target.Enabled = false;
    }

    /// <summary>Duyệt control lồng nhau để không bỏ sót một trường có thể đổi payload đang chờ.</summary>
    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
}


/// <summary>Một màn hình cho một món: nhập nguyên liệu theo cột size, tách bảng nóng/lạnh; lưu nguyên tử.</summary>
public sealed class DishRecipeEditorForm : Form
{
    private sealed record Choice(long Id, string Label);
    private sealed class Row(long ingredient, string type)
    {
        public long Ingredient { get; } = ingredient;
        public string Type { get; } = type;
        public Dictionary<long, decimal> Amounts { get; } = [];
    }
    private readonly IngredientSnapshot ingredients;
    private readonly MenuAdminSnapshot menu;
    private readonly IReadOnlyList<RecipeView> recipes;
    private readonly ComboBox dish;
    private readonly ComboBox sugar;
    private readonly CheckedListBox choices;
    private readonly TabControl tabs = new() { Name = "RecipeSizeTabs", Height = 340, Width = 990 };
    private readonly List<Row> rows = [];
    private readonly List<DataGridView> grids = [];
    private readonly Button save;
    private readonly Label feedback = Ui.Label("", 9, false, CafeTheme.Danger);
    private List<MenuVariant> variants = [];
    private bool rendering, busy, uncertain;
    private RecipeDishInput? pending;

    /// <summary>Nạp các size đang khai báo của món; giữ công thức hiện có, không tạo cấu hình/giá mới.</summary>
    public DishRecipeEditorForm(IngredientSnapshot ingredients, MenuAdminSnapshot menu, IReadOnlyList<RecipeView> recipes, long? selectedDish = null)
    {
        this.ingredients = ingredients; this.menu = menu; this.recipes = recipes;
        Name = "DishRecipeEditorForm"; Text = "Công thức theo món"; AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1080, 850); MinimumSize = new Size(820, 620); Font = CafeTheme.Body();
        BackColor = CafeTheme.Cream; Padding = new Padding(16); StartPosition = FormStartPosition.CenterParent;
        dish = new ComboBox { Name = "RecipeDish", Width = 460, DropDownStyle = ComboBoxStyle.DropDownList,
            DisplayMember = "Label", ValueMember = "Id", BindingContext = new BindingContext(),
            DataSource = menu.Mon.OrderBy(m => m.ThuTuHienThi).ThenBy(m => m.MaMon).Select(m => new Choice(m.MaMon, m.TenMon)).ToList() };
        sugar = new ComboBox { Name = "RecipeDishSugar", Width = 220, DropDownStyle = ComboBoxStyle.DropDownList,
            DisplayMember = "Label", ValueMember = "Id", BindingContext = new BindingContext(),
            DataSource = new[] { 0,25,50,75,100 }.Select(x => new Choice(x, x + "%")).ToList() };
        choices = new CheckedListBox { Name = "RecipeDishIngredients", Width = 460, Height = 115, CheckOnClick = true, Font = CafeTheme.Body(10) };
        foreach (var n in ingredients.NguyenLieu.Where(n => n.DangSuDung))
            choices.Items.Add(new Choice(n.MaNguyenLieu, $"{n.TenNguyenLieu} ({(n.MaDonViCoSo == 1 ? "g" : "ml")})"));
        choices.DisplayMember = "Label";
        var add = Ui.Button("Thêm nguyên liệu đã chọn", AddIngredients); add.Name = "RecipeDishAdd";
        var remove = Ui.Button("Bỏ nguyên liệu", RemoveIngredient); remove.Name = "RecipeDishRemove";
        var copy = Ui.Button("Lấy lượng Lạnh sang Nóng", CopyCold); copy.Name = "RecipeCopyCold";
        save = Ui.Button("Lưu tất cả size", () => { }, true); save.Name = "RecipeDishSave";
        save.Click += async (_, _) => await Save();
        var guide = Ui.Label("Nhập lượng cho 1 ly ở từng size; 0 hoặc trống = không dùng. Đường/đá nhập lượng ở mức 100%, hệ thống tự tính theo mức khách chọn. Nguyên liệu khác dùng lượng cố định; món nóng không có đá.", 9, false, CafeTheme.Muted);
        guide.MaximumSize = new Size(990, 0); feedback.Name = "RecipeDishFeedback"; feedback.MaximumSize = new Size(990, 0);
        AdminUi.Compose(this, Ui.Panel("Công thức theo món",
            Ui.Row(Ui.Field("Món", dish), Ui.Field("Đường mặc định cho các size", sugar)),
            Ui.Row(Ui.Field("Chọn nhiều nguyên liệu", choices), Ui.Stack(add, remove, copy)),
            guide, tabs, feedback), save, Ui.Button("Hủy", () => Close()));
        dish.SelectedValueChanged += (_, _) => LoadDish();
        if (selectedDish is { } id) { dish.SelectedValue = id; dish.Enabled = false; }
        LoadDish();
        FormClosing += (_, e) => { if (busy || uncertain) { e.Cancel = true; feedback.Text = "Kết quả chưa rõ. Chọn Kiểm tra / thử lại trước khi đóng."; } };
    }

    /// <summary>Nạp toàn bộ lượng theo cấu hình; chỉ gợi ý từ lạnh cho cấu hình nóng còn thiếu, không ghi đè công thức cũ.</summary>
    private void LoadDish()
    {
        rows.Clear(); pending = null;
        variants = dish.SelectedValue is long id ? menu.CauHinh.Where(v => v.MaMon == id)
            .OrderBy(v => v.KichCo == "S" ? 0 : v.KichCo == "M" ? 1 : 2).ThenBy(v => v.NhietDo).ToList() : [];
        var current = recipes.Where(r => variants.Any(v => v.MaCauHinhMon == r.MaCauHinhMon)).ToList();
        sugar.SelectedValue = (long)(current.FirstOrDefault()?.PhanTramDuongMacDinh ?? 50);
        foreach (var recipe in current)
            foreach (var line in recipe.ThanhPhan)
            {
                var row = rows.FirstOrDefault(r => r.Ingredient == line.MaNguyenLieu && r.Type == line.LoaiDinhMuc);
                if (row is null) { row = new(line.MaNguyenLieu, line.LoaiDinhMuc); rows.Add(row); }
                row.Amounts[recipe.MaCauHinhMon!.Value] = line.DinhMucCoSo;
            }
        foreach (var hot in variants.Where(v => v.NhietDo == "HOT" && !current.Any(r => r.MaCauHinhMon == v.MaCauHinhMon)))
        {
            var cold = variants.FirstOrDefault(v => v.NhietDo == "ICED" && v.KichCo == hot.KichCo);
            if (cold is not null) foreach (var row in rows.Where(r => r.Type != "ICE")) row.Amounts[hot.MaCauHinhMon] = row.Amounts.GetValueOrDefault(cold.MaCauHinhMon);
        }
        feedback.Text = variants.Count == 0 ? "Món chưa có size/nóng lạnh. Tạo cấu hình món trước rồi mở lại bảng này." : "";
        Render(); save.Enabled = variants.Count > 0;
    }

    /// <summary>Thêm dòng với lượng trống, không tự bịa định mức cho các size.</summary>
    private void AddIngredients()
    {
        if (!Commit()) return;
        foreach (var item in choices.CheckedItems.Cast<Choice>())
        {
            if (rows.Any(r => r.Ingredient == item.Id)) continue;
            var key = ingredients.NguyenLieu.Single(n => n.MaNguyenLieu == item.Id).TenNguyenLieu.Trim().ToLowerInvariant();
            rows.Add(new(item.Id, key is "đường" or "duong" ? "SUGAR" : key is "đá" or "da" ? "ICE" : "FIXED"));
        }
        Render();
    }

    /// <summary>Bỏ nguyên liệu đang chọn khỏi bảng nóng và lạnh sau khi xác nhận sửa ô đang nhập.</summary>
    private void RemoveIngredient()
    {
        if (!Commit()) return;
        if (tabs.SelectedTab?.Controls.OfType<DataGridView>().Single().CurrentRow?.Tag is Row row) rows.Remove(row);
        Render();
    }

    /// <summary>Sao chép lượng lạnh sang nóng cùng size, bỏ đá; hỏi trước khi thay lượng đã nhập.</summary>
    private void CopyCold()
    {
        if (!Commit()) return;
        if (!variants.Any(v => v.NhietDo == "HOT") || !variants.Any(v => v.NhietDo == "ICED"))
        { feedback.Text = "Món cần có cả cấu hình Nóng và Lạnh để lấy lượng."; return; }
        if (rows.Any(r => variants.Any(v => v.NhietDo == "HOT" && r.Amounts.GetValueOrDefault(v.MaCauHinhMon) > 0)) &&
            MessageBox.Show(this, "Thay lượng ở bảng Nóng bằng lượng Lạnh cùng size? Dòng đá sẽ được bỏ.", "Lấy lượng", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        foreach (var hot in variants.Where(v => v.NhietDo == "HOT"))
        {
            var cold = variants.FirstOrDefault(v => v.NhietDo == "ICED" && v.KichCo == hot.KichCo);
            if (cold is not null) foreach (var row in rows) row.Amounts[hot.MaCauHinhMon] = row.Type == "ICE" ? 0 : row.Amounts.GetValueOrDefault(cold.MaCauHinhMon);
        }
        Render(); feedback.Text = "Đã lấy lượng sang Nóng. Bạn có thể chỉnh riêng từng ô trước khi lưu.";
    }

    /// <summary>Dựng hai bảng cùng các dòng nguyên liệu, mỗi cột lượng tương ứng một size thực của món.</summary>
    private void Render()
    {
        var previous = tabs.SelectedTab?.Name;
        rendering = true;
        try
        {
            foreach (var old in tabs.TabPages.Cast<TabPage>().ToArray()) { tabs.TabPages.Remove(old); old.Dispose(); }
            grids.Clear();
            foreach (var temperature in new[] { "ICED", "HOT" })
            {
                var columns = variants.Where(v => v.NhietDo == temperature).ToList();
                if (columns.Count == 0) continue;
                var page = new TabPage(temperature == "HOT" ? "Nóng · không đá" : "Lạnh") { Name = temperature, BackColor = CafeTheme.Cream };
                var grid = Ui.Grid(new([], []), 300); grid.Name = "RecipeMatrix" + temperature; grid.Dock = DockStyle.Fill;
                grid.DataSource = null; grid.Columns.Clear(); grid.ReadOnly = false; grid.AllowUserToAddRows = false;
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ingredient", HeaderText = "Nguyên liệu", ReadOnly = true, FillWeight = 30 });
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Unit", HeaderText = "Đơn vị", ReadOnly = true, FillWeight = 10 });
                foreach (var v in columns)
                    grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Size" + v.MaCauHinhMon, HeaderText = "Size " + v.KichCo, Tag = v,
                        ValueType = typeof(decimal), FillWeight = 20, DefaultCellStyle = new() { Format = "0.####", NullValue = "" } });
                foreach (var row in rows.Where(r => temperature != "HOT" || r.Type != "ICE"))
                {
                    var n = ingredients.NguyenLieu.Single(n => n.MaNguyenLieu == row.Ingredient);
                    var values = new List<object?> { n.TenNguyenLieu, n.MaDonViCoSo == 1 ? "g" : "ml" };
                    values.AddRange(columns.Select(v => (object?)(row.Amounts.GetValueOrDefault(v.MaCauHinhMon) is var amount && amount > 0 ? amount : null)));
                    grid.Rows[grid.Rows.Add(values.ToArray())].Tag = row;
                }
                WireGrid(grid); grids.Add(grid); page.Controls.Add(grid); tabs.TabPages.Add(page);
            }
            if (previous is not null && tabs.TabPages.ContainsKey(previous)) tabs.SelectedTab = tabs.TabPages[previous];
            for (var i = 0; i < choices.Items.Count; i++) choices.SetItemChecked(i, rows.Any(r => r.Ingredient == ((Choice)choices.Items[i]).Id));
        }
        finally { rendering = false; }
    }

    /// <summary>Kiểm số thập phân và đồng bộ lượng về nháp dùng chung giữa các tab.</summary>
    private void WireGrid(DataGridView grid)
    {
        grid.CellValidating += (_, e) =>
        {
            if (rendering || e.RowIndex < 0 || e.ColumnIndex < 2) return;
            if (!Amount(e.FormattedValue, out var parsedAmount)) { e.Cancel = true; feedback.Text = "Lượng không âm, tối đa 4 số thập phân. Để trống nếu không dùng."; }
        };
        grid.CellParsing += (_, e) => { if (e.ColumnIndex >= 2 && Amount(e.Value, out var value)) { e.Value = value; e.ParsingApplied = true; } };
        grid.CellValueChanged += (_, e) =>
        {
            if (rendering || e.RowIndex < 0 || grid.Rows[e.RowIndex].Tag is not Row row) return;
            if (e.ColumnIndex >= 2 && grid.Columns[e.ColumnIndex].Tag is MenuVariant v && Amount(grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value, out var value)) row.Amounts[v.MaCauHinhMon] = value;
            feedback.Text = "";
        };
        grid.DataError += (_, e) => { e.ThrowException = false; e.Cancel = true; feedback.Text = "Kiểm tra lượng nguyên liệu."; };
    }

    /// <summary>Nhận dấu phẩy/chấm thập phân; ô trống tương đương không dùng, không nhận phân cách hàng nghìn.</summary>
    private static bool Amount(object? input, out decimal amount)
    {
        var text = Convert.ToString(input)?.Trim() ?? "";
        if (text.Length == 0) { amount = 0; return true; }
        if (text.Contains(',') && text.Contains('.')) { amount = 0; return false; }
        return decimal.TryParse(text.Replace(',', '.'), System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out amount)
            && amount >= 0 && amount <= 999999999999m && amount == decimal.Round(amount, 4);
    }

    /// <summary>Chốt ô đang gõ trước khi thêm/bỏ/sao chép/lưu để không mất giá trị cuối.</summary>
    private bool Commit() => grids.All(g => g.EndEdit());

    /// <summary>Tạo lệnh một món, giữ version từng công thức và receipt duy nhất khi thử lại.</summary>
    private async Task Save()
    {
        if (busy || dish.SelectedValue is not long id || !Commit()) return;
        if (pending is null)
        {
            var entries = new List<RecipeDishVariantInput>();
            foreach (var v in variants)
            {
                var recipe = recipes.FirstOrDefault(r => r.MaCauHinhMon == v.MaCauHinhMon);
                var lines = rows.Where(r => (v.NhietDo != "HOT" || r.Type != "ICE") && r.Amounts.GetValueOrDefault(v.MaCauHinhMon) > 0)
                    .Select(r => new RecipeLineInput(r.Ingredient, ingredients.NguyenLieu.Single(n => n.MaNguyenLieu == r.Ingredient).MaDonViCoSo,
                        r.Amounts[v.MaCauHinhMon], r.Type, 0)).ToList();
                if (lines.Count == 0) { feedback.Text = $"Nhập ít nhất một nguyên liệu cho size {v.KichCo} · {(v.NhietDo == "HOT" ? "Nóng" : "Lạnh")}."; return; }
                entries.Add(new(recipe?.MaCongThuc, new(v.MaCauHinhMon, null,
                    recipe?.TenCongThuc ?? $"{dish.Text} · {v.KichCo} · {(v.NhietDo == "HOT" ? "Nóng" : "Lạnh")}", recipe?.GhiChu,
                    lines, recipe?.PhienBanDuLieu, Guid.Empty, (int)(long)sugar.SelectedValue!)));
            }
            if (entries.Count == 0) return;
            pending = new(id, entries, Guid.NewGuid());
        }
        busy = true; SetFields(false);
        try
        {
            await AdminUi.Recover(() => LiveSession.Api.SaveDishRecipesAsync(pending), () => LiveSession.Api.AdminReceiptAsync("recipes", pending.MaThaoTac));
            uncertain = false; busy = false; DialogResult = DialogResult.OK; Close();
        }
        catch (CafeApiException ex)
        {
            uncertain = ex.StatusCode is null || (int)ex.StatusCode >= 500;
            feedback.Text = ex.Message;
            if (!uncertain) { pending = null; SetFields(true); }
            save.Text = uncertain ? "Kiểm tra / thử lại" : "Lưu tất cả size";
        }
        finally { busy = false; if (!save.IsDisposed) save.Enabled = true; }
    }

    /// <summary>Giữ nháp bất biến trong khi chờ kết quả; chỉ nút kiểm tra/thử lại được hoạt động.</summary>
    private void SetFields(bool enabled)
    {
        dish.Enabled = false; sugar.Enabled = choices.Enabled = tabs.Enabled = enabled;
        foreach (var button in Descendants(this).OfType<Button>()) if (button != save) button.Enabled = enabled;
        save.Enabled = enabled;
    }

    /// <summary>Duyệt control lồng để khóa thao tác thay đổi nháp đang được lưu.</summary>
    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
}
