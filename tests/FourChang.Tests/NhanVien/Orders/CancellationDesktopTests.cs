using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shell;
using FourChang.Presentation.Shared.Contracts.Common;
using FourChang.Tests.Admin;
using FourChang.Tests.Shared;
using static FourChang.Tests.Shared.TestData;

namespace FourChang.Tests.Staff;

/// <summary>Thao tác ba vai trò trên WinForms thật: chuyển yêu cầu, duyệt hủy và chi tại quầy bằng dialog thật.</summary>
public sealed class CancellationDesktopTests
{
    /// <summary>Chuẩn bị một yêu cầu khách trong fixture riêng rồi worker STA bấm các nút giao diện thực.</summary>
    [Fact, Trait("Feature", "CancellationApproval"), Trait("Layer", "WinForms")]
    public async Task StaffChuyen_AdminDuyet_StaffGiaoTien_TrenGiaoDien()
    {
        await using var api = await TestApi.Start("Cancellation-Desktop", runtime: new() { ["Orders__CancellationEnabled"] = "true", ["Orders__SqlIntegrationValidated"] = "true" });
        var admin = await api.AdminLogin(); var customer = await api.NewCustomer(); var owner = await api.Login(customer);
        var data = await OperationsScenarioSeed.Create(api);
        await api.Sql($"UPDATE dbo.DonHang SET KenhDatHang='WEB',MaKhachHang={customer.Id},MaCaLamViec=NULL WHERE MaDonHang={data.Order}");
        var detail = (await api.Send("GET", $"/api/admin/orders/{data.Order}", headers: admin)).Expect(200, "Đọc version").Json;
        owner["Idempotency-Key"] = NewId();
        (await api.Send("POST", $"/api/customer/orders/{data.Order}/cancel", Body(("Reason", "Khách đến quầy yêu cầu hủy"), ("ExpectedVersion", detail["don"]!.Text("phienBanDuLieu"))), owner)).Expect(200, "Gửi yêu cầu khách");
        var employee = await api.Employee(admin, "Nguyễn Hoàng Minh");
        var session = (await api.Send("POST", "/api/pos/shifts", Body(("MaNhanVien", employee.Number("maNhanVien")), ("HoTen", employee.Text("hoTen")), ("TenCa", "Sáng"), ("MaMay", "CANCEL-UI"), ("TienMatDauCa", 500000), ("MaThaoTac", NewId())))).Expect(200, "Mở ca").Json;
        await DesktopWorker.Run(api, "CANCELLATION", new() { ["FOURCHANG_API_URL"] = api.Url, ["TEST_CANCEL_STAFF_TOKEN"] = session.Text("token"), ["TEST_CANCEL_SHIFT"] = session.Number("shiftId").ToString(), ["TEST_CANCEL_ADMIN_TOKEN"] = admin["Authorization"][7..] });
        Assert.Equal("CANCELLED", await api.Sql($"SELECT TrangThai FROM dbo.DonHang WHERE MaDonHang={data.Order}"));
        Assert.Equal("1", await api.Sql($"SELECT COUNT(*) FROM dbo.LanChiHoanTien a JOIN dbo.HoSoHoanTien r ON a.MaHoanTien=r.MaHoanTien WHERE r.MaDonHang={data.Order} AND a.TrangThai='SUCCEEDED'"));
    }

