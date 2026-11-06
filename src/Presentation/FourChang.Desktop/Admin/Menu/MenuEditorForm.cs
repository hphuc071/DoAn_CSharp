using FourChang.Desktop.Admin.Employees;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;
namespace FourChang.Desktop.Admin.Menu;

/// <summary>Editor ADM-03: trường có nhãn, nút thẳng hàng, giữ nháp và request khi lỗi mạng.</summary>
internal sealed class MenuEditorForm : Form
{
    private readonly string kind;
    private readonly long? id;
    private readonly MenuAdminSnapshot data;
    private readonly List<Control> fields = [];
    private readonly Button save = Ui.Button("Lưu thay đổi", () => { }, true);
    private readonly Button cancel;
    private readonly Label error = Ui.Label("", 9, false, CafeTheme.Danger);
    private readonly Control formBody;
    private Func<Guid, object> build = null!;
    private object? pending;
    private Guid operation;
    private bool sending;

    /// <summary>Tạo form cho một đối tượng, không thay đổi dữ liệu cho đến khi người dùng bấm Lưu.</summary>
    internal MenuEditorForm(string kind, long? id, MenuAdminSnapshot data)
    {
        this.kind = kind;
        this.id = id;
        this.data = data;
        Name = "MenuEditor_" + kind;
        var noun = kind switch
        {
            "dishes" => "món",
            "variants" => "cấu hình món",
            "categories" => "danh mục",
            "toppings" => "topping",
            _ => "banner"
        };
        Text = (id is null ? "Thêm " : "Chỉnh sửa ") + noun;
        ClientSize = new(880, 780);
        MinimumSize = new(650, 560);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = CafeTheme.Cream;
        Font = CafeTheme.Body();
        cancel = Ui.Button("Hủy", () => Close());
        save.Name = "SaveMenu";
        switch (kind)
        {
            case "categories":
                Category();
                break;
            case "dishes":
                Dish();
                break;
            case "variants":
                Variant();
                break;
            case "toppings":
                Topping();
                break;
            case "banners":
                Banner();
                break;
            default:
                throw new InvalidOperationException();
        }
        formBody = Ui.Panel("Thông tin " + noun, ComposeFields());
        var header = new Panel { Dock = DockStyle.Top, Height = 100, BackColor = CafeTheme.Surface, Padding = new Padding(26, 16, 26, 12) };
        var eyebrow = Ui.Label("THỰC ĐƠN  /  " + (id is null ? "THÊM MỚI" : "CHỈNH SỬA"), 8, true, CafeTheme.Coffee);
        eyebrow.Dock = DockStyle.Top;
        eyebrow.Height = 24;
        var title = Ui.Label(Text, 20, true);
        title.Dock = DockStyle.Top;
        title.Height = 42;
        header.Controls.Add(title);
        header.Controls.Add(eyebrow);
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new(24) };
        scroll.Controls.Add(formBody);
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 88, Padding = new(24, 12, 24, 12), BackColor = CafeTheme.Surface };
        var actions = Ui.Row(cancel, save);
        actions.AutoSize = false;
        actions.WrapContents = false;
        actions.Width = 360;
        actions.Height = 56;
        actions.Dock = DockStyle.Right;
        footer.Controls.Add(actions);
        error.Visible = false;
        error.TextChanged += (_, _) => error.Visible = !string.IsNullOrWhiteSpace(error.Text);
        error.Dock = DockStyle.Bottom;
        error.Padding = new(24, 8, 24, 8);
        error.MaximumSize = new(800, 0);
        Controls.Add(scroll);
        Controls.Add(error);
        Controls.Add(footer);
        Controls.Add(header);
        save.Click += async (_, _) => await Save();
        FormClosing += (_, e) => { if (sending) e.Cancel = true; };
        CafeVisuals.Apply(this);
    }
    /// <summary>Gắn nhãn metadata cho khung UI để ghép cột, giữ nguyên input và binding đã có.</summary>
    private void AddField(string label, Control input)
    {
        var field = AdminListLayout.Field(label, input);
        field.Tag = label;
        if (input is TextBox text && label is "Mô tả" or "Nội dung")
        {
            text.Multiline = true;
            text.AcceptsReturn = true;
            text.ScrollBars = ScrollBars.Vertical;
            field.Height = 118;
            if (field is TableLayoutPanel table)
                table.RowStyles[1].Height = 90;
        }
        fields.Add(field);
    }

    /// <summary>Ghép các trường liên quan thành hai cột; nội dung dài và tùy chọn pha chế vẫn chiếm đủ hàng.</summary>
    private Control[] ComposeFields()
    {
        var remaining = fields.ToList();
        var result = new List<Control>();
        void Pair(string left, string right)
        {
            var a = remaining.FirstOrDefault(c => Equals(c.Tag, left));
            var b = remaining.FirstOrDefault(c => Equals(c.Tag, right));
            if (a is null || b is null)
                return;
            remaining.Remove(a);
            remaining.Remove(b);
            var row = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 1, Height = Math.Max(a.Height, b.Height), Margin = Padding.Empty };
            row.ColumnStyles.Add(new(SizeType.Percent, 50));
            row.ColumnStyles.Add(new(SizeType.Percent, 50));
            a.Dock = b.Dock = DockStyle.Fill;
            a.Margin = new Padding(0, 0, 10, 0);
            b.Margin = new Padding(10, 0, 0, 0);
            row.Controls.Add(a, 0, 0);
            row.Controls.Add(b, 1, 0);
            result.Add(row);
        }
        switch (kind)
        {
            case "dishes":
                Pair("Danh mục", "Tên món");
                break;
            case "categories":
                Pair("Tên danh mục", "Thứ tự");
                break;
            case "variants":
                Pair("Món", "Giá bán (VND)");
                Pair("Size", "Nhiệt độ");
                break;
            case "toppings":
                Pair("Tên topping", "Giá mỗi phần (VND)");
                break;
            case "banners":
                Pair("Tên banner", "Thứ tự");
                Pair("Ảnh", "Liên kết");
                Pair("Bắt đầu", "Kết thúc (bỏ chọn: không giới hạn)");
                break;
        }
        result.AddRange(remaining);
        return result.ToArray();
    }

    /// <summary>Ô chữ có giới hạn theo cột; API vẫn xác thực lại, không tin validation phía giao diện.</summary>
    private TextBox TextField(string label, string? value = null, int max = 500)
    {
        var input = Ui.Input(label, value ?? "", 700);
        input.MaxLength = max;
        input.Name = label;
        AddField(label, input);
        return input;
    }
    /// <summary>Số VND/giới hạn dùng NumericUpDown, không cho nhập chữ hoặc giá âm.</summary>
    private NumericUpDown Number(string label, decimal value = 0, decimal max = 999999999999999999m)
    {
        var input = new NumericUpDown { Width = 700, Maximum = max, Minimum = 0, Value = Math.Clamp(value, 0, max), ThousandsSeparator = true, Font = CafeTheme.Body(), Name = label };
        AddField(label, input);
        return input;
    }
    /// <summary>Cờ trạng thái có chữ rõ ràng, không gắn quyền hoặc trạng thái kho vào checkbox.</summary>
    private CheckBox Flag(string label, bool value = false)
    {
        var input = new CheckBox { Text = label, Checked = value, AutoSize = true, MinimumSize = new(0, 32), Name = label };
        fields.Add(input);
        return input;
    }
    /// <summary>Lựa chọn khóa dùng item có mã; người trùng tên không làm mất liên kết.</summary>
    private ComboBox Select(string label, IEnumerable<(long Id, string Text)> values, long? selected = null, bool showId = true)
    {
        var input = Ui.Select();
        input.Width = 700;
        input.Name = label;
        foreach (var (key, text) in values)
            input.Items.Add(new Option(key, showId ? text + " · #" + key : text));
        if (input.Items.Count > 0)
            input.SelectedIndex = 0;
        for (int i = 0; i < input.Items.Count; i++)
            if (((Option)input.Items[i]!).Id == selected)
                input.SelectedIndex = i;
        AddField(label, input);
        return input;
    }
    /// <summary>Thời điểm nhập theo máy Admin, chuyển UTC rõ ràng trước khi gửi API.</summary>
    private DateTimePicker Date(string label, DateTime? value, bool optional = false)
    {
        var input = new DateTimePicker { Width = 700, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy HH:mm", ShowCheckBox = optional, Checked = value is not null, Value = value?.ToLocalTime() ?? DateTime.Now, Name = label };
        AddField(label, input);
        return input;
    }
    /// <summary>Không suy khóa từ SelectedIndex vì danh sách có thể sắp xếp lại.</summary>
    private static long Key(ComboBox combo) => combo.SelectedItem is Option item ? item.Id : 0;
    /// <summary>Caption hiển thị tên và mã, dữ liệu gửi chỉ dùng khóa thật.</summary>
    private sealed record Option(long Id, string Caption)
    {
        public override string ToString() => Caption;
    }

    /// <summary>Danh mục hiện số món chịu tác động và checkbox xác nhận trước khi ẩn/ngừng.</summary>
    private void Category()
    {
        var x = id is null ? null : data.DanhMuc.Single(x => x.MaDanhMuc == id);
        var name = TextField("Tên danh mục", x?.TenDanhMuc, 100);
        var description = TextField("Mô tả", x?.MoTa);
        var order = Number("Thứ tự", x?.ThuTuHienThi ?? 0, int.MaxValue);
        var publish = Flag("Công bố danh mục", x?.DaCongBo ?? false);
        var active = Flag("Đang sử dụng", x?.DangSuDung ?? true);
        var confirm = Flag($"Xác nhận tác động tới {x?.SoMon ?? 0} món khi ẩn/ngừng");
        build = op => new MenuCategoryInput(name.Text, description.Text, (int)order.Value, publish.Checked, active.Checked, confirm.Checked, x?.PhienBanDuLieu, op);
    }
    /// <summary>Món tách công bố/bật bán và lịch; nháp được lưu trước khi có BOM.</summary>
    private void Dish()
    {
        var x = id is null ? null : data.Mon.Single(x => x.MaMon == id);
        var category = Select("Danh mục", data.DanhMuc.Select(c => (c.MaDanhMuc, c.TenDanhMuc)), x?.MaDanhMuc);
        var name = TextField("Tên món", x?.TenMon, 150);
        var description = TextField("Mô tả", x?.MoTa, 1000);
        var image = TextField("Ảnh (sprite:0…5 hoặc đường dẫn ảnh)", x?.DuongDanAnh);
        var order = Number("Thứ tự", x?.ThuTuHienThi ?? 0, int.MaxValue);
        var date = Date("Công bố từ (bỏ chọn: ngay khi công bố)", x?.CongBoTuLuc, true);
        var publish = Flag("Công bố món", x?.DaCongBo ?? false);
        var sell = Flag("Bật bán (vẫn chịu kiểm tra tồn kho)", x?.DangBan ?? true);
        fields.Add(Ui.Label("Món mới: lưu nháp → tạo cấu hình → gắn BOM tại ADM-05 → công bố.", 9, false, CafeTheme.Muted));
        build = op => new MenuDishInput(Key(category), name.Text, description.Text, image.Text, (int)order.Value, publish.Checked, sell.Checked, date.Checked ? date.Value.ToUniversalTime() : null, x?.PhienBanDuLieu, op);
    }
    /// <summary>Giá theo cấu hình, tổ hợp đường/đá và liên kết topping đều gửi chung một transaction.</summary>
    private void Variant()
    {
        var x = id is null ? null : data.CauHinh.Single(x => x.MaCauHinhMon == id);
        var dish = Select("Món", data.Mon.Select(m => (m.MaMon, m.TenMon)), x?.MaMon);
        dish.Enabled = x is null;
        var size = Select("Size", new[] { (1L, "S"), (2L, "M"), (3L, "L") }, x?.KichCo switch
        {
            "M" => 2,
            "L" => 3,
            _ => 1
        }, false);
        var temperature = Select("Nhiệt độ", new[] { (1L, "Nóng"), (2L, "Lạnh") }, x?.NhietDo == "HOT" ? 1 : 2, false);
        var price = Number("Giá bán (VND)", x?.GiaBan ?? 0);
        var sell = Flag("Bật cấu hình", x?.DangBan ?? true);
        var def = Flag("Cấu hình mặc định của món", x?.LaMacDinh ?? false);
        var options = new CheckedListBox { Width = 700, Height = 180, CheckOnClick = true, MultiColumn = false, ColumnWidth = 170, Name = "BrewChoices", Font = CafeTheme.Body(9) };
        var defaultChoice = Ui.Select();
        defaultChoice.Width = 700;
        defaultChoice.Name = "DefaultBrewChoice";
        var pairs = new List<(int Sugar, int Ice)>();
        void Rebuild()
        {
            pairs.Clear();
            options.Items.Clear();
            defaultChoice.Items.Clear();
            foreach (var sugar in new[] { 0, 25, 50, 75, 100 })
                foreach (var ice in Key(temperature) == 1 ? new[] { 0 } : new[] { 0, 25, 50, 75, 100 })
                {
                    pairs.Add((sugar, ice));
                    var caption = $"{sugar}% đường · {ice}% đá";
                    options.Items.Add(caption, x?.TuyChon.Any(c => c.PhanTramDuong == sugar && c.PhanTramDa == ice) ?? (sugar == 50 && ice == (Key(temperature) == 1 ? 0 : 50)));
                    defaultChoice.Items.Add(caption);
                }
            defaultChoice.SelectedIndex = Math.Max(0, pairs.FindIndex(p => x?.TuyChon.Any(c => c.LaMacDinh && c.PhanTramDuong == p.Sugar && c.PhanTramDa == p.Ice) ?? (p.Sugar == 50 && p.Ice == (Key(temperature) == 1 ? 0 : 50))));
        }
        Rebuild();
        temperature.SelectedIndexChanged += (_, _) => Rebuild();
        fields.Add(Ui.Label("Các cặp đường/đá được phép", 10, true));
        fields.Add(options);
        fields.Add(AdminListLayout.Field("Cặp mặc định (phải được tích trong danh sách)", defaultChoice));
        var toppingInputs = new List<(long Id, CheckBox Enabled, NumericUpDown Limit)>();
        fields.Add(Ui.Label("Topping áp dụng · giá theo phần từ danh sách Topping", 10, true));
        foreach (var top in data.MonThem)
        {
            var link = x?.MonThem.FirstOrDefault(l => l.MaMonThem == top.MaMonThem);
            var check = new CheckBox { Text = $"{top.TenMonThem} · {top.GiaMoiPhan:N0}đ/phần", Checked = link is not null, Width = 430, Height = 38 };
            var limit = new NumericUpDown { Minimum = 1, Maximum = top.GioiHanMacDinhMoiLy, Value = link?.GioiHanMoiLy ?? 1, Width = 100 };
            fields.Add(Ui.Row(check, limit));
            toppingInputs.Add((top.MaMonThem, check, limit));
        }
        build = op => new MenuVariantInput(Key(dish), Key(size) switch { 2 => "M", 3 => "L", _ => "S" }, Key(temperature) == 1 ? "HOT" : "ICED", price.Value, sell.Checked, def.Checked, options.CheckedIndices.Cast<int>().Select(i => new MenuChoice(pairs[i].Sugar, pairs[i].Ice, i == defaultChoice.SelectedIndex)).ToList(), toppingInputs.Where(t => t.Enabled.Checked).Select(t => new MenuToppingLink(t.Id, (int)t.Limit.Value)).ToList(), x?.PhienBanDuLieu, op);
    }
    /// <summary>Topping giá/phần và giới hạn; mới lưu ngừng rồi gắn BOM trước khi bật.</summary>
    private void Topping()
    {
        var x = id is null ? null : data.MonThem.Single(x => x.MaMonThem == id);
        var name = TextField("Tên topping", x?.TenMonThem, 100);
        var description = TextField("Mô tả", x?.MoTa);
        var price = Number("Giá mỗi phần (VND)", x?.GiaMoiPhan ?? 0);
        var limit = Number("Giới hạn phần mỗi ly", x?.GioiHanMacDinhMoiLy ?? 1, int.MaxValue);
        var image = TextField("Ảnh", x?.DuongDanAnh);
        var active = Flag("Đang sử dụng (cần BOM hợp lệ)", x?.DangSuDung ?? false);
        build = op => new MenuToppingInput(name.Text, description.Text, price.Value, (int)limit.Value, image.Text, active.Checked, x?.PhienBanDuLieu, op);
    }
    /// <summary>Banner dùng khu vực hero đang có, không đổi layout; lịch tùy chọn kết thúc và có công bố riêng.</summary>
    private void Banner()
    {
        var x = id is null ? null : data.Banner.Single(x => x.MaBanner == id);
        var name = TextField("Tên banner", x?.TenBanner, 150);
        var content = TextField("Nội dung", x?.NoiDung, 1000);
        var image = TextField("Ảnh", x?.DuongDanAnh ?? "sprite:2");
        var link = TextField("Liên kết", x?.LienKet ?? "/#menu");
        var order = Number("Thứ tự", x?.ThuTuHienThi ?? 0, int.MaxValue);
        var start = Date("Bắt đầu", x?.BatDauLuc ?? DateTime.UtcNow);
        var end = Date("Kết thúc (bỏ chọn: không giới hạn)", x?.KetThucLuc, true);
        var publish = Flag("Công bố banner", x?.DaCongBo ?? false);
        build = op => new MenuBannerInput(name.Text, content.Text, image.Text, link.Text, "HOME_HERO", (int)order.Value, start.Value.ToUniversalTime(), end.Checked ? end.Value.ToUniversalTime() : null, publish.Checked, x?.PhienBanDuLieu, op);
    }
    /// <summary>Lỗi dữ liệu cho sửa; timeout giữ payload/mã và khóa trường để thử lại đúng yêu cầu, tránh ghi trùng.</summary>
    private async Task Save()
    {
        if (sending)
            return;
        sending = true;
        save.Enabled = cancel.Enabled = false;
        formBody.Enabled = false;
        try
        {
            if (pending is null)
            {
                operation = Guid.NewGuid();
                pending = build(operation);
            }
            await WorkforceUi.Recover(() => LiveSession.Api.SaveMenuAsync(kind, id, pending), () => LiveSession.Api.MenuReceiptAsync(operation));
            sending = false;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (CafeApiException ex)
        {
            error.Text = ex.Message;
            if (ex.StatusCode is not null)
            {
                pending = null;
                formBody.Enabled = true;
            }
            save.Text = pending is null ? "Lưu thay đổi" : "Thử lại cùng yêu cầu";
            if (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                WorkforceUi.Error(this, ex);
        }
        finally { sending = false; if (!IsDisposed) save.Enabled = cancel.Enabled = true; }
    }
}
