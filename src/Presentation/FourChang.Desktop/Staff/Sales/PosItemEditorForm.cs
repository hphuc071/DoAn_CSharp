using System.Text.Json;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.Contracts.Customer;

namespace FourChang.Desktop.Staff.Sales;

/// <summary>Tách size, nhiệt độ, đường, đá trên UI; vẫn gửi đúng ID biến thể và cặp pha chế do API công bố.</summary>
public sealed class PosItemEditorForm : Form
{
    private readonly MenuProduct product;
    private readonly List<MenuVariantItem> variants;
    private readonly PosDraftLine? old;
    private readonly ComboBox size = Ui.Select();
    private readonly ComboBox temperature = Ui.Select();
    private readonly ComboBox sugar = Ui.Select();
    private readonly ComboBox ice = Ui.Select();
    private readonly NumericUpDown quantity = new() { Minimum = 1, Maximum = 99, Value = 1, Width = 150 };
    private readonly TextBox note = Ui.Input("Ví dụ: ít ngọt, giao cùng các món khác…", "", 400);
    private readonly FlowLayoutPanel toppings = Ui.Row();
    private readonly Label total = Ui.Label("", 14, true);
    private readonly Label message = Ui.Label("", 9, false, CafeTheme.Danger);
    private readonly Button save;
    private readonly Control iceField;
    private readonly List<(MenuToppingItem Top, NumericUpDown Count)> portions = [];
    private MenuVariantItem? selected;
    private bool updating;
    private bool busy;
    public PosDraftLine? Result { get; private set; }
    private sealed record Choice<T>(string Label, T Value) { public override string ToString() => Label; }

    /// <summary>Dựng form sáng, có hai hàng tùy chọn riêng, topping/số lượng và tổng tiền ở footer cố định.</summary>
    public PosItemEditorForm(MenuProduct item, List<MenuVariantItem> data, PosDraftLine? existing = null)
    {
        product = item; variants = data.Where(x => x.Available).ToList(); old = existing;
        Name = "PosItemEditor"; Text = "Chọn cấu hình · " + item.Name;
        ClientSize = new(760, 710); MinimumSize = new(700, 620); StartPosition = FormStartPosition.CenterParent;
        Font = CafeTheme.Body(); BackColor = CafeTheme.Cream;
        size.Name = "PosSize"; temperature.Name = "PosTemperature"; sugar.Name = "PosSugar"; ice.Name = "PosIce";
        size.Width = temperature.Width = sugar.Width = ice.Width = 290;
        quantity.Name = "PosQuantity"; note.Name = "PosNote"; note.MaxLength = 500;
        quantity.Value = existing?.Quantity ?? 1; note.Text = existing?.Note ?? "";
        save = Ui.Button(existing is null ? "Thêm vào đơn" : "Lưu cấu hình", () => { }, true);
        save.Name = "PosAddConfiguredItem"; save.Click += async (_, _) => await Save();
        iceField = Ui.Field("Mức đá", ice); iceField.Name = "PosIceField";
        var body = Ui.Panel(item.Name, Ui.Label("Chọn từng tùy chọn theo khẩu vị của khách.", 10, false, CafeTheme.Muted),
            Ui.Row(Ui.Field("Kích cỡ", size), Ui.Field("Nóng / lạnh", temperature)),
            Ui.Row(Ui.Field("Mức đường", sugar), iceField),
            Ui.Label("Topping · số phần cho mỗi ly", 11, true), toppings,
            Ui.Row(Ui.Field("Số ly", quantity), Ui.Field("Ghi chú cho món (không bắt buộc)", note)), message);
        total.Name = "PosConfiguredTotal";
        total.Width = 290; total.Height = 42; total.AutoSize = false; total.TextAlign = ContentAlignment.MiddleLeft;
        AdminUi.Compose(this, body, total, save, Ui.Button("Đóng", () => Close()));
        foreach (var value in variants.Select(x => x.KichCo).Distinct().OrderBy(x => x == "S" ? 0 : x == "M" ? 1 : 2)) size.Items.Add(value);
        // Mở đúng cấu hình đang niêm yết trên thẻ; khi sửa giữ cấu hình của dòng giỏ.
        var initial = existing?.Variant ?? variants.FirstOrDefault(x => x.MaCauHinhMon == item.Id) ?? variants.FirstOrDefault();
        size.SelectedItem = initial?.KichCo;
        if (size.SelectedIndex < 0 && size.Items.Count > 0) size.SelectedIndex = 0;
        RefreshTemperatures(initial?.NhietDo, existing?.Brew);
        size.SelectedIndexChanged += (_, _) => { if (!updating) RefreshTemperatures((temperature.SelectedItem as Choice<string>)?.Value, CurrentBrew()); };
        temperature.SelectedIndexChanged += (_, _) => { if (!updating) RefreshVariant(CurrentBrew()); };
        sugar.SelectedIndexChanged += (_, _) => { if (!updating) RefreshIce((ice.SelectedItem as Choice<int>)?.Value); };
        ice.SelectedIndexChanged += (_, _) => { if (!updating) RefreshPrice(); };
        quantity.ValueChanged += (_, _) => RefreshPrice();
        FormClosing += (_, e) => { if (busy) e.Cancel = true; };
    }

