using FourChang.Desktop;
namespace FourChang.Pos;
internal static class Program
{
    [STAThread]
    private static void Main(string[] args) => DesktopLauncher.Run("Staff",args);
}
