using FourChang.Desktop.Admin.Employees;
using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;
namespace FourChang.Desktop.Admin.Menu;

/// <summary>Editor ADM-02: trường có nhãn, nút thẳng hàng, giữ nháp và request khi lỗi mạng.</summary>
internal sealed class MenuEditorForm : Form
{
    internal const string ImageFileFilter = "Ảnh PNG / JPG / WebP|*.png;*.jpg;*.jpeg;*.webp|Tất cả tệp|*.*";
    internal long? SavedId { get; private set; }
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
    private string? imageValue;
    private byte[]? imageUpload;
    private Panel? imagePreview;
    private Label? imageCaption;

    /// <summary>Tạo form cho một đối tượng, không thay đổi dữ liệu cho đến khi người dùng bấm Lưu.</summary>
    internal MenuEditorForm(string kind, long? id, MenuAdminSnapshot data) : this(kind, id, data, null, null) { }

    /// <summary>Nạp định lượng vào form topping để không phải mở công thức ở màn hình khác.</summary>
    internal MenuEditorForm(string kind, long? id, MenuAdminSnapshot data, IngredientSnapshot? ingredients, IReadOnlyList<RecipeView>? recipes)
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        if (kind is not ("dishes" or "variants" or "toppings")) throw new ArgumentOutOfRangeException(nameof(kind));
        this.kind = kind;
        this.id = id;
        this.data = data;
        Name = "MenuEditor_" + kind;
        var noun = kind switch
        {
            "dishes" => "món",
            "variants" => "cấu hình món",
            "toppings" => "topping",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
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
            case "dishes":
                Dish();
                break;
            case "variants":
                Variant();
                break;
            case "toppings":
                Topping(ingredients, recipes);
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
                Pair("Loại món", "Tên món");
                break;
            case "variants":
                Pair("Món", "Giá bán (VND)");
                Pair("Size", "Nhiệt độ");
                break;
            case "toppings":
                Pair("Tên topping", "Giá mỗi phần (VND)");
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
    /// <summary>Đọc khóa thật từ lựa chọn, không dùng caption để suy mã.</summary>
    private static long Key(ComboBox combo) => combo.SelectedItem is Option item ? item.Id : 0;
    /// <summary>Caption hiển thị tên và mã, dữ liệu gửi chỉ dùng khóa thật.</summary>
    private sealed record Option(long Id, string Caption)
    {
        public override string ToString() => Caption;
    }

    /// <summary>Thông tin món và cờ hiển thị/bật bán; món mới ẩn đến khi đủ cấu hình và công thức.</summary>
    private void Dish()
    {
        var x = id is null ? null : data.Mon.Single(x => x.MaMon == id);
        var category = Select("Loại món", data.DanhMuc.Select(c => (c.MaDanhMuc, c.TenDanhMuc)), x?.MaDanhMuc, false);
        var name = TextField("Tên món", x?.TenMon, 150);
        var description = TextField("Mô tả", x?.MoTa, 1000);
        ComboBox? template = null;
        if (x is null)
        {
            template = Select("Dùng công thức từ món", new[] { (0L, "Tự tạo công thức sau") }.Concat(data.Mon.Select(m => (m.MaMon, m.TenMon))), 0, false);
            template.Name = "MenuDishTemplate";
            fields.Add(Ui.Label("Chọn món mẫu để lấy size, nóng/lạnh, giá và nguyên liệu hiện tại. Mỗi món có công thức riêng, sửa lại được sau.", 9, false, CafeTheme.Muted));
        }
        ImageField(x?.DuongDanAnh);
        var publish = Flag("Hiển thị món", x?.DaCongBo ?? false);
        var sell = Flag("Bật bán (vẫn chịu kiểm tra tồn kho)", x?.DangBan ?? false);
        fields.Add(Ui.Label("Món mới: chọn món mẫu hoặc tự tạo cấu hình/công thức, sau đó hiển thị và bật bán.", 9, false, CafeTheme.Muted));
        build = op => new MenuDishInput(Key(category), name.Text, description.Text, imageValue, x?.ThuTuHienThi ?? 0, publish.Checked, sell.Checked, null, x?.PhienBanDuLieu, op, template is not null && Key(template) > 0 ? Key(template) : null);
    }
    /// <summary>Chỉ đưa món có đủ công thức đang áp dụng vào danh sách mẫu; kiểm tra server vẫn là bắt buộc.</summary>
    internal void SetRecipeTemplates(IReadOnlyList<RecipeView> recipes)
    {
        if (kind != "dishes" || id is not null) return;
        var picker = Controls.Find("MenuDishTemplate", true).OfType<ComboBox>().Single();
        picker.Items.Clear(); picker.Items.Add(new Option(0, "Tự tạo công thức sau"));
        foreach (var dish in data.Mon)
        {
            var variants = data.CauHinh.Where(v => v.MaMon == dish.MaMon && v.DangBan).ToList();
            if (variants.Count > 0 && variants.Count(v => v.LaMacDinh) == 1 && variants.All(v => recipes.Any(r => r.MaCauHinhMon == v.MaCauHinhMon && r.TrangThai == "ACTIVE" && r.ThanhPhan.Count > 0)))
                picker.Items.Add(new Option(dish.MaMon, dish.TenMon));
        }
        picker.SelectedIndex = 0;
    }

    /// <summary>Khai báo riêng size, nhiệt độ và giá; đường/đá và topping theo chính sách chung.</summary>
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
        var sell = Flag("Bật cấu hình", x?.DangBan ?? false);
        var def = Flag("Cấu hình mặc định của món", x?.LaMacDinh ?? false);
        if (x is null)
        {
            // Cấu hình đầu tiên mặc định được chọn, tránh tạo món chưa có cấu hình mặc định.
            void SuggestDefault() => def.Checked = !data.CauHinh.Any(v => v.MaMon == Key(dish));
            dish.SelectedIndexChanged += (_, _) => SuggestDefault(); SuggestDefault();
        }
        fields.Add(Ui.Label("Đường: 0%, 25%, 50%, 75%, 100%. Món nóng không có đá; món lạnh chọn thêm mức đá tương tự.", 10));
        fields.Add(Ui.Label("Topping đang sử dụng áp dụng cho mọi món. Giá và số phần tối đa quản lý ở tab Topping.", 10));
        build = op => new MenuVariantInput(Key(dish), Key(size) switch { 2 => "M", 3 => "L", _ => "S" },
            Key(temperature) == 1 ? "HOT" : "ICED", price.Value, sell.Checked, def.Checked, [], [], x?.PhienBanDuLieu, op);

    }
    /// <summary>Topping giá/phần và giới hạn; mới lưu ngừng rồi gắn BOM trước khi bật.</summary>
    private void Topping(IngredientSnapshot? ingredients, IReadOnlyList<RecipeView>? recipes)
    {
        var x = id is null ? null : data.MonThem.Single(x => x.MaMonThem == id);
        var name = TextField("Tên topping", x?.TenMonThem, 100);
        var description = TextField("Mô tả", x?.MoTa);
        var price = Number("Giá mỗi phần (VND)", x?.GiaMoiPhan ?? 0);
        var limit = Number("Giới hạn phần mỗi ly", x?.GioiHanMacDinhMoiLy ?? 1, int.MaxValue);
        var recipe = id is null ? null : recipes?.SingleOrDefault(r => r.MaMonThem == id);
        ComboBox? material = null;
        NumericUpDown? quantity = null;
        if (ingredients is not null)
        {
            var simple = recipe?.ThanhPhan.Count == 1 && recipe.ThanhPhan[0].LoaiDinhMuc == "FIXED";
            var candidates = ingredients.NguyenLieu.Where(n => n.DangSuDung || recipe?.ThanhPhan.Any(l => l.MaNguyenLieu == n.MaNguyenLieu) == true);
            material = Select("Nguyên liệu lấy từ kho", new[] { (0L, recipe is null ? "Chọn nguyên liệu" : "Giữ định lượng hiện tại") }
                .Concat(candidates.Select(n => (n.MaNguyenLieu, $"{n.TenNguyenLieu} ({(n.MaDonViCoSo == 1 ? "g" : "ml")})"))), simple ? recipe!.ThanhPhan[0].MaNguyenLieu : 0, false);
            material.Name = "ToppingIngredient";
            quantity = FourChang.Desktop.Admin.Ingredients.IngredientEditorForm.Amount(simple ? recipe!.ThanhPhan[0].DinhMucCoSo : 0);
            quantity.Name = "ToppingPortion"; AddField("Lượng mỗi phần", quantity);
            var unitHint = Ui.Label("", 9, false, CafeTheme.Muted);
            void DescribeUnit()
            {
                var n = ingredients.NguyenLieu.FirstOrDefault(n => n.MaNguyenLieu == Key(material));
                unitHint.Text = n is null ? "Chọn nguyên liệu để nhập định lượng cho một phần." : $"Một phần dùng số {(n.MaDonViCoSo == 1 ? "gam (g)" : "mililit (ml)")} đã nhập; hệ thống tự trừ kho khi bắt đầu pha.";
                quantity.Enabled = n is not null;
            }
            material.SelectedIndexChanged += (_, _) => DescribeUnit(); DescribeUnit(); fields.Add(unitHint);
            if (recipe is not null && !simple) fields.Add(Ui.Label("Topping đang có công thức nhiều nguyên liệu. Giữ định lượng hiện tại để không thay công thức cũ; chọn nguyên liệu mới sẽ thay bằng định lượng một nguyên liệu.", 9, false, CafeTheme.Muted));
        }
        ImageField(x?.DuongDanAnh);
        var active = Flag("Đang sử dụng", x?.DangSuDung ?? true);
        build = op =>
        {
            if (material is not null && Key(material) == 0 && recipe is null) throw new CafeApiException("Chọn nguyên liệu lấy từ kho cho topping.", System.Net.HttpStatusCode.BadRequest);
            if (material is not null && Key(material) > 0 && quantity!.Value <= 0) throw new CafeApiException("Lượng mỗi phần phải lớn hơn 0.", System.Net.HttpStatusCode.BadRequest);
            return new MenuToppingInput(name.Text, description.Text, price.Value, (int)limit.Value, imageValue, active.Checked, x?.PhienBanDuLieu, op,
                material is not null && Key(material) > 0 ? new ToppingPortionInput(Key(material), quantity!.Value, recipe?.MaCongThuc, recipe?.PhienBanDuLieu) : null);
        };
    }

