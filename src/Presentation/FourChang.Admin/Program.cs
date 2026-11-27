// UC: UC-ADM-01 đến UC-ADM-08
// Chức năng: Điểm khởi động ứng dụng quản lý trên Windows.
// Cần nhớ: Giao diện dùng lại FourChang.Desktop và gọi Backend qua API client.

using FourChang.Desktop;
namespace FourChang.Admin;
internal static class Program
{
    [STAThread]
    private static void Main(string[] args) => DesktopLauncher.Run("Admin",args);
}
