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
    [InlineData("UC-ADM-04", "recipes")]
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
                    // Cả hai lựa chọn phải lưu được bằng form thật và có đúng một hàng cơ sở hệ số 1.
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
                        Assert.Equal(selectedId, saved.MaDonViCoSo); Assert.Single(saved.QuyDoi); Assert.Equal(1m, saved.QuyDoi[0].HeSoVeDonViCoSo);
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
                    using var editor = new RecipeEditorForm(await LiveSession.Api.GetIngredientsAsync(), await LiveSession.Api.GetAdminMenuAsync(), null);
                    editor.StartPosition = FormStartPosition.Manual; editor.Location = new Point(-12000, -12000); editor.Show(); await Task.Delay(100);
                    var materials = await LiveSession.Api.GetIngredientsAsync();
                    var ml = materials.DonVi.Single(x => x.KyHieu == "ml");
                    var liquid = materials.NguyenLieu.First(x => x.MaDonViCoSo == ml.MaDonVi && x.DangSuDung);
                    Descendants(editor).OfType<ComboBox>().Single(x => x.Name == "RecipeIngredient").SelectedValue = liquid.MaNguyenLieu;
                    var recipeUnit = Descendants(editor).OfType<ComboBox>().Single(x => x.Name == "RecipeIngredientUnit");
                    Assert.False(recipeUnit.Enabled); Assert.Equal(ml.MaDonVi, recipeUnit.SelectedValue);
                    AssertFooterVisible(editor);
                    Capture(editor, Path.Combine(output, code + "-editor.png"));
                    editor.Size = editor.MinimumSize; await Task.Delay(100);
                    AssertFooterVisible(editor);
                    Capture(editor, Path.Combine(output, code + "-editor-compact.png")); editor.Close();
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
        var original = TestData.Password(); var replacement = TestData.Password();
        var input = new CustomerSaveInput(Guid.NewGuid(), "reset" + Guid.NewGuid().ToString("N")[..12], "Khách yêu cầu cấp lại mật khẩu", "reset" + Guid.NewGuid().ToString("N") + "@example.test", TestData.Phone(), original);
        var account = (await LiveSession.Api.SaveCustomerAsync(null, input)).Account!;
        using var http = new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("FOURCHANG_API_URL")!) };
        var customer = new CafeApiClient(http);
        customer.Token = (await customer.LoginCustomerAsync(new(input.TenDangNhap, original))).Token;
        using var editor = new CustomerEditorForm(await LiveSession.Api.GetCustomerAsync(account.MaKhachHang), "reset-password");
        editor.StartPosition = FormStartPosition.Manual; editor.Location = new Point(-12000, -12000); editor.Show();
        var fields = Descendants(editor).OfType<TextBox>().ToDictionary(x => x.Name);
        var save = Descendants(editor).OfType<Button>().Single(x => x.Name == "CustomerSave");
        fields["CustomerTemporaryPassword"].Text = replacement;
        fields["CustomerPasswordConfirmation"].Text = replacement + "x";
        fields["CustomerReason"].Text = "Khách yêu cầu cấp lại mật khẩu.";
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
        var save = Descendants(form).OfType<Button>().Single(x => x.Text is "Lưu nháp" or "Lưu nguyên liệu");
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
