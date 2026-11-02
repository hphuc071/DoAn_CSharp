using System.Diagnostics;
using FourChang.Desktop.Data;
using FourChang.Desktop.DesignSystem;
using FourChang.Desktop.Features.Admin.Customers;
using FourChang.Desktop.Shell;
using FourChang.Presentation.Shared.Integration;

internal static class Program
{
    private static string output = "";
    private static int passed;
    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Application.SetDefaultFont(CafeTheme.Body());
        output = args[0]; Directory.CreateDirectory(output);
        var sourceApi = Environment.GetEnvironmentVariable("FOURCHANG_TEST_API_URL") ?? "http://localhost:5183";
        var proxyUrl = Environment.GetEnvironmentVariable("FOURCHANG_TEST_PROXY_URL") ?? "http://localhost:5185/";
        using var lostReply = new LostReplyProxy(sourceApi,proxyUrl);
        Environment.SetEnvironmentVariable("FOURCHANG_API_URL",proxyUrl);
        var exitCode=1;
        using var host=new Form { ShowInTaskbar=false, StartPosition=FormStartPosition.Manual, Location=new Point(-22000,-22000), Size=new Size(20,20) };
        host.Shown+=(_,_)=>host.BeginInvoke(new Action(()=>{exitCode=RunTests();host.Close();}));
        Application.Run(host);
        return exitCode;
    }
    private static int RunTests()
    {
        try
        {
            var password = Environment.GetEnvironmentVariable("FOURCHANG_TEST_ADMIN_PASSWORD") ?? throw new InvalidOperationException("Chạy bằng Test-Presentation.ps1 để cấp môi trường kiểm thử.");
            var session = Wait(LiveSession.Api.LoginAdminAsync(new("quanly",password)));
            LiveSession.Api.Token = session.Token; LiveSession.Current = session; LiveSession.Preview = false;
            var stamp = Guid.NewGuid().ToString("N")[..12];
            var create = new CustomerEditorForm(null,"create");
            {
                Show(create);
                Field<TextBox>(create,"CustomerUsername").Text="uiqa"+stamp;
                Field<TextBox>(create,"CustomerName").Text="Khách kiểm tra giao diện";
                Field<TextBox>(create,"CustomerEmail").Text="uiqa"+stamp+"@example.test";
                Check(Field<TextBox>(create,"CustomerTemporaryPassword").UseSystemPasswordChar,"Mật khẩu tạm được che trên biểu mẫu");
                Capture(create,"customer-create");
                Field<Button>(create,"CustomerSave").PerformClick();
                PumpUntil(()=>Field<Button>(create,"CustomerSave").Text=="Kiểm tra / thử lại" && Field<Button>(create,"CustomerSave").Enabled);
                Check(!Field<TextBox>(create,"CustomerName").Enabled && Field<TextBox>(create,"CustomerName").Text=="Khách kiểm tra giao diện","Mất phản hồi giữ nguyên nháp và khóa chỉnh sửa yêu cầu chưa rõ");
                Capture(create,"customer-lost-reply");
                Field<Button>(create,"CustomerSave").PerformClick();
                PumpUntil(()=>!Field<Button>(create,"CustomerSave").Visible);
                Check(LostReplyProxy.CreatePosts==1,"Thử lại sau mất phản hồi tra kết quả cũ, không POST tạo lần hai");
                Check(Field<TextBox>(create,"CustomerTemporaryPassword").Enabled && Field<TextBox>(create,"CustomerTemporaryPassword").ReadOnly,"Sau lưu có thể bàn giao nhưng không sửa mật khẩu tạm");
                create.Close();
            }
            create.Dispose();
            var customer=Wait(LiveSession.Api.GetCustomersAsync("uiqa"+stamp,1)).Items.Single();
            Check(customer.BatBuocDoiMatKhau,"Tạo từ WinForms lưu đúng cờ bắt buộc đổi mật khẩu");
            using(var shell=new MainForm("Admin","Quản lý"))
            {
                Show(shell);shell.Navigate("customers");PumpUntil(()=>All(shell).OfType<DataGridView>().Any(x=>x.Name=="CustomersGrid"&&x.Rows.Count>0));
                Capture(shell,"customer-list");
                Check(All(shell).OfType<Button>().Any(x=>x.Text=="Đặt lại mật khẩu"),"Màn hình Khách hàng có đúng thao tác reset");
                var initialTotal=Wait(LiveSession.Api.GetCustomersAsync("",1)).Total;
                var customerGrid=Field<DataGridView>(shell,"CustomersGrid");
                var search=Field<TextBox>(shell,"CustomerSearch");
                search.Text="uiqa"+stamp;Field<Button>(shell,"CustomerFind").PerformClick();
                PumpUntil(()=>customerGrid.Rows.Count==1 && customerGrid.Rows[0].DataBoundItem is CustomerAccount found && found.TenDangNhap=="uiqa"+stamp);
                Check(customerGrid.Rows.Count==1,"Tìm theo từ khóa chỉ hiện khách phù hợp");
                Capture(shell,"customer-filtered");
                search.Clear();Field<Button>(shell,"CustomerReload").PerformClick();
                PumpUntil(()=>Field<Label>(shell,"CustomerResultCount").Text==$"{initialTotal:N0} khách hàng");
                Check(Field<Label>(shell,"CustomerResultCount").Text==$"{initialTotal:N0} khách hàng","Xóa từ khóa rồi Tải lại khôi phục tổng khách ban đầu");
                Check(customerGrid.Rows.Count==Math.Min(20,initialTotal),"Xóa từ khóa rồi Tải lại khôi phục dữ liệu trang đầy đủ");
                Check(Field<Label>(shell,"CustomerPaging").Text.StartsWith("Trang 1 /"),"Tải lại từ khóa hiện tại đưa danh sách về trang 1");
                Capture(shell,"customer-reloaded");
                search.Text="khong-co-ket-qua-"+stamp;Field<Button>(shell,"CustomerFind").PerformClick();
                PumpUntil(()=>customerGrid.Rows.Count==0);
                Check(Field<Label>(shell,"CustomerResultCount").Text=="0 khách hàng","Từ khóa không tồn tại hiển thị danh sách rỗng và tổng 0");
                Capture(shell,"customer-empty");
                search.Clear();Field<Button>(shell,"CustomerReload").PerformClick();
                PumpUntil(()=>customerGrid.Rows.Count==Math.Min(20,initialTotal));
                Check(Field<Label>(shell,"CustomerResultCount").Text==$"{initialTotal:N0} khách hàng","Tải lại sau kết quả rỗng vẫn khôi phục được toàn bộ danh sách");
                shell.ClientSize=new Size(1240,820);Pump(250);Capture(shell,"customer-list-compact");shell.Close();
            }
            var detail=Wait(LiveSession.Api.GetCustomerAsync(customer.MaKhachHang));
            using(var info=new CustomerDetailsForm(detail)){Show(info);Capture(info,"customer-details");info.Close();}
            foreach(var action in new[]{"status","reset-password","delete"})
            {
                using var form=new CustomerEditorForm(detail,action);Show(form);Capture(form,"customer-"+action);form.Close();
            }
            using(var update=new CustomerEditorForm(detail,"update"))
            {
                Show(update);Field<TextBox>(update,"CustomerName").Text="Khách đã sửa từ WinForms";
                Capture(update,"customer-edit");Field<Button>(update,"CustomerSave").PerformClick();PumpUntil(()=>!update.Visible);
                Check(update.DialogResult==DialogResult.OK,"Sửa từ WinForms hoàn tất và đóng form đúng kết quả");
            }
            detail=Wait(LiveSession.Api.GetCustomerAsync(customer.MaKhachHang));
            Check(detail.Account.HoTen=="Khách đã sửa từ WinForms","Dữ liệu sửa từ giao diện có trong SQL qua API");
            using(var delete=new CustomerEditorForm(detail,"delete"))
            {
                Show(delete);Field<TextBox>(delete,"CustomerReason").Text="Dọn hồ sơ kiểm tra giao diện chưa tham chiếu";
                Field<Button>(delete,"CustomerSave").PerformClick();PumpUntil(()=>!delete.Visible);
                Check(delete.DialogResult==DialogResult.OK,"Xóa có điều kiện từ WinForms hoàn tất");
            }
            Check(Wait(LiveSession.Api.GetCustomersAsync("uiqa"+stamp,1)).Total==0,"Xóa từ giao diện cập nhật danh sách thật");
            Wait(LiveSession.Api.LogoutAsync());
            Console.WriteLine($"UI QA PASSED: {passed} checks, 12 screenshots.");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex.ToString());return 1;}
    }
    private static void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);passed++;Console.WriteLine("PASS: "+text);}
    private static T Field<T>(Control root,string name) where T:Control => All(root).OfType<T>().Single(x=>x.Name==name);
    private static IEnumerable<Control> All(Control root){foreach(Control child in root.Controls){yield return child;foreach(var inner in All(child))yield return inner;}}
    private static T Wait<T>(Task<T> task){PumpUntil(()=>task.IsCompleted);return task.GetAwaiter().GetResult();}
    private static void Show(Form form){form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-16000,-16000);form.Show();form.WindowState=FormWindowState.Normal;Pump(200);}
    private static void Capture(Form form,string name){form.PerformLayout();Pump(150);using var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(Path.Combine(output,name+".png"));}
    private static void Pump(int milliseconds){var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<milliseconds){Application.DoEvents();Thread.Sleep(10);}}
    private static void PumpUntil(Func<bool> condition){var watch=Stopwatch.StartNew();while(!condition()){if(watch.ElapsedMilliseconds>30000)throw new TimeoutException("Giao diện chưa hoàn thành thao tác trong 30 giây.");Application.DoEvents();Thread.Sleep(10);}Pump(80);}
}

