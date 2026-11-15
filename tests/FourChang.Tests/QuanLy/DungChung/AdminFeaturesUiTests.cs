using System.Diagnostics;
using System.Net;
using FourChang.Desktop.Admin.Customers.Views;
using FourChang.Desktop.Shared.Theme;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shell;
using FourChang.Desktop.Admin.Ingredients;
using FourChang.Desktop.Admin.Recipes;
using FourChang.Presentation.Shared.Contracts.Common;
using FourChang.Tests.Shared;

namespace FourChang.Tests.Admin;

/// <summary>Chạy WinForms thật trên STA trong process con; chụp bằng chứng từng trang và dialog.</summary>
[Collection("AdminFeatures"), Trait("Actor", "QuanLy"), Trait("Layer", "WinForms")]
public sealed class AdminFeaturesUiTests(AdminFeatureFixture fixture)
{
    /// <summary>Kiểm tra năm route đều đọc API thật, có bảng dữ liệu, nút không bị cắt và resize được.</summary>
    [Theory]
    [InlineData("UC-ADM-01", "customers")]
    [InlineData("UC-ADM-02", "menu")]
    [InlineData("UC-ADM-03", "ingredients")]
    [InlineData("UC-ADM-02", "recipes")]
    [InlineData("UC-ADM-05", "inventory")]
    [InlineData("DASHBOARD", "dashboard")]
    public Task GiaoDien_NamUseCase_DocApi_Resize_VaDialog(string code, string route) => RunRoute(fixture, code, route);

    /// <summary>Dùng chung cách mở worker cho test các trang và test reset riêng của UC-ADM-01.</summary>
    internal static async Task RunRoute(AdminFeatureFixture fixture, string code, string route)
    {
        await fixture.Api.RunApiCommand("--seed-customers");
        await DesktopWorker.Run(fixture.Api, "ADM01-05", new()
        {
            ["FOURCHANG_API_URL"] = fixture.Api.Url,
            ["FOURCHANG_TEST_ADMIN_PASSWORD"] = fixture.Api.AdminPassword,
            ["FOURCHANG_TEST_ADMIN_TOKEN"] = fixture.Admin["Authorization"].Replace("Bearer ", "", StringComparison.Ordinal),
            ["FOURCHANG_TEST_ROUTE"] = route,
            ["FOURCHANG_TEST_UC"] = code,
            ["FOURCHANG_POS_CREDENTIALS_PATH"] = Path.Combine(fixture.Api.Artifacts, "ui-device.dpapi")
        });
    }

