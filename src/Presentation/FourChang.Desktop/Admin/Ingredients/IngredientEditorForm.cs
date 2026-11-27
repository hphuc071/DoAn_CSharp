// UC: UC-ADM-03
// Chức năng: Nhập thông tin nguyên liệu và xác nhận ảnh hưởng khi ngừng sử dụng.
// Cần nhớ: Đọc hàm từ bước kiểm tra dữ liệu đến bước trả kết quả; các quyết định nghiệp vụ do Backend kiểm tra.

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
    private readonly NumericUpDown warning;
    private readonly CheckBox active;
    private readonly Label error;
    private readonly Button save;
    private IngredientInput? pending;
    private bool busy;
    private bool uncertain;

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
        warning = Amount(item?.NguongCanhBao ?? 0); warning.Name = "IngredientWarning";
        active = new CheckBox { Name = "IngredientActive", Text = "Đang sử dụng nguyên liệu", Checked = item?.DangSuDung ?? true,
            AutoSize = true, Font = CafeTheme.Body(10), Margin = new Padding(0, 8, 0, 8) };
        error = Ui.Label("", 9, false, CafeTheme.Danger); error.Name = "IngredientFeedback"; error.Visible = false;
        var hint = Ui.Label("Ngưỡng chỉ nhắc nhập hàng, không bị trừ khỏi tồn. Nhập/xuất hàng và kiểm kê thực hiện tại Quản lý kho.", 9, false, CafeTheme.Muted);
        hint.MaximumSize = new Size(560, 0);
        save = Ui.Button("Lưu nguyên liệu", () => { }, true); save.Name = "IngredientSave";
        save.Click += async (_, _) => await Save();
        var body = Ui.Panel("Thông tin nguyên liệu",
            AdminListLayout.Fields(AdminListLayout.Field("Tên nguyên liệu *", name), AdminListLayout.Field("Đơn vị mặc định", baseUnit)),
            AdminListLayout.Field("Ngưỡng cảnh báo (theo đơn vị đã chọn)", warning),
            AdminListLayout.Field("Mô tả", note), active, hint, error);
        AdminUi.Compose(this, body, save, Ui.Button("Hủy", () => Close()));
        FormClosing += (_, e) =>
        {
            if (!busy && !uncertain) return;
            e.Cancel = true; error.Visible = true;
            error.Text = "Kết quả chưa rõ. Chọn Kiểm tra / thử lại trước khi đóng.";
        };
        if (snapshot.DonVi.Count(x => x.DangSuDung && IngredientUnits.IsSupported(x.KyHieu)) != 2)
        { error.Text = "Chưa tải đủ hai đơn vị mặc định Gam và Mililit. Vui lòng tải lại dữ liệu."; error.Visible = true; save.Enabled = false; }
    }

    /// <summary>Giữ độ chính xác 4 số lẻ; không làm tròn âm thầm các ngưỡng người quản lý nhập.</summary>
    public static NumericUpDown Amount(decimal value) => new CompactAmountInput()
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

    /// <summary>Lưu đúng một lệnh; ngừng phải xác nhận danh sách phụ thuộc mới nhất, lỗi mạng giữ nguyên nháp.</summary>
    private async Task Save()
    {
        if (busy || baseUnit.SelectedValue is not long unit) return;
        busy = true; SetFields(false); error.Visible = false;
        try
        {
            if (pending is null)
            {
                string? confirmation = null;
                if (existing?.DangSuDung == true && !active.Checked)
                {
                    var impact = await LiveSession.Api.GetIngredientImpactAsync(existing.MaNguyenLieu);
                    using var dialog = new IngredientStopConfirmation(existing.TenNguyenLieu, impact.MonPhuThuoc);
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    confirmation = impact.PhienBanAnhHuong;
                }
                pending = new IngredientInput(name.Text, unit, 0, warning.Value, note.Text, active.Checked,
                    [new ConversionInput(unit, 1)], existing?.PhienBanDuLieu, operation, confirmation);
            }
            await AdminUi.Recover(() => LiveSession.Api.SaveIngredientAsync(existing?.MaNguyenLieu, pending),
                () => LiveSession.Api.AdminReceiptAsync("ingredients", operation));
            uncertain = false; busy = false; DialogResult = DialogResult.OK; Close();
        }
        catch (CafeApiException exception)
        {
            uncertain = pending is not null && (exception.StatusCode is null || (int)exception.StatusCode >= 500);
            if (!uncertain) pending = null;
            error.Text = exception.Message; error.Visible = true;
        }
        finally
        {
            busy = false;
            if (!IsDisposed) { SetFields(!uncertain); save.Enabled = true; save.Text = uncertain ? "Kiểm tra / thử lại" : "Lưu nguyên liệu"; }
        }
    }

    /// <summary>Khóa dữ liệu trong khi gửi hoặc chưa rõ kết quả; không cho thay payload của mã thao tác đang chờ.</summary>
    private void SetFields(bool enabled)
    {
        name.Enabled = note.Enabled = warning.Enabled = active.Enabled = enabled;
        baseUnit.Enabled = enabled && (existing is null || existing.SoThamChieu == 0);
        save.Enabled = enabled;
    }

}

