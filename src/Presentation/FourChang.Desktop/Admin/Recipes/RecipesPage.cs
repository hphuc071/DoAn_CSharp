using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Admin.Menu;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Desktop.Admin.Recipes;

/// <summary>Quản lý công thức theo món/cấu hình; hiển thị cả cấu hình chưa có công thức để thêm ngay.</summary>
public sealed class RecipesPage : AdminWorkspacePage<RecipeView>
{
    private readonly ComboBox group = Ui.Select("Tất cả", "Món", "Topping");
    private readonly ComboBox state = Ui.Select("Tất cả", "Chưa có công thức", "Đã có công thức");
    private MenuAdminSnapshot menu = new([], [], [], [], []);

    /// <summary>Dựng danh sách dễ đọc, bộ lọc và thao tác thêm/sửa ngay theo dòng được chọn.</summary>
    public RecipesPage() : base("Công thức theo món", "Mỗi món một bảng nguyên liệu theo size, tách Nóng/Lạnh. Topping có công thức riêng.", true, "THỰC ĐƠN / CÔNG THỨC")
    {
        Name = "RecipesPage"; Grid.Name = "RecipesGrid";
        Column("TenDoiTuong", "Món / topping", 28); Column("KichCo", "Size", 7); Column("NhietDo", "Nhiệt độ", 14);
        Column("TenCongThuc", "Tên công thức", 25); Column("ThanhPhan", "Số loại", 13); Column("TrangThai", "Tình trạng", 23);
        Filters.MinimumSize = new Size(640, 0);
        group.Name = "RecipeGroupFilter"; state.Name = "RecipeStateFilter"; group.Width = 150; state.Width = 250;
        Filters.Controls.Add(Ui.Field("Áp dụng cho", group)); Filters.Controls.Add(Ui.Field("Tình trạng", state));
        group.SelectedIndexChanged += (_, _) => Render(); state.SelectedIndexChanged += (_, _) => Render();
        SearchBox.PlaceholderText = "Tìm tên món, topping hoặc công thức…";
        AddAction(Actions, "Thêm công thức", () => Edit(null), true).Name = "RecipeAdd";
        var edit = AddAction(Actions, "Thêm / sửa theo dòng", async () => { if (Selected() is { } item) await Edit(item); });
        AddAction(Actions, "Thêm size / nóng-lạnh", CreateVariant).Name = "RecipeAddVariant";
        edit.Name = "RecipeEdit"; Grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) edit.PerformClick(); };
        Grid.CellFormatting += (_, args) =>
        {
            if (args.RowIndex < 0 || args.ColumnIndex < 0 || Grid.Rows[args.RowIndex].DataBoundItem is not RecipeView recipe) return;
            switch (Grid.Columns[args.ColumnIndex].DataPropertyName)
            {
                case "TrangThai": args.Value = recipe.TrangThai == "MISSING" ? "Chưa có công thức" : recipe.TrangThai == "PARTIAL" ? "Còn size chưa có" : recipe.TrangThai == "ACTIVE" ? "Đang áp dụng" : "Chưa áp dụng"; break;
                case "ThanhPhan": args.Value = recipe.ThanhPhan.Count.ToString(); break;
                case "NhietDo": args.Value = recipe.NhietDo == "HOT" ? "Nóng" : recipe.NhietDo == "ICED" ? "Lạnh" : recipe.NhietDo ?? "—"; break;
                default: return;
            }
            args.FormattingApplied = true;
        };
    }

    /// <summary>Ghép công thức đang có với cấu hình/topping còn thiếu; dòng thiếu chỉ là dữ liệu màn hình, không ghi DB.</summary>
    protected override async Task<IReadOnlyList<RecipeView>> Fetch()
    {
        menu = await LiveSession.Api.GetAdminMenuAsync();
        var recipes = (await LiveSession.Api.GetRecipesAsync()).ToList();
        foreach (var variant in menu.CauHinh.Where(v => !recipes.Any(x => x.MaCauHinhMon == v.MaCauHinhMon)))
            recipes.Add(new(0, variant.MaCauHinhMon, null, 0, "Chọn để thêm công thức", "MISSING", null, null, null, [], [],
                menu.Mon.Single(m => m.MaMon == variant.MaMon).TenMon, variant.KichCo, variant.NhietDo));
        foreach (var top in menu.MonThem.Where(t => !recipes.Any(x => x.MaMonThem == t.MaMonThem)))
            recipes.Add(new(0, null, top.MaMonThem, 0, "Chọn để thêm công thức", "MISSING", null, null, null, [], [], top.TenMonThem));
        // Một dòng mỗi món; bên trong vẫn giữ BOM riêng từng cấu hình để không đổi cách tính kho.
        var dishes = recipes.Where(r => r.MaCauHinhMon is not null).GroupBy(r => menu.CauHinh.Single(v => v.MaCauHinhMon == r.MaCauHinhMon).MaMon)
            .Select(g => g.First() with { TenCongThuc = "Bảng nguyên liệu theo size",
                KichCo = string.Join(" / ", g.Select(r => r.KichCo).Distinct().Order()),
                NhietDo = string.Join(" / ", g.Select(r => r.NhietDo == "HOT" ? "Nóng" : "Lạnh").Distinct()),
                TrangThai = g.All(r => r.TrangThai == "MISSING") ? "MISSING" : g.Any(r => r.TrangThai == "MISSING") ? "PARTIAL" : "ACTIVE" });
        return dishes.Concat(recipes.Where(r => r.MaMonThem is not null)).OrderBy(x => x.TrangThai == "ACTIVE" ? 1 : 0).ThenBy(x => x.TenDoiTuong).ToList();
    }

    /// <summary>Lọc theo nhóm, trạng thái và tên món/công thức thay vì chỉ mã kỹ thuật.</summary>
    protected override bool Match(RecipeView item, string query) =>
        (group.SelectedIndex == 0 || (group.SelectedIndex == 1) == (item.MaMonThem is null)) &&
        (state.SelectedIndex == 0 || (state.SelectedIndex == 1) == (item.TrangThai is "MISSING" or "PARTIAL")) &&
        (query.Length == 0 || $"{item.TenDoiTuong} {item.TenCongThuc} {item.KichCo}".Contains(query, StringComparison.CurrentCultureIgnoreCase));

    /// <summary>Tái sử dụng form cấu hình; lưu xong mở ngay công thức đúng size mới, không bắt chuyển tab tìm lại.</summary>
    private async Task CreateVariant()
    {
        using var form = new MenuVariantBatchForm(await LiveSession.Api.GetAdminMenuAsync());
        if (form.ShowDialog(FindForm()) != DialogResult.OK || form.SavedDishId is not { } dish) return;
        using var recipe = new DishRecipeEditorForm(await LiveSession.Api.GetIngredientsAsync(),
            await LiveSession.Api.GetAdminMenuAsync(), await LiveSession.Api.GetRecipesAsync(), dish);
        recipe.ShowDialog(FindForm());
        await Reload();
    }

    /// <summary>Dòng thiếu mở thêm đúng cấu hình; dòng đã có mở sửa. Nạp nguồn hiện tại trước khi dùng mẫu.</summary>
    private async Task Edit(RecipeView? item)
    {
        var ingredients = await LiveSession.Api.GetIngredientsAsync();
        var latestMenu = await LiveSession.Api.GetAdminMenuAsync();
        var recipes = await LiveSession.Api.GetRecipesAsync();
        var missing = item?.TrangThai == "MISSING";
        using Form form = item?.MaMonThem is not null
            ? new RecipeEditorForm(ingredients, latestMenu, missing ? null : item, recipes, missing ? item : null)
            : new DishRecipeEditorForm(ingredients, latestMenu, recipes, item?.MaCauHinhMon is { } variant
                ? latestMenu.CauHinh.Single(v => v.MaCauHinhMon == variant).MaMon : null);
        if (form.ShowDialog(FindForm()) == DialogResult.OK) await Reload();
    }
}
