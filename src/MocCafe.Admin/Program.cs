using MocCafe.Desktop;
namespace MocCafe.Admin;
internal static class Program
{
    [STAThread]
    private static void Main(string[] args) => DesktopLauncher.Run("Admin",args);
}