// Proxy chỉ phục vụ QA: cho server commit lần tạo đầu rồi chủ động cắt phần thân phản hồi.
// Không dùng proxy này trong đồ án hoặc lưu nội dung request/mật khẩu.
internal sealed class LostReplyProxy : IDisposable
{
    private readonly System.Net.HttpListener listener=new();
    private readonly HttpClient http=new();
    public static int CreatePosts;
    private readonly string sourceApi;
    public LostReplyProxy(string sourceApi,string proxyUrl)
    {
        this.sourceApi=sourceApi.TrimEnd('/');
        listener.Prefixes.Add(proxyUrl.TrimEnd('/')+"/");listener.Start();
        _=Task.Run(async()=>{while(listener.IsListening){try{var context=await listener.GetContextAsync();_=Forward(context);}catch(System.Net.HttpListenerException){break;}catch(ObjectDisposedException){break;}}});
    }
    private async Task Forward(System.Net.HttpListenerContext context)
    {
        try
        {
            using var request=new HttpRequestMessage(new HttpMethod(context.Request.HttpMethod),sourceApi+context.Request.RawUrl);
            if(context.Request.HasEntityBody){using var body=new MemoryStream();await context.Request.InputStream.CopyToAsync(body);request.Content=new ByteArrayContent(body.ToArray());if(context.Request.ContentType is {} type)request.Content.Headers.TryAddWithoutValidation("Content-Type",type);}
            if(context.Request.Headers["Authorization"] is {} auth)request.Headers.TryAddWithoutValidation("Authorization",auth);
            using var response=await http.SendAsync(request);var bytes=await response.Content.ReadAsByteArrayAsync();
            context.Response.StatusCode=(int)response.StatusCode;context.Response.ContentType="application/json";
            if(context.Request.HttpMethod=="POST" && context.Request.Url!.AbsolutePath.TrimEnd('/')=="/api/admin/customers" && Interlocked.Increment(ref CreatePosts)==1 && response.IsSuccessStatusCode)
            {
                context.Response.ContentLength64=bytes.Length+10;
                await context.Response.OutputStream.WriteAsync(bytes.AsMemory(0,10));context.Response.Abort();return;
            }
            context.Response.ContentLength64=bytes.Length;await context.Response.OutputStream.WriteAsync(bytes);context.Response.Close();
        }
        catch(Exception){context.Response.Abort();}
    }
    public void Dispose(){listener.Close();http.Dispose();}
}
