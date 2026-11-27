// UC: UC-ADM-04
// Chức năng: Nhập ngày, nguồn, nguyên liệu, số lượng và giá của phiếu.
// Cần nhớ: Lưu nháp chưa cộng/trừ kho; việc xác nhận phải được Backend chấp nhận.

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
    private readonly ComboBox type = Ui.Select("Nhập kho", "Xuất kho");
    private readonly DateTimePicker date = new() { Name = "StockDocumentDate", Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 190,
        Value = DateTime.UtcNow.AddHours(7).Date };
    private readonly Label total = Ui.Label("", 12, true, CafeTheme.Coffee);
    private readonly ComboBox purpose = Ui.Select("Hỏng", "Hết hạn");
    private readonly ComboBox ingredient = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 270 };
    private readonly TextBox source = Ui.Input("Nhà cung cấp / nguồn nhập", "", 440);
    private readonly TextBox reason = Ui.Input("Ghi chú", "", 440);
    private readonly StockAmountInput quantity = new(4) { Maximum = 999999999999m, Width = 160, ThousandsSeparator = true };
    private readonly StockAmountInput price = new(2) { Maximum = 999999999999m, Width = 160, ThousandsSeparator = true };
    private readonly DataGridView grid = Ui.Grid(new([], []), 180);
    private readonly Label title = Ui.Label("", 16, true);
    private readonly Label workflow = Ui.Label("Xác nhận để khóa phiếu. Ngày tương lai: chờ đến ngày mới cập nhật kho.", 9, false, CafeTheme.Muted);
    private readonly Label stockHint = Ui.Label("", 9, false, CafeTheme.Muted);
    private readonly Label feedback = Ui.Label("", 9, false, CafeTheme.Danger);
    private readonly Label lineCount = Ui.Label("Nguyên liệu trong phiếu", 11, true);
    private readonly Label empty = Ui.Label("Chưa có nguyên liệu. Chọn nguyên liệu, nhập số lượng rồi bấm “Thêm vào phiếu”.", 9, false, CafeTheme.Muted);
    private readonly Label lineTotal = Ui.Label("", 11, true, CafeTheme.Coffee);
    private readonly Control sourceField;
    private readonly Control purposeField;
    private readonly Control reasonField;
    private readonly Control quantityField;
    private readonly Control priceField;
    private readonly TableLayoutPanel ingredientFields;
    private readonly List<StockDocumentLineInput> lines = [];
    private readonly IReadOnlyList<StockView> stocks;
    private readonly Button save;
    private readonly Button confirm;
    private readonly Button close;
    private readonly Control entryContent;
    private long? workingId;
    private byte[]? workingVersion;
    private StockDocumentAction? pendingPost;
    private bool saving;
    private readonly Button add;
    private readonly Button remove;
    private readonly Button newLine;
    private long? editingIngredient;
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
        Text = document is null ? "Lập phiếu kho" : (document.TrangThai == "FAILED" ? "Sửa phiếu cần kiểm tra " : "Sửa nháp ") + document.SoPhieuKho;
        if (document is not null && document.LoaiPhieu is not ("RECEIPT" or "ISSUE"))
            throw new ArgumentException("Phiếu lịch sử này chỉ được xem chi tiết.", nameof(document));
        stocks = inventory.NguyenLieu;
        workingId = document?.MaPhieuKho; workingVersion = document?.PhienBanDuLieu;

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

        save = Ui.Button("Lưu phiếu nháp", () => { });
        save.Name = "StockDocumentSave";
        add = Ui.Button("Thêm vào phiếu", AddLine, true);
        add.Name = "StockDocumentAddLine";
        remove = Ui.Button("Bỏ nguyên liệu đã chọn", RemoveLine);
        remove.Name = "StockDocumentRemoveLine";
        newLine = Ui.Button("Nhập dòng mới", ResetLine);
        newLine.Name = "StockDocumentNewLine";
        lineTotal.Name = "StockDocumentLineTotal";
        confirm = Ui.Button("Xác nhận phiếu", () => { }, true);
        confirm.Name = "StockDocumentConfirm";
        close = Ui.Button("Đóng", () => Close());

        sourceField = AdminListLayout.Field("Nguồn nhập (tùy chọn)", source);
        reasonField = AdminListLayout.Field("Ghi chú (tùy chọn)", reason);
        purposeField = AdminListLayout.Field("Lý do xuất nguyên liệu", purpose);
        var context = new Panel { Dock = DockStyle.Fill, Height = 76, Margin = Padding.Empty };
        sourceField.Dock = purposeField.Dock = DockStyle.Fill;
        context.Controls.AddRange([sourceField, purposeField]);
        var headerFields = AdminListLayout.Fields(AdminListLayout.Field("Loại phiếu", type), AdminListLayout.Field("Ngày nhập / xuất", date), context);
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
            type.SelectedIndex = Array.IndexOf(new[] { "RECEIPT", "ISSUE" }, document.LoaiPhieu);
            date.Value = (document.NgayThucHien ?? DateOnly.FromDateTime(document.TaoLuc.AddHours(7))).ToDateTime(TimeOnly.MinValue);
            source.Text = document.Nguon;
            reason.Text = document.LyDo;
            purpose.SelectedIndex = Array.IndexOf(new[] { "DAMAGED", "EXPIRED" }, document.MaMucDich) is var i && i >= 0 ? i : 0;
            lines.AddRange(document.ThanhPhan.Select(x => new StockDocumentLineInput(x.MaNguyenLieu, x.ThucDem ?? x.SoLuong, x.DonGiaNhap, x.TonSo, x.PhienBanTonKho, x.GhiChu)));
        }

        var content = AdminListLayout.Column(title, workflow,
            Ui.Label("1. Thông tin phiếu", 11, true), headerFields, reasonField,
            Ui.Label("2. Chọn nguyên liệu và nhập số lượng", 11, true), ingredientFields, stockHint,
            lineTotal, feedback, Ui.Row(add, newLine, remove), lineCount, grid, empty, total);
        var body = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(20), BackColor = CafeTheme.Surface };
        body.Controls.Add(content);
        entryContent = body;
        body.SizeChanged += (_, _) => WrapLabels(body);
        ingredient.SelectedIndexChanged += (_, _) => ShowStock();
        type.SelectedIndexChanged += (_, _) => ShowStock();
        grid.CurrentCellChanged += (_, _) => LoadSelectedLine();
        quantity.ValueChanged += (_, _) => UpdateLineTotal();
        price.ValueChanged += (_, _) => UpdateLineTotal();
        save.Click += async (_, _) => await Save(false);
        confirm.Click += async (_, _) => await Save(true);
        FormClosing += (_, e) =>
        {
            if (saving) { e.Cancel = true; return; }
            // Nếu ghi sổ lỗi nhưng nháp đã lưu, trang ngoài vẫn tải lại đúng phiếu này khi đóng.
            if (SavedDocumentId is not null && DialogResult == DialogResult.None) DialogResult = DialogResult.OK;
        };
        AdminUi.Compose(this, body, save, confirm, close);
        Render();
        ShowStock();
        ResetLine();
    }

    /// <summary>Tạo cột rõ nghĩa; khóa kỹ thuật chỉ dùng chọn đúng dòng, không chiếm chỗ trên bảng.</summary>
    private void ConfigureGrid()
    {
        grid.AutoGenerateColumns = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        foreach (var (name, caption, weight) in new[]
        {
            ("MaNguyenLieu", "Mã", 30f), ("NguyenLieu", "Nguyên liệu", 180f), ("DonVi", "Đơn vị", 55f),
            ("SoLuong", "Số lượng nhập", 100f), ("DonGiaNhap", "Đơn giá (đ / đơn vị)", 130f), ("ThanhTien", "Thành tiền (đ)", 120f), ("TonSoDaChot", "Tồn sổ đối chiếu", 110f)
        })
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = name, DataPropertyName = name, HeaderText = caption, FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable });
        grid.Columns["MaNguyenLieu"].Visible = false;
        grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex].Name != "DonGiaNhap" || e.Value is not decimal value) return;
            e.Value = Format(value) + " đ/" + grid.Rows[e.RowIndex].Cells["DonVi"].Value;
            e.FormattingApplied = true;
        };
        foreach (var name in new[] { "SoLuong", "DonGiaNhap", "ThanhTien", "TonSoDaChot" })
        {
            grid.Columns[name].DefaultCellStyle.Format = "#,##0.####";
            grid.Columns[name].DefaultCellStyle.FormatProvider = CultureInfo.GetCultureInfo("vi-VN");
            grid.Columns[name].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.Columns[name].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            // Cùng lề phải cho tiêu đề và giá trị, tránh cảm giác dữ liệu lệch sang cột kế bên.
            grid.Columns[name].DefaultCellStyle.Padding = new Padding(10, 4, 14, 4);
            grid.Columns[name].HeaderCell.Style.Padding = new Padding(10, 0, 14, 0);
        }
        grid.Columns["NguyenLieu"].MinimumWidth = 160;
        grid.Columns["NguyenLieu"].DefaultCellStyle.Padding = new Padding(12, 4, 12, 4);
        grid.Columns["NguyenLieu"].HeaderCell.Style.Padding = new Padding(12, 0, 12, 0);
        grid.Columns["DonVi"].MinimumWidth = 85;
        grid.Columns["DonVi"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.Columns["DonVi"].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.Columns["DonVi"].DefaultCellStyle.Padding = grid.Columns["DonVi"].HeaderCell.Style.Padding = new Padding(1);
        grid.Columns["SoLuong"].MinimumWidth = 120;
        grid.Columns["DonGiaNhap"].MinimumWidth = 160;
        grid.Columns["ThanhTien"].MinimumWidth = 135;
        grid.Columns["DonGiaNhap"].HeaderText = "Đơn giá (đ)";
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.ColumnHeadersHeight = 50;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    }

    /// <summary>Chỉ hiện trường cần thiết: nhập có nguồn/giá; xuất có lý do hỏng/hết hạn.</summary>
    private void ShowStock()
    {
        var receipt = type.SelectedIndex == 0;
        var issue = type.SelectedIndex == 1;
        ((TableLayoutPanel)reasonField).GetControlFromPosition(0, 0)!.Text = receipt ? "Ghi chú (tùy chọn)" : "Lý do lập phiếu *";
        date.Enabled = true;
        total.Visible = receipt;
        sourceField.Visible = receipt;
        purposeField.Visible = issue;
        priceField.Visible = receipt;
        ingredientFields.ColumnStyles[0].Width = receipt ? 40 : 60;
        ingredientFields.ColumnStyles[1].Width = receipt ? 30 : 40;
        ingredientFields.ColumnStyles[2].Width = receipt ? 30 : 0;
        title.Text = "Phiếu " + type.Text.ToLower(CultureInfo.GetCultureInfo("vi-VN"));
        grid.Columns["DonGiaNhap"].Visible = receipt;
        grid.Columns["ThanhTien"].Visible = receipt;
        grid.Columns["TonSoDaChot"].Visible = false;
        grid.Columns["SoLuong"].HeaderText = receipt ? "Số lượng nhập" : "Số lượng xuất";
        if (ingredient.SelectedItem is not StockView n) return;
        var unit = n.DonVi;
        var caption = receipt ? "Số lượng nhập" : "Số lượng xuất";
        ((TableLayoutPanel)quantityField).GetControlFromPosition(0, 0)!.Text = $"{caption} ({unit}) *";
        ((TableLayoutPanel)priceField).GetControlFromPosition(0, 0)!.Text = $"Đơn giá (đ/{unit}, tùy chọn)";
        stockHint.Text = $"Trong kho: {Format(n.TonThucTe)} {unit}  ·  Đang giữ: {Format(n.GiuCho)} {unit}";
        UpdateLineTotal();
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
        if (ingredient.SelectedItem is not StockView n)
        { feedback.Text = "Chọn nguyên liệu để thêm vào phiếu."; return; }
        if (quantity.Value <= 0)
        { feedback.Text = $"Nhập số lượng lớn hơn 0 {n.DonVi} trước khi thêm vào phiếu."; quantity.Focus(); return; }
        if (lines.Any(x => x.MaNguyenLieu == n.MaNguyenLieu && x.MaNguyenLieu != editingIngredient))
        {
            feedback.Text = "Nguyên liệu đã có trong phiếu. Chọn dòng trong bảng để sửa số lượng.";
            return;
        }
        if (editingIngredient is long previous) lines.RemoveAll(x => x.MaNguyenLieu == previous);
        lines.Add(new(n.MaNguyenLieu, quantity.Value, type.SelectedIndex == 0 ? price.Value : null));
        feedback.Text = "";
        Render();
        ResetLine();
    }

    /// <summary>Thêm xong trả về chế độ nhập dòng mới; chỉ sửa dòng khi người dùng chủ động chọn trong bảng.</summary>
    private void ResetLine()
    {
        if (pending is not null) return;
        rendering = true;
        try { grid.ClearSelection(); grid.CurrentCell = null; }
        finally { rendering = false; }
        editingIngredient = null;
        quantity.Value = 0; price.Value = 0;
        add.Text = "Thêm vào phiếu";
        newLine.Enabled = false; remove.Enabled = false;
        ingredient.Focus();
        UpdateLineTotal();
    }

    /// <summary>Tạm tính một dòng ngay khi nhập; đơn vị lấy từ nguyên liệu, không đổi giá trị gửi API.</summary>
    private void UpdateLineTotal()
    {
        lineTotal.Visible = type.SelectedIndex == 0;
        lineTotal.Text = "Thành tiền dòng: " + Format(quantity.Value * price.Value) + " đ";
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
                x.SoLuong, x.DonGiaNhap, ThanhTien = x.DonGiaNhap * x.SoLuong, x.TonSoDaChot
            }).ToList();
            if (selected is not null)
                foreach (DataGridViewRow row in grid.Rows)
                    if (row.Cells["MaNguyenLieu"].Value is long id && id == selected) grid.CurrentCell = row.Cells["NguyenLieu"];
            total.Text = "Tổng tiền nhập: " + Format(lines.Sum(x => x.SoLuong * (x.DonGiaNhap ?? 0))) + " đ";
            lineCount.Text = $"3. Danh sách nguyên liệu ({lines.Count}) — chọn dòng để sửa";
            grid.Height = Math.Clamp(grid.ColumnHeadersHeight + lines.Count * grid.RowTemplate.Height + 6, 100, 240);
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
        editingIngredient = id;
        add.Text = "Cập nhật dòng";
        newLine.Enabled = true; remove.Enabled = true;
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
            ResetLine();
        }
    }

    /// <summary>Đóng cả form modal và modeless sau khi API đã trả kết quả thành công.</summary>
    private void Complete()
    {
        saving = false;
        DialogResult = DialogResult.OK;
        Close();
    }

    /// <summary>Lưu nháp trước, rồi xác nhận bằng đúng ID/rowversion. Thử lại không tạo thêm phiếu hoặc ghi sổ hai lần.</summary>
    private async Task Save(bool post)
    {
        if (saving) return;
        if (pending is null && pendingPost is null)
        {
            // Không bỏ sót dòng đang nhập khi người dùng bấm Lưu hoặc Xác nhận trực tiếp.
            if (quantity.Value > 0 || editingIngredient is not null)
            {
                AddLine();
                if (!string.IsNullOrEmpty(feedback.Text)) return;
            }
            if (lines.Count == 0 || (type.SelectedIndex != 0 && string.IsNullOrWhiteSpace(reason.Text)))
            {
                feedback.Text = lines.Count == 0 ? "Hãy thêm ít nhất một nguyên liệu hoặc nhập số lượng cho dòng mới." : "Hãy nhập lý do lập phiếu.";
                return;
            }
        }
        saving = true;
        save.Enabled = confirm.Enabled = close.Enabled = entryContent.Enabled = false;
        try
        {
            if (pendingPost is null)
            {
                pending ??= new(new[] { "RECEIPT", "ISSUE" }[type.SelectedIndex], source.Text,
                    type.SelectedIndex == 1 ? new[] { "DAMAGED", "EXPIRED" }[purpose.SelectedIndex] : null,
                    reason.Text, lines.ToList(), workingVersion, Guid.NewGuid(), DateOnly.FromDateTime(date.Value));
                var receipt = await AdminUi.Recover(() => LiveSession.Api.SaveStockDocumentAsync(workingId, pending),
                    () => LiveSession.Api.StockDocumentReceiptAsync(pending.MaThaoTac));
                workingId = SavedDocumentId = receipt.Ma;
                workingVersion = receipt.PhienBanDuLieu;
                pending = null;
                if (!post) { Complete(); return; }
                pendingPost = new("Xác nhận phiếu " + type.Text.ToLower(CultureInfo.GetCultureInfo("vi-VN")), workingVersion!, Guid.NewGuid());
            }
            var posted = await AdminUi.Recover(() => LiveSession.Api.ActStockDocumentAsync(workingId!.Value, "post", pendingPost),
                () => LiveSession.Api.StockDocumentReceiptAsync(pendingPost.MaThaoTac));
            workingVersion = posted.PhienBanDuLieu;
            pendingPost = null;
            Complete();
        }
        catch (FourChang.Presentation.Shared.ApiClients.CafeApiException ex)
        {
            feedback.Text = (SavedDocumentId is null ? "" : "Đã lưu nháp, chưa xác nhận được. ") + ex.Message;
            if (ex.StatusCode is not null && (int)ex.StatusCode < 500) { pending = null; pendingPost = null; }
        }
        finally
        {
            saving = false;
            if (!IsDisposed)
            {
                entryContent.Enabled = pending is null && pendingPost is null;
                save.Enabled = pendingPost is null;
                confirm.Enabled = close.Enabled = true;
            }
        }
    }

}