    /// <summary>Worker chỉ mở các route cố định của test; không nhận lệnh hoặc đường dẫn thực thi từ UI.</summary>
    internal static int RunHarness(string output)
    {
        FourChang.Desktop.DesktopLauncher.ConfigureUi();
        var route = Environment.GetEnvironmentVariable("FOURCHANG_TEST_ROUTE")!;
        var code = Environment.GetEnvironmentVariable("FOURCHANG_TEST_UC")!;
        if (!new[] { "customers", "menu", "ingredients", "recipes", "inventory", "dashboard", "customer-reset", "stock", "manage-orders", "invoices", "refunds", "reports", "employees", "work-sessions", "pos-sale", "staff-orders", "staff-invoices" }.Contains(route)) return 2;
        var exit = 1;
        using var loop = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000), Size = new Size(20, 20) };
        loop.Shown += async (_, _) =>
        {
            try
            {
                var password = Environment.GetEnvironmentVariable("FOURCHANG_TEST_ADMIN_PASSWORD")!;
                // Dùng phiên fixture đã đăng nhập thật; test layout không đăng nhập lặp vượt limiter khi chạy cả bộ.
                var token=Environment.GetEnvironmentVariable("FOURCHANG_TEST_ADMIN_TOKEN");
                var session = string.IsNullOrWhiteSpace(token)
                    ? await LiveSession.Api.LoginAdminAsync(new SignIn("quanly", password))
                    : new SessionResult(token, "ADMIN", "Quản lý", 1);
                LiveSession.Api.Token = session.Token; LiveSession.Current = session; LiveSession.Preview = false;
                if (route is "staff-orders" or "staff-invoices")
                {
                    await StaffDocumentsUiChecks.Run(output,route=="staff-invoices");exit=0;return;
                }
                if (route == "pos-sale")
                {
                    await PosSalesUiChecks.Run(output); exit = 0; return;
                }
                using var form = new MainForm("Admin", "Quản lý") { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-16000, -16000) };
                form.Show(); form.Navigate(route == "customer-reset" ? "customers" : route);
                try { await WaitUntil(() => route == "reports"
                    ? Descendants(form).OfType<TabControl>().Any(t => t.Name == "ReportTables" && t.TabPages.Count == 6)
                    : Descendants(form).OfType<DataGridView>().Any(g => g.Rows.Count > 0)); }
                catch
                {
                    Capture(form, Path.Combine(output, code + "-failed.png"));
                    File.WriteAllLines(Path.Combine(output, code + "-layout.txt"), Descendants(form).Select(c => $"{c.GetType().Name} {c.Name} Visible={c.Visible} Handle={c.IsHandleCreated} Bounds={c.Bounds}"));
                    throw;
                }
                // Resize nhiều lần để phát hiện vùng lọc tự cao dần hoặc cắt hàng đã xuống dòng.
                foreach (var width in new[] { 1430, 1280, 1500, 1280 })
                {
                    form.ClientSize = new Size(width, 850); await Task.Delay(100);
                    foreach (var table in Descendants(form).Where(x => x.Name == "AdminTableArea"))
                        foreach (var grid in table.Controls.OfType<DataGridView>())
                            Assert.True(grid.Bottom <= table.ClientSize.Height, $"Grid không được đè lên bộ đếm/phân trang: grid={grid.Bounds}, vùng bảng={table.ClientSize}, min={grid.MinimumSize}.");
                    foreach (var surface in Descendants(form).Where(x => x.Name == "AdminFilterSurface"))
                    {
                        foreach (var control in Descendants(surface).Where(x => x.Visible && x is Button or TextBox or ComboBox or DateTimePicker or NumericUpDown))
                        {
                            var bounds = surface.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
                            Assert.True(bounds.Bottom <= surface.ClientSize.Height && bounds.Top >= 0,
                                $"Bộ lọc cắt {control.GetType().Name} {control.Name}: {bounds}; vùng chứa {surface.ClientSize}");
                        }
                        Assert.True(surface.Height < 330, $"Bộ lọc có khoảng trống bất thường: {surface.Height}px");
                    }
                }
                form.ClientSize = new Size(1430, 960); await Task.Delay(100);
                Capture(form, Path.Combine(output, code + "-desktop.png"));
                form.ClientSize = new Size(1280, 850); await Task.Delay(150);
                Capture(form, Path.Combine(output, code + "-compact.png"));
                var buttons = Descendants(form).OfType<Button>().Where(b => b.Visible && (b.Name == "DashboardReload" || b.Parent is FlowLayoutPanel)).ToList();
                Assert.NotEmpty(buttons);
                foreach (var button in buttons) Assert.True(button.Height >= 32 && button.Width > 0, "Nút có vùng thao tác hợp lệ");
                if (new[] { "stock", "manage-orders", "invoices", "refunds", "reports", "employees", "work-sessions", "pos-sale", "staff-orders", "staff-invoices" }.Contains(route))
                {
                    // Ngày lọc không phụ thuộc vùng Windows, tránh tháng/ngày bị hiểu đảo ngược.
                    foreach (var picker in Descendants(form).OfType<DateTimePicker>().Where(x => x.Visible))
                    {
                        Assert.Equal(DateTimePickerFormat.Custom, picker.Format);
                        Assert.Equal("dd/MM/yyyy", picker.CustomFormat);
                    }
                    // Kiểm tra hình học sau wrap, không coi việc Form mở thành công là layout đã đúng.
                    foreach (var row in Descendants(form).OfType<FlowLayoutPanel>().Where(x => x.Visible && !x.AutoScroll))
                        foreach (Control child in row.Controls)
                            if (child.Visible) Assert.True(child.Bottom <= row.ClientSize.Height, $"Hàng {row.Name} cắt control {child.Text}");
                    if (route == "reports")
                    {
                        var report = Descendants(form).Single(x => x.Name == "ReportsManagementPage");
                        Assert.True(report.Height > 600, "Báo cáo phải đủ chiều cao để hiển thị KPI và bảng, không bị giới hạn UserControl 150px.");
                        Assert.Equal(8, Descendants(report).Count(x => x.GetType().Name == "MetricCard"));
                    }
                }
                if (route == "menu")
                {
                    var tabs = Descendants(form).OfType<TabControl>().Single(t => t.Name == "MenuTabs");
                    Assert.Equal(new[] { "Món", "Cấu hình món", "Topping", "Công thức" }, tabs.TabPages.Cast<TabPage>().Select(x => x.Text));
                    Assert.DoesNotContain(Descendants(form).OfType<Button>(), b => b.Text == "Xóa có điều kiện");
                    tabs.SelectedIndex = 3;
                    await WaitUntil(() => Descendants(form).OfType<DataGridView>().Any(g => g.Name == "RecipesGrid" && g.Rows.Count > 0));
                    Assert.DoesNotContain(Descendants(form).OfType<Button>(), b => b.Text is "Mô phỏng" or "Tạo phiên bản mới" or "Công bố / lên lịch" or "Xóa nháp");
                    var recipeGrid = Descendants(form).OfType<DataGridView>().Single(g => g.Name == "RecipesGrid");
                    foreach (DataGridViewRow row in recipeGrid.Rows)
                    {
                        var recipe = Assert.IsType<RecipeView>(row.DataBoundItem);
                        Assert.Equal(recipe.ThanhPhan.Count.ToString(), row.Cells[recipeGrid.Columns.Cast<DataGridViewColumn>().Single(c => c.DataPropertyName == "ThanhPhan").Index].FormattedValue);
                    }
                    Capture(form, Path.Combine(output, "ADM02-recipes-integrated.png"));
                    tabs.SelectedIndex = 0;
                    var editorType = typeof(RecipeEditorForm).Assembly.GetType("FourChang.Desktop.Admin.Menu.MenuEditorForm")!;
                    using var dishEditor = (Form)Activator.CreateInstance(editorType, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
                        new object?[] { "dishes", null, await LiveSession.Api.GetAdminMenuAsync() }, null)!;
                    dishEditor.StartPosition = FormStartPosition.Manual; dishEditor.Location = new Point(-12000, -12000); dishEditor.Show();
                    Assert.DoesNotContain(Descendants(dishEditor), c => c.Name == "Thứ tự" || c.Name.StartsWith("Ảnh (sprite:"));
                    Assert.Contains(Descendants(dishEditor).OfType<Button>(), b => b.Name == "MenuChooseImage" && b.Text == "Chọn hình ảnh");
                    var filter = (string)editorType.GetField("ImageFileFilter", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.GetRawConstantValue()!;
                    Assert.Contains("*.webp", filter);
                    // WebP thật tải từ thiết bị phải giải mã được, xem trước và lưu qua cùng luồng nút chọn ảnh.
                    var imageFile = Path.Combine(output, "menu-upload-source.webp");
                    using (var bitmap = SkiaSharp.SKBitmap.Decode(MenuAdministrationTests.ImageBytes()))
                    using (var source = SkiaSharp.SKImage.FromBitmap(bitmap))
                    using (var webp = source.Encode(SkiaSharp.SKEncodedImageFormat.Webp, 90)) File.WriteAllBytes(imageFile, webp.ToArray());
                    editorType.GetMethod("SelectImage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(dishEditor, new object[] { imageFile });
                    var preview = Descendants(dishEditor).OfType<PictureBox>().Single(); Assert.NotNull(preview.Image);
                    Assert.Equal(12, preview.Image.Width); Assert.Equal(12, preview.Image.Height);
                    // Chỉ chạy thêm file thật nếu người thực thi chủ động cung cấp, vẫn lưu vào DB fixture.
                    var downloadedImage = Environment.GetEnvironmentVariable("FOURCHANG_TEST_IMAGE_FILE");
                    if (!string.IsNullOrWhiteSpace(downloadedImage) && File.Exists(downloadedImage))
                    {
                        editorType.GetMethod("SelectImage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(dishEditor, new object[] { downloadedImage });
                        Assert.True(preview.IsDisposed);
                        Assert.NotNull(Descendants(dishEditor).OfType<PictureBox>().Single().Image);
                    }
                    editorType.GetMethod("SetRecipeTemplates", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(dishEditor, new object[] { await LiveSession.Api.GetRecipesAsync() });
                    var visibility = Descendants(dishEditor).OfType<CheckBox>().Single(x => x.Text == "Hiển thị món");
                    var selling = Descendants(dishEditor).OfType<CheckBox>().Single(x => x.Text.StartsWith("Bật bán"));
                    Assert.False(visibility.Checked); Assert.False(selling.Checked);
                    visibility.Checked = selling.Checked = true;
                    visibility.Checked = selling.Checked = false;
                    var sourcePicker = Descendants(dishEditor).OfType<ComboBox>().Single(x => x.Name == "MenuDishTemplate");
                    sourcePicker.SelectedIndex = 1;
                    var imageDishName = "Món có ảnh UI " + Guid.NewGuid().ToString("N")[..8];
                    Descendants(dishEditor).OfType<TextBox>().Single(x => x.Name == "Tên món").Text = imageDishName;
                    Capture(dishEditor, Path.Combine(output, "ADM02-image-picker.png"));
                    Descendants(dishEditor).OfType<Button>().Single(x => x.Name == "SaveMenu").PerformClick();
                    await WaitUntil(() => !dishEditor.Visible);
                    Assert.Equal(DialogResult.OK, dishEditor.DialogResult);
                    var savedDish = (await LiveSession.Api.GetAdminMenuAsync()).Mon.Single(x => x.TenMon == imageDishName);
                    Assert.False(savedDish.DaCongBo); Assert.False(savedDish.DangBan);
                    Assert.StartsWith("/menu-images/", savedDish.DuongDanAnh);
                    Assert.True(savedDish.ThuTuHienThi > 0);
                    var savedMenu = await LiveSession.Api.GetAdminMenuAsync();
                    var savedVariants = savedMenu.CauHinh.Where(v => v.MaMon == savedDish.MaMon).ToList();
                    Assert.NotEmpty(savedVariants);
                    var savedRecipes = await LiveSession.Api.GetRecipesAsync();
                    Assert.All(savedVariants, v => Assert.Contains(savedRecipes, r => r.MaCauHinhMon == v.MaCauHinhMon));
                }
                if (route == "menu")
                {
                    var draft = await LiveSession.Api.SaveMenuAsync("dishes", null, new MenuDishInput(1, "Bảng cấu hình UI " + Guid.NewGuid().ToString("N")[..6], null, null, 0, false, false, null, null, Guid.NewGuid()));
                    var batchType = typeof(RecipeEditorForm).Assembly.GetType("FourChang.Desktop.Admin.Menu.MenuVariantBatchForm")!;
                    using var batch = (Form)Activator.CreateInstance(batchType, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
                        new object?[] { await LiveSession.Api.GetAdminMenuAsync(), draft.Ma }, null)!;
                    batch.StartPosition = FormStartPosition.Manual; batch.Location = new Point(-12000, -12000); batch.Show();
                    var prices = Descendants(batch).OfType<DataGridView>().Single(g => g.Name == "VariantPriceMatrix");
                    Assert.False(prices.Columns["HOT"].Visible);
                    for (var i = 0; i < 3; i++) { prices.Rows[i].Cells[0].Value = true; prices.Rows[i].Cells["ICED"].Value = 25000m + i * 5000; }
                    Descendants(batch).OfType<ComboBox>().Single(c => c.Name == "VariantDefault").SelectedItem = "M – Lạnh";
                    Capture(batch, Path.Combine(output, "ADM02-variant-batch.png"));
                    batch.Size = batch.MinimumSize; await Task.Delay(100);
                    var saveBatch = Descendants(batch).OfType<Button>().Single(b => b.Name == "VariantBatchSave");
                    Assert.True(batch.ClientRectangle.Contains(batch.RectangleToClient(saveBatch.RectangleToScreen(saveBatch.ClientRectangle))));
                    Assert.True(TextRenderer.MeasureText(saveBatch.Text, saveBatch.Font).Width + 24 < saveBatch.Width);
                    Capture(batch, Path.Combine(output, "ADM02-variant-batch-compact.png"));
                    saveBatch.PerformClick();
                    await WaitUntil(() => !batch.Visible || Descendants(batch).Any(c => c.Name == "VariantBatchFeedback" && c.Text.Length > 0));
                    Assert.True(!batch.Visible, "Lưu bảng giá: " + Descendants(batch).FirstOrDefault(c => c.Name == "VariantBatchFeedback")?.Text);
                    var snapshot = await LiveSession.Api.GetAdminMenuAsync();
                    var created = snapshot.CauHinh.Where(v => v.MaMon == draft.Ma).ToList();
                    Assert.Equal(3, created.Count); Assert.All(created, v => Assert.False(v.DangBan)); Assert.Single(created, v => v.LaMacDinh && v.KichCo == "M");
                    using var reopen = (Form)Activator.CreateInstance(batchType, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
                        new object?[] { snapshot, draft.Ma }, null)!;
                    reopen.StartPosition = FormStartPosition.Manual; reopen.Location = new Point(-12000, -12000); reopen.Show();
                    var next = Descendants(reopen).OfType<DataGridView>().Single(g => g.Name == "VariantPriceMatrix");
                    Assert.Equal(30000m, next.Rows[1].Cells["ICED"].Value);
                    Descendants(reopen).OfType<CheckBox>().Single(c => c.Name == "VariantHot").Checked = true;
                    Assert.True(next.Columns["HOT"].Visible);
                    for (var i = 0; i < 3; i++) next.Rows[i].Cells["HOT"].Value = 27000m + i * 5000;
                    Descendants(reopen).OfType<Button>().Single(b => b.Name == "VariantBatchSave").PerformClick();
                    await WaitUntil(() => !reopen.Visible || Descendants(reopen).Any(c => c.Name == "VariantBatchFeedback" && c.Text.Length > 0));
                    Assert.True(!reopen.Visible, "Cập nhật bảng giá: " + Descendants(reopen).FirstOrDefault(c => c.Name == "VariantBatchFeedback")?.Text);
                    var updated = (await LiveSession.Api.GetAdminMenuAsync()).CauHinh.Where(v => v.MaMon == draft.Ma).ToList(); Assert.Equal(6, updated.Count);
                    foreach (var old in created) Assert.Equal(old.GiaBan, updated.Single(v => v.MaCauHinhMon == old.MaCauHinhMon).GiaBan);
                }
                if (route == "menu")
                {
                    var toppingType = typeof(RecipeEditorForm).Assembly.GetType("FourChang.Desktop.Admin.Menu.MenuEditorForm")!;
                    using var toppingForm = (Form)Activator.CreateInstance(toppingType, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
                        new object?[] { "toppings", null, await LiveSession.Api.GetAdminMenuAsync(), await LiveSession.Api.GetIngredientsAsync(), await LiveSession.Api.GetRecipesAsync() }, null)!;
                    toppingForm.StartPosition = FormStartPosition.Manual; toppingForm.Location = new Point(-12000, -12000); toppingForm.Show();
                    var toppingName = "Topping một phần UI " + Guid.NewGuid().ToString("N")[..6];
                    Descendants(toppingForm).OfType<TextBox>().Single(t => t.Name == "Tên topping").Text = toppingName;
                    var material = Descendants(toppingForm).OfType<ComboBox>().Single(c => c.Name == "ToppingIngredient");
                    material.SelectedIndex = 1;
                    Descendants(toppingForm).OfType<NumericUpDown>().Single(c => c.Name == "ToppingPortion").Value = 30;
                    Assert.True(Descendants(toppingForm).OfType<CheckBox>().Single(c => c.Text == "Đang sử dụng").Checked);
                    Capture(toppingForm, Path.Combine(output, "ADM02-topping-portion.png"));
                    Descendants(toppingForm).OfType<Button>().Single(b => b.Name == "SaveMenu").PerformClick();
                    await WaitUntil(() => !toppingForm.Visible);
                    var savedTop = (await LiveSession.Api.GetAdminMenuAsync()).MonThem.Single(t => t.TenMonThem == toppingName);
                    var savedRecipe = (await LiveSession.Api.GetRecipesAsync()).Single(r => r.MaMonThem == savedTop.MaMonThem);
                    Assert.Equal(30m, Assert.Single(savedRecipe.ThanhPhan).DinhMucCoSo);
                    using var reopened = (Form)Activator.CreateInstance(toppingType, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
                        new object?[] { "toppings", savedTop.MaMonThem, await LiveSession.Api.GetAdminMenuAsync(), await LiveSession.Api.GetIngredientsAsync(), await LiveSession.Api.GetRecipesAsync() }, null)!;
                    reopened.StartPosition = FormStartPosition.Manual; reopened.Location = new Point(-12000, -12000); reopened.Show();
                    Assert.Equal(30m, Descendants(reopened).OfType<NumericUpDown>().Single(c => c.Name == "ToppingPortion").Value);
                }
                if (route == "ingredients")
                {
                    Assert.DoesNotContain(Descendants(form).OfType<Button>(), x => x.Text == "Đơn vị tính");
                    using var editor = new IngredientEditorForm(await LiveSession.Api.GetIngredientsAsync(), null);
                    editor.StartPosition = FormStartPosition.Manual; editor.Location = new Point(-12000, -12000); editor.Show(); await Task.Delay(100);
                    AssertFooterVisible(editor);
                    var unit = Descendants(editor).OfType<ComboBox>().Single(x => x.Name == "IngredientBaseUnit");
                    Assert.Equal(2, unit.Items.Count);
                    Assert.Empty(Descendants(editor).OfType<DataGridView>());
                    Assert.DoesNotContain(Descendants(editor).OfType<Button>(), x => x.Text.Contains("quy đổi", StringComparison.CurrentCultureIgnoreCase) || x.Text == "Đơn vị tính");
                    // Cả hai lựa chọn phải lưu được bằng form thật và không tạo dòng quy đổi.
                    foreach (var unitIndex in new[] { 0, 1 })
                    {
                        using var create = new IngredientEditorForm(await LiveSession.Api.GetIngredientsAsync(), null);
                        create.StartPosition = FormStartPosition.Manual; create.Location = new Point(-14000, -14000); create.Show();
                        var materialName = "Nguyên liệu UI " + Guid.NewGuid().ToString("N")[..8];
                        Descendants(create).OfType<TextBox>().Single(x => x.Name == "IngredientName").Text = materialName;
                        var selectedUnit = Descendants(create).OfType<ComboBox>().Single(x => x.Name == "IngredientBaseUnit");
                        selectedUnit.SelectedIndex = unitIndex;
                        var selectedId = (long)selectedUnit.SelectedValue!;
                        Descendants(create).OfType<Button>().Single(x => x.Name == "IngredientSave").PerformClick();
                        await WaitUntil(() => !create.Visible);
                        Assert.Equal(DialogResult.OK, create.DialogResult);
                        var saved = (await LiveSession.Api.GetIngredientsAsync()).NguyenLieu.Single(x => x.TenNguyenLieu == materialName);
                        Assert.Equal(selectedId, saved.MaDonViCoSo); Assert.Empty(saved.QuyDoi);
                        using var edit = new IngredientEditorForm(await LiveSession.Api.GetIngredientsAsync(), saved);
                        edit.StartPosition = FormStartPosition.Manual; edit.Location = new Point(-14000, -14000); edit.Show();
                        Assert.Equal(selectedId, Descendants(edit).OfType<ComboBox>().Single(x => x.Name == "IngredientBaseUnit").SelectedValue);
                        edit.Close();
                    }
                    Capture(editor, Path.Combine(output, code + "-editor.png"));
                    editor.Size = editor.MinimumSize; await Task.Delay(100);
                    AssertFooterVisible(editor);
                    Capture(editor, Path.Combine(output, code + "-editor-compact.png")); editor.Close();
                }
                if (route == "recipes")
                {
                    // Dòng chưa có công thức phải hiện rõ và mở đúng món/size để thêm từ mẫu.
                    var missingDish = await LiveSession.Api.SaveMenuAsync("dishes", null, new MenuDishInput(1, "Món chưa có công thức " + Guid.NewGuid().ToString("N")[..8], null, null, 0, false, true, null, null, Guid.NewGuid()));
                    var missingVariant = await LiveSession.Api.SaveMenuAsync("variants", null, new MenuVariantInput(missingDish.Ma, "M", "ICED", 35000, false, true, [], [], null, Guid.NewGuid()));
                    var recipePage = Descendants(form).OfType<RecipesPage>().Single();
                    Assert.Contains(Descendants(recipePage).OfType<Button>(), x => x.Name == "RecipeAddVariant" && x.Text == "Thêm size / nóng-lạnh");
                    await recipePage.Reload();
                    var pageGrid = Descendants(recipePage).OfType<DataGridView>().Single(g => g.Name == "RecipesGrid");
                    Descendants(recipePage).OfType<ComboBox>().Single(x => x.Name == "RecipeStateFilter").SelectedIndex = 1;
                    var missing = pageGrid.Rows.Cast<DataGridViewRow>().Select(row => (RecipeView)row.DataBoundItem!).Single(x => x.MaCauHinhMon == missingVariant.Ma);
                    Assert.Equal("MISSING", missing.TrangThai); Assert.Equal("M", missing.KichCo); Assert.Equal("Lạnh", missing.NhietDo);
                    Assert.All(pageGrid.Rows.Cast<DataGridViewRow>(), row => Assert.Equal("MISSING", ((RecipeView)row.DataBoundItem!).TrangThai));
                    Capture(form, Path.Combine(output, "ADM02-missing-recipes.png"));
                    var sourceRecipe = (await LiveSession.Api.GetRecipesAsync()).First(x => x.TrangThai == "ACTIVE" && x.MaCauHinhMon is not null);
                    using (var fromTemplate = new RecipeEditorForm(await LiveSession.Api.GetIngredientsAsync(), await LiveSession.Api.GetAdminMenuAsync(), null, await LiveSession.Api.GetRecipesAsync(), missing))
                    {
                        fromTemplate.StartPosition = FormStartPosition.Manual; fromTemplate.Location = new Point(-12000,-12000); fromTemplate.Show();
                        Assert.Equal(missingVariant.Ma, Descendants(fromTemplate).OfType<ComboBox>().Single(x => x.Name == "RecipeTarget").SelectedValue);
                        Descendants(fromTemplate).OfType<ComboBox>().Single(x => x.Name == "RecipeTemplate").SelectedValue = sourceRecipe.MaCongThuc;
                        Descendants(fromTemplate).OfType<Button>().Single(x => x.Name == "RecipeApplyTemplate").PerformClick();
                        Assert.Equal(sourceRecipe.ThanhPhan.Count, Descendants(fromTemplate).OfType<DataGridView>().Single(g => g.Name == "RecipeLines").Rows.Count);
                        Capture(fromTemplate, Path.Combine(output, "ADM02-recipe-from-template.png"));
                        Descendants(fromTemplate).OfType<Button>().Single(x => x.Name == "RecipeSave").PerformClick();
                        await WaitUntil(() => !fromTemplate.Visible);
                        Assert.Equal(DialogResult.OK, fromTemplate.DialogResult);
                    }
                    var copiedRecipe = (await LiveSession.Api.GetRecipesAsync()).Single(x => x.MaCauHinhMon == missingVariant.Ma);
                    Assert.NotEqual(sourceRecipe.MaCongThuc, copiedRecipe.MaCongThuc);
                    Assert.Equal(sourceRecipe.ThanhPhan, copiedRecipe.ThanhPhan);
                    await recipePage.Reload();
                    Assert.DoesNotContain(pageGrid.Rows.Cast<DataGridViewRow>(), row => ((RecipeView)row.DataBoundItem!).MaCauHinhMon == missingVariant.Ma);
                    Descendants(recipePage).OfType<ComboBox>().Single(x => x.Name == "RecipeStateFilter").SelectedIndex = 0;
                    using var editor = new RecipeEditorForm(await LiveSession.Api.GetIngredientsAsync(), await LiveSession.Api.GetAdminMenuAsync(), null, await LiveSession.Api.GetRecipesAsync());
                    editor.StartPosition = FormStartPosition.Manual; editor.Location = new Point(-12000, -12000); editor.Show(); await Task.Delay(100);
                    // Tất cả cấu hình/topping seed đã có công thức: thêm mới không cho chọn trùng.
                    var targetPicker = Descendants(editor).OfType<ComboBox>().Single(x => x.Name == "RecipeTarget");
                    var configured = await LiveSession.Api.GetRecipesAsync();
                    Assert.All(targetPicker.Items.Cast<object>(), option =>
                    {
                        var id = (long)option.GetType().GetProperty("Id")!.GetValue(option)!;
                        Assert.DoesNotContain(configured, x => x.MaCauHinhMon == id);
                    });
                    if (targetPicker.Items.Count == 0)
                        Assert.False(Descendants(editor).OfType<Button>().Single(x => x.Name == "RecipeSave").Enabled);
                    var materials = await LiveSession.Api.GetIngredientsAsync();
                    var ingredientChoices = Descendants(editor).OfType<CheckedListBox>().Single(x => x.Name == "RecipeIngredientChoices");
                    ingredientChoices.SetItemChecked(0, true); ingredientChoices.SetItemChecked(1, true);
                    Descendants(editor).OfType<Button>().Single(x => x.Name == "RecipeLineAdd").PerformClick();
                    var quantities = Descendants(editor).OfType<DataGridView>().Single(x => x.Name == "RecipeLines");
                    Assert.Equal(2, quantities.Rows.Count); Assert.True(quantities.Columns[1].ReadOnly);
                    Assert.Equal(new[] { "g", "ml" }, quantities.Rows.Cast<DataGridViewRow>().Select(x => x.Cells[1].Value?.ToString()));
                    quantities.Rows[0].Cells[2].Value = 2m; quantities.Rows[1].Cells[2].Value = 2.5m;
                    Assert.Equal("2", quantities.Rows[0].Cells[2].FormattedValue);
                    Assert.Equal(2.5m.ToString("0.#"), quantities.Rows[1].Cells[2].FormattedValue);
                    Assert.DoesNotContain(Descendants(editor), x => x.Name == "RecipeLossPercent");
                    AssertFooterVisible(editor);
                    Capture(editor, Path.Combine(output, code + "-editor.png"));
                    editor.Size = editor.MinimumSize; await Task.Delay(100);
                    AssertFooterVisible(editor);
                    Capture(editor, Path.Combine(output, code + "-editor-compact.png"));
                    Descendants(editor).OfType<TextBox>().Single(x => x.Name == "RecipeName").Text = "Công thức UI " + Guid.NewGuid().ToString("N")[..8];
                    var current = (await LiveSession.Api.GetRecipesAsync()).First(x => x.TrangThai == "ACTIVE" && x.MaCauHinhMon is not null);
                    editor.Close();
                    using var edit = new RecipeEditorForm(await LiveSession.Api.GetIngredientsAsync(), await LiveSession.Api.GetAdminMenuAsync(), current);
                    edit.StartPosition = FormStartPosition.Manual; edit.Location = new Point(-12000,-12000); edit.Show();
                    Assert.Equal(current.MaCauHinhMon, Descendants(edit).OfType<ComboBox>().Single(x => x.Name == "RecipeTarget").SelectedValue);
                    var selectedIngredients = Descendants(edit).OfType<CheckedListBox>().Single(x => x.Name == "RecipeIngredientChoices");
                    selectedIngredients.SetItemChecked(0, true); selectedIngredients.SetItemChecked(1, true);
                    Descendants(edit).OfType<Button>().Single(x => x.Name == "RecipeLineAdd").PerformClick();
                    var lineTable = Descendants(edit).OfType<DataGridView>().Single(x => x.Name == "RecipeLines");
                    Assert.True(lineTable.Rows.Count >= 2);
                    var ingredientId = materials.NguyenLieu.Single(x => x.TenNguyenLieu == (string)lineTable.Rows[0].Cells[0].Value!).MaNguyenLieu;
                    lineTable.CurrentCell = lineTable.Rows[0].Cells[2]; lineTable.BeginEdit(true);
                    ((DataGridViewTextBoxEditingControl)lineTable.EditingControl!).Text = "2,5";
                    Assert.True(lineTable.EndEdit()); Assert.Equal(2.5m, lineTable.Rows[0].Cells[2].Value);
                    lineTable.Rows[0].Cells[3].Value = "FIXED";
                    lineTable.Rows[1].Cells[2].Value = 100m;
                    Descendants(edit).OfType<ComboBox>().Single(x => x.Name == "RecipeDefaultSugar").SelectedValue = 75L;
                    Assert.Equal(2.5m.ToString("0.#"), lineTable.Rows[0].Cells[2].FormattedValue);
                    Assert.DoesNotContain(Descendants(edit), x => x.Name == "RecipeLossPercent");
                    Capture(edit, Path.Combine(output, "ADM02-recipe-friendly.png"));
                    Descendants(edit).OfType<TextBox>().Single(x => x.Name == "RecipeName").Text = "Công thức vừa lưu từ UI";
                    Descendants(edit).OfType<Button>().Single(x => x.Name == "RecipeSave").PerformClick();
                    await WaitUntil(() => !edit.Visible || Descendants(edit).Any(x => x.Name == "RecipeFeedback" && x.Text.Length > 0));
                    if (edit.Visible)
                    {
                        Capture(edit, Path.Combine(output, "ADM02-recipe-save-error.png"));
                        Assert.Fail("Lưu công thức: " + Descendants(edit).Single(x => x.Name == "RecipeFeedback").Text);
                    }
                    Assert.Equal(DialogResult.OK, edit.DialogResult);
                    var savedRecipe = (await LiveSession.Api.GetRecipesAsync()).Single(x => x.TenCongThuc == "Công thức vừa lưu từ UI" && x.TrangThai == "ACTIVE");
                    Assert.Equal(75, savedRecipe.PhanTramDuongMacDinh);
                    Assert.Contains(savedRecipe.ThanhPhan, x => x.MaNguyenLieu == ingredientId && x.LoaiDinhMuc == "FIXED" && x.DinhMucCoSo == 2.5m && x.TyLeHaoHut == 0);
                }
                if (route == "recipes")
                {
                    // Một màn hình cho6 cấu hình: nhập số lẻ, bỏ đá ở nóng, lưu cả bảng qua API thật.
                    var matrixDish = await LiveSession.Api.SaveMenuAsync("dishes", null, new MenuDishInput(1, "Món bảng size UI " + Guid.NewGuid().ToString("N")[..6], null, null, 0, false, false, null, null, Guid.NewGuid()));
                    foreach (var size in new[] { "S", "M", "L" })
                        foreach (var temp in new[] { "ICED", "HOT" })
                            await LiveSession.Api.SaveMenuAsync("variants", null, new MenuVariantInput(matrixDish.Ma, size, temp, 35000, false, size == "S" && temp == "ICED", [], [], null, Guid.NewGuid()));
                    var material = await LiveSession.Api.GetIngredientsAsync();
                    var menuMatrix = await LiveSession.Api.GetAdminMenuAsync();
                    using var matrix = new DishRecipeEditorForm(material, menuMatrix, await LiveSession.Api.GetRecipesAsync(), matrixDish.Ma);
                    matrix.StartPosition = FormStartPosition.Manual; matrix.Location = new Point(-12000, -12000); matrix.Show();
                    var picker = Descendants(matrix).OfType<CheckedListBox>().Single(x => x.Name == "RecipeDishIngredients");
                    for (var i = 0; i < picker.Items.Count; i++)
                        if ((picker.GetItemText(picker.Items[i]) ?? "").StartsWith("Sữa tươi") || (picker.GetItemText(picker.Items[i]) ?? "").StartsWith("Đá")) picker.SetItemChecked(i, true);
                    Descendants(matrix).OfType<Button>().Single(x => x.Name == "RecipeDishAdd").PerformClick();
                    var cold = Descendants(matrix).OfType<DataGridView>().Single(g => g.Name == "RecipeMatrixICED");
                    var hot = Descendants(matrix).OfType<DataGridView>().Single(g => g.Name == "RecipeMatrixHOT");
                    Assert.Equal(6, cold.Columns.Count); Assert.Equal(2, cold.Rows.Count); Assert.Single(hot.Rows.Cast<DataGridViewRow>());
                    foreach (DataGridViewRow row in cold.Rows) for (var i = 3; i < 6; i++) row.Cells[i].Value = 100m + i;
                    cold.CurrentCell = cold.Rows[0].Cells[3]; cold.BeginEdit(true);
                    ((DataGridViewTextBoxEditingControl)cold.EditingControl!).Text = "2,5";
                    Assert.True(cold.EndEdit()); Assert.Equal(2.5m, cold.Rows[0].Cells[3].Value);
                    // Nóng đang trống nên sao chép không cần xác nhận thay dữ liệu.
                    Descendants(matrix).OfType<Button>().Single(x => x.Name == "RecipeCopyCold").PerformClick();
                    hot = Descendants(matrix).OfType<DataGridView>().Single(g => g.Name == "RecipeMatrixHOT");
                    Assert.DoesNotContain(hot.Rows.Cast<DataGridViewRow>(), r => (string)r.Cells[0].Value! == "Đá");
                    var matrixTabs = Descendants(matrix).OfType<TabControl>().Single(t => t.Name == "RecipeSizeTabs");
                    matrixTabs.SelectedTab = matrixTabs.TabPages["HOT"];
                    hot.Rows[0].Cells[3].Value = 120m;
                    Descendants(matrix).OfType<ComboBox>().Single(c => c.Name == "RecipeDishSugar").SelectedValue = 75L;
                    Capture(matrix, Path.Combine(output, "ADM02-matrix-hot.png"));
                    matrixTabs.SelectedTab = matrixTabs.TabPages["ICED"];
                    Capture(matrix, Path.Combine(output, "ADM02-matrix-cold.png"));
                    AssertFooterVisible(matrix); matrix.Size = matrix.MinimumSize; await Task.Delay(100); AssertFooterVisible(matrix);
                    Capture(matrix, Path.Combine(output, "ADM02-matrix-compact.png"));
                    Descendants(matrix).OfType<Button>().Single(x => x.Name == "RecipeDishSave").PerformClick();
                    await WaitUntil(() => !matrix.Visible || Descendants(matrix).Any(x => x.Name == "RecipeDishFeedback" && x.Text.Length > 0));
                    Assert.True(!matrix.Visible, "Lưu bảng size: " + Descendants(matrix).FirstOrDefault(x => x.Name == "RecipeDishFeedback")?.Text);
                    var ids = menuMatrix.CauHinh.Where(v => v.MaMon == matrixDish.Ma).Select(v => v.MaCauHinhMon).ToHashSet();
                    var results = (await LiveSession.Api.GetRecipesAsync()).Where(r => r.MaCauHinhMon is { } v && ids.Contains(v)).ToList();
                    Assert.Equal(6, results.Count); Assert.All(results, r => Assert.Equal(75, r.PhanTramDuongMacDinh));
                    Assert.Contains(results, r => r.KichCo == "S" && r.NhietDo == "HOT" && r.ThanhPhan.Any(l => l.DinhMucCoSo == 120));
                    await Descendants(form).OfType<RecipesPage>().Single().Reload();
                    var list = Descendants(form).OfType<DataGridView>().Single(g => g.Name == "RecipesGrid");
                    Descendants(form).OfType<ComboBox>().Single(c => c.Name == "RecipeStateFilter").SelectedIndex = 0;
                    Assert.Single(list.Rows.Cast<DataGridViewRow>(), r => ((RecipeView)r.DataBoundItem!).TenDoiTuong == menuMatrix.Mon.Single(m => m.MaMon == matrixDish.Ma).TenMon);
                }
                if (route == "reports") await ReportNavigationUiChecks.Run(form, output);
                if (route == "stock") await StockDocumentEditorChecks.Run(output);
                if (route == "employees") await WorkforceUiChecks.Employees(form, output);
                if (route == "work-sessions") await WorkforceUiChecks.WorkSessions(form, output);
                if (route == "manage-orders") CheckCompactOrderActions(form);
                if (route == "menu") CheckSelectedRow(form);
                if (route == "customer-reset") await ResetPassword(output);
                if (route == "dashboard")
                {
                    var grid = Descendants(form).OfType<DataGridView>().Single(x => x.Name == "DashboardStock");
                    var column = grid.Columns.Cast<DataGridViewColumn>().Single(x => x.HeaderText == "Đơn vị");
                    Assert.All(grid.Rows.Cast<DataGridViewRow>(), row => Assert.False(long.TryParse(row.Cells[column.Index].Value?.ToString(), out _), "Không hiển thị mã đơn vị thay ký hiệu."));
                }
                form.Close(); exit = 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); }
            finally { loop.Close(); }
        };
        Application.Run(loop); return exit;
    }

    /// <summary>Kiểm thanh đơn chỉ ba nút và menu phụ vẫn có thao tác hợp lệ; mô phỏng trạng thái chỉ ở binding UI.</summary>
    private static void CheckCompactOrderActions(Form form)
    {
        var bar=Descendants(form).OfType<FlowLayoutPanel>().Single(x=>x.Name=="WorkspaceActions");
        Assert.InRange(bar.Controls.OfType<Button>().Count(x=>x.Visible),1,3);
        var grid=Descendants(form).OfType<DataGridView>().Single(x=>x.Name=="AdminDataGrid");
        var next=Descendants(form).OfType<Button>().Single(x=>x.Name=="OrderNextAction");
        var more=Descendants(form).OfType<Button>().Single(x=>x.Name=="OrderOtherActions");
        var saved=grid.DataSource;
        var original=(AdminOrderRow)grid.CurrentRow!.DataBoundItem;
        try
        {
            foreach(var pair in new[]{("PENDING_CONFIRMATION","Tiếp nhận"),("ACCEPTED","Bắt đầu pha"),("PREPARING","Pha xong"),("READY","Bàn giao / hoàn tất")})
            {
                grid.DataSource=new List<AdminOrderRow>{original with{TrangThai=pair.Item1}};
                Assert.True(next.Visible);Assert.Equal(pair.Item2,next.Text);
            }
            grid.DataSource=new List<AdminOrderRow>{original with{TrangThai="ACCEPTED"}};
            more.PerformClick();Assert.NotNull(more.ContextMenuStrip);Assert.True(more.ContextMenuStrip!.Visible);
            Assert.Contains(more.ContextMenuStrip.Items.Cast<ToolStripItem>(),x=>x.Text=="Hủy phần chưa pha");
            more.ContextMenuStrip.Close();
            grid.DataSource=new List<AdminOrderRow>{original with{TrangThai="COMPLETED"}};
            Assert.False(next.Visible);Assert.False(more.Enabled);
        }
        finally{grid.DataSource=saved;}
    }

    /// <summary>Đối chiếu pixel nền trước badge/text: tất cả cột của dòng chọn phải dùng cùng màu, không bị đứt nền.</summary>
    private static void CheckSelectedRow(Form form)
    {
        var grid = Descendants(form).OfType<DataGridView>().Single(x => x.Name == "MenuGrid_dishes");
        grid.ClearSelection(); grid.Rows[0].Selected = true; grid.Refresh();
        using var bitmap = new Bitmap(grid.Width, grid.Height); grid.DrawToBitmap(bitmap, grid.ClientRectangle);
        for (var index = 0; index < grid.Columns.Count; index++)
        {
            var rect = grid.GetCellDisplayRectangle(index, 0, true);
            if (rect.Width < 10) continue;
            Assert.Equal(CafeTheme.Selection.ToArgb(), bitmap.GetPixel(rect.Left + 4, rect.Top + rect.Height / 2).ToArgb());
        }
    }

    /// <summary>Cấp lại mật khẩu qua form thật, xác nhận lưu/thu hồi phiên rồi khách đổi tạm và đăng nhập đầy đủ.</summary>
    private static async Task ResetPassword(string output)
    {
        var original = TestData.Password(); var replacement = "reset";
        var input = new CustomerSaveInput(Guid.NewGuid(), "reset" + Guid.NewGuid().ToString("N")[..12], "Khách yêu cầu cấp lại mật khẩu", "reset" + Guid.NewGuid().ToString("N") + "@example.test", TestData.Phone(), original);
        // Fixture phải đăng ký qua Web; Admin chỉ reset khách đã tồn tại.
        var registered = await LiveSession.Api.RegisterAsync(new(input.TenDangNhap, input.HoTen,
            input.Email, input.SoDienThoai, original, Guid.NewGuid(), original));
        var account = (await LiveSession.Api.GetCustomerAsync(registered.MaKhachHang)).Account;
        using var http = new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("FOURCHANG_API_URL")!) };
        var customer = new CafeApiClient(http);
        customer.Token = (await customer.LoginCustomerAsync(new(input.TenDangNhap, original))).Token;
        using var editor = new CustomerEditorForm(await LiveSession.Api.GetCustomerAsync(account.MaKhachHang), "reset-password");
        editor.StartPosition = FormStartPosition.Manual; editor.Location = new Point(-12000, -12000); editor.Show();
        var fields = Descendants(editor).OfType<TextBox>().ToDictionary(x => x.Name);
        var save = Descendants(editor).OfType<Button>().Single(x => x.Name == "CustomerSave");
        fields["CustomerTemporaryPassword"].Text = replacement;
        fields["CustomerPasswordConfirmation"].Text = replacement + "x";
        Assert.DoesNotContain("CustomerReason", fields.Keys);
        save.PerformClick();
        Assert.Contains("chưa khớp", Descendants(editor).OfType<Label>().Single(x => x.Name == "CustomerFeedback").Text);
        fields["CustomerPasswordConfirmation"].Text = replacement;
        Capture(editor, Path.Combine(output, "customer-reset-before.png"));
        save.PerformClick(); await WaitUntil(() => !save.Visible);
        Capture(editor, Path.Combine(output, "customer-reset-saved.png"));
        Assert.True((await LiveSession.Api.GetCustomerAsync(account.MaKhachHang)).Account.BatBuocDoiMatKhau);
        var revoked = await Assert.ThrowsAsync<CafeApiException>(() => customer.CheckTemporaryPasswordAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        var previous = await Assert.ThrowsAsync<CafeApiException>(() => customer.LoginCustomerAsync(new(input.TenDangNhap, original)));
        Assert.Equal(HttpStatusCode.Unauthorized, previous.StatusCode);
        var temporary = await customer.LoginCustomerAsync(new(input.TenDangNhap, replacement));
        Assert.True(temporary.MustChangePassword); customer.Token = temporary.Token;
        var context = await customer.CheckTemporaryPasswordAsync();
        var final = TestData.Password();
        await customer.ChangeTemporaryPasswordAsync(new(final, Guid.NewGuid(), Convert.FromBase64String(context.GetProperty("phienBanDuLieu").GetString()!), final));
        var full = await customer.LoginCustomerAsync(new(input.TenDangNhap, final));
        Assert.False(full.MustChangePassword); customer.Token = full.Token;
        Assert.Equal(account.MaKhachHang, (await customer.GetSelfProfileAsync()).MaKhachHang);
        editor.Close();
    }

    /// <summary>Nút lưu phải thực sự nằm trong vùng cửa sổ ở cả kích thước thường và tối thiểu.</summary>
    private static void AssertFooterVisible(Form form)
    {
        var save = Descendants(form).OfType<Button>().Single(x => x.Text is "Lưu công thức" or "Lưu nguyên liệu" or "Lưu tất cả size");
        var bounds = form.RectangleToClient(save.RectangleToScreen(save.ClientRectangle));
        Assert.True(form.ClientRectangle.Contains(bounds), "Nút lưu bị cắt hoặc nằm ngoài vùng nhìn thấy của dialog.");
    }

    /// <summary>Duyệt cây control thật để tìm bảng/nút, không phụ thuộc vị trí phần tử trong layout.</summary>
    internal static IEnumerable<Control> Descendants(Control control)
    {
        foreach (Control child in control.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    /// <summary>Chờ có điều kiện trong message loop với giới hạn rõ ràng, không dùng Sleep chặn UI.</summary>
    internal static async Task WaitUntil(Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(25)) await Task.Delay(50);
        Assert.True(condition(), "Màn hình chưa tải được bảng từ API thật.");
    }
    /// <summary>Chụp bitmap để rà layout trực quan, không chụp form có mật khẩu đang hiển thị.</summary>
    internal static void Capture(Form form, string path)
    {
        using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size)); image.Save(path);
    }
}