    /// <summary>Chọn ảnh bằng nút, xem trước và bỏ ảnh; không yêu cầu nhập sprite hoặc đường dẫn máy.</summary>
    private void ImageField(string? current)
    {
        imageValue = current;
        imagePreview = new Panel { Width = 160, Height = 140, BackColor = CafeTheme.Soft, Name = "MenuImagePreview" };
        var initial = CatalogImage.Create(current, "Ảnh món", 140); initial.Dock = DockStyle.Fill; imagePreview.Controls.Add(initial);
        imageCaption = Ui.Label(current is null ? "Chưa chọn ảnh" : "Đang dùng ảnh đã lưu", 9, false, CafeTheme.Muted);
        imageCaption.Name = "MenuImageCaption"; imageCaption.MaximumSize = new Size(420, 0);
        var choose = Ui.Button("Chọn hình ảnh", () =>
        {
            using var picker = new OpenFileDialog { Title = "Chọn hình ảnh món", Filter = ImageFileFilter, CheckFileExists = true, Multiselect = false };
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            try { SelectImage(picker.FileName); }
            catch (Exception ex) when (ex is IOException or ArgumentException or OutOfMemoryException or NotSupportedException)
            { error.Text = "Không đọc được ảnh. Chọn ảnh PNG/JPG/WebP tối đa 5 MB, kích thước tối đa 4096 × 4096."; }
        });
        choose.Name = "MenuChooseImage";
        var clear = Ui.Button("Bỏ ảnh", () =>
        {
            imageValue = null; imageUpload = null; SetPreview(null);
            imageCaption.Text = "Chưa chọn ảnh";
        });
        clear.Name = "MenuClearImage";
        fields.Add(Ui.Stack(Ui.Label("Hình ảnh", 10, true), Ui.Row(imagePreview,
            Ui.Stack(Ui.Row(choose, clear), imageCaption, Ui.Label("PNG / JPG / WebP · tối đa 5 MB. Ảnh được lưu chung cho Web và POS.", 9, false, CafeTheme.Muted)))));
    }

