using MocCafe.Desktop;
namespace MocCafe.Pos;
internal static class Program
{
    [STAThread]
    private static void Main(string[] args) => DesktopLauncher.Run("Staff",args);
}