    /// <summary>Chỉ cho chọn nhiệt độ có thật của size đang chọn; món chỉ bán lạnh không được tự thêm nóng.</summary>
    private void RefreshTemperatures(string? preferred, MenuBrewOption? brew)
    {
        updating = true;
        temperature.Items.Clear();
        foreach (var value in variants.Where(x => x.KichCo == size.Text).Select(x => x.NhietDo).Distinct())
            temperature.Items.Add(new Choice<string>(value == "HOT" ? "Nóng" : "Lạnh", value));
        temperature.SelectedIndex = temperature.Items.Cast<Choice<string>>().ToList().FindIndex(x => x.Value == preferred);
        if (temperature.SelectedIndex < 0 && temperature.Items.Count > 0) temperature.SelectedIndex = 0;
        temperature.Enabled = temperature.Items.Count > 1;
        updating = false; RefreshVariant(brew);
    }

    /// <summary>Map size/nhiệt độ về đúng biến thể; giữ topping còn hợp lệ khi đổi lựa chọn, lấy giá từ API.</summary>
    private void RefreshVariant(MenuBrewOption? preferred)
    {
        updating = true;
        var saved = portions.ToDictionary(x => x.Top.MaMonThem, x => (int)x.Count.Value);
        selected = variants.SingleOrDefault(x => x.KichCo == size.Text && x.NhietDo == (temperature.SelectedItem as Choice<string>)?.Value);
        sugar.Items.Clear();
        foreach (var value in AllowedPairs().Select(x => x.PhanTramDuong).Distinct().Order()) sugar.Items.Add(new Choice<int>(value + "%", value));
        SelectPercent(sugar, preferred?.PhanTramDuong);
        foreach (Control control in toppings.Controls.Cast<Control>().ToArray()) control.Dispose();
        toppings.Controls.Clear(); portions.Clear();
        foreach (var top in selected?.Toppings.Where(x => x.Available) ?? [])
        {
            var value = saved.GetValueOrDefault(top.MaMonThem, old?.Toppings.FirstOrDefault(x => x.MaMonThem == top.MaMonThem)?.SoPhanMoiLy ?? 0);
            var input = new NumericUpDown { Minimum = 0, Maximum = top.GioiHanMoiLy, Value = Math.Min(value, top.GioiHanMoiLy), Width = 170, Name = "PosTopping" + top.MaMonThem };
            input.ValueChanged += (_, _) => RefreshPrice(); portions.Add((top, input)); toppings.Controls.Add(Ui.Field($"{top.TenMonThem} · {top.GiaMoiPhan:N0}đ", input));
        }
        updating = false; RefreshIce(preferred?.PhanTramDa);
    }

