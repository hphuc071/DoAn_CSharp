using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shell;
using FourChang.Presentation.Shared.Contracts.Customer;
using FourChang.Desktop.Shared.Invoices;
using FourChang.Presentation.Shared.Contracts.Admin;
using System.Data;
namespace FourChang.Tests.Admin;
/// <summary>Thao tác bằng control thật, tạo dữ liệu trong fixture rồi dùng API đọc hiện có.</summary>
internal static class StaffDocumentsUiChecks
{
    /// <summary>Vào trang đúng vai trò, mở dòng hiện hữu và kiểm nút/viewport ở hai kích thước.</summary>
    internal static async Task Run(string output,bool invoice)
    {
        var saved=LiveSession.Current;var token=LiveSession.Api.Token;
        try
        {
            var employee=await LiveSession.Api.SaveEmployeeAsync(new("Nguyễn Hoàng Minh","0912345678",DateTime.Today,"ACTIVE",MaThaoTac:Guid.NewGuid()));
            var session=await LiveSession.Api.OpenShiftAsync(new(employee.MaNhanVien,"Sáng","UI-DOC-"+Guid.NewGuid().ToString("N")[..8],500000,employee.HoTen));
            LiveSession.Current=session;LiveSession.Api.Token=session.Token;LiveSession.ShiftLocked=false;
            var product=(await LiveSession.Api.GetMenuAsync()).First(x=>x.Available);
            var variant=(await LiveSession.Api.GetMenuVariantsAsync(product.MaMon)).First(x=>x.Available);var pair=variant.TuyChonPhaChe[0];
            var receipt=await LiveSession.Api.CheckoutPosAsync(new(Guid.NewGuid(),"TAKEAWAY",null,"CASH",1000000,variant.GiaBan,[new(variant.MaCauHinhMon,1,pair.PhanTramDuong,pair.PhanTramDa,null,[])]));
            using var shell=new MainForm("Staff",employee.HoTen){ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-14000,-14000)};
            shell.Show();shell.Navigate(invoice?"payment":"orders");
            await AdminFeaturesUiTests.WaitUntil(()=>Controls(shell).OfType<DataGridView>().Any(x=>x.Name=="StaffDocumentsGrid"&&x.Rows.Count>0));
            var grid=Controls(shell).OfType<DataGridView>().Single(x=>x.Name=="StaffDocumentsGrid");
            var search=Controls(shell).OfType<TextBox>().Single(x=>x.Name=="StaffDocumentSearch");
            search.Text=invoice?"HD-"+receipt.MaDonHang.ToString("D12"):receipt.MaDon;
            if(invoice)await ApplyFilters(shell,grid);
            await AdminFeaturesUiTests.WaitUntil(()=>grid.Rows.Count==1);
            if(invoice)
            {
                var end=Controls(shell).OfType<DateTimePicker>().Single(x=>x.Name=="StaffInvoiceTo");
                end.Value=DateTime.Today.AddDays(-1);await ApplyFilters(shell,grid);Assert.Equal(0,grid.Rows.Count);
                end.Value=DateTime.Today;await ApplyFilters(shell,grid);Assert.Equal(1,grid.Rows.Count);
            }
            foreach(var width in new[]{1440,1280})
            {
                shell.ClientSize=new Size(width,850);await Task.Delay(100);
                var button=Controls(shell).OfType<Button>().Single(x=>x.Text==(invoice?"Xem hóa đơn":"Xem đơn hàng"));
                var bounds=shell.RectangleToClient(button.RectangleToScreen(button.ClientRectangle));
                Assert.True(bounds.Top>=0&&bounds.Bottom<=shell.ClientSize.Height);
            }
            AdminFeaturesUiTests.Capture(shell,Path.Combine(output,invoice?"staff-invoices.png":"staff-orders.png"));
            var finished=false;Exception? failure=null;
            using var timer=new System.Windows.Forms.Timer{Interval=75};
            timer.Tick+=(_,_)=>
            {
                var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x!=shell&&x.Text.StartsWith(invoice?"Hóa đơn HD-":"Đơn hàng POS-",StringComparison.Ordinal));
                if(dialog is null)return;
                try
                {
                    Assert.Contains(Controls(dialog).OfType<Button>(),x=>x.Text=="Đóng");
                    if(invoice)
                    {
                        Assert.Equal("BasicInvoiceDetails",dialog.Name);Assert.Empty(Controls(dialog).OfType<TabControl>());Assert.Empty(Controls(dialog).OfType<RichTextBox>());
                        foreach(var name in new[]{"InvoiceShop","InvoiceInformation","InvoiceCustomer","InvoiceItems","InvoicePayment"})Assert.Single(Controls(dialog),x=>x.Name==name);
                        var items=Controls(dialog).OfType<DataGridView>().Single(x=>x.Name=="InvoiceItemsGrid");
                        Assert.Single(items.Rows.Cast<DataGridViewRow>());Assert.Equal(variant.GiaBan,Convert.ToDecimal(items.Rows[0].Cells["DonGia"].Value));
                        Assert.Contains(Controls(dialog).OfType<Label>(),x=>x.Text=="Khách lẻ");
                        Assert.DoesNotContain(Controls(dialog).OfType<Label>(),x=>x.Text=="Địa chỉ giao hàng"||x.Text=="Ma Hoa Don");
                    }
                    AdminFeaturesUiTests.Capture(dialog,Path.Combine(output,invoice?"staff-invoice-detail.png":"staff-order-detail.png"));
                }
                catch(Exception ex){failure=ex;}
                finally{timer.Stop();finished=true;dialog.Close();}
            };
            timer.Start();Controls(shell).OfType<Button>().Single(x=>x.Text==(invoice?"Xem hóa đơn":"Xem đơn hàng")).PerformClick();
            await AdminFeaturesUiTests.WaitUntil(()=>finished);Assert.Null(failure);
            if(invoice)
            {
                // Dữ liệu biến thể chỉ dùng kiểm tra hiển thị: không tạo/sửa đơn giao hàng trong database.
                var savedOrder=await LiveSession.Api.GetStaffOrderAsync(receipt.MaDonHang);
                var delivery=savedOrder with {TenNguoiNhan="Nguyễn Minh Anh",DienThoai="0901234567",DiaChi="123 Nguyễn Trãi, TP. Hồ Chí Minh",Don=savedOrder.Don with {HinhThuc="DELIVERY"}};
                var row=(AdminInvoiceView)grid.CurrentRow!.DataBoundItem;
                var issued=await LiveSession.Api.GetStaffInvoiceAsync(row.MaHoaDon);
                using var deliveryForm=new InvoiceDetailsForm(issued.HoaDon,issued.SnapshotJson,delivery);
                deliveryForm.Show(shell);await Task.Delay(150);
                foreach(var text in new[]{"Nguyễn Minh Anh","0901234567","123 Nguyễn Trãi, TP. Hồ Chí Minh","Địa chỉ giao hàng"})Assert.Contains(Controls(deliveryForm).OfType<Label>(),x=>x.Text==text);
                AdminFeaturesUiTests.Capture(deliveryForm,Path.Combine(output,"staff-invoice-customer-address.png"));deliveryForm.Close();
            }
            search.Text="NO-SUCH-DOCUMENT";if(invoice)await ApplyFilters(shell,grid);Assert.Equal(0,grid.Rows.Count);
            search.Clear();if(invoice)await ApplyFilters(shell,grid);Assert.True(grid.Rows.Count>0);shell.Close();
        }
        finally{LiveSession.Current=saved;LiveSession.Api.Token=token;LiveSession.ShiftLocked=false;}
    }
    /// <summary>Áp dụng từ khóa/ngày bằng nút thật và chờ fetch hoàn tất, không chỉ quan sát dữ liệu cũ.</summary>
    private static async Task ApplyFilters(Form shell,DataGridView grid)
    {
        var before=grid.Tag;Controls(shell).OfType<Button>().Single(x=>x.Name=="StaffInvoiceSearch").PerformClick();
        await AdminFeaturesUiTests.WaitUntil(()=>!Equals(before,grid.Tag));
    }
    /// <summary>Tìm control bằng tên ổn định, không phụ thuộc tọa độ hoặc thứ tự dòng.</summary>
    private static IEnumerable<Control> Controls(Control root)=>AdminFeaturesUiTests.Descendants(root);
}