    /// <summary>Worker STA dùng message loop và timer nhập dialog thật; chỉ dữ liệu SQL fixture được tác động.</summary>
    internal static int RunHarness(string output)
    {
        FourChang.Desktop.DesktopLauncher.ConfigureUi();
        var result = 1; using var loop = new Form { ShowInTaskbar = false, Location = new(-20000, -20000) };
        loop.Shown += async (_, _) =>
        {
            try
            {
                LiveSession.Preview = false;
                foreach (var step in new[] { ("Staff", "Gửi yêu cầu duyệt"), ("Admin", "Duyệt hủy / hoàn tại quầy"), ("Staff", "Hoàn tiền tại quầy") })
                {
                    var token = Environment.GetEnvironmentVariable(step.Item1 == "Staff" ? "TEST_CANCEL_STAFF_TOKEN" : "TEST_CANCEL_ADMIN_TOKEN")!;
                    LiveSession.Api.Token = token; LiveSession.Current = new SessionResult(token, step.Item1 == "Staff" ? "STAFF" : "ADMIN", "Kiểm thử", 1, long.Parse(Environment.GetEnvironmentVariable("TEST_CANCEL_SHIFT")!)); LiveSession.ShiftLocked = false;
                    if (step.Item2 == "Hoàn tiền tại quầy")
                    {
                        // Sau khi Admin duyệt, trang hủy phải chỉ báo thành công; không xuất hiện nút chi tiền.
                        using var confirmed = new MainForm("Staff", "Kiểm thử") { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-16000, -16000), ClientSize = new(1400, 950) };
                        confirmed.Show(); confirmed.Navigate("cancel");
                        var until = DateTime.UtcNow.AddSeconds(20);
                        while (!All(confirmed).OfType<Label>().Any(x => x.Name == "OrderSelectionStatus" && x.Text == "Hủy thành công"))
                        { if (DateTime.UtcNow > until) throw new TimeoutException("Không hiển thị hủy thành công sau duyệt"); await Task.Delay(100); }
                        var submit = All(confirmed).OfType<Button>().Single(x => x.Name == "OrderNextAction");
                        Assert.Equal("Gửi yêu cầu duyệt", submit.Text); Assert.False(submit.Enabled);
                        var toolbar = All(confirmed).OfType<FlowLayoutPanel>().Single(x => x.Name == "WorkspaceActions");
                        Assert.Single(toolbar.Controls.OfType<Button>(), x => x.Visible);
                        using var proof = new Bitmap(confirmed.Width, confirmed.Height); confirmed.DrawToBitmap(proof, new Rectangle(Point.Empty, proof.Size)); proof.Save(Path.Combine(output, "staff-cancellation-success.png"));
                        confirmed.Close();
                    }
                    using var form = new MainForm(step.Item1, "Kiểm thử") { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-16000, -16000), ClientSize = new(1400, 950) };
                                        form.Show();
                    form.Navigate(step.Item2 == "Hoàn tiền tại quầy" ? "payment" : step.Item1 == "Staff" ? "cancel" : "cancelled-orders");
                    if (step.Item2 == "Hoàn tiền tại quầy") All(form).OfType<TabControl>().Single(x => x.Name == "StaffBillingTabs").SelectedIndex = 1;
                    var deadline = DateTime.UtcNow.AddSeconds(20);
                    while (!All(form).OfType<Button>().Any(x => x.Name == "OrderNextAction" && x.Visible && x.Enabled && x.Text == step.Item2))
                    { if (DateTime.UtcNow > deadline) throw new TimeoutException("Không hiện bước " + step.Item2); await Task.Delay(100); }
                    if (step.Item2 == "Gửi yêu cầu duyệt")
                    {
                        var bar = All(form).OfType<FlowLayoutPanel>().Single(x => x.Name == "WorkspaceActions");
                        Assert.Single(bar.Controls.OfType<Button>(), x => x.Visible);
                        Assert.DoesNotContain(bar.Controls.OfType<Button>(), x => x.Visible && x.Text == "Hoàn tiền tại quầy");
                    }
                    using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(output, step.Item2 == "Gửi yêu cầu duyệt" ? "staff-forward.png" : step.Item1 == "Admin" ? "admin-approve.png" : "staff-counter-refund.png"));
                    var submitted = false;
                    using var timer = new System.Windows.Forms.Timer { Interval = 100 };
                    timer.Tick += (_, _) =>
                    {
                        var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.GetType().Name.StartsWith("AdminCommandDialog", StringComparison.Ordinal));
                        if (dialog is null) return; timer.Stop();
                        var text = All(dialog).OfType<TextBox>().ToArray();
                        var receiver = text.FirstOrDefault(x => x.ReadOnly)?.Text ?? "";
                        foreach (var input in text.Where(x => !x.ReadOnly)) input.Text = step.Item2 == "Hoàn tiền tại quầy" && input == text.First(x => !x.ReadOnly) ? receiver : "Đã kiểm tra khách / biên nhận quầy UI-TEST";
                        foreach (var check in All(dialog).OfType<CheckBox>().Where(x => x.Visible)) check.Checked = true;
                        submitted = true; All(dialog).OfType<Button>().Single(x => x.Name == "CommandConfirm").PerformClick();
                    };
                    timer.Start(); All(form).OfType<Button>().Single(x => x.Name == "OrderNextAction").PerformClick();
                    while (!submitted || Application.OpenForms.Cast<Form>().Any(x => x.GetType().Name.StartsWith("AdminCommandDialog", StringComparison.Ordinal)))
                    { if (DateTime.UtcNow > deadline.AddSeconds(15)) throw new TimeoutException("Dialog chưa hoàn thành " + step.Item2); await Task.Delay(100); }
                    form.Close();
                }
                result = 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); }
            finally { loop.Close(); }
        };
        Application.Run(loop); return result;
    }

    /// <summary>Duyệt control thật trong form để bấm đúng nút/ô nhập mà không gọi tắt service.</summary>
    private static IEnumerable<Control> All(Control parent) => parent.Controls.Cast<Control>().SelectMany(x => new[] { x }.Concat(All(x)));
}
