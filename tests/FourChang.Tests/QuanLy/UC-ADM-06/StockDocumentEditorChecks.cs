using FourChang.Desktop.Admin.StockDocuments;
using FourChang.Desktop.Shared.Session;

namespace FourChang.Tests.Admin;

/// <summary>Kiểm tra editor phiếu kho thật trên STA: trường theo loại, số lượng, sửa dòng và nháp không đổi tồn.</summary>
internal static class StockDocumentEditorChecks
{
    /// <summary>Thao tác bằng control của form, lưu qua API/SQL fixture; không ghi vào dữ liệu quán.</summary>
    internal static async Task Run(string output)
    {
        var before = await LiveSession.Api.GetInventoryAsync();
        var material = before.NguyenLieu.First(x => x.DonVi == "g" && x.TonThucTe is not null);
        using var editor = new StockDocumentEditorForm(before);
        editor.StartPosition = FormStartPosition.Manual;
        editor.Location = new Point(-12000, -12000);
        editor.Show();
        await Task.Delay(100);
        var controls = Descendants(editor).Where(x => !string.IsNullOrEmpty(x.Name)).ToDictionary(x => x.Name);
        var type = (ComboBox)controls["StockDocumentType"];
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
        type.SelectedIndex = 2;
        Assert.Contains("thực đếm", Descendants(editor).OfType<Label>().Single(x => x.Text.StartsWith("Số lượng thực đếm")).Text);
        quantity.Value = 0;
        add.PerformClick();
        Assert.Single(grid.Rows.Cast<DataGridViewRow>());
        remove.PerformClick();
        Assert.Empty(grid.Rows.Cast<DataGridViewRow>());
        type.SelectedIndex = 0;
        add.PerformClick();
        Assert.Empty(grid.Rows.Cast<DataGridViewRow>());
        Assert.Contains("lớn hơn 0", feedback.Text);
        save.PerformClick();
        Assert.True(editor.Visible);
        Assert.Contains("ít nhất một nguyên liệu", feedback.Text);
        var liquid = before.NguyenLieu.First(x => x.DonVi == "ml");
        ingredient.SelectedValue = liquid.MaNguyenLieu;
        Assert.Contains("2 lít", controls["StockDocumentStockHint"].Text);
        ingredient.SelectedValue = material.MaNguyenLieu;
        Assert.Contains("2 kg", controls["StockDocumentStockHint"].Text);
        quantity.Value = 2000;
        price.Value = 100;
        add.PerformClick();
        quantity.Value = 2500;
        add.PerformClick();
        Assert.Single(grid.Rows.Cast<DataGridViewRow>());
        Assert.Equal(2500m, grid.Rows[0].Cells["SoLuong"].Value);
        Assert.False(grid.Columns["TonSoDaChot"].Visible);
        Assert.False(grid.Columns["MaNguyenLieu"].Visible);
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
        Assert.Single(draft.ThanhPhan);
        Assert.Equal(2500m, draft.ThanhPhan[0].SoLuong);
        Assert.Equal(100m, draft.ThanhPhan[0].DonGiaNhap);
        var after = await LiveSession.Api.GetInventoryAsync();
        Assert.Equal(material.TonThucTe, after.NguyenLieu.Single(x => x.MaNguyenLieu == material.MaNguyenLieu).TonThucTe);
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
