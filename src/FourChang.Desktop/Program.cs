using FourChang.Desktop.Shell;
using FourChang.Desktop.DesignSystem;
namespace FourChang.Desktop;

// Shared presentation host. POS and Admin have independent executable entry points.
public static class DesktopLauncher
{
    [STAThread]
    public static void Run(string role, string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetDefaultFont(CafeTheme.Body());
        FourChang.Desktop.Data.LiveSession.Preview = args.Contains("--preview") || args.Contains("--render-previews");
        if(args.Length>1 && args[0] is "--render-previews" or "--render-connected")
        {
            var connected=args[0]=="--render-connected";
            if(connected)
            {
                var token=Environment.GetEnvironmentVariable("FOURCHANG_TEST_TOKEN");
                if(string.IsNullOrWhiteSpace(token))throw new InvalidOperationException("Render connected cần phiên API hợp lệ.");
                FourChang.Desktop.Data.LiveSession.Api.Token=token;
                long? shift=long.TryParse(Environment.GetEnvironmentVariable("FOURCHANG_TEST_SHIFT"),out var id)?id:null;
                FourChang.Desktop.Data.LiveSession.Current=new FourChang.Presentation.Shared.Integration.SessionResult(token,role,"Kiểm tra giao diện",0,shift);
            }
            Directory.CreateDirectory(args[1]);
            using(var entry=new EntryForm(role,_=>{}))
            {
                entry.StartPosition=FormStartPosition.Manual;entry.Location=new Point(-20000,-20000);entry.Show();Application.DoEvents();if(connected){WaitForDatabaseUi();if(role=="Staff")VerifyEmployeeSelector(entry);}
                using var preview=new Bitmap(entry.Width,entry.Height);entry.DrawToBitmap(preview,new Rectangle(0,0,entry.Width,entry.Height));preview.Save(Path.Combine(args[1],$"{role}-entry.png"));entry.Close();
            }
            foreach(var renderRole in new[]{role})
            {
                using var shell=new MainForm(renderRole,"Nhân viên");
                if(args.Contains("--compact"))shell.ClientSize=new Size(1240,820);
                shell.StartPosition=FormStartPosition.Manual;shell.Location=new Point(-20000,-20000);shell.Show();
                var routes=FourChang.Presentation.Shared.Catalog.ScreenCatalog.All.Where(s=>s.Role==renderRole).Select(s=>s.Route).ToList();
                if(renderRole=="Admin")routes.Insert(0,"dashboard");
                foreach(var route in routes)
                {
                    shell.WindowState=FormWindowState.Normal; shell.ClientSize=args.Contains("--compact")?new Size(1240,820):new Size(1440,960); shell.Location=new Point(-20000,-20000); shell.Navigate(route);Application.DoEvents();shell.PerformLayout();Application.DoEvents();
                    if(connected)WaitForDatabaseUi();
                    shell.WindowState=FormWindowState.Normal; shell.PerformLayout(); Application.DoEvents();
                    VerifyPresentation(shell);
                    using var bitmap=new Bitmap(shell.Width,shell.Height);shell.DrawToBitmap(bitmap,new Rectangle(0,0,shell.Width,shell.Height));
                    bitmap.Save(Path.Combine(args[1],$"{renderRole}-{route}.png"));
                    foreach(var tabs in connected?Array.Empty<TabControl>():FindTabs(shell))
                    {
                        for(var i=1;i<tabs.TabCount;i++)
                        {
                            tabs.SelectedIndex=i;Application.DoEvents();shell.PerformLayout();Application.DoEvents();
                            VerifyPresentation(shell);
                            using var tabImage=new Bitmap(shell.Width,shell.Height);shell.DrawToBitmap(tabImage,new Rectangle(0,0,shell.Width,shell.Height));
                            tabImage.Save(Path.Combine(args[1],$"{renderRole}-{route}-tab{i}.png"));
                        }
                        tabs.SelectedIndex=0;
                    }
                }
                shell.Close();
            }
            return;
        }

        if(args.Contains("--preview")) Application.Run(new MainForm(role,role=="Staff"?"Nhân viên":"Quản lý"));
        else Application.Run(new RoleApplicationContext(role));
    }
    private static void VerifyEmployeeSelector(Control root)
    {
        var combos=FindCombos(root).ToArray();
        if(combos.Length!=2||combos.Any(c=>c.Items.Count==0||c.SelectedIndex<0))throw new InvalidOperationException("Danh sách nhân viên hoặc ca chưa tải được.");
        if(!combos.Any(c=>c.SelectedItem is FourChang.Presentation.Shared.Integration.Employee))throw new InvalidOperationException("Không chọn được hồ sơ nhân viên từ API.");
    }
    private static IEnumerable<ComboBox> FindCombos(Control root)
    {
        foreach(Control c in root.Controls){if(c is ComboBox combo)yield return combo;foreach(var child in FindCombos(c))yield return child;}
    }
    private static void WaitForDatabaseUi()
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();
        while(timer.ElapsedMilliseconds<1600){Application.DoEvents();Thread.Sleep(15);}
    }
    private static IEnumerable<TabControl> FindTabs(Control parent)
    {
        foreach(Control c in parent.Controls)
        {
            if(c is TabControl tabs)yield return tabs;
            foreach(var nested in FindTabs(c))yield return nested;
        }
    }
    private static void VerifyPresentation(Control root)
    {
        if(root.Text.Contains("BẢN THIẾT KẾ",StringComparison.OrdinalIgnoreCase)||root.Text.Contains("Dữ liệu mẫu",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unexpected presentation banner.");
        foreach(Control child in root.Controls)VerifyPresentation(child);
    }
}