    /// <summary>Đọc và chuẩn hóa ảnh thật thành PNG; chỉ tải lên lúc Lưu để Hủy không ghi ảnh mới.</summary>
    internal void SelectImage(string file)
    {
        if (new FileInfo(file).Length > 5 * 1024 * 1024) throw new ArgumentException("Ảnh quá lớn.");
        var input = File.ReadAllBytes(file);
        if (input.Length > 5 * 1024 * 1024) throw new ArgumentException("Ảnh quá lớn.");
        // Nhận dạng nội dung thật, kể cả WebP tải từ mạng; không phụ thuộc phần mở rộng/codec Windows.
        using var data = SkiaSharp.SKData.CreateCopy(input);
        using var codec = SkiaSharp.SKCodec.Create(data) ?? throw new ArgumentException("Không đọc được ảnh.");
        if (codec.EncodedFormat is not (SkiaSharp.SKEncodedImageFormat.Png or SkiaSharp.SKEncodedImageFormat.Jpeg or SkiaSharp.SKEncodedImageFormat.Webp))
            throw new ArgumentException("Chỉ hỗ trợ PNG, JPG hoặc WebP.");
        var info = codec.Info;
        if (info.Width is <= 0 or > 4096 || info.Height is <= 0 or > 4096) throw new ArgumentException("Ảnh quá lớn.");
        using var decoded = new SkiaSharp.SKBitmap(new SkiaSharp.SKImageInfo(info.Width, info.Height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul));
        if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SkiaSharp.SKCodecResult.Success)
            throw new ArgumentException("Ảnh bị lỗi hoặc chưa tải đầy đủ.");
        using var normalizedImage = SkiaSharp.SKImage.FromBitmap(decoded);
        using var png = normalizedImage.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100) ?? throw new ArgumentException("Không chuyển được ảnh.");
        if (png.Size > 5 * 1024 * 1024) throw new ArgumentException("Ảnh quá lớn.");
        var bytes = png.ToArray();
        using var previewSource = new MemoryStream(bytes);
        using var normalized = Image.FromStream(previewSource);
        SetPreview(new Bitmap(normalized)); imageUpload = bytes;
        imageCaption!.Text = Path.GetFileName(file); error.Text = "";
    }

    /// <summary>Giải phóng ảnh/control cũ khi thay xem trước, tránh khóa file ảnh người dùng.</summary>
    private void SetPreview(Image? image)
    {
        foreach (Control child in imagePreview!.Controls.Cast<Control>().ToArray()) child.Dispose();
        imagePreview.Controls.Clear();
        if (image is null) return;
        var picture = new PictureBox { Image = image, Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
        picture.Disposed += (_, _) => picture.Image?.Dispose(); imagePreview.Controls.Add(picture);
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
                if (imageUpload is not null)
                {
                    imageValue = (await LiveSession.Api.UploadMenuImageAsync(imageUpload)).Path;
                    imageUpload = null;
                }
                operation = Guid.NewGuid();
                pending = build(operation);
            }
            var receipt = await AdminUi.Recover(() => LiveSession.Api.SaveMenuAsync(kind, id, pending), () => LiveSession.Api.MenuReceiptAsync(operation));
            SavedId = receipt.Ma;
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
                AdminUi.Error(this, ex);
        }
        finally { sending = false; if (!IsDisposed) save.Enabled = cancel.Enabled = true; }
    }
}


