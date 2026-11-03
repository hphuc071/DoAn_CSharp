using FourChang.Desktop;
namespace FourChang.Admin;
internal static class Program
{
    [STAThread]
    private static void Main(string[] args) => DesktopLauncher.Run("Admin",args);
}