/// <summary>Ô số riêng của phiếu kho: phân nhóm hàng nghìn bằng dấu chấm, dấu phẩy thập phân và không số 0 thừa.</summary>
internal sealed class StockAmountInput : NumericUpDown
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    internal StockAmountInput(int decimals) { DecimalPlaces = decimals; ThousandsSeparator = true; }

    /// <summary>Giữ nguyên độ chính xác g/ml và giá, chỉ rút gọn phần trình bày.</summary>
    protected override void UpdateEditText()
    {
        if (UserEdit) { ValidateEditText(); return; }
        ChangingText = true;
        Text = Value.ToString(DecimalPlaces == 0 ? "#,##0" : "#,##0." + new string('#', DecimalPlaces), Vietnamese);
    }

    /// <summary>Đọc đúng cùng định dạng Việt Nam khi gõ/dán, độc lập vùng Windows đang dùng.</summary>
    protected override void ValidateEditText()
    {
        var valid = decimal.TryParse(Text, NumberStyles.Number, Vietnamese, out var amount);
        UserEdit = false;
        if (valid) Value = Math.Clamp(decimal.Round(amount, DecimalPlaces), Minimum, Maximum);
        UpdateEditText();
    }

    /// <summary>Chấp nhận dấu phẩy thập phân bằng cơ chế bàn phím native, không đổi culture toàn ứng dụng.</summary>
    protected override void OnTextBoxKeyPress(object? source, KeyPressEventArgs e)
    {
        var previous = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = Vietnamese; base.OnTextBoxKeyPress(source, e); }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    /// <summary>Chốt nội dung đang nhập trước tăng/giảm để lớp native không phân tích lại theo vùng Windows.</summary>
    public override void UpButton() { if (UserEdit) ValidateEditText(); base.UpButton(); }
    public override void DownButton() { if (UserEdit) ValidateEditText(); base.DownButton(); }
}
