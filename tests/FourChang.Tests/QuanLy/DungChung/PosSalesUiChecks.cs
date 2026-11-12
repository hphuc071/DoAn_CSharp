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
            Controls(shell).OfType<NumericUpDown>().Single(x=>x.Name=="PosCashTender").Value=1000000;
            Controls(shell).OfType<Button>().Single(x=>x.Name=="PosCheckout").PerformClick();
            await AdminFeaturesUiTests.WaitUntil(()=>Controls(shell).OfType<Label>().Any(x=>x.Text.StartsWith("Đã thanh toán đơn")));
            Assert.Empty(Controls(shell).OfType<DataGridView>().Single(x=>x.Name=="PosCartGrid").Rows.Cast<DataGridViewRow>());
            AdminFeaturesUiTests.Capture(shell,Path.Combine(output,"POS-cash-success.png"));shell.Close();
        }
        finally{LiveSession.Current=original;LiveSession.Api.Token=token;LiveSession.ShiftLocked=false;}
    }
    /// <summary>Duyệt control theo Name để test không phụ thuộc tọa độ hoặc thứ tự layout.</summary>
    private static IEnumerable<Control> Controls(Control root)=>AdminFeaturesUiTests.Descendants(root);
}
