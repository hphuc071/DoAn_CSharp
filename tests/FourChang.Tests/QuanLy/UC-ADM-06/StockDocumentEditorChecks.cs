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
        await LiveSession.Api.ActStockDocumentAsync(draft.MaPhieuKho, "post", new("Nhập hàng kiểm thử", draft.PhienBanDuLieu, Guid.NewGuid()));
        await ((StockDocumentsPage)page).Reload();
        Assert.False(Descendants(page).OfType<Button>().Single(b => b.Text == "Sửa nháp").Visible);
        Assert.False(Descendants(page).OfType<Button>().Single(b => b.Text == "Xóa nháp").Visible);
        Assert.Equal("Thành công", grid.CurrentRow!.Cells[3].FormattedValue);
        // Đổi ngày là lọc ngay, không cần tích hay bấm tìm kiếm.
        from.Value = today; until.Value = today;
        Assert.DoesNotContain(grid.Rows.Cast<DataGridViewRow>(), r => r.DataBoundItem is StockDocumentView d && d.MaPhieuKho == draft.MaPhieuKho);
    }

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
