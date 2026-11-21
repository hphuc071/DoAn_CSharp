using FourChang.Desktop.Admin.StockDocuments;
using FourChang.Desktop.Shared.Session;
using FourChang.Presentation.Shared.Contracts.Admin;

namespace FourChang.Tests.Admin;

/// <summary>Kiểm tra editor phiếu kho thật trên STA: trường theo loại, số lượng, sửa dòng và nháp không đổi tồn.</summary>
internal static class StockDocumentEditorChecks
{
    /// <summary>Thao tác bằng control của form, lưu qua API/SQL fixture; không ghi vào dữ liệu quán.</summary>
    internal static async Task Run(string output)
    {
        File.WriteAllText(Path.Combine(output, "stock-stage.txt"), "load inventory");
        var before = await LiveSession.Api.GetInventoryAsync();
        var material = before.NguyenLieu.First(x => x.DonVi == "g" && x.TonThucTe is not null);
        using var editor = new StockDocumentEditorForm(before);
        editor.StartPosition = FormStartPosition.Manual;
        editor.Location = new Point(-12000, -12000);
        File.WriteAllText(Path.Combine(output, "stock-stage.txt"), "show editor");
        editor.Show();
        await Task.Delay(100);
        var controls = Descendants(editor).Where(x => !string.IsNullOrEmpty(x.Name)).ToDictionary(x => x.Name);
        var date = (DateTimePicker)controls["StockDocumentDate"];
        Assert.Equal("dd/MM/yyyy", date.CustomFormat);
        date.Value = DateTime.UtcNow.AddHours(7).Date.AddDays(1);
        var type = (ComboBox)controls["StockDocumentType"];
        Assert.Equal(2, type.Items.Count);
        var ingredient = (ComboBox)controls["StockDocumentIngredient"];
        var quantity = (NumericUpDown)controls["StockDocumentQuantity"];
        var price = (NumericUpDown)controls["StockDocumentPrice"];
        var reason = (TextBox)controls["StockDocumentReason"];
        var feedback = (Label)controls["StockDocumentFeedback"];
        var grid = (DataGridView)controls["StockDocumentLines"];
        var add = (Button)controls["StockDocumentAddLine"];
        var remove = (Button)controls["StockDocumentRemoveLine"];
        var save = (Button)controls["StockDocumentSave"];
        Assert.False(controls["StockDocumentPurpose"].Visible);
        Assert.True(price.Visible);
        Assert.True(controls["StockDocumentSource"].Visible);
        type.SelectedIndex = 1;
        Assert.True(controls["StockDocumentPurpose"].Visible);
        Assert.False(price.Visible);
        Assert.False(controls["StockDocumentSource"].Visible);
        type.SelectedIndex = 0;
        quantity.Value = 0;
        add.PerformClick();
        Assert.Empty(grid.Rows.Cast<DataGridViewRow>());
        Assert.Contains("lớn hơn 0", feedback.Text);
        save.PerformClick();
        Assert.True(editor.Visible);
        Assert.Contains("ít nhất một nguyên liệu", feedback.Text);
        var liquid = before.NguyenLieu.First(x => x.DonVi == "ml");
        ingredient.SelectedValue = liquid.MaNguyenLieu;
        Assert.DoesNotContain("Ví dụ", controls["StockDocumentStockHint"].Text);
        ingredient.SelectedValue = material.MaNguyenLieu;
        Assert.DoesNotContain("Ví dụ", controls["StockDocumentStockHint"].Text);
        date.Value = DateTime.UtcNow.AddHours(7).Date.AddDays(1);
        Assert.True(date.Enabled);
        // Định dạng không phụ thuộc Windows en-US; nhập lại chuỗi nhóm nghìn vẫn giữ đúng giá trị.
        quantity.Value = 2000;
        Assert.Equal("2.000", quantity.Text);
        quantity.Text = "2.000,25";
        Assert.Equal(2000.25m, quantity.Value);
        price.Value = 20000;
        Assert.Equal("20.000", price.Text);
        price.Text = "20.000,5";
        Assert.Equal(20000.5m, price.Value);
        quantity.UpButton();
        Assert.Equal(2001.25m, quantity.Value);
        quantity.DownButton();
        Assert.Equal(2000.25m, quantity.Value);
        quantity.Value = 2000;
        price.Value = 100;
        add.PerformClick();
        Assert.Equal(0, quantity.Value);
        Assert.Equal("Thêm vào phiếu", add.Text);
        quantity.Value = 1;
        add.PerformClick();
        Assert.Contains("đã có trong phiếu", feedback.Text);
        Assert.Single(grid.Rows.Cast<DataGridViewRow>());
        grid.CurrentCell = grid.Rows[0].Cells["NguyenLieu"];
        grid.Rows[0].Selected = true;
        Assert.Equal("Cập nhật dòng", add.Text);
        quantity.Value = 2500;
        add.PerformClick();
        Assert.Single(grid.Rows.Cast<DataGridViewRow>());
        Assert.Equal(2500m, grid.Rows[0].Cells["SoLuong"].Value);
        Assert.Equal(250000m, grid.Rows[0].Cells["ThanhTien"].Value);
        Assert.Equal("0", quantity.Text);
        Assert.Equal("2.500", grid.Rows[0].Cells["SoLuong"].FormattedValue);
        Assert.Equal("100 đ/g", grid.Rows[0].Cells["DonGiaNhap"].FormattedValue);
        Assert.DoesNotContain(Descendants(editor).OfType<TextBox>(), x => x.PlaceholderText.Contains("Ví dụ"));
        Assert.False(grid.Columns["TonSoDaChot"].Visible);
        Assert.False(grid.Columns["MaNguyenLieu"].Visible);
        ingredient.SelectedValue = liquid.MaNguyenLieu;
        quantity.Value = 500; price.Value = 10;
        Assert.Equal("Thành tiền dòng: 5.000 đ", controls["StockDocumentLineTotal"].Text);
        add.PerformClick();
        Assert.Equal(2, grid.RowCount);
        var liquidRow = grid.Rows.Cast<DataGridViewRow>().Single(r => (long)r.Cells["MaNguyenLieu"].Value == liquid.MaNguyenLieu);
        grid.CurrentCell = liquidRow.Cells["NguyenLieu"];
        remove.PerformClick();
        Assert.Single(grid.Rows.Cast<DataGridViewRow>());
        reason.Text = "Nhập hàng qua biểu mẫu " + Guid.NewGuid().ToString("N");
        var savedReason = reason.Text;
        ((TextBox)controls["StockDocumentSource"]).Text = "Nhà cung cấp cà phê mẫu";
        foreach (var row in Descendants(editor).OfType<TableLayoutPanel>().Where(x => x.Name.EndsWith("Fields")))
            foreach (Control field in row.Controls)
                if (field.Visible) Assert.True(field.Bottom <= row.ClientSize.Height, $"Trường nhập bị cắt tại {row.Name}: {field.Bounds} / {row.ClientSize}");
        File.WriteAllLines(Path.Combine(output, "UC-ADM-06-editor-layout.txt"), Descendants(editor).Select(c => $"{c.GetType().Name} {c.Name} parent={c.Parent?.GetType().Name} bounds={c.Bounds} client={c.ClientSize} visible={c.Visible}"));
        Capture(editor, Path.Combine(output, "UC-ADM-06-editor.png"));
        editor.Size = editor.MinimumSize;
        await Task.Delay(100);
        var bounds = editor.RectangleToClient(save.RectangleToScreen(save.ClientRectangle));
        Assert.True(bounds.Bottom <= editor.ClientSize.Height && bounds.Top >= 0);
        Capture(editor, Path.Combine(output, "UC-ADM-06-editor-compact.png"));
        save.PerformClick();
        for (var i = 0; i < 100 && editor.Visible; i++) await Task.Delay(100);
        Assert.Equal(DialogResult.OK, editor.DialogResult);
        var draft = (await LiveSession.Api.GetStockDocumentsAsync()).Single(x => x.LyDo == savedReason);
        Assert.Equal("DRAFT", draft.TrangThai);
        Assert.Equal(DateOnly.FromDateTime(date.Value), draft.NgayThucHien);
        Assert.Single(draft.ThanhPhan);
        Assert.Equal(2500m, draft.ThanhPhan[0].SoLuong);
        Assert.Equal(100m, draft.ThanhPhan[0].DonGiaNhap);
        var after = await LiveSession.Api.GetInventoryAsync();
        Assert.Equal(material.TonThucTe, after.NguyenLieu.Single(x => x.MaNguyenLieu == material.MaNguyenLieu).TonThucTe);
    }

