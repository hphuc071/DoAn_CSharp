using FourChang.Tests.Admin;
using FourChang.Tests.Shared;
using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shell;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Common;

namespace FourChang.Tests.Staff;

/// <summary>Phân nhóm tại server và polling WinForms thật; không coi build thành công là đã hết nhấp nháy.</summary>
[Collection("AdminFeatures"), Trait("Feature", "OrderListSeparation")]
public sealed class OrderListRefreshTests(AdminFeatureFixture fixture)
{
    /// <summary>Nhóm hủy gồm đơn đã hủy và yêu cầu chờ; Count/Skip/Take chạy sau lọc, từ chối đưa đơn về nhóm bình thường.</summary>
    [Fact]
    public async Task TachNhom_TruocPhanTrang_VaTraDonVeSauTuChoi()
    {
        var normal = await OperationsScenarioSeed.Create(fixture);
        var cancelled = await OperationsScenarioSeed.Create(fixture, "CANCELLED");
        var waiting = await OperationsScenarioSeed.Create(fixture);
        var customer = await fixture.Api.NewCustomer();
        await fixture.Api.Sql($"UPDATE dbo.DonHang SET KenhDatHang='WEB',MaKhachHang={customer.Id} WHERE MaDonHang={waiting.Order}; INSERT dbo.HuyMon(MaNhomHuy,MaChiTietDonHang,SoLuongHuy,LyDoHuy,MaKhachHangYeuCau,TrangThai,TienHangGiam,PhiGiaoHangGiam) VALUES(NEWID(),{waiting.Line},2,N'Yêu cầu chờ duyệt',{customer.Id},'REQUESTED',0,0)");
        var cancelledIds = await Ids("CANCELLATION"); var normalIds = await Ids("NORMAL");
        Assert.Contains(cancelled.Order, cancelledIds); Assert.Contains(waiting.Order, cancelledIds); Assert.DoesNotContain(normal.Order, cancelledIds);
        Assert.Contains(normal.Order, normalIds); Assert.DoesNotContain(cancelled.Order, normalIds); Assert.DoesNotContain(waiting.Order, normalIds);
        (await fixture.Api.Send("GET", "/api/admin/orders?NhomDon=INVALID", headers: fixture.Admin)).Expect(400, "Nhóm không hợp lệ");
        await fixture.Api.Sql($"UPDATE dbo.HuyMon SET TrangThai='REJECTED' WHERE MaChiTietDonHang={waiting.Line}");
        Assert.Contains(waiting.Order, await Ids("NORMAL")); Assert.DoesNotContain(waiting.Order, await Ids("CANCELLATION"));
    }

    /// <summary>Đợi qua hai chu kỳ polling, dữ liệu không đổi không reset binding/chọn/cuộn; dữ liệu đổi vẫn cập nhật đúng dòng.</summary>
    [Fact, Trait("Layer", "WinForms")]
    public async Task PollingKhongGanLaiBangVaOverlay_NhungVanNhanThayDoi()
    {
        for (var index = 0; index < 14; index++) await OperationsScenarioSeed.Create(fixture);
        await DesktopWorker.Run(fixture.Api, "ORDER-REFRESH", new() { ["FOURCHANG_API_URL"] = fixture.Api.Url, ["TEST_ORDER_REFRESH_ADMIN"] = fixture.Admin["Authorization"][7..] });
    }

    /// <summary>Đọc lần lượt các trang nhỏ để kiểm chứng nhóm và tổng số không lệch khi phân trang phía server.</summary>
    private async Task<List<long>> Ids(string group)
    {
        var ids = new List<long>(); var page = 1; long total;
        do
        {
            var data = (await fixture.Api.Send("GET", $"/api/admin/orders?NhomDon={group}&KichThuocTrang=1&Trang={page++}", headers: fixture.Admin)).Expect(200, "Tra nhóm").Json;
            total = data.Number("tongSo"); ids.AddRange(data["duLieu"]!.AsArray().Select(x => x!.Number("maDonHang")));
        } while (ids.Count < total);
        Assert.Equal(total, ids.Count); Assert.Equal(ids.Count, ids.Distinct().Count()); return ids;
    }

