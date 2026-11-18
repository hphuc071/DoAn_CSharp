using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Admin.Recipes;

/// <summary>Mỗi món một dòng; định lượng topping được quản lý riêng ở tab Topping.</summary>
public sealed class RecipesPage : AdminWorkspacePage<RecipeView>
{
    private readonly ComboBox state = Ui.Select("Tất cả", "Chưa có công thức", "Đã có công thức");
    private readonly Dictionary<long, int> ingredientCounts = [];
    private MenuAdminSnapshot menu = new([], [], [], [], []);

    public RecipesPage() : base("Công thức theo món", "Chọn món để xem hoặc chỉnh nguyên liệu theo size và nóng/lạnh.", true, "THỰC ĐƠN / CÔNG THỨC")
    {
        Name = "RecipesPage"; Grid.Name = "RecipesGrid";
        Column("TenDoiTuong", "Tên món", 34); Column("KichCo", "Size", 16);
        Column("NhietDo", "Nóng / lạnh", 19); Column("ThanhPhan", "Nguyên liệu", 16);
        Column("TrangThai", "Tình trạng", 25);
        Grid.Columns[0].MinimumWidth = 180; Grid.Columns[0].DefaultCellStyle.Font = CafeTheme.Strong();
        Grid.Columns[1].MinimumWidth = 125; Grid.Columns[2].MinimumWidth = 140;
        Grid.Columns[3].MinimumWidth = 125; Grid.Columns[4].MinimumWidth = 190;
        Grid.Columns[3].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        Grid.RowTemplate.Height = 56; Grid.ColumnHeadersHeight = 44;
        Grid.ShowCellToolTips = true; Grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = CafeTheme.Soft;
        Filters.MinimumSize = new Size(640, 0);
        state.Name = "RecipeStateFilter"; state.Width = 230;
        Filters.Controls.Add(Ui.Field("Tình trạng", state));
        state.SelectedIndexChanged += (_, _) => Render();
        SearchBox.PlaceholderText = "Tìm tên món…";
        AddAction(Actions, "Thêm công thức", () => Edit(null), true).Name = "RecipeAdd";
        var edit = AddAction(Actions, "Chỉnh sửa", async () => { if (Selected() is { } item) await Edit(item); });
        edit.Name = "RecipeEdit";
        Grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) edit.PerformClick(); };
        Grid.CellFormatting += (_, args) =>
        {
            if (args.RowIndex < 0 || args.ColumnIndex < 0 || Grid.Rows[args.RowIndex].DataBoundItem is not RecipeView recipe) return;
            switch (Grid.Columns[args.ColumnIndex].DataPropertyName)
            {
                case "TrangThai": args.Value = StatusText(recipe); break;
                case "ThanhPhan": args.Value = ingredientCounts.GetValueOrDefault(recipe.MaCauHinhMon ?? 0).ToString(); break;
                default: return;
            }
            args.FormattingApplied = true;
        };
        Grid.CellPainting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || Grid.Columns[e.ColumnIndex].DataPropertyName != "TrangThai" ||
                Grid.Rows[e.RowIndex].DataBoundItem is not RecipeView recipe || e.Graphics is null) return;
            e.PaintBackground(e.ClipBounds, true);
            var ready = recipe.TrangThai == "ACTIVE";
            var bounds = new Rectangle(e.CellBounds.X + 10, e.CellBounds.Y + 12, e.CellBounds.Width - 20, e.CellBounds.Height - 24);
            using var fill = new SolidBrush(ready ? CafeTheme.Sage : CafeTheme.Amber);
            using var path = CafeTheme.Rounded(bounds, 8);
            e.Graphics.FillPath(fill, path);
            TextRenderer.DrawText(e.Graphics, StatusText(recipe), Grid.Font, bounds, ready ? CafeTheme.SuccessInk : CafeTheme.AmberInk,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            e.Handled = true;
        };
    }

    private static string StatusText(RecipeView recipe) => recipe.TrangThai switch
    {
        "MISSING" => "Chưa có công thức", "PARTIAL" => "Chưa đủ công thức", "ACTIVE" => "Đã có công thức", _ => "Chưa áp dụng"
    };

    /// <summary>Chỉ ghép công thức món; không thêm hoặc sửa dữ liệu công thức topping.</summary>
    protected override async Task<IReadOnlyList<RecipeView>> Fetch()
    {
        var latestMenu = await LiveSession.Api.GetAdminMenuAsync();
        var recipes = (await LiveSession.Api.GetRecipesAsync()).Where(r => r.MaCauHinhMon is not null && r.MaMonThem is null).ToList();
        foreach (var variant in latestMenu.CauHinh.Where(v => !recipes.Any(x => x.MaCauHinhMon == v.MaCauHinhMon)))
            recipes.Add(new(0, variant.MaCauHinhMon, null, 0, "Chọn để thêm công thức", "MISSING", null, null, null, [], [],
                latestMenu.Mon.Single(m => m.MaMon == variant.MaMon).TenMon, variant.KichCo, variant.NhietDo));
        var counts = new Dictionary<long, int>();
        var dishes = recipes.GroupBy(r => latestMenu.CauHinh.Single(v => v.MaCauHinhMon == r.MaCauHinhMon).MaMon).Select(g =>
        {
            var first = g.First();
            counts[first.MaCauHinhMon!.Value] = g.SelectMany(r => r.ThanhPhan).Select(l => l.MaNguyenLieu).Distinct().Count();
            return first with {
                KichCo = string.Join(" / ", g.Select(r => r.KichCo).Distinct().OrderBy(s => s == "S" ? 0 : s == "M" ? 1 : s == "L" ? 2 : 3).ThenBy(s => s)),
                NhietDo = string.Join(" / ", g.Select(r => r.NhietDo).Distinct().OrderBy(t => t == "HOT" ? 0 : 1).Select(t => t == "HOT" ? "Nóng" : "Lạnh")),
                TrangThai = g.All(r => r.TrangThai == "MISSING") ? "MISSING" : g.Any(r => r.TrangThai == "MISSING") ? "PARTIAL" : "ACTIVE"
            };
        }).OrderBy(x => x.TrangThai == "ACTIVE" ? 1 : 0).ThenBy(x => x.TenDoiTuong).ToList();
        menu = latestMenu; ingredientCounts.Clear();
        foreach (var (id, count) in counts) ingredientCounts[id] = count;
        return dishes;
    }

    protected override bool Match(RecipeView item, string query) =>
        (state.SelectedIndex == 0 || (state.SelectedIndex == 1) == (item.TrangThai is "MISSING" or "PARTIAL")) &&
        (query.Length == 0 || $"{item.TenDoiTuong} {item.KichCo}".Contains(query, StringComparison.CurrentCultureIgnoreCase));

    private async Task Edit(RecipeView? item)
    {
        var ingredients = await LiveSession.Api.GetIngredientsAsync();
        var latestMenu = await LiveSession.Api.GetAdminMenuAsync();
        var recipes = await LiveSession.Api.GetRecipesAsync();
        using var form = new DishRecipeEditorForm(ingredients, latestMenu, recipes, item?.MaCauHinhMon is { } variant
            ? latestMenu.CauHinh.Single(v => v.MaCauHinhMon == variant).MaMon : null);
        if (form.ShowDialog(FindForm()) == DialogResult.OK) await Reload();
    }
}