    /// <summary>Lưu qua nút Lập phiếu thật: bỏ bộ lọc đang che, hiển thị/chọn nháp vừa lưu và khóa sau xác nhận.</summary>
    internal static async Task SavedDraftIsVisible(Form main, string output)
    {
        var page = Descendants(main).Single(c => c.GetType().Name == "StockDocumentsPage");
        var grid = Descendants(page).OfType<DataGridView>().Single(c => c.Name == "AdminDataGrid");
        var from = Descendants(page).OfType<DateTimePicker>().Single(c => c.Name == "StockDocumentFrom");
        var until = Descendants(page).OfType<DateTimePicker>().Single(c => c.Name == "StockDocumentUntil");
        Assert.False(from.ShowCheckBox); Assert.False(until.ShowCheckBox);
        var today = DateTime.UtcNow.AddHours(7).Date;
        from.Value = today; until.Value = today;
        var search = Descendants(page).OfType<TextBox>().First();
        search.Text = "Không khớp phiếu mới";
        var source = "Nguồn kiểm thử " + Guid.NewGuid().ToString("N")[..8];
        var savedDate = today.AddDays(-40);
        using var fill = new System.Windows.Forms.Timer { Interval = 50 };
        fill.Tick += (_, _) =>
        {
            var editor = Application.OpenForms.OfType<StockDocumentEditorForm>().FirstOrDefault();
            if (editor is null) return;
            fill.Stop();
            var controls = Descendants(editor).Where(c => c.Name.Length > 0).ToDictionary(c => c.Name);
            ((DateTimePicker)controls["StockDocumentDate"]).Value = savedDate;
            ((TextBox)controls["StockDocumentSource"]).Text = source;
            ((NumericUpDown)controls["StockDocumentQuantity"]).Value = 2000;
            ((NumericUpDown)controls["StockDocumentPrice"]).Value = 20000;
            ((Button)controls["StockDocumentAddLine"]).PerformClick();
            ((Button)controls["StockDocumentSave"]).PerformClick();
        };
        fill.Start();
        Descendants(page).OfType<Button>().Single(b => b.Text == "Lập phiếu").PerformClick();
        await AdminFeaturesUiTests.WaitUntil(() => grid.Rows.Cast<DataGridViewRow>().Any(r => r.DataBoundItem is StockDocumentView d && d.Nguon == source));
        var draft = (StockDocumentView)grid.CurrentRow!.DataBoundItem;
        Assert.Equal(source, draft.Nguon);
        Assert.Equal("DRAFT", draft.TrangThai);
        Assert.Equal("Nháp", grid.CurrentRow.Cells[3].FormattedValue);
        Assert.True(from.Value.Date <= savedDate);
        Assert.True(until.Value.Date >= savedDate);
        Assert.True(Descendants(page).OfType<Button>().Single(b => b.Text == "Sửa nháp").Visible);
        Capture(main, Path.Combine(output, "ADM04-saved-draft-selected.png"));
        using var approve = new System.Windows.Forms.Timer { Interval = 50 };
        approve.Tick += (_, _) =>
        {
            var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Name == "StockDocumentPostDialog");
            if (dialog is null) return;
            approve.Stop();
            Descendants(dialog).OfType<Button>().Single(b => b.Name == "StockDocumentPostConfirm").PerformClick();
        };
        approve.Start();
        Descendants(page).OfType<Button>().Single(b => b.Text == "Xác nhận nhập / xuất").PerformClick();
        await AdminFeaturesUiTests.WaitUntil(() => grid.CurrentRow?.DataBoundItem is StockDocumentView current && current.MaPhieuKho == draft.MaPhieuKho && current.TrangThai == "POSTED");
        Assert.False(Descendants(page).OfType<Button>().Single(b => b.Text == "Sửa nháp").Visible);
        Assert.False(Descendants(page).OfType<Button>().Single(b => b.Text == "Xóa nháp").Visible);
        Assert.Equal("Thành công", grid.CurrentRow!.Cells[3].FormattedValue);
        // Đổi ngày là lọc ngay, không cần tích hay bấm tìm kiếm.
        from.Value = today; until.Value = today;
        Assert.DoesNotContain(grid.Rows.Cast<DataGridViewRow>(), r => r.DataBoundItem is StockDocumentView d && d.MaPhieuKho == draft.MaPhieuKho);
    }

    /// <summary>Xác nhận ngay trong editor, tự lấy dòng đang nhập; lỗi xuất giữ cùng nháp và sửa rồi thử lại không tạo phiếu trùng.</summary>
    internal static async Task ConfirmInsideEditor(Form main, string output)
    {
        var before = await LiveSession.Api.GetInventoryAsync();
        var material = before.NguyenLieu.First(x => x.TonThucTe > 100 && x.KhaDung > 10);
        using (var editor = OpenEditor(before))
        {
            var controls = Named(editor);
            ((ComboBox)controls["StockDocumentIngredient"]).SelectedValue = material.MaNguyenLieu;
            ((NumericUpDown)controls["StockDocumentQuantity"]).Value = 2.5m;
            ((NumericUpDown)controls["StockDocumentPrice"]).Value = 20000;
            Capture(editor, Path.Combine(output, "ADM04-confirm-in-editor.png"));
            ((Button)controls["StockDocumentConfirm"]).PerformClick();
            await AdminFeaturesUiTests.WaitUntil(() => editor.DialogResult == DialogResult.OK && !editor.Visible);
            var posted = (await LiveSession.Api.GetStockDocumentsAsync()).Single(d => d.MaPhieuKho == editor.SavedDocumentId);
            Assert.Equal("POSTED", posted.TrangThai);
            Assert.Equal(2.5m, Assert.Single(posted.ThanhPhan).SoLuong);
            Assert.Equal(material.TonThucTe + 2.5m, (await LiveSession.Api.GetInventoryAsync()).NguyenLieu.Single(x => x.MaNguyenLieu == material.MaNguyenLieu).TonThucTe);
        }
        var documentCount = (await LiveSession.Api.GetStockDocumentsAsync()).Count;
        using (var issue = OpenEditor(await LiveSession.Api.GetInventoryAsync()))
        {
            var controls = Named(issue);
            ((ComboBox)controls["StockDocumentType"]).SelectedIndex = 1;
            ((ComboBox)controls["StockDocumentIngredient"]).SelectedValue = material.MaNguyenLieu;
            ((TextBox)controls["StockDocumentReason"]).Text = "Xuất hàng hỏng kiểm thử";
            ((NumericUpDown)controls["StockDocumentQuantity"]).Value = material.TonThucTe!.Value + 100;
            ((Button)controls["StockDocumentConfirm"]).PerformClick();
            await AdminFeaturesUiTests.WaitUntil(() => issue.SavedDocumentId is not null && controls["StockDocumentFeedback"].Text.Contains("Đã lưu nháp"));
            var savedId = issue.SavedDocumentId;
            Assert.True(issue.Visible);
            var grid = (DataGridView)controls["StockDocumentLines"];
            grid.CurrentCell = grid.Rows[0].Cells["NguyenLieu"];
            ((NumericUpDown)controls["StockDocumentQuantity"]).Value = 1;
            ((Button)controls["StockDocumentConfirm"]).PerformClick();
            await AdminFeaturesUiTests.WaitUntil(() => issue.DialogResult == DialogResult.OK && !issue.Visible);
            Assert.Equal(savedId, issue.SavedDocumentId);
            var documents = await LiveSession.Api.GetStockDocumentsAsync();
            Assert.Equal(documentCount + 1, documents.Count);
            Assert.Equal("POSTED", documents.Single(d => d.MaPhieuKho == savedId).TrangThai);
        }
        using (var future = OpenEditor(await LiveSession.Api.GetInventoryAsync()))
        {
            var controls = Named(future);
            ((DateTimePicker)controls["StockDocumentDate"]).Value = DateTime.UtcNow.AddHours(7).Date.AddDays(1);
            ((NumericUpDown)controls["StockDocumentQuantity"]).Value = 1;
            ((Button)controls["StockDocumentConfirm"]).PerformClick();
            await AdminFeaturesUiTests.WaitUntil(() => future.DialogResult == DialogResult.OK && !future.Visible);
            var scheduled = (await LiveSession.Api.GetStockDocumentsAsync()).Single(d => d.MaPhieuKho == future.SavedDocumentId);
            Assert.Equal("SCHEDULED", scheduled.TrangThai);
            Assert.Null(scheduled.GhiSoLuc);
            var page = Descendants(main).OfType<StockDocumentsPage>().Single();
            var until = Descendants(page).OfType<DateTimePicker>().Single(p => p.Name == "StockDocumentUntil");
            until.Value = DateTime.UtcNow.AddHours(7).Date.AddDays(1);
            await page.Reload();
            var grid = Descendants(page).OfType<DataGridView>().Single(g => g.Name == "AdminDataGrid");
            var row = grid.Rows.Cast<DataGridViewRow>().Single(r => ((StockDocumentView)r.DataBoundItem).MaPhieuKho == scheduled.MaPhieuKho);
            grid.CurrentCell = row.Cells[0];
            Assert.Equal("Đang xử lý", row.Cells[3].FormattedValue);
            Assert.False(Descendants(page).OfType<Button>().Single(b => b.Name == "StockDocumentEditButton").Visible);
            var cancel = Descendants(page).OfType<Button>().Single(b => b.Name == "StockDocumentCancelButton");
            Assert.True(cancel.Visible);
            Capture(main, Path.Combine(output, "ADM04-scheduled-locked.png"));
            var actualRows = grid.DataSource;
            grid.DataSource = new List<StockDocumentView> { scheduled with { TrangThai = "FAILED", LoiXuLy = "Không đủ tồn kho để xuất." } };
            Assert.Equal("Cần kiểm tra", grid.Rows[0].Cells[3].FormattedValue);
            Assert.True(Descendants(page).OfType<Button>().Single(b => b.Name == "StockDocumentEditButton").Visible);
            grid.DataSource = actualRows;
            grid.CurrentCell = grid.Rows.Cast<DataGridViewRow>().Single(r => ((StockDocumentView)r.DataBoundItem).MaPhieuKho == scheduled.MaPhieuKho).Cells[0];
            using var approve = new System.Windows.Forms.Timer { Interval = 50 };
            approve.Tick += (_, _) =>
            {
                var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Text == "Hủy phiếu đang chờ xử lý");
                if (dialog is null) return;
                approve.Stop();
                Descendants(dialog).OfType<TextBox>().Single().Text = "Hủy lịch kiểm thử";
                Descendants(dialog).OfType<Button>().Single(b => b.Text == "Xác nhận hủy phiếu").PerformClick();
            };
            approve.Start(); cancel.PerformClick();
            await AdminFeaturesUiTests.WaitUntil(() => grid.Rows.Cast<DataGridViewRow>().Any(r => r.DataBoundItem is StockDocumentView d && d.MaPhieuKho == scheduled.MaPhieuKho && d.TrangThai == "CANCELLED"));
        }
    }

    /// <summary>Chi tiết nhập/xuất chỉ xem, không có nút hủy phiếu đã thành công; kiểm bố cục và trường theo loại.</summary>
    internal static async Task DetailsAreReadOnly(Form main, string output)
    {
        var page = Descendants(main).OfType<StockDocumentsPage>().Single();
        var list = Descendants(page).OfType<DataGridView>().Single(g => g.Name == "AdminDataGrid");
        foreach (var type in new[] { "RECEIPT", "ISSUE" })
        {
            var row = list.Rows.Cast<DataGridViewRow>().First(r => r.DataBoundItem is StockDocumentView d && d.TrangThai == "POSTED" && d.LoaiPhieu == type);
            list.CurrentCell = row.Cells[0];
            Assert.DoesNotContain(Descendants(page).OfType<Button>(), b => b.Visible && b.Text.StartsWith("Hủy phiếu"));
            var seen = false;
            using var timer = new System.Windows.Forms.Timer { Interval = 50 };
            timer.Tick += (_, _) =>
            {
                var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Name == "StockDocumentDetailsForm");
                if (dialog is null) return;
                timer.Stop();
                var controls = Descendants(dialog).ToList();
                var grid = controls.OfType<DataGridView>().Single(g => g.Name == "StockDocumentDetailsLines");
                Assert.True(grid.ReadOnly);
                Assert.Equal(type == "RECEIPT" ? 5 : 3, grid.Columns.Cast<DataGridViewColumn>().Count(c => c.Visible));
                Assert.Equal(type == "RECEIPT", controls.Single(c => c.Name == "StockDocumentDetailsTotal").Visible);
                if (type == "ISSUE") Assert.DoesNotContain(controls, c => c.Name == "StockDocumentDetailsSource");
                Assert.Equal("Thành công", controls.Single(c => c.Name == "StockDocumentDetailsStatus").Text);
                Assert.Single(controls.OfType<Button>());
                Capture(dialog, Path.Combine(output, "ADM04-details-" + type + ".png"));
                dialog.Size = dialog.MinimumSize; dialog.PerformLayout();
                var close = controls.Single(c => c.Name == "StockDocumentDetailsClose");
                Assert.True(dialog.RectangleToClient(close.RectangleToScreen(close.ClientRectangle)).Bottom <= dialog.ClientSize.Height);
                Assert.True(grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).Sum(c => c.Width) <= grid.ClientSize.Width);
                seen = true; dialog.Close();
            };
            timer.Start(); Descendants(page).OfType<Button>().Single(b => b.Text == "Chi tiết").PerformClick();
            await AdminFeaturesUiTests.WaitUntil(() => seen);
        }
    }

    private static StockDocumentEditorForm OpenEditor(StockSnapshot snapshot)
    {
        var editor = new StockDocumentEditorForm(snapshot) { StartPosition = FormStartPosition.Manual, Location = new Point(-12000, -12000) };
        editor.Show(); return editor;
    }
    private static Dictionary<string, Control> Named(Control parent) => Descendants(parent).Where(c => c.Name.Length > 0).ToDictionary(c => c.Name);

    /// <summary>Duyệt cây control để tìm control theo Name ổn định, không phụ thuộc chữ nút hoặc vị trí.</summary>
    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    /// <summary>Chụp form đã render để rà soát layout bên cạnh assertion hình học.</summary>
    private static void Capture(Form form, string path)
    {
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        bitmap.Save(path);
    }
}
