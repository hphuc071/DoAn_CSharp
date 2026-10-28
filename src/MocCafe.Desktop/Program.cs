using MocCafe.Desktop.Shell;
using MocCafe.Desktop.DesignSystem;
namespace MocCafe.Desktop;

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
        if(args.Length>1 && args[0]=="--render-previews")
        {
            Directory.CreateDirectory(args[1]);
            using(var entry=new EntryForm(role,_=>{}))
            {
                entry.StartPosition=FormStartPosition.Manual;entry.Location=new Point(-20000,-20000);entry.Show();Application.DoEvents();
                using var preview=new Bitmap(entry.Width,entry.Height);entry.DrawToBitmap(preview,new Rectangle(0,0,entry.Width,entry.Height));preview.Save(Path.Combine(args[1],$"{role}-entry.png"));entry.Close();
            }
            foreach(var renderRole in new[]{role})
            {
                using var shell=new MainForm(renderRole,"Nhân viên");
                if(args.Contains("--compact"))shell.ClientSize=new Size(1240,820);
                shell.StartPosition=FormStartPosition.Manual;shell.Location=new Point(-20000,-20000);shell.Show();
                var routes=MocCafe.Presentation.Shared.Catalog.ScreenCatalog.All.Where(s=>s.Role==renderRole).Select(s=>s.Route).ToList();
                if(renderRole=="Admin")routes.Insert(0,"dashboard");
                foreach(var route in routes)
                {
                    shell.Navigate(route);Application.DoEvents();shell.PerformLayout();Application.DoEvents();
                    VerifyPresentation(shell);
                    using var bitmap=new Bitmap(shell.Width,shell.Height);shell.DrawToBitmap(bitmap,new Rectangle(0,0,shell.Width,shell.Height));
                    bitmap.Save(Path.Combine(args[1],$"{renderRole}-{route}.png"));
                    foreach(var tabs in FindTabs(shell))
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