/// <summary>Chọn món một lần, nhập giá nhiều size/nhiệt độ và chuyển thẳng sang bảng công thức.</summary>
internal sealed class MenuVariantBatchForm : Form
{
    private sealed record Choice(long Id, string Label);
    private readonly MenuAdminSnapshot data;
    private readonly ComboBox dish;
    private readonly CheckBox cold = new() { Name = "VariantCold", Text = "Lạnh", AutoSize = true, Checked = true };
    private readonly CheckBox hot = new() { Name = "VariantHot", Text = "Nóng", AutoSize = true };
    private readonly ComboBox defaultChoice = new() { Name = "VariantDefault", Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DataGridView grid = Ui.Grid(new([], []), 230);
    private readonly Label feedback = Ui.Label("", 9, false, CafeTheme.Danger);
    private readonly Button save;
    private readonly Dictionary<string, decimal?> prices = [];
    private readonly HashSet<string> sizes = [];
    private bool rendering, busy, uncertain;
    private MenuVariantBatchInput? pending;
    internal long? SavedDishId { get; private set; }

    /// <summary>Cấu hình có sẵn được nạp để sửa giá, giữ trạng thái bán và không tạo tổ hợp trùng.</summary>
    internal MenuVariantBatchForm(MenuAdminSnapshot data, long? selectedDish = null)
    {
        this.data = data;
        Name = "MenuVariantBatchForm"; Text = "Cấu hình size và giá"; ClientSize = new Size(860, 650); MinimumSize = new Size(690, 550);
        AutoScaleMode = AutoScaleMode.Dpi; BackColor = CafeTheme.Cream; Font = CafeTheme.Body(); Padding = new Padding(16); StartPosition = FormStartPosition.CenterParent;
        dish = new ComboBox { Name = "VariantDish", Width = 530, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Label", ValueMember = "Id", BindingContext = new BindingContext(),
            DataSource = data.Mon.OrderBy(m => m.ThuTuHienThi).Select(m => new Choice(m.MaMon, m.TenMon)).ToList() };
        grid.Name = "VariantPriceMatrix"; grid.DataSource = null; grid.Columns.Clear(); grid.ReadOnly = false;
        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "UseSize", HeaderText = "Sử dụng", FillWeight = 16 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Size", HeaderText = "Size", ReadOnly = true, FillWeight = 14 });
        foreach (var temp in new[] { "ICED", "HOT" }) grid.Columns.Add(new DataGridViewTextBoxColumn { Name = temp, HeaderText = temp == "ICED" ? "Giá lạnh (đ)" : "Giá nóng (đ)", ValueType = typeof(decimal), FillWeight = 35, DefaultCellStyle = new() { Format = "N0", NullValue = "Nhập giá" } });
        grid.CurrentCellDirtyStateChanged += (_, _) => { if (grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewCheckBoxCell) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        grid.CellValidating += (_, e) =>
        {
            if (rendering || e.RowIndex < 0 || e.ColumnIndex < 2 || grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ReadOnly) return;
            if (!Price(e.FormattedValue, out var validatedPrice)) { e.Cancel = true; feedback.Text = "Giá phải là số đồng nguyên không âm, ví dụ 35000."; }
        };
        grid.CellParsing += (_, e) => { if (e.ColumnIndex >= 2 && Price(e.Value, out var value)) { e.Value = value; e.ParsingApplied = true; } };
        grid.CellValueChanged += (_, e) =>
        {
            if (rendering || e.RowIndex < 0) return;
            var size = (string)grid.Rows[e.RowIndex].Cells[1].Value;
            if (e.ColumnIndex == 0)
            {
                // Bỏ chọn không xóa cấu hình đã có hoặc làm mất liên kết đơn/công thức.
                if (Equals(grid.Rows[e.RowIndex].Cells[0].Value, true)) sizes.Add(size); else sizes.Remove(size);
                RefreshChoices();
            }
            if (e.ColumnIndex >= 2 && Price(grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value, out var value)) prices[size + ":" + grid.Columns[e.ColumnIndex].Name] = value;
        };
        grid.DataError += (_, e) => { e.ThrowException = false; e.Cancel = true; feedback.Text = "Kiểm tra lại giá đã nhập."; };
        cold.CheckedChanged += (_, _) => RefreshChoices(); hot.CheckedChanged += (_, _) => RefreshChoices();
        save = Ui.Button("Lưu & nhập công thức", () => { }, true); save.Name = "VariantBatchSave"; save.MinimumSize = new Size(260, 44); save.Width = 260; save.Click += async (_, _) => await Save();
        var hint = Ui.Label("Bỏ chọn size để bỏ qua tạo mới; cấu hình đã có vẫn được giữ. Cấu hình mới lưu tắt bán để bạn hoàn thiện công thức trước.", 9, false, CafeTheme.Muted); hint.MaximumSize = new Size(760, 0);
        feedback.Name = "VariantBatchFeedback"; feedback.MaximumSize = new Size(760, 0);
        AdminUi.Compose(this, Ui.Panel("Cấu hình size và giá", Ui.Field("Món", dish), Ui.Row(Ui.Label("Có phục vụ:", 10, true), cold, hot), grid,
            Ui.Field("Lựa chọn mặc định khi khách chọn món", defaultChoice), hint, feedback), save, Ui.Button("Hủy", () => Close()));
        dish.SelectedValueChanged += (_, _) => LoadDish();
        if (selectedDish is { } id) { dish.SelectedValue = id; dish.Enabled = false; }
        LoadDish();
        FormClosing += (_, e) => { if (busy || uncertain) { e.Cancel = true; feedback.Text = "Kết quả chưa rõ. Kiểm tra / thử lại trước khi đóng."; } };
    }

    /// <summary>Nạp giá và lựa chọn mặc định hiện hành của món, không điền giá tùy ý.</summary>
    private void LoadDish()
    {
        rendering = true; prices.Clear(); sizes.Clear(); grid.Rows.Clear();
        var variants = Current();
        cold.Checked = variants.Count == 0 || variants.Any(v => v.NhietDo == "ICED"); hot.Checked = variants.Any(v => v.NhietDo == "HOT");
        foreach (var v in variants) { prices[v.KichCo + ":" + v.NhietDo] = v.GiaBan; sizes.Add(v.KichCo); }
        if (variants.Count == 0) sizes.Add("M");
        foreach (var size in new[] { "S", "M", "L" }) grid.Rows.Add(sizes.Contains(size), size, prices.GetValueOrDefault(size + ":ICED"), prices.GetValueOrDefault(size + ":HOT"));
        rendering = false; feedback.Text = ""; RefreshChoices();
        if (variants.FirstOrDefault(v => v.LaMacDinh) is { } def) defaultChoice.SelectedItem = Label(def.KichCo, def.NhietDo);
    }

    /// <summary>Chỉ hiện giá theo nhiệt độ đã chọn, danh sách mặc định chỉ lấy tổ hợp sẽ được lưu.</summary>
    private void RefreshChoices()
    {
        if (rendering) return;
        rendering = true;
        grid.Columns["ICED"].Visible = cold.Checked; grid.Columns["HOT"].Visible = hot.Checked;
        foreach (DataGridViewRow row in grid.Rows)
            foreach (var temp in new[] { "ICED", "HOT" }) row.Cells[temp].ReadOnly = !sizes.Contains((string)row.Cells[1].Value);
        var old = defaultChoice.SelectedItem as string;
        defaultChoice.Items.Clear();
        var keys = Current().Select(v => (v.KichCo, v.NhietDo)).Concat(sizes.SelectMany(size => new[] { "ICED", "HOT" }.Where(t => t == "ICED" ? cold.Checked : hot.Checked).Select(t => (size, t)))).Distinct();
        foreach (var (size, temp) in keys.OrderBy(v => v.Item1 == "S" ? 0 : v.Item1 == "M" ? 1 : 2).ThenBy(v => v.Item2)) defaultChoice.Items.Add(Label(size, temp));
        if (old is not null && defaultChoice.Items.Contains(old)) defaultChoice.SelectedItem = old;
        else if (defaultChoice.Items.Count > 0) defaultChoice.SelectedIndex = 0;
        rendering = false;
    }

    /// <summary>Giữ cấu hình hiện có của món, kể cả khi bỏ chọn size tạo mới.</summary>
    private List<MenuVariant> Current() => dish.SelectedValue is long id ? data.CauHinh.Where(v => v.MaMon == id).ToList() : [];
    /// <summary>Tên tổ hợp dùng trong lựa chọn mặc định, không hiển thị ID kỹ thuật.</summary>
    private static string Label(string size, string temp) => $"{size} – {(temp == "HOT" ? "Nóng" : "Lạnh")}";
    /// <summary>Nhận giá VND nguyên, bỏ dấu phân cách nhóm do N0 hiển thị; không làm tròn giá lẻ.</summary>
    private static bool Price(object? input, out decimal? value)
    {
        var text = Convert.ToString(input)?.Trim() ?? "";
        if (input is decimal amount) { value = amount; return amount >= 0 && amount <= 999999999999999999m && amount == decimal.Truncate(amount); }
        if (text.Length == 0) { value = null; return true; }
        if (decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out var price) && price >= 0 && price <= 999999999999999999m && price == decimal.Truncate(price)) { value = price; return true; }
        value = null; return false;
    }

    /// <summary>Lưu bảng giá nguyên tử rồi trả món để trang gọi mở bảng công thức; retry giữ nguyên lệnh.</summary>
    private async Task Save()
    {
        if (busy || dish.SelectedValue is not long id || !grid.EndEdit()) return;
        if (pending is null)
        {
            var lines = Current().Select(v => new MenuVariantBatchLine(v.MaCauHinhMon, v.KichCo, v.NhietDo, v.GiaBan, false, v.PhienBanDuLieu)).ToDictionary(v => (v.KichCo, v.NhietDo));
            foreach (var size in sizes)
                foreach (var temp in new[] { "ICED", "HOT" }.Where(t => t == "ICED" ? cold.Checked : hot.Checked))
                {
                    if (prices.GetValueOrDefault(size + ":" + temp) is not { } price) { feedback.Text = $"Nhập giá cho {Label(size, temp)}."; return; }
                    var old = lines.GetValueOrDefault((size, temp));
                    lines[(size, temp)] = new(old?.MaCauHinhMon, size, temp, price, false, old?.PhienBanDuLieu);
                }
            if (lines.Count == 0 || defaultChoice.SelectedItem is not string def) { feedback.Text = "Chọn size, nhiệt độ và cấu hình mặc định."; return; }
            pending = new(id, lines.Values.Select(v => v with { LaMacDinh = Label(v.KichCo, v.NhietDo) == def }).ToList(), Guid.NewGuid());
        }
        busy = true; SetFields(false);
        try
        {
            await AdminUi.Recover(() => LiveSession.Api.SaveVariantBatchAsync(pending), () => LiveSession.Api.MenuReceiptAsync(pending.MaThaoTac));
            SavedDishId = pending.MaMon; uncertain = false; busy = false; DialogResult = DialogResult.OK; Close();
        }
        catch (CafeApiException ex)
        {
            uncertain = ex.StatusCode is null || (int)ex.StatusCode >= 500; feedback.Text = ex.Message;
            if (!uncertain) { pending = null; SetFields(true); }
            save.Text = uncertain ? "Kiểm tra / thử lại" : "Lưu & nhập công thức";
        }
        finally { busy = false; if (!save.IsDisposed) save.Enabled = true; }
    }
    /// <summary>Khóa nháp khi đang ghi hoặc chưa rõ kết quả để không thay nội dung lần thử lại.</summary>
    private void SetFields(bool enabled)
    {
        dish.Enabled = false; cold.Enabled = hot.Enabled = grid.Enabled = defaultChoice.Enabled = enabled; save.Enabled = enabled;
    }
}
