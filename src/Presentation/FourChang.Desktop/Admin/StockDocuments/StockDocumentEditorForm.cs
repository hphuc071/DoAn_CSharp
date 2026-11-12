using FourChang.Desktop.Shared.Controls;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.Contracts.Admin;
using System.Globalization;

namespace FourChang.Desktop.Admin.StockDocuments;

/// <summary>Biểu mẫu phiếu nháp: hướng dẫn theo loại phiếu, giữ nguyên lệnh lưu và cơ chế tra kết quả cũ.</summary>
public sealed class StockDocumentEditorForm : Form
{
    private readonly ComboBox type = Ui.Select("Nhập kho", "Xuất kho", "Kiểm kê", "Điều chỉnh");
    private readonly ComboBox purpose = Ui.Select("Hỏng", "Hết hạn", "Nội bộ");
    private readonly ComboBox ingredient = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 270 };
    private readonly TextBox source = Ui.Input("Ví dụ: Nhà cung cấp cà phê A", "", 440);
    private readonly TextBox reason = Ui.Input("Ví dụ: Nhập nguyên liệu theo phiếu giao hàng", "", 440);
    private readonly NumericUpDown quantity = new() { DecimalPlaces = 4, Maximum = 999999999999m, Width = 160, ThousandsSeparator = true };
    private readonly NumericUpDown price = new() { DecimalPlaces = 2, Maximum = 999999999999m, Width = 160, ThousandsSeparator = true };
    private readonly DataGridView grid = Ui.Grid(new([], []), 180);
    private readonly Label title = Ui.Label("", 16, true);
    private readonly Label workflow = Ui.Label("1. Thông tin phiếu  →  2. Thêm nguyên liệu  →  3. Lưu nháp  →  4. Ghi sổ", 9, false, CafeTheme.Muted);
    private readonly Label stockHint = Ui.Label("", 9, false, CafeTheme.Muted);
    private readonly Label feedback = Ui.Label("", 9, false, CafeTheme.Danger);
    private readonly Label lineCount = Ui.Label("Nguyên liệu trong phiếu", 11, true);
    private readonly Label empty = Ui.Label("Chưa có nguyên liệu. Chọn nguyên liệu, nhập số lượng rồi bấm “Thêm vào phiếu”.", 9, false, CafeTheme.Muted);
    private readonly Label countContext = Ui.Label("", 9, false, CafeTheme.Muted);
    private readonly Control sourceField;
    private readonly Control purposeField;
    private readonly Control quantityField;
    private readonly Control priceField;
    private readonly TableLayoutPanel ingredientFields;
    private readonly List<StockDocumentLineInput> lines = [];
    private readonly IReadOnlyList<StockView> stocks;
    private readonly StockDocumentView? existing;
    private readonly Button save;
    private readonly Button add;
    private readonly Button remove;
    private bool rendering;
    private StockDocumentInput? pending;

    /// <summary>Mã phiếu đã lưu thành công, giúp danh sách chọn đúng nháp vừa tạo.</summary>
    public long? SavedDocumentId { get; private set; }

    /// <summary>Dựng các nhóm thông tin; Lưu/Hủy luôn ở footer và không tự ghi sổ khi đóng biểu mẫu.</summary>
    public StockDocumentEditorForm(StockSnapshot inventory, StockDocumentView? document = null)
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new(1000, 860);
        MinimumSize = new(740, 560);
        StartPosition = FormStartPosition.CenterParent;
        Font = CafeTheme.Body();
        BackColor = CafeTheme.Cream;
        Name = "StockDocumentEditor";
        Text = document is null ? "Lập phiếu kho" : "Sửa nháp " + document.SoPhieuKho;
        stocks = inventory.NguyenLieu;
        existing = document;

        ingredient.BindingContext = new BindingContext();
        ingredient.DataSource = stocks.ToList();
        ingredient.DisplayMember = "TenNguyenLieu";
        ingredient.ValueMember = "MaNguyenLieu";
        type.Name = "StockDocumentType";
        purpose.Name = "StockDocumentPurpose";
        quantity.Name = "StockDocumentQuantity";
        price.Name = "StockDocumentPrice";
        source.Name = "StockDocumentSource";
        reason.Name = "StockDocumentReason";
        ingredient.Name = "StockDocumentIngredient";
        feedback.Name = "StockDocumentFeedback";
        // Không chừa một hàng lỗi rỗng; chỉ mở vùng phản hồi khi có nội dung cần xử lý.
        feedback.Visible = false;
        feedback.TextChanged += (_, _) => feedback.Visible = !string.IsNullOrWhiteSpace(feedback.Text);
        stockHint.Name = "StockDocumentStockHint";
        grid.Name = "StockDocumentLines";

        save = Ui.Button("Lưu phiếu nháp", () => { }, true);
        save.Name = "StockDocumentSave";
        add = Ui.Button("Thêm vào phiếu", AddLine, true);
        add.Name = "StockDocumentAddLine";
        remove = Ui.Button("Bỏ nguyên liệu đã chọn", RemoveLine);
        remove.Name = "StockDocumentRemoveLine";
        var cancel = Ui.Button("Đóng", () => Close());

        sourceField = AdminListLayout.Field("Nhà cung cấp / nguồn nhập (tùy chọn)", source);
        purposeField = AdminListLayout.Field("Lý do xuất nguyên liệu", purpose);
        var context = new Panel { Dock = DockStyle.Fill, Height = 76, Margin = Padding.Empty };
        sourceField.Dock = purposeField.Dock = DockStyle.Fill;
        countContext.Dock = DockStyle.Fill;
        countContext.Padding = new Padding(0, 24, 0, 0);
        context.Controls.AddRange([sourceField, purposeField, countContext]);
        var headerFields = AdminListLayout.Fields(AdminListLayout.Field("Loại phiếu", type), context);
        // Panel ngữ cảnh không được ép hàng cao theo kích thước mặc định 100px rồi bị cắt bởi hàng 84px.
        headerFields.Name = "StockDocumentHeaderFields";
        headerFields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        quantityField = AdminListLayout.Field("Số lượng nhập", quantity);
        priceField = AdminListLayout.Field("Đơn giá theo đơn vị cơ sở (đ)", price);
        ingredientFields = AdminListLayout.Fields(AdminListLayout.Field("Nguyên liệu", ingredient), quantityField, priceField);
        ingredientFields.Name = "StockDocumentIngredientFields";
        ConfigureGrid();

        if (document is not null)
        {
            type.SelectedIndex = Array.IndexOf(new[] { "RECEIPT", "ISSUE", "COUNT", "ADJUSTMENT" }, document.LoaiPhieu);
            source.Text = document.Nguon;
            reason.Text = document.LyDo;
            purpose.SelectedIndex = Array.IndexOf(new[] { "DAMAGED", "EXPIRED", "INTERNAL" }, document.MaMucDich) is var i && i >= 0 ? i : 0;
            lines.AddRange(document.ThanhPhan.Select(x => new StockDocumentLineInput(x.MaNguyenLieu, x.ThucDem ?? x.SoLuong, x.DonGiaNhap, x.TonSo, x.PhienBanTonKho, x.GhiChu)));
        }

        var content = AdminListLayout.Column(title, workflow, headerFields,
            AdminListLayout.Field("Lý do lập phiếu *", reason), ingredientFields, stockHint, feedback,
            Ui.Row(add, remove), lineCount, grid, empty,
            Ui.Label("Lưu nháp chưa thay đổi tồn kho. Sau khi lưu: chọn phiếu trong danh sách → Ghi sổ để cập nhật kho.", 9, false, CafeTheme.Muted));
        var body = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(20), BackColor = CafeTheme.Surface };
        body.Controls.Add(content);
        body.SizeChanged += (_, _) => WrapLabels(body);
        ingredient.SelectedIndexChanged += (_, _) => ShowStock();
        type.SelectedIndexChanged += (_, _) => ShowStock();
        grid.SelectionChanged += (_, _) => LoadSelectedLine();
        save.Click += async (_, _) => await Save();
        AdminUi.Compose(this, body, save, cancel);
        Render();
        ShowStock();
        LoadSelectedLine();
    }

    /// <summary>Tạo cột rõ nghĩa; khóa kỹ thuật chỉ dùng chọn đúng dòng, không chiếm chỗ trên bảng.</summary>
    private void ConfigureGrid()
    {
        grid.AutoGenerateColumns = false;
        foreach (var (name, caption, weight) in new[]
        {
            ("MaNguyenLieu", "Mã", 30f), ("NguyenLieu", "Nguyên liệu", 180f), ("DonVi", "Đơn vị", 55f),
            ("SoLuong", "Số lượng nhập", 100f), ("DonGiaNhap", "Đơn giá (đ / đơn vị)", 130f), ("TonSoDaChot", "Tồn sổ đối chiếu", 110f)
        })
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = name, DataPropertyName = name, HeaderText = caption, FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable });
        grid.Columns["MaNguyenLieu"].Visible = false;
        foreach (var name in new[] { "SoLuong", "DonGiaNhap", "TonSoDaChot" })
        {
            grid.Columns[name].DefaultCellStyle.Format = "#,##0.####";
            grid.Columns[name].DefaultCellStyle.FormatProvider = CultureInfo.GetCultureInfo("vi-VN");
            grid.Columns[name].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
    }

    /// <summary>Chỉ hiện trường cần thiết: nhập có nguồn/giá; xuất có mục đích; kiểm kê/điều chỉnh nhập số tồn thực tế.</summary>
    private void ShowStock()
    {
        var receipt = type.SelectedIndex == 0;
        var issue = type.SelectedIndex == 1;
        sourceField.Visible = receipt;
        purposeField.Visible = issue;
        countContext.Visible = !receipt && !issue;
        countContext.Text = "Nhập số tồn thực tế sau đối chiếu, không nhập số chênh lệch.\nHệ thống kiểm tra lại mốc tồn khi ghi sổ.";
        priceField.Visible = receipt;
        ingredientFields.ColumnStyles[0].Width = receipt ? 40 : 60;
        ingredientFields.ColumnStyles[1].Width = receipt ? 30 : 40;
        ingredientFields.ColumnStyles[2].Width = receipt ? 30 : 0;
        title.Text = "Phiếu " + type.Text.ToLower(CultureInfo.GetCultureInfo("vi-VN"));
        grid.Columns["DonGiaNhap"].Visible = receipt;
        grid.Columns["TonSoDaChot"].Visible = !receipt && !issue;
        grid.Columns["SoLuong"].HeaderText = receipt ? "Số lượng nhập" : issue ? "Số lượng xuất" : "Số tồn thực đếm";
        if (ingredient.SelectedItem is not StockView n) return;
        var unit = n.DonVi;
        var caption = receipt ? "Số lượng nhập" : issue ? "Số lượng xuất" : type.SelectedIndex == 2 ? "Số lượng thực đếm" : "Tồn sau điều chỉnh";
        ((TableLayoutPanel)quantityField).GetControlFromPosition(0, 0)!.Text = $"{caption} ({unit}) *";
        ((TableLayoutPanel)priceField).GetControlFromPosition(0, 0)!.Text = $"Đơn giá / {unit} (đ, tùy chọn)";
        var example = unit == "g" ? "Ví dụ: 2 kg → nhập 2.000 g." : "Ví dụ: 2 lít → nhập 2.000 ml.";
        stockHint.Text = $"Trong kho: {Format(n.TonThucTe)} {unit}  ·  Đang giữ: {Format(n.GiuCho)} {unit}. {example}";
        if (receipt) stockHint.Text += $"\nĐơn giá tính cho 1 {unit}; để 0 nếu chưa ghi nhận giá.";
        if (!receipt && !issue) stockHint.Text += $"\nMốc đối chiếu: {n.CapNhatLuc:dd/MM/yyyy HH:mm:ss}.";
    }

    /// <summary>Cho nhãn trợ giúp xuống dòng khi thu nhỏ; không ép ô nhập hoặc footer ra ngoài cửa sổ.</summary>
    private void WrapLabels(Control body)
    {
        var width = Math.Max(200, body.ClientSize.Width - body.Padding.Horizontal);
        foreach (var label in new[] { workflow, stockHint, feedback, empty }) label.MaximumSize = new Size(width, 0);
    }

    /// <summary>Định dạng để đọc dễ; giữ đầy đủ tối đa bốn chữ số thập phân của dữ liệu kho.</summary>
    private static string Format(decimal? value) => value?.ToString("#,##0.####", CultureInfo.GetCultureInfo("vi-VN")) ?? "Chưa xác định";

    /// <summary>Chặn lỗi nhập ngay tại dòng theo cùng quy tắc server; cập nhật dòng cùng nguyên liệu thay vì thêm trùng.</summary>
    private void AddLine()
    {
        if (pending is not null) { feedback.Text = "Chưa rõ kết quả lưu. Tra kết quả trước khi đổi nội dung phiếu."; return; }
        if (ingredient.SelectedItem is not StockView n || n.TonThucTe is null || n.PhienBanDuLieu is null)
        { feedback.Text = "Chưa tải được tồn kho của nguyên liệu. Đóng và tải lại danh sách kho."; return; }
        if (type.SelectedIndex < 2 && quantity.Value <= 0)
        { feedback.Text = $"Nhập số lượng lớn hơn 0 {n.DonVi} trước khi thêm vào phiếu."; quantity.Focus(); return; }
        lines.RemoveAll(x => x.MaNguyenLieu == n.MaNguyenLieu);
        lines.Add(new(n.MaNguyenLieu, quantity.Value, type.SelectedIndex == 0 ? price.Value : null,
            type.SelectedIndex >= 2 ? n.TonThucTe : null, type.SelectedIndex >= 2 ? n.PhienBanDuLieu : null));
        feedback.Text = "";
        Render(n.MaNguyenLieu);
    }

    /// <summary>Bind lại bảng và chọn đúng nguyên liệu vừa sửa, không dùng số thứ tự làm mã nguyên liệu.</summary>
    private void Render(long? selected = null)
    {
        rendering = true;
        try
        {
            grid.DataSource = lines.Select(x => new
            {
                x.MaNguyenLieu,
                NguyenLieu = stocks.Single(s => s.MaNguyenLieu == x.MaNguyenLieu).TenNguyenLieu,
                DonVi = stocks.Single(s => s.MaNguyenLieu == x.MaNguyenLieu).DonVi,
                x.SoLuong, x.DonGiaNhap, x.TonSoDaChot
            }).ToList();
            if (selected is not null)
                foreach (DataGridViewRow row in grid.Rows)
                    if (row.Cells["MaNguyenLieu"].Value is long id && id == selected) grid.CurrentCell = row.Cells["NguyenLieu"];
            lineCount.Text = $"Nguyên liệu trong phiếu ({lines.Count})";
            grid.Visible = lines.Count > 0;
            empty.Visible = lines.Count == 0;
            remove.Enabled = lines.Count > 0;
        }
        finally { rendering = false; }
    }

    /// <summary>Chọn dòng để đưa thông tin lên ô nhập; thao tác chọn không sửa dữ liệu phiếu.</summary>
    private void LoadSelectedLine()
    {
        if (rendering || grid.CurrentRow?.Cells["MaNguyenLieu"].Value is not long id) return;
        var line = lines.Single(x => x.MaNguyenLieu == id);
        ingredient.SelectedValue = id;
        quantity.Value = Math.Clamp(line.SoLuong, quantity.Minimum, quantity.Maximum);
        price.Value = Math.Clamp(line.DonGiaNhap ?? 0, price.Minimum, price.Maximum);
    }

    /// <summary>Bỏ nguyên liệu đang chọn khỏi bản nháp; không thực hiện bất kỳ lệnh trừ kho nào.</summary>
    private void RemoveLine()
    {
        if (pending is not null) { feedback.Text = "Chưa rõ kết quả lưu. Tra kết quả trước khi thay đổi dòng."; return; }
        if (grid.CurrentRow?.Cells["MaNguyenLieu"].Value is long id)
        {
            lines.RemoveAll(x => x.MaNguyenLieu == id);
            Render();
        }
    }

    /// <summary>Lưu online và giữ nguyên payload/mã khi chưa rõ kết quả; nháp thành công vẫn chưa cập nhật tồn.</summary>
    private async Task Save()
    {
        if (pending is null && (lines.Count == 0 || string.IsNullOrWhiteSpace(reason.Text)))
        {
            feedback.Text = lines.Count == 0 ? "Hãy thêm ít nhất một nguyên liệu vào phiếu." : "Hãy nhập lý do lập phiếu.";
            if (lines.Count == 0) quantity.Focus(); else reason.Focus();
            return;
        }
        save.Enabled = false;
        try
        {
            pending ??= new(new[] { "RECEIPT", "ISSUE", "COUNT", "ADJUSTMENT" }[type.SelectedIndex], source.Text,
                type.SelectedIndex == 1 ? new[] { "DAMAGED", "EXPIRED", "INTERNAL" }[purpose.SelectedIndex] : null,
                reason.Text, lines.ToList(), existing?.PhienBanDuLieu, Guid.NewGuid());
            var receipt = await AdminUi.Recover(() => LiveSession.Api.SaveStockDocumentAsync(existing?.MaPhieuKho, pending),
                () => LiveSession.Api.StockDocumentReceiptAsync(pending.MaThaoTac));
            SavedDocumentId = receipt.Ma;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (FourChang.Presentation.Shared.ApiClients.CafeApiException ex)
        {
            feedback.Text = ex.Message;
            if (ex.StatusCode is not null && (int)ex.StatusCode < 500) pending = null;
        }
        finally { if (!save.IsDisposed) save.Enabled = true; }
    }
}
