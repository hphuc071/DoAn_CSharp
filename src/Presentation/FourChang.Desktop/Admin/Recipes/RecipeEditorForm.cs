using FourChang.Desktop.Admin.Ingredients;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.ApiClients;

namespace FourChang.Desktop.Admin.Recipes;

/// <summary>Editor nháp BOM dùng control có cấu trúc, không nhập định mức bằng JSON hoặc ghi chú tự do.</summary>
public sealed class RecipeEditorForm : Form
{
    private sealed record Target(long Id, string Label);
    private readonly RecipeView? existing;
    private readonly IngredientSnapshot ingredients;
    private readonly List<RecipeLineInput> lines = [];
    private readonly TextBox name;
    private readonly TextBox note;
    private readonly ComboBox mode;
    private readonly ComboBox target;
    private readonly DataGridView grid;
    private readonly Button save;
    private readonly Guid operation = Guid.NewGuid();

    /// <summary>Chọn đúng cấu hình hoặc topping, đơn vị và loại FIXED/SUGAR/ICE; lưu nháp độc lập công bố.</summary>
    public RecipeEditorForm(IngredientSnapshot ingredients, MenuAdminSnapshot menu, RecipeView? item)
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        this.ingredients = ingredients; existing = item;
        Text = item is null ? "Tạo nháp BOM" : "Sửa nháp BOM"; ClientSize = new Size(980, 760); MinimumSize = new Size(760, 600);
        Font = CafeTheme.Body(); BackColor = CafeTheme.Cream; Padding = new Padding(16); AutoScroll = true; StartPosition = FormStartPosition.CenterParent;
        name = Ui.Input("Tên công thức", item?.TenCongThuc ?? "", 420); note = Ui.Input("Ghi chú quản trị", item?.GhiChu ?? "", 360);
        name.MaxLength = 150; note.MaxLength = 500;
        mode = Ui.Select("Cấu hình món", "Topping"); mode.SelectedIndex = item?.MaMonThem is not null ? 1 : 0;
        target = new ComboBox { Width = 480, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Label", ValueMember = "Id" };
        void Targets()
        {
            target.DataSource = mode.SelectedIndex == 0
                ? menu.CauHinh.Select(v => new Target(v.MaCauHinhMon, $"{menu.Mon.Single(m => m.MaMon == v.MaMon).TenMon} · {v.KichCo} · {v.NhietDo}")).ToList()
                : menu.MonThem.Select(t => new Target(t.MaMonThem, t.TenMonThem)).ToList();
        }
        Targets(); mode.SelectedIndexChanged += (_, _) => Targets();
        if (item is not null) { target.SelectedValue = item.MaCauHinhMon ?? item.MaMonThem; mode.Enabled = target.Enabled = false; }
        if (item is not null)
            lines.AddRange(item.ThanhPhan.Select(x => new RecipeLineInput(x.MaNguyenLieu, ingredients.NguyenLieu.Single(n => n.MaNguyenLieu == x.MaNguyenLieu).MaDonViCoSo, x.DinhMucCoSo, x.LoaiDinhMuc, x.TyLeHaoHut)));
        var ingredient = new ComboBox { Width = 250, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "TenNguyenLieu", ValueMember = "MaNguyenLieu", DataSource = ingredients.NguyenLieu.Where(x => x.DangSuDung).ToList() };
        var unit = IngredientEditorForm.UnitCombo(ingredients); unit.Name = "RecipeIngredientUnit"; unit.Enabled = false;
        ingredient.Name = "RecipeIngredient";
        // Định mức nhập trực tiếp theo đơn vị của nguyên liệu, không chọn chéo g/ml hoặc tự quy đổi.
        void FollowIngredient()
        {
            if (ingredient.SelectedValue is long id) unit.SelectedValue = ingredients.NguyenLieu.Single(x => x.MaNguyenLieu == id).MaDonViCoSo;
        }
        ingredient.SelectedValueChanged += (_, _) => FollowIngredient(); FollowIngredient();
        var amount = IngredientEditorForm.Amount(1);
        var loss = IngredientEditorForm.Amount(0); loss.DecimalPlaces = 6; loss.Maximum = 0.999999m; loss.Width = 150;
        var type = Ui.Select("FIXED", "SUGAR", "ICE");
        grid = Ui.Grid(new([], []), 160);
        var add = Ui.Button("Thêm / sửa thành phần", () =>
        {
            if (ingredient.SelectedValue is not long id || unit.SelectedValue is not long unitId) return;
            lines.RemoveAll(x => x.MaNguyenLieu == id && x.LoaiDinhMuc == type.Text);
            lines.Add(new(id, unitId, amount.Value, type.Text, loss.Value)); Render();
        });
        var remove = Ui.Button("Bỏ dòng", () => { if (grid.CurrentRow is { } row && row.Index < lines.Count) lines.RemoveAt(row.Index); Render(); });
        save = Ui.Button("Lưu nháp", () => { }, true); save.Click += async (_, _) => await Save();
        AdminUi.Compose(this, Ui.Panel(Text, Ui.Row(Ui.Field("Tên công thức", name), Ui.Field("Loại", mode)), Ui.Row(Ui.Field("Áp dụng cho", target), Ui.Field("Ghi chú quản trị", note)), grid,
            Ui.Row(Ui.Field("Nguyên liệu", ingredient), Ui.Field("Đơn vị nguyên liệu", unit), Ui.Field("Định mức", amount)),
            Ui.Row(Ui.Field("Loại định mức", type), Ui.Field("Tỷ lệ hao hụt 0–<1", loss), add, remove)), save, Ui.Button("Hủy", () => Close()));
        Render();
    }

    /// <summary>Hiển thị định mức có đơn vị rõ ràng; không chỉnh trực tiếp giá trị cell không được xác thực.</summary>
    private void Render()
    {
        grid.DataSource = lines.Select(x => new
        {
            NguyenLieu = ingredients.NguyenLieu.Single(n => n.MaNguyenLieu == x.MaNguyenLieu).TenNguyenLieu,
            DonVi = ingredients.DonVi.Single(u => u.MaDonVi == x.MaDonVi).KyHieu,
            DinhMuc = x.DinhMuc,
            Loai = x.LoaiDinhMuc,
            HaoHut = x.TyLeHaoHut
        }).ToList();
        var captions = new[] { "Nguyên liệu", "Đơn vị", "Định mức", "Loại định mức", "Tỷ lệ hao hụt" };
        for (var index = 0; index < grid.Columns.Count; index++) grid.Columns[index].HeaderText = captions[index];
    }

    /// <summary>Gửi nháp với version/mã lệnh; giữ nội dung khi validation hoặc mạng lỗi.</summary>
    private async Task Save()
    {
        if (target.SelectedValue is not long id) return;
        save.Enabled = false;
        try
        {
            var input = new RecipeInput(mode.SelectedIndex == 0 ? id : null, mode.SelectedIndex == 1 ? id : null, name.Text, note.Text, lines.ToList(), existing?.PhienBanDuLieu, operation);
            await AdminUi.Recover(() => LiveSession.Api.SaveRecipeAsync(existing?.MaCongThuc, input), () => LiveSession.Api.AdminReceiptAsync("recipes", operation));
            DialogResult = DialogResult.OK; Close();
        }
        catch (CafeApiException ex) { AdminUi.Error(this, ex); }
        finally { if (!save.IsDisposed) save.Enabled = true; }
    }
}