    /// <summary>Món nóng bắt buộc đá 0 và ẩn cả nhãn; món lạnh chỉ liệt kê mức đá hợp lệ với mức đường hiện tại.</summary>
    private void RefreshIce(int? preferred)
    {
        updating = true; ice.Items.Clear();
        var sugarValue = (sugar.SelectedItem as Choice<int>)?.Value;
        foreach (var value in AllowedPairs().Where(x => x.PhanTramDuong == sugarValue).Select(x => x.PhanTramDa).Distinct().Order()) ice.Items.Add(new Choice<int>(value + "%", value));
        SelectPercent(ice, preferred); iceField.Visible = selected?.NhietDo != "HOT";
        updating = false; RefreshPrice();
    }

    /// <summary>Không tạo tổ hợp đường/đá ngoài tập server công bố, kể cả khi client có nhiều dropdown riêng.</summary>
    private IEnumerable<MenuBrewOption> AllowedPairs() => selected?.TuyChonPhaChe.Where(x => selected.NhietDo != "HOT" || x.PhanTramDa == 0) ?? [];

    /// <summary>Ưu tiên lựa chọn cũ, sau đó mức 50% nếu có; không tự thêm giá trị chưa được cấu hình.</summary>
    private static void SelectPercent(ComboBox control, int? preferred)
    {
        var choices = control.Items.Cast<Choice<int>>().ToList();
        var index = choices.FindIndex(x => x.Value == preferred);
        if (index < 0) index = choices.FindIndex(x => x.Value == 50);
        control.SelectedIndex = index >= 0 ? index : choices.Count > 0 ? 0 : -1;
    }

    /// <summary>Lấy cặp có thật để lưu nháp; không suy giá hoặc lựa chọn từ chuỗi nhãn hiển thị.</summary>
    private MenuBrewOption? CurrentBrew() => sugar.SelectedItem is Choice<int> s && ice.SelectedItem is Choice<int> i
        ? AllowedPairs().FirstOrDefault(x => x.PhanTramDuong == s.Value && x.PhanTramDa == i.Value) : null;

    /// <summary>Cập nhật tổng tạm tính theo số ly/topping; server vẫn kiểm tra và tính lại khi thu ngân.</summary>
    private void RefreshPrice()
    {
        if (updating) return;
        total.Text = selected is null ? "Chưa có cấu hình" : $"Tạm tính: {(selected.GiaBan + portions.Sum(x => x.Top.GiaMoiPhan * x.Count.Value)) * quantity.Value:N0}đ";
        save.Enabled = !busy && selected is not null && CurrentBrew() is not null;
        message.Text = selected is null || CurrentBrew() is null ? "Không có cấu hình hợp lệ đang bán cho lựa chọn này." : "";
    }

    /// <summary>Kiểm tra lại biến thể giá/BOM/topping trước lưu; lỗi giữ nguyên form và giỏ, không tự thay giá.</summary>
    private async Task Save()
    {
        if (busy || selected is not { } variant || CurrentBrew() is not { } brew) return;
        busy = true; save.Enabled = false;
        try
        {
            var latest = await LiveSession.Api.GetMenuVariantsAsync(product.MaMon);
            var current = latest.SingleOrDefault(x => x.MaCauHinhMon == variant.MaCauHinhMon);
            if (current is null || !current.Available || JsonSerializer.Serialize(current) != JsonSerializer.Serialize(variant))
                throw new InvalidOperationException("Giá/cấu hình đã thay đổi. Đóng và chọn lại món; giỏ cũ vẫn được giữ.");
            Result = new(old?.Key ?? Guid.NewGuid(), product.MaMon, product.Name, current, brew, (int)quantity.Value, note.Text.Trim(),
                portions.Where(x => x.Count.Value > 0).Select(x => new OrderToppingInput(x.Top.MaMonThem, (int)x.Count.Value)).ToList());
            busy = false; DialogResult = DialogResult.OK; Close();
        }
        catch (Exception error) when (error is FourChang.Presentation.Shared.ApiClients.CafeApiException or InvalidOperationException) { message.Text = error.Message; }
        finally { busy = false; if (!IsDisposed) save.Enabled = selected is not null && CurrentBrew() is not null; }
    }
}
