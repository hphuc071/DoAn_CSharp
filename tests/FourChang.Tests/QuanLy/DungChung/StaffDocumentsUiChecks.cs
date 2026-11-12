using FourChang.Desktop.Shared.Session;
using FourChang.Desktop.Shared.Controls.Admin;
using FourChang.Desktop.Shell;
using FourChang.Presentation.Shared.Contracts.Customer;
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
            await AdminFeaturesUiTests.WaitUntil(()=>grid.Rows.Count==1);
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
                try{Assert.Contains(Controls(dialog).OfType<Button>(),x=>x.Text=="Đóng");AdminFeaturesUiTests.Capture(dialog,Path.Combine(output,invoice?"staff-invoice-detail.png":"staff-order-detail.png"));}
                catch(Exception ex){failure=ex;}
                finally{timer.Stop();finished=true;dialog.Close();}
            };
            timer.Start();Controls(shell).OfType<Button>().Single(x=>x.Text==(invoice?"Xem hóa đơn":"Xem đơn hàng")).PerformClick();
            await AdminFeaturesUiTests.WaitUntil(()=>finished);Assert.Null(failure);
            search.Text="NO-SUCH-DOCUMENT";Assert.Equal(0,grid.Rows.Count);
            search.Clear();Assert.True(grid.Rows.Count>0);shell.Close();
        }
        finally{LiveSession.Current=saved;LiveSession.Api.Token=token;LiveSession.ShiftLocked=false;}
    }
    /// <summary>Tìm control bằng tên ổn định, không phụ thuộc tọa độ hoặc thứ tự dòng.</summary>
    private static IEnumerable<Control> Controls(Control root)=>AdminFeaturesUiTests.Descendants(root);
}
