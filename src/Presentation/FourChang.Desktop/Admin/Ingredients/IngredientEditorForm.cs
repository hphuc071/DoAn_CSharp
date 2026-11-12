using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.ApiClients;

namespace FourChang.Desktop.Admin.Ingredients;

/// <summary>Biểu mẫu nguyên liệu gọn: chỉ Gam/Mililit, không có màn hình đơn vị hoặc nhập hệ số quy đổi.</summary>
public sealed class IngredientEditorForm : Form
{
    private sealed record UnitChoice(long MaDonVi, string HienThi);
    private readonly IngredientView? existing;
    private readonly Guid operation = Guid.NewGuid();
    private readonly TextBox name;
    private readonly TextBox note;
    private readonly ComboBox baseUnit;
    private readonly NumericUpDown safety;
    private readonly NumericUpDown warning;
    private readonly CheckBox active;
    private readonly Label error;
    private readonly Button save;

    /// <summary>Chọn một trong hai đơn vị cố định và nhập ngưỡng; form không nhận số tồn hoặc định mức BOM.</summary>
    public IngredientEditorForm(IngredientSnapshot snapshot, IngredientView? item)
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        existing = item;
        Text = item is null ? "Thêm nguyên liệu" : "Sửa nguyên liệu";
        ClientSize = new Size(760, 600); MinimumSize = new Size(680, 560);
        StartPosition = FormStartPosition.CenterParent; BackColor = CafeTheme.Cream;
        Font = CafeTheme.Body(); Padding = new Padding(16);
        name = Ui.Input("Ví dụ: Hạt cà phê, Sữa tươi…", item?.TenNguyenLieu ?? "", 360);
        name.Name = "IngredientName"; name.MaxLength = 150;
        note = Ui.Input("Thông tin bổ sung về nguyên liệu", item?.MoTa ?? "", 580);
        note.Name = "IngredientNote"; note.MaxLength = 500;
        baseUnit = UnitCombo(snapshot); baseUnit.Name = "IngredientBaseUnit";
        if (item is not null) baseUnit.SelectedValue = item.MaDonViCoSo;
        // Đơn vị của nguyên liệu đã phát sinh được khóa ở cả UI và API, tránh đổi ý nghĩa tồn/BOM cũ.
        baseUnit.Enabled = item is null || item.SoThamChieu == 0;
        safety = Amount(item?.TonAnToan ?? 0); safety.Name = "IngredientSafety";
        warning = Amount(item?.NguongCanhBao ?? 0); warning.Name = "IngredientWarning";
        active = new CheckBox { Text = "Đang sử dụng nguyên liệu", Checked = item?.DangSuDung ?? true,
            AutoSize = true, Font = CafeTheme.Body(10), Margin = new Padding(0, 8, 0, 8) };
        error = Ui.Label("", 9, false, CafeTheme.Danger); error.Name = "IngredientFeedback"; error.Visible = false;
        var hint = Ui.Label("Các lượng được nhập trực tiếp theo Gam hoặc Mililit đã chọn. Không cần khai báo quy đổi.", 9, false, CafeTheme.Muted);
        hint.MaximumSize = new Size(560, 0);
        save = Ui.Button("Lưu nguyên liệu", () => { }, true); save.Name = "IngredientSave";
        save.Click += async (_, _) => await Save();
        var body = Ui.Panel("Thông tin nguyên liệu",
            AdminListLayout.Fields(AdminListLayout.Field("Tên nguyên liệu *", name), AdminListLayout.Field("Đơn vị mặc định", baseUnit)),
            AdminListLayout.Fields(AdminListLayout.Field("Tồn an toàn", safety), AdminListLayout.Field("Ngưỡng cảnh báo", warning)),
            AdminListLayout.Field("Mô tả", note), active, hint, error);
        AdminUi.Compose(this, body, save, Ui.Button("Hủy", () => Close()));
        if (snapshot.DonVi.Count(x => x.DangSuDung && IngredientUnits.IsSupported(x.KyHieu)) != 2)
        { error.Text = "Chưa tải đủ hai đơn vị mặc định Gam và Mililit. Vui lòng tải lại dữ liệu."; error.Visible = true; save.Enabled = false; }
    }

    /// <summary>Giữ độ chính xác 4 số lẻ; không làm tròn âm thầm các ngưỡng người quản lý nhập.</summary>
    public static NumericUpDown Amount(decimal value) => new()
    {
        DecimalPlaces = 4, Maximum = 999999999999m, Minimum = 0,
        Value = Math.Clamp(value, 0, 999999999999m), ThousandsSeparator = true, Width = 180, Font = CafeTheme.Body()
    };

    /// <summary>Chỉ hai lựa chọn từ ID thật trong API; cùng helper được dùng cho phần BOM.</summary>
    public static ComboBox UnitCombo(IngredientSnapshot snapshot) => new()
    {
        Width = 210, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = nameof(UnitChoice.HienThi), ValueMember = nameof(UnitChoice.MaDonVi),
        BindingContext = new BindingContext(),
        DataSource = snapshot.DonVi.Where(x => x.DangSuDung && IngredientUnits.IsSupported(x.KyHieu))
            .OrderBy(x => x.KyHieu == IngredientUnits.Gram ? 0 : 1).Select(x => new UnitChoice(x.MaDonVi, IngredientUnits.Label(x.KyHieu))).ToList(),
        Font = CafeTheme.Body()
    };

    /// <summary>Lưu một quy đổi cơ sở hệ số 1 để tương thích BOM/kho hiện hữu; không xóa bảng hay sửa số dư.</summary>
    private async Task Save()
    {
        if (baseUnit.SelectedValue is not long unit) return;
        if (string.IsNullOrWhiteSpace(name.Text)) { error.Text = "Vui lòng nhập tên nguyên liệu."; error.Visible = true; name.Focus(); return; }
        if (warning.Value < safety.Value) { error.Text = "Ngưỡng cảnh báo phải lớn hơn hoặc bằng tồn an toàn."; error.Visible = true; warning.Focus(); return; }
        save.Enabled = false; error.Visible = false;
        try
        {
            var input = new IngredientInput(name.Text, unit, safety.Value, warning.Value, note.Text, active.Checked,
                [new ConversionInput(unit, 1)], existing?.PhienBanDuLieu, operation);
            await AdminUi.Recover(() => LiveSession.Api.SaveIngredientAsync(existing?.MaNguyenLieu, input),
                () => LiveSession.Api.AdminReceiptAsync("ingredients", operation));
            DialogResult = DialogResult.OK; Close();
        }
        catch (CafeApiException exception) { error.Text = exception.Message; error.Visible = true; }
        finally { if (!save.IsDisposed) save.Enabled = true; }
    }
}
