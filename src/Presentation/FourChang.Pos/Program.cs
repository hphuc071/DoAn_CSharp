// UC: UC-POS-01 đến UC-POS-05
// Chức năng: Điểm khởi động ứng dụng nhân viên tại quầy.
// Cần nhớ: Mở phiên ca trước khi thực hiện các thao tác bán hàng theo quyền.

using FourChang.Desktop;
namespace FourChang.Pos;
internal static class Program
{
    [STAThread]
    private static void Main(string[] args) => DesktopLauncher.Run("Staff",args);
}