    /// <summary>Worker mở form thật với API SQL, theo dõi số lần binding và trạng thái loading qua các tick timer.</summary>
    internal static int RunHarness(string output)
    {
        FourChang.Desktop.DesktopLauncher.ConfigureUi(); var result = 1;
        using var loop = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) };
        loop.Shown += async (_, _) =>
        {
            try
            {
                var token = Environment.GetEnvironmentVariable("TEST_ORDER_REFRESH_ADMIN")!;
                LiveSession.Preview = false; LiveSession.Api.Token = token; LiveSession.Current = new SessionResult(token, "ADMIN", "Kiểm thử", 1);
                using var form = new MainForm("Admin", "Kiểm thử") { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-16000, -16000), ClientSize = new(1400, 950) };
                form.Show(); form.Navigate("manage-orders");
                await Until(() => All(form).OfType<DataGridView>().Any(x => x.Name == "AdminDataGrid" && x.RowCount >= 12));
                var grid = All(form).OfType<DataGridView>().Single(x => x.Name == "AdminDataGrid");
                grid.CurrentCell = grid.Rows[9].Cells[0]; grid.FirstDisplayedScrollingRowIndex = 3; grid.Focus();
                var selected = ((AdminOrderRow)grid.CurrentRow!.DataBoundItem).MaDonHang; var top = grid.FirstDisplayedScrollingRowIndex;
                var source = grid.DataSource; var bindingChanges = 0; var loadingFlashes = 0;
                grid.DataSourceChanged += (_, _) => bindingChanges++;
                foreach (var loading in All(form).Where(x => x.GetType().Name == "AdminLoadingStrip")) loading.VisibleChanged += (_, _) => { if (loading.Visible) loadingFlashes++; };
                // Ô đang nhập dở vẫn giữ nguyên, nhưng polling dùng bộ lọc đã áp dụng và tiếp tục nhận trạng thái mới.
                var draftSearch = All(form).OfType<TextBox>().Single(x => x.PlaceholderText == "Mã đơn hoặc tên khách hàng");
                draftSearch.Text = "Bộ lọc đang nhập chưa áp dụng"; draftSearch.Focus();
                var originalTime = grid.Tag;
                await Task.Delay(6800);
                Assert.NotEqual(originalTime, grid.Tag);
                Assert.DoesNotContain(All(form).OfType<Label>(),x=>x.Visible&&x.Text.StartsWith("Cập nhật",StringComparison.Ordinal)); // Đã có fetch thật, không chỉ tắt timer để làm test đạt.
                Assert.Equal("Bộ lọc đang nhập chưa áp dụng", draftSearch.Text); Assert.True(draftSearch.Focused);
                Assert.Same(source, grid.DataSource); Assert.Equal(0, bindingChanges); Assert.Equal(0, loadingFlashes);
                Assert.Equal(selected, ((AdminOrderRow)grid.CurrentRow!.DataBoundItem).MaDonHang); Assert.Equal(top, grid.FirstDisplayedScrollingRowIndex);
                var detail = await LiveSession.Api.GetAdminOrderAsync(selected);
                await LiveSession.Api.AdminOrderActionAsync(selected, new("ACCEPT", "Test thay đổi dữ liệu nền", detail.Don.PhienBanDuLieu, Guid.NewGuid()));
                await Until(() => grid.Rows.Cast<DataGridViewRow>().Any(x => x.DataBoundItem is AdminOrderRow r && r.MaDonHang == selected && r.TrangThai == "ACCEPTED"));
                Assert.Equal(selected, ((AdminOrderRow)grid.CurrentRow!.DataBoundItem).MaDonHang); Assert.Equal(top, grid.FirstDisplayedScrollingRowIndex); Assert.Equal(0, loadingFlashes);
                using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(output, "orders-stable-refresh.png"));
                await AcceptWithoutReason(form,grid,output);
                form.Close(); result = 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); }
            finally { loop.Close(); }
        };
        Application.Run(loop); return result;
    }

    /// <summary>Bấm tiếp nhận ở dialog thật, không nhập lý do; API vẫn ghi nhật ký và chuyển trạng thái.</summary>
    private static async Task AcceptWithoutReason(Form form,DataGridView grid,string output)
    {
        All(form).OfType<TextBox>().Single(x=>x.PlaceholderText=="Mã đơn hoặc tên khách hàng").Clear();
        var pending=grid.Rows.Cast<DataGridViewRow>().First(x=>x.DataBoundItem is AdminOrderRow r&&r.TrangThai=="PENDING_CONFIRMATION");
        grid.CurrentCell=pending.Cells[0];var id=((AdminOrderRow)pending.DataBoundItem).MaDonHang;
        var handled=false;Exception? failure=null;
        using var timer=new System.Windows.Forms.Timer{Interval=80};
        timer.Tick+=(_,_)=>
        {
            var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text.StartsWith("Tiếp nhận đơn ·"));if(dialog is null)return;
            try
            {
                timer.Stop();Assert.Empty(All(dialog).OfType<TextBox>());
                using var image=new Bitmap(dialog.Width,dialog.Height);dialog.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(output,"accept-without-reason.png"));
                All(dialog).OfType<Button>().Single(x=>x.Name=="CommandConfirm").PerformClick();handled=true;
            }
            catch(Exception error){failure=error;handled=true;dialog.Close();}
        };
        timer.Start();All(form).OfType<Button>().Single(x=>x.Name=="OrderNextAction").PerformClick();
        await Until(()=>handled&&!Application.OpenForms.Cast<Form>().Any(x=>x.Text.StartsWith("Tiếp nhận đơn ·")));
        Assert.Null(failure);
        Assert.Equal("ACCEPTED",(await LiveSession.Api.GetAdminOrderAsync(id)).Don.TrangThai);
    }

    /// <summary>Cho message loop tiếp tục xử lý timer/API; giới hạn chờ tránh treo worker khi lỗi.</summary>
    private static async Task Until(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!predicate()) { if (DateTime.UtcNow > deadline) throw new TimeoutException("Không nhận được dữ liệu đơn mới."); await Task.Delay(100); }
    }

    /// <summary>Duyệt các control con để xác nhận hành vi thật của grid và loading strip.</summary>
    private static IEnumerable<Control> All(Control parent) => parent.Controls.Cast<Control>().SelectMany(x => new[] { x }.Concat(All(x)));
}