/// <summary>Xác nhận ngừng với danh sách công thức thực tế; Hủy không gửi lệnh cập nhật.</summary>
internal sealed class IngredientStopConfirmation : Form
{
    public IngredientStopConfirmation(string name, IReadOnlyList<string> dependencies)
    {
        Name = "IngredientStopConfirmation"; Text = "Xác nhận ngừng nguyên liệu";
        AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(660, 460); MinimumSize = new Size(580, 400);
        StartPosition = FormStartPosition.CenterParent; Font = CafeTheme.Body(); BackColor = CafeTheme.Cream;
        var message = Ui.Label($"Ngừng sử dụng {name}?", 12, true); message.MaximumSize = new Size(580, 0);
        var guide = Ui.Label(dependencies.Count == 0 ? "Chưa có món/topping phụ thuộc nguyên liệu này."
            : "Các món/topping sau sẽ tạm hết và không nhận đặt mới:", 10); guide.MaximumSize = new Size(580, 0);
        var list = new TextBox { Name = "IngredientAffectedRecipes", Multiline = true, ReadOnly = true,
            ScrollBars = ScrollBars.Vertical, Width = 580, Height = 210, Text = string.Join(Environment.NewLine, dependencies),
            BackColor = CafeTheme.Surface, BorderStyle = BorderStyle.FixedSingle, Font = CafeTheme.Body() };
        var confirm = Ui.Button("Xác nhận ngừng", () => { DialogResult = DialogResult.OK; Close(); }, true);
        confirm.Name = "IngredientConfirmStop";
        var cancel = Ui.Button("Hủy", () => { DialogResult = DialogResult.Cancel; Close(); }); CancelButton = cancel; Shown += (_, _) => { list.SelectionLength = 0; cancel.Focus(); };
        AdminUi.Compose(this, Ui.Panel("Ảnh hưởng khi ngừng nguyên liệu", message, guide, list,
            Ui.Label("Số tồn, đơn đã nhận và lịch sử được giữ nguyên.", 9, false, CafeTheme.Muted)), confirm, cancel);
    }
}

/// <summary>Ô lượng giữ nguyên decimal, chỉ bỏ số 0 thừa khi trình bày g/ml hoặc phần trăm.</summary>
internal sealed class CompactAmountInput : NumericUpDown
{
    /// <summary>Giữ xử lý nhập liệu native rồi định dạng số lẻ tùy chọn, không làm tròn Value.</summary>
    protected override void UpdateEditText()
    {
        base.UpdateEditText();
        var format = (ThousandsSeparator ? "#,##0" : "0") + (DecimalPlaces > 0 ? "." + new string('#', DecimalPlaces) : "");
        ChangingText = true;
        Text = Value.ToString(format, System.Globalization.CultureInfo.CurrentCulture);
    }
}
