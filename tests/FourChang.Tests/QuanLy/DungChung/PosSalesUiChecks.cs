using FourChang.Desktop.Shell;
using FourChang.Desktop.Shared.Session;
using FourChang.Presentation.Shared.Contracts.Admin;
using FourChang.Presentation.Shared.Contracts.Staff;
using FourChang.Desktop.Staff.Sales;
namespace FourChang.Tests.Admin;
/// <summary>Đi qua form cấu hình và nút thu tiền thật, không gọi callback giả để tạo dòng giỏ.</summary>
internal static class PosSalesUiChecks
{
    /// <summary>Thêm nhiều món ở cửa sổ thực, chụp ảnh và kiểm khoản thu qua phản hồi API.</summary>
    internal static async Task Run(string output)
    {
        var original=LiveSession.Current;var token=LiveSession.Api.Token;
        try
        {
            var employee=await LiveSession.Api.SaveEmployeeAsync(new EmployeeInput("Trần Hoàng Phúc","0912345678",DateTime.Today,"ACTIVE",MaThaoTac:Guid.NewGuid()));
            var session=await LiveSession.Api.OpenShiftAsync(new(employee.MaNhanVien,"Chiều","UI-POS-"+Guid.NewGuid().ToString("N")[..8],500000,employee.HoTen));
            LiveSession.Current=session;LiveSession.Api.Token=session.Token;LiveSession.ShiftLocked=false;
            using var shell=new MainForm("Staff",employee.HoTen){ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-14000,-14000)};shell.Show();
            await AdminFeaturesUiTests.WaitUntil(()=>Controls(shell).OfType<Button>().Count(x=>x.Name.StartsWith("PosProduct")&&x.Enabled)>=2);
            // Hai chu kỳ có fetch thật nhưng không hủy/dựng lại các thẻ món không đổi.
            var cards=Controls(shell).OfType<Button>().Where(x=>x.Name.StartsWith("PosProduct")).ToArray();
            var disposed=0;foreach(var card in cards)card.Disposed+=(_,_)=>disposed++;
            var stamp=Controls(shell).Single(x=>x.Name=="PosProductGrid");var previous=stamp.Tag;
            await Task.Delay(10600);
            Assert.NotEqual(previous,stamp.Tag);
            Assert.DoesNotContain(Controls(shell).OfType<Label>(),x=>x.Visible&&x.Text.StartsWith("Thực đơn cập nhật"));Assert.Equal(0,disposed);
            foreach(var card in cards)Assert.Same(card,Controls(shell).Single(x=>x.Name==card.Name));
            await CheckListedPrices(shell,output);
            await CheckSeparateOptions(shell,output);
            var names=Controls(shell).OfType<Button>().Where(x=>x.Name.StartsWith("PosProduct")&&x.Enabled).Take(2).Select(x=>x.Name).ToList();
            for(var i=0;i<2;i++)
            {
                var completed=false;Exception? error=null;
                using var timer=new System.Windows.Forms.Timer{Interval=70};
                timer.Tick+=(_,_)=>
                {
                    var form=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Name=="PosItemEditor");if(form is null)return;
                    try
                    {
                        Controls(form).OfType<NumericUpDown>().Single(x=>x.Name=="PosQuantity").Value=2;
                        Controls(form).OfType<TextBox>().Single(x=>x.Name=="PosNote").Text="Ghi chú pha chế "+i;
                        var button=Controls(form).OfType<Button>().Single(x=>x.Name=="PosAddConfiguredItem");
                        if(!button.Enabled)return;timer.Stop();button.PerformClick();completed=true;
                    }
                    catch(Exception ex){error=ex;timer.Stop();form.Close();completed=true;}
                };
                timer.Start();Controls(shell).OfType<Button>().Single(x=>x.Name==names[i]).PerformClick();
                await AdminFeaturesUiTests.WaitUntil(()=>completed&& !Application.OpenForms.Cast<Form>().Any(x=>x.Name=="PosItemEditor"));
                Assert.Null(error);
                var expected=i+1;await AdminFeaturesUiTests.WaitUntil(()=>Controls(shell).OfType<DataGridView>().Single(x=>x.Name=="PosCartGrid").Rows.Count==expected);
            }
            // Thu ngân phải nhìn thấy nút khi cửa sổ mở; không buộc cuộn xuống để thu đơn.
            var pay=Controls(shell).OfType<Button>().Single(x=>x.Name=="PosCheckout");
            foreach(var size in new[]{new Size(1440,960),new Size(1280,850)})
            {
                shell.ClientSize=size;await Task.Delay(100);
                var bounds=shell.RectangleToClient(pay.RectangleToScreen(pay.ClientRectangle));
                Assert.True(bounds.Top>=0&&bounds.Bottom<=shell.ClientSize.Height,$"Nút thanh toán bị che: {bounds} / {shell.ClientSize}");
            }
            AdminFeaturesUiTests.Capture(shell,Path.Combine(output,"POS-multi-item-cart.png"));
            Assert.DoesNotContain(Controls(shell),x=>x.Name=="PosCashTender");
            Assert.DoesNotContain(Controls(shell).OfType<Label>(),x=>x.Text.Contains("Khách đưa")||x.Text.StartsWith("Tiền thừa"));
            Controls(shell).OfType<Button>().Single(x=>x.Name=="PosCheckout").PerformClick();
            await AdminFeaturesUiTests.WaitUntil(()=>Controls(shell).OfType<Label>().Any(x=>x.Text.StartsWith("Đã thanh toán đơn")));
            Assert.Empty(Controls(shell).OfType<DataGridView>().Single(x=>x.Name=="PosCartGrid").Rows.Cast<DataGridViewRow>());
            AdminFeaturesUiTests.Capture(shell,Path.Combine(output,"POS-cash-success.png"));
            await CheckPreparation(shell,output);shell.Close();
        }
        finally{LiveSession.Current=original;LiveSession.Api.Token=token;LiveSession.ShiftLocked=false;}
    }
    /// <summary>Mỗi món mở đúng cấu hình/giá trên thẻ, kể cả khi S nóng đứng trước M lạnh trong API.</summary>
    private static async Task CheckListedPrices(Form owner, string output)
    {
        foreach (var product in (await LiveSession.Api.GetMenuAsync()).Where(x => x.Available))
        {
            var variants = await LiveSession.Api.GetMenuVariantsAsync(product.MaMon);
            var expected = variants.Single(x => x.MaCauHinhMon == product.Id);
            using var editor = new PosItemEditorForm(product, variants);
            editor.Show(owner); await Task.Delay(80);
            Assert.Equal(expected.KichCo, Controls(editor).OfType<ComboBox>().Single(x => x.Name == "PosSize").Text);
            Assert.Equal(expected.NhietDo == "HOT" ? "Nóng" : "Lạnh", Controls(editor).OfType<ComboBox>().Single(x => x.Name == "PosTemperature").Text);
            Assert.Equal(product.Price, expected.GiaBan);
            Assert.Equal($"Tạm tính: {product.Price:N0}đ", Controls(editor).OfType<Label>().Single(x => x.Name == "PosConfiguredTotal").Text);
            Controls(editor).OfType<NumericUpDown>().Single(x => x.Name == "PosQuantity").Value = 2;
            Assert.Equal($"Tạm tính: {product.Price * 2:N0}đ", Controls(editor).OfType<Label>().Single(x => x.Name == "PosConfiguredTotal").Text);
            if (product.Name == "Bạc xỉu") AdminFeaturesUiTests.Capture(editor, Path.Combine(output, "POS-listed-price-bac-xiu.png"));
            editor.Close();
        }
    }
    /// <summary>Đổi nóng/lạnh trên form thật: size riêng, nóng ẩn đá, lạnh có đường/đá và giá hợp lệ.</summary>
    private static async Task CheckSeparateOptions(Form owner,string output)
    {
        var menu=await LiveSession.Api.GetMenuAsync();var product=menu.First(x=>x.Name=="Latte");
        using var editor=new PosItemEditorForm(product,await LiveSession.Api.GetMenuVariantsAsync(product.MaMon));
        editor.Show(owner);await Task.Delay(150);
        var size=Controls(editor).OfType<ComboBox>().Single(x=>x.Name=="PosSize");
        var temperature=Controls(editor).OfType<ComboBox>().Single(x=>x.Name=="PosTemperature");
        Assert.All(size.Items.Cast<object>(),x=>Assert.Contains(x.ToString(),new[]{"S","M","L"}));
        foreach(var name in new[]{"Nóng","Lạnh"})
        {
            temperature.SelectedIndex=temperature.Items.Cast<object>().ToList().FindIndex(x=>x.ToString()==name);
            Assert.True(temperature.SelectedIndex>=0);await Task.Delay(100);
            Assert.Equal(name=="Lạnh",Controls(editor).Single(x=>x.Name=="PosIceField").Visible);
            Assert.True(Controls(editor).OfType<ComboBox>().Single(x=>x.Name=="PosSugar").Items.Count>0);
            var save=Controls(editor).OfType<Button>().Single(x=>x.Name=="PosAddConfiguredItem");Assert.True(save.Enabled);
            var bounds=editor.RectangleToClient(save.RectangleToScreen(save.ClientRectangle));Assert.True(bounds.Bottom<=editor.ClientSize.Height);
            AdminFeaturesUiTests.Capture(editor,Path.Combine(output,name=="Nóng"?"POS-options-hot.png":"POS-options-cold.png"));
        }
        editor.Close();
    }

    /// <summary>Đi từ đơn POS đã thu tiền thật qua pha/hoàn tất; mỗi dòng hai ly được điền tự động.</summary>
    private static async Task CheckPreparation(MainForm shell,string output)
    {
        shell.Navigate("orders");
        await AdminFeaturesUiTests.WaitUntil(()=>Controls(shell).OfType<DataGridView>().Any(x=>x.Name=="AdminDataGrid"&&x.RowCount>0));
        var grid=Controls(shell).OfType<DataGridView>().Single(x=>x.Name=="AdminDataGrid");
        var shiftGroup=Controls(shell).OfType<ComboBox>().Single(x=>x.Name=="OrderShiftGroup");
        Assert.Contains("Đơn nhận từ ca trước",shiftGroup.Items.Cast<string>());
        Assert.Equal(0,shiftGroup.SelectedIndex);
        AdminFeaturesUiTests.Capture(shell,Path.Combine(output,"POS-orders-current-shift.png"));
        var groupStamp=grid.Tag;shiftGroup.SelectedIndex=1;
        Controls(shell).OfType<Button>().Single(x=>x.Text=="Tìm kiếm").PerformClick();
        await AdminFeaturesUiTests.WaitUntil(()=>!Equals(groupStamp,grid.Tag));
        Assert.Empty(grid.Rows.Cast<DataGridViewRow>());
        AdminFeaturesUiTests.Capture(shell,Path.Combine(output,"POS-orders-previous-shift.png"));
        groupStamp=grid.Tag;shiftGroup.SelectedIndex=0;
        Controls(shell).OfType<Button>().Single(x=>x.Text=="Tìm kiếm").PerformClick();
        await AdminFeaturesUiTests.WaitUntil(()=>!Equals(groupStamp,grid.Tag)&&grid.RowCount>0);
        var row=grid.Rows.Cast<DataGridViewRow>().First(x=>x.DataBoundItem is AdminOrderRow r&&r.Kenh=="POS"&&r.TrangThai=="ACCEPTED");
        grid.CurrentCell=row.Cells[0];var id=((AdminOrderRow)row.DataBoundItem).MaDonHang;
        await CheckBasicDetails(shell,output);
        // Kiểm tra bố cục địa chỉ bằng dữ liệu hiển thị có kiểm soát, không sửa đơn trong SQL.
        var savedDetail = await LiveSession.Api.GetStaffOrderAsync(id);
        foreach(var delivery in new[]{false,true})
        {
            var example = savedDetail with { DiaChiKhachHang="123 Nguyễn Trãi, TP. Hồ Chí Minh", DiaChi="45 Lê Lợi, TP. Hồ Chí Minh", Don=savedDetail.Don with { HinhThuc=delivery?"DELIVERY":"TAKEAWAY" } };
            using var addressForm = new FourChang.Desktop.Shared.Orders.OrderDetailsForm(example);
            addressForm.Show(shell);await Task.Delay(120);
            Assert.Contains(Controls(addressForm).OfType<Label>(),x=>x.Text==(delivery?"Địa chỉ giao hàng":"Địa chỉ khách hàng"));
            Assert.Contains(Controls(addressForm).OfType<Label>(),x=>x.Text==(delivery?example.DiaChi:example.DiaChiKhachHang));
            AdminFeaturesUiTests.Capture(addressForm,Path.Combine(output,delivery?"order-delivery-address.png":"order-customer-address.png"));addressForm.Close();
        }
        foreach(var caption in new[]{"Bắt đầu pha","Pha xong","Pha xong","Bàn giao / hoàn tất"})
        {
            await AdminFeaturesUiTests.WaitUntil(()=>Controls(shell).OfType<Button>().Any(x=>x.Name=="OrderNextAction"&&x.Enabled&&x.Text==caption));
            var handled=false;Exception? failure=null;
            using var timer=new System.Windows.Forms.Timer{Interval=80};
            timer.Tick+=(_,_)=>
            {
                var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>Controls(x).OfType<Button>().Any(b=>b.Name=="CommandConfirm"));
                if(dialog is null)return;
                try
                {
                    timer.Stop();
                    if(caption=="Tiếp nhận")Controls(dialog).OfType<TextBox>().Single().Text="Nhân viên tiếp nhận đơn";
                    else
                    {
                        Assert.DoesNotContain(Controls(dialog).OfType<TextBox>(),x=>x.Parent is not UpDownBase);Assert.Empty(Controls(dialog).OfType<CheckBox>());
                        var quantity=Controls(dialog).OfType<NumericUpDown>().SingleOrDefault(x=>x.Name=="OrderActionQuantity");
                        if(quantity is not null){Assert.Equal(2m,quantity.Value);Assert.Equal(2m,quantity.Maximum);}
                        Assert.DoesNotContain("START",dialog.Text);Assert.True(dialog.ClientSize.Height<=440);
                    }
                    AdminFeaturesUiTests.Capture(dialog,Path.Combine(output,"POS-action-"+caption.Replace(" / ","-")+".png"));
                    Controls(dialog).OfType<Button>().Single(x=>x.Name=="CommandConfirm").PerformClick();handled=true;
                }
                catch(Exception error){failure=error;dialog.Close();handled=true;}
            };
            timer.Start();Controls(shell).OfType<Button>().Single(x=>x.Name=="OrderNextAction").PerformClick();
            try { await AdminFeaturesUiTests.WaitUntil(()=>handled&&!Application.OpenForms.Cast<Form>().Any(x=>Controls(x).OfType<Button>().Any(b=>b.Name=="CommandConfirm"))); }
            catch(Exception error)
            {
                var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>Controls(x).OfType<Button>().Any(b=>b.Name=="CommandConfirm"));
                if(dialog is not null)AdminFeaturesUiTests.Capture(dialog,Path.Combine(output,"POS-action-failed.png"));
                throw new InvalidOperationException(caption+": "+(dialog is null?"Chưa mở dialog":string.Join(" | ",Controls(dialog).OfType<Label>().Select(x=>x.Text))),error);
            }
            Assert.Null(failure);
            // START xử lý lần lượt hai dòng; FINISH cũng lần lượt hai dòng, mỗi dòng 2 ly.
            if(caption=="Bắt đầu pha")
            {
                var detail=await LiveSession.Api.GetStaffOrderAsync(id);
                var remaining=detail.Mon.Single(x=>x.SoLuong-x.DaHuy-x.DaPha>0);
                await LiveSession.Api.StaffOrderActionAsync(id,new("START","",detail.Don.PhienBanDuLieu,Guid.NewGuid(),remaining.MaDong,2));
            }
        }
        var completed=await LiveSession.Api.GetStaffOrderAsync(id);Assert.Equal("COMPLETED",completed.Don.TrangThai);
        Assert.All(completed.Mon,x=>{Assert.Equal(2,x.DaPha);Assert.Equal(2,x.PhaXong);});
        shell.Navigate("shift");
        await AdminFeaturesUiTests.WaitUntil(()=>Controls(shell).OfType<Label>().Any(x=>x.Name=="ShiftCompletedOrdersTotal"&&x.Text==completed.Don.TaiChinh.NghiaVu.ToString("N0")+"đ"));
        Assert.Equal("1",Controls(shell).OfType<Label>().Single(x=>x.Name=="ShiftCompletedOrdersCount").Text);
        AdminFeaturesUiTests.Capture(shell,Path.Combine(output,"POS-shift-completed-total.png"));
    }

    /// <summary>Mở nút Chi tiết thật, chỉ có bốn phần cơ bản, không còn tab kho/tiêu hao/nhật ký.</summary>
    private static async Task CheckBasicDetails(MainForm shell,string output)
    {
        var handled=false;Exception? failure=null;
        using var timer=new System.Windows.Forms.Timer{Interval=80};
        timer.Tick+=(_,_)=>
        {
            var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Name=="BasicOrderDetails");
            if(dialog is null)return;
            try
            {
                timer.Stop();Assert.Empty(Controls(dialog).OfType<TabControl>());
                foreach(var name in new[]{"OrderInformation","OrderCustomer","OrderItems","OrderPayment"})Assert.Single(Controls(dialog),x=>x.Name==name);
                var grid=Controls(dialog).OfType<DataGridView>().Single(x=>x.Name=="OrderItemsGrid");
                Assert.Equal(2,grid.Rows.Count);Assert.Equal(6,grid.Columns.Count);
                Assert.DoesNotContain(grid.Columns.Cast<DataGridViewColumn>(),x=>x.DataPropertyName.StartsWith("Ma")||x.DataPropertyName=="PhienBanDuLieu");
                Assert.DoesNotContain(Controls(dialog).OfType<Label>(),x=>x.Text.StartsWith("Địa chỉ giao:")||x.Text=="Bàn");
                Assert.Contains(Controls(dialog).OfType<Label>(),x=>x.Text=="Địa chỉ khách hàng");
                AdminFeaturesUiTests.Capture(dialog,Path.Combine(output,"POS-order-four-sections.png"));
            }
            catch(Exception error){failure=error;}
            finally{handled=true;dialog.Close();}
        };
        timer.Start();Controls(shell).OfType<Button>().Single(x=>x.Text=="Chi tiết").PerformClick();
        await AdminFeaturesUiTests.WaitUntil(()=>handled);Assert.Null(failure);
    }
    /// <summary>Duyệt control theo Name để test không phụ thuộc tọa độ hoặc thứ tự layout.</summary>
    private static IEnumerable<Control> Controls(Control root)=>AdminFeaturesUiTests.Descendants(root);
}
